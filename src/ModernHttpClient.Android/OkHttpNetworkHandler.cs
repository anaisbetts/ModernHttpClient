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

                var contentStream = await request.Content.ReadAsStreamAsync();
                await copyToAsync(contentStream, rq.OutputStream, cancellationToken).ConfigureAwait(false);
                rq.OutputStream.Close();
            }

            var body = new MemoryStream();
            var reason = default(string);

            try {
                await copyToAsync(rq.InputStream, body, cancellationToken);
            } catch (Exception ex) {
                reason = ex.Message;
            } finally {
                rq.InputStream.Close();
            }

            if (reason != null) {
                try {
                    await rq.ErrorStream.CopyToAsync (body).ConfigureAwait (false);
                } finally {
                    rq.ErrorStream.Close();
                }
            }

            var ret = new HttpResponseMessage((HttpStatusCode)rq.ResponseCode) {
                Content = new ByteArrayHttpContent(body.ToArray()),
                RequestMessage = request,
                ReasonPhrase = reason,
            };

            if (rq.HeaderFields.Count > 0) {
                foreach(var k in rq.HeaderFields.Keys) {
                    if (k == null) break;
                    ret.Headers.TryAddWithoutValidation(k, rq.HeaderFields[k].FirstOrDefault());
                }
            }

            return ret;
        }

        async Task copyToAsync(Stream source, Stream target, CancellationToken ct)
        {
            var buf = new byte[4096];
            var read = 0;

            do {
                read = await source.ReadAsync(buf, 0, 4096).ConfigureAwait(false);
                if (read > 0) target.Write(buf, 0, read);
            } while (!ct.IsCancellationRequested && read > 0);

            if (ct.IsCancellationRequested) {
                throw new OperationCanceledException();
            }
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
