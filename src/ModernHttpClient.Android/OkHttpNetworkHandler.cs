using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using System.IO;
using System.Net.Http;
using OkHttp;
using System.Net;

namespace ModernHttpClient
{
    public class OkHttpNetworkHandler : HttpMessageHandler
    {
        readonly OkHttpClient client = new OkHttpClient();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var rq = client.Open(new Java.Net.URL(request.RequestUri.ToString()));
            rq.RequestMethod = request.Method.Method.ToUpperInvariant();

            foreach (var kvp in request.Headers) { rq.SetRequestProperty(kvp.Key, kvp.Value.FirstOrDefault()); }

            if (request.Content != null) {
                foreach (var kvp in request.Content.Headers) { rq.SetRequestProperty (kvp.Key, kvp.Value.FirstOrDefault ()); }

                await Task.Run(async () => {
                    var contentStream = await request.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    await copyToAsync(contentStream, rq.OutputStream, cancellationToken).ConfigureAwait(false);
                }).ConfigureAwait(false);

                rq.OutputStream.Close();
            }

            return await Task.Run (() => {
                cancellationToken.ThrowIfCancellationRequested();

                // NB: This is the line that blocks until we have headers
                var ret = new HttpResponseMessage((HttpStatusCode)rq.ResponseCode);

                cancellationToken.ThrowIfCancellationRequested();

                ret.Content = new StreamContent(new ConcatenatingStream(new Func<Stream>[] {
                    () => rq.InputStream,
                    () => rq.ErrorStream ?? new MemoryStream (),
                }, true));

                cancellationToken.Register (ret.Content.Dispose);

                ret.RequestMessage = request;
                return ret;
            });
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
            });
        }
    }
}
