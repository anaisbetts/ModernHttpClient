using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using AFNetworking;
using MonoTouch.Foundation;
using System.IO;
using System.Net;

namespace ModernHttpClient
{
    public class AFNetworkHandler : HttpMessageHandler
    {
        static Dictionary<NSMutableUrlRequest, object[]> pins = new Dictionary<NSMutableUrlRequest, object[]>();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers = request.Headers;

            var ms = new MemoryStream();
            if (request.Content != null) {
                await request.Content.CopyToAsync(ms).ConfigureAwait(false);
                headers = headers.Union(request.Content.Headers);
            }

            var rq = new NSMutableUrlRequest() {
                AllowsCellularAccess = true,
                Body = NSData.FromArray(ms.ToArray()),
                CachePolicy = NSUrlRequestCachePolicy.UseProtocolCachePolicy,
                Headers = headers.Aggregate(new NSMutableDictionary(), (acc, x) => {
                    acc.Add(new NSString(x.Key), new NSString(x.Value.LastOrDefault()));
                    return acc;
                }),
                HttpMethod = request.Method.ToString().ToUpperInvariant(),
                Url = new NSUrl(request.RequestUri.ToString()),
            };

            var host = request.RequestUri.GetLeftPart(UriPartial.Authority);
            var op = default(AFHTTPRequestOperation);
            var err = default(NSError);
            var handler = new AFHTTPClient(new NSUrl(host));

            // NB: I have no idea how async methods affects object lifetime and
            // GC'ing of local variables, soooooooo....
            lock (pins) { pins[rq] = new object[] { op, handler, }; }

            try {
                op = await enqueueOperation(handler, new AFHTTPRequestOperation(rq), cancellationToken);
            } catch (ApplicationException ex) {
                op = (AFHTTPRequestOperation)ex.Data["op"];
                err = (NSError)ex.Data["err"];
            }

            var resp = (NSHttpUrlResponse)op.Response;

            if (op.IsCancelled) {
                lock (pins) { pins.Remove(rq); }
                throw new TaskCanceledException();
            }

            var ret = new HttpResponseMessage((HttpStatusCode)resp.StatusCode) {
                Content = new ByteArrayContent(op.ResponseData.ToArray()),
                RequestMessage = request,
                ReasonPhrase = (err != null ? err.LocalizedDescription : null),
            };

            foreach(var v in resp.AllHeaderFields) {
                ret.Headers.TryAddWithoutValidation(v.Key.ToString(), v.Value.ToString());
            }

            lock (pins) { pins.Remove(rq); }
            return ret;
        }

        Task<AFHTTPRequestOperation> enqueueOperation(AFHTTPClient handler, AFHTTPRequestOperation operation, CancellationToken cancelToken)
        {
            var tcs = new TaskCompletionSource<AFHTTPRequestOperation>();
            if (cancelToken.IsCancellationRequested) {
                tcs.SetCanceled();
                return tcs.Task;
            }

            bool completed = false;
            operation.SetCompletionBlockWithSuccess(
                (op, _) => { 
                    if (completed) return;

                    completed = true;
                    tcs.SetResult(op);
                },
                (op, err) => {
                    if (completed) return;

                    // NB: Secret Handshake is Secret
                    completed = true;
                    var ex = new ApplicationException();
                    ex.Data.Add("op", op);
                    ex.Data.Add("err", err);
                    tcs.SetException(ex);
                });

            handler.EnqueueHTTPRequestOperation(operation);
            cancelToken.Register(() => {
                if (completed) return;

                completed = true;
                operation.Cancel();
                tcs.SetCanceled();
            });

            return tcs.Task;
        }
    }

    class ByteArrayHttpContent : HttpContent
    {
        byte[] data;
        public ByteArrayHttpContent(byte[] data)
        {
            this.data = data;
        }

        protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            await (new MemoryStream(data)).CopyToAsync(stream);
        }

        protected override bool TryComputeLength(out long length)
        {
            length = data.Length;
            return true;
        }
    }
}
