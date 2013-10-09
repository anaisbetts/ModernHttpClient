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
            cancellationToken.Register(() => client.Cancel(rq));

            foreach (var kvp in request.Headers) { rq.SetRequestProperty(kvp.Key, kvp.Value.FirstOrDefault()); }
            rq.RequestMethod = request.Method.Method.ToUpperInvariant();

            if (request.Content != null) {
                await request.Content.CopyToAsync(rq.OutputStream).ConfigureAwait(false);
                rq.OutputStream.Close();

                foreach (var kvp in request.Content.Headers) { rq.SetRequestProperty (kvp.Key, kvp.Value.FirstOrDefault ()); }
            }


            var body = new MemoryStream();
            var reason = default(string);


            try {
                await Task.Run(() => {
                    rq.InputStream.CopyTo(body);
                }).ConfigureAwait(false);
            } catch (Exception ex) {
                reason = ex.Message;
            }

            if (reason != null) {
                await rq.ErrorStream.CopyToAsync(body).ConfigureAwait(false);
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
