using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using OkHttp;

namespace ModernHttpClient
{
    public class NativeMessageHandler : HttpMessageHandler
    {
        readonly OkHttpClient client = new OkHttpClient();
        readonly bool throwOnCaptiveNetwork;

        readonly Dictionary<HttpRequestMessage, WeakReference> registeredProgressCallbacks = 
            new Dictionary<HttpRequestMessage, WeakReference>();

        public NativeMessageHandler() : this(false) {}

        public NativeMessageHandler(bool throwOnCaptiveNetwork)
        {
            this.throwOnCaptiveNetwork = throwOnCaptiveNetwork;
        }

        public void RegisterForProgress(HttpRequestMessage request, ProgressDelegate callback)
        {
            if (callback == null && registeredProgressCallbacks.ContainsKey(request)) {
                registeredProgressCallbacks.Remove(request);
                return;
            }

            registeredProgressCallbacks[request] = new WeakReference(callback);
        }

        ProgressDelegate getAndRemoveCallbackFromRegister(HttpRequestMessage request)
        {
            ProgressDelegate emptyDelegate = delegate { };

            lock (registeredProgressCallbacks) {
                if (!registeredProgressCallbacks.ContainsKey(request)) return emptyDelegate;

                var weakRef = registeredProgressCallbacks[request];
                if (weakRef == null) return emptyDelegate;

                var callback = weakRef.Target as ProgressDelegate;
                if (callback == null) return emptyDelegate;

                registeredProgressCallbacks.Remove(request);
                return callback;
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var java_uri = request.RequestUri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
            var url = new Java.Net.URL(java_uri);

            var body = default(RequestBody);
            if (request.Content != null) {
                var bytes = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                body = RequestBody.Create(MediaType.Parse(request.Content.Headers.ContentType.MediaType), bytes);
            }

            var builder = new Request.Builder()
                .Method(request.Method.Method.ToUpperInvariant(), body)
                .Url(url);

            var keyValuePairs = request.Headers
                .Union(request.Content != null ? 
                    (IEnumerable<KeyValuePair<string, IEnumerable<string>>>)request.Content.Headers :
                    Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>())
                .SelectMany(x => x.Value.Select(val => new { Key = x.Key, Value = val }));

            foreach (var kvp in keyValuePairs) builder.AddHeader(kvp.Key, kvp.Value);

            cancellationToken.ThrowIfCancellationRequested();

            var rq = builder.Build();
            var call = client.NewCall(rq);
            cancellationToken.Register(call.Cancel);

            var resp = await call.EnqueueAsync().ConfigureAwait(false);
            var respBody = resp.Body();

            cancellationToken.ThrowIfCancellationRequested();

            var ret = new HttpResponseMessage((HttpStatusCode)resp.Code());
            if (respBody != null) {
                var content = new ProgressStreamContent(respBody.ByteStream());
                content.Progress = getAndRemoveCallbackFromRegister(request);
                ret.Content = content;
            } else {
                ret.Content = new ByteArrayContent(new byte[0]);
            }

            var respHeaders = resp.Headers();
            foreach (var k in respHeaders.Names()) {
                ret.Headers.TryAddWithoutValidation(k, respHeaders.Get(k));
                ret.Content.Headers.TryAddWithoutValidation(k, respHeaders.Get(k));
            }

            return ret; 

            /*
            return await Task.Run (() => {
                var ret = default(HttpResponseMessage);

                // NB: This is the line that blocks until we have headers
                try {
                    ret = new HttpResponseMessage((HttpStatusCode)rq.ResponseCode);
                } catch(Java.Net.UnknownHostException e) {
                    throw new WebException("Name resolution failure", e, WebExceptionStatus.NameResolutionFailure, null);
                } catch(Java.Net.ConnectException e) {
                    throw new WebException("Connection failed", e, WebExceptionStatus.ConnectFailure, null);
                }

                // Test to see if we're being redirected (i.e. in a captive network)
                if (throwOnCaptiveNetwork && (url.Host != rq.URL.Host)) {
                    throw new WebException("Hostnames don't match, you are probably on a captive network");
                }

                cancellationToken.ThrowIfCancellationRequested();

                var progressStreamContent = new ProgressStreamContent(new ConcatenatingStream(new Func<Stream>[] {
                    () => ret.IsSuccessStatusCode ? rq.InputStream : new MemoryStream(),
                    () => rq.ErrorStream ?? new MemoryStream (),
                }, true));

                progressStreamContent.Progress = getAndRemoveCallbackFromRegister(request);
                ret.Content = progressStreamContent;

                var keyValuePairs = rq.HeaderFields.Keys
                    .Where(k => k != null)      // Yes, this happens. I can't even. 
                    .SelectMany(k => rq.HeaderFields[k]
                        .Select(val => new { Key = k, Value = val }));

                foreach (var v in keyValuePairs) {
                    ret.Headers.TryAddWithoutValidation(v.Key, v.Value);
                    ret.Content.Headers.TryAddWithoutValidation(v.Key, v.Value);
                }

                cancellationToken.Register (ret.Content.Dispose);

                ret.RequestMessage = request;
                return ret;
            }, cancellationToken).ConfigureAwait(false);
*/
        }

        async Task copyToAsync(Stream source, Stream target, CancellationToken ct)
        {
            await Task.Run(async () => {
                var buf = new byte[4096];
                var read = 0;

                do {
                    read = await source.ReadAsync(buf, 0, 4096, ct).ConfigureAwait(false);

                    if (read > 0) {
                        await target.WriteAsync(buf, 0, read, ct).ConfigureAwait(false);
                    }
                } while (!ct.IsCancellationRequested && read > 0);

                ct.ThrowIfCancellationRequested();
            }, ct).ConfigureAwait(false);
        }

    }

    public static class AwaitableOkHttp
    {
        public static Task<Response> EnqueueAsync(this Call This)
        {
            var cb = new OkTaskCallback();
            This.Enqueue(cb);

            return cb.Task;
        }

        class OkTaskCallback : Java.Lang.Object, ICallback
        {
            readonly TaskCompletionSource<Response> tcs = new TaskCompletionSource<Response>();
            public Task<Response> Task { get { return tcs.Task; } }

            public void OnFailure(Request p0, Java.Lang.Throwable p1)
            {
                tcs.TrySetException(p1);
            }

            public void OnResponse(Response p0)
            {
                tcs.TrySetResult(p0);
            }
        }
    }
}
