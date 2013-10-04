using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Threading;
using AFNetworking;
using MonoTouch.Foundation;
using System.IO;
using System.Net;

namespace ModernHttpClient
{
    public class AFNetworkHandler : HttpMessageHandler
    {
        AFHTTPClient handler;

        public AFNetworkHandler()
        {
            handler = new AFHTTPClient();
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms);

            var rq = new NSMutableUrlRequest() {
                AllowsCellularAccess = true,
                Body = NSData.FromArray(ms.ToArray()),
                CachePolicy = NSUrlRequestCachePolicy.UseProtocolCachePolicy,
                Headers = request.Headers.Aggregate(new NSMutableDictionary(), (acc, x) => {
                    acc.Add(new NSString(x.Key), new NSString(x.Value.LastOrDefault()));
                    return acc;
                }),
                HttpMethod = request.Method.ToString().ToUpperInvariant(),
                Url = new NSUrl(request.RequestUri.ToString()),
            };

            var op = default(AFHTTPRequestOperation);
            var err = default(NSError);

            try {
                op = await enqueueOperation(new AFHTTPRequestOperation(rq), cancellationToken);
            } catch (ApplicationException ex) {
                op = (AFHTTPRequestOperation)ex.Data["op"];
                err = (NSError)ex.Data["err"];
            }

            var resp = (NSHttpUrlResponse)op.Response;

            if (op.IsCancelled) {
                throw new TaskCanceledException();
            }

            var ret = new HttpResponseMessage((HttpStatusCode)resp.StatusCode) {
                Content = new ByteArrayContent(op.ResponseData.ToArray()),
                RequestMessage = request,
                ReasonPhrase = (err != null ? err.LocalizedDescription : null),
            };

            foreach(var v in resp.AllHeaderFields) {
                ret.Headers.Add(v.Key.ToString(), v.Value.ToString());
            }

            return ret;
        }

        Task<AFHTTPRequestOperation> enqueueOperation(AFHTTPRequestOperation operation, CancellationToken cancelToken)
        {
            var tcs = new TaskCompletionSource<AFHTTPRequestOperation>();
            if (cancelToken.IsCancellationRequested) {
                tcs.SetCanceled();
                return tcs.Task;
            }

            bool completed = false;
            operation.SetCompletionBlockWithSuccess(
                (op, _) => { 
                    completed = true;
                    tcs.SetResult(op);
                },
                (op, err) => {
                    completed = true;

                    // NB: Secret Handshake is Secret
                    var ex = new ApplicationException();
                    ex.Data.Add("op", op);
                    ex.Data.Add("err", err);
                    tcs.SetException(ex);
                });

            handler.EnqueueHTTPRequestOperation(operation);
            cancelToken.Register(() => {
                if (completed) return;

                operation.Cancel();
                tcs.SetCanceled();
            });

            return tcs.Task;
        }
    }
}

