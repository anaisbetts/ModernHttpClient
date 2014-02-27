using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using MonoTouch.Foundation;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ModernHttpClient
{
    public class NSUrlSessionHandler : HttpMessageHandler
    {
        readonly NSUrlSession session;

        readonly Dictionary<NSUrlSessionTask, Tuple<HttpResponseMessage, CancellationToken>> inflightRequests = 
            new Dictionary<NSUrlSessionTask, Tuple<HttpResponseMessage, CancellationToken>>();

        public NSUrlSessionHandler()
        {
            session = NSUrlSession.FromConfiguration(
                NSUrlSessionConfiguration.DefaultSessionConfiguration, 
                new DataTaskDelegate(this), null);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var headers = request.Headers as IEnumerable<KeyValuePair<string, IEnumerable<string>>>;
            var ms = new MemoryStream();

            if (request.Content != null) {
                await request.Content.CopyToAsync(ms).ConfigureAwait(false);
                headers = headers.Union(request.Content.Headers).ToArray();
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
                Url = NSUrl.FromString(request.RequestUri.AbsoluteUri),
            };

            var host = request.RequestUri.GetLeftPart(UriPartial.Authority);

            var op = session.CreateDataTask(rq);

            cancellationToken.ThrowIfCancellationRequested();

            lock (inflightRequests) {
                inflightRequests[op] = Tuple.Create(new HttpResponseMessage(), cancellationToken);
            }

            op.Resume();
        }

        class DataTaskDelegate : NSUrlSessionDataDelegate
        {
            NSUrlSessionHandler This { get; protected set; }
            public DataTaskDelegate(NSUrlSessionHandler that)
            {
                this.This = that;
            }

            public override void DidReceiveResponse(NSUrlSession session, NSUrlSessionDataTask dataTask, NSUrlResponse response, Action<NSUrlSessionResponseDisposition> completionHandler)
            {
                completionHandler(NSUrlSessionResponseDisposition.Allow);
            }

            public override void DidCompleteWithError (NSUrlSession session, NSUrlSessionTask task, NSError error)
            {
            }

            public override void DidReceiveData (NSUrlSession session, NSUrlSessionDataTask dataTask, NSData data)
            {
            }

            public override void WillCacheResponse (NSUrlSession session, NSUrlSessionDataTask dataTask, NSCachedUrlResponse proposedResponse, Action<NSCachedUrlResponse> completionHandler)
            {
                completionHandler(null);
            }

            Tuple<HttpResponseMessage, CancellationToken> getResponseForTask(NSUrlSessionTask task)
            {
                lock (This.inflightRequests) {
                    return This.inflightRequests[task];
                }
            }
        }
    }
}