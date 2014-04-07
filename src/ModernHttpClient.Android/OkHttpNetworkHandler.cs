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
    public class OkHttpNetworkHandler : HttpMessageHandler
    {
        readonly OkHttpClient client = new OkHttpClient();
        readonly bool throwOnCaptiveNetwork;

        public OkHttpNetworkHandler() : this(false) {}

        public OkHttpNetworkHandler(bool throwOnCaptiveNetwork)
        {
            this.throwOnCaptiveNetwork = throwOnCaptiveNetwork;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var java_uri = request.RequestUri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
            var url = new Java.Net.URL(java_uri);
            Java.Net.HttpURLConnection rq;
            try {
                rq = client.Open(url);
            } catch(Java.Net.UnknownHostException e) {
                throw new WebException("Name resolution failure", e, WebExceptionStatus.NameResolutionFailure, null);
            }
            rq.RequestMethod = request.Method.Method.ToUpperInvariant();

            foreach (var kvp in request.Headers) { rq.SetRequestProperty(kvp.Key, kvp.Value.FirstOrDefault()); }

            if (request.Content != null) {
                foreach (var kvp in request.Content.Headers) { rq.SetRequestProperty (kvp.Key, kvp.Value.FirstOrDefault ()); }

                await Task.Run(async () => {
                    var contentStream = await request.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    await copyToAsync(contentStream, rq.OutputStream, cancellationToken).ConfigureAwait(false);
                }, cancellationToken).ConfigureAwait(false);

                rq.OutputStream.Close();
            }

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

                ret.Content = new StreamContent(new ConcatenatingStream(new Func<Stream>[] {
                    () => ret.IsSuccessStatusCode ? rq.InputStream : new MemoryStream(),
                    () => rq.ErrorStream ?? new MemoryStream (),
                }, true));

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
        }

        async Task copyToAsync(Stream source, Stream target, CancellationToken ct)
        {
            await Task.Run(async () => {
                var buf = new byte[4096];
                var read = 0;

                do {
                    read = await source.ReadAsync(buf, 0, 4096).ConfigureAwait(false);

                    if (read > 0) {
                        target.Write(buf, 0, read);
                    }
                } while (!ct.IsCancellationRequested && read > 0);

                ct.ThrowIfCancellationRequested();
            }, ct).ConfigureAwait(false);
        }
    }
}
