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

                var contentStream = await Task.Run(async () => await request.Content.ReadAsStreamAsync()).ConfigureAwait(false);
                await copyToAsync(contentStream, rq.OutputStream, cancellationToken).ConfigureAwait(false);

                rq.OutputStream.Close();
            }

            return await Task.Run (() => {
                if (cancellationToken.IsCancellationRequested) {
                    throw new TaskCanceledException();
                }

                // XXX: Because of Xamarin's implementation of HttpClient, the 
                // CancellationToken passed above gets Disposed as soon as this
                // method returns. This means that this cancelationToken is most
                // likely ineffective. 
                return new HttpResponseMessage ((HttpStatusCode)rq.ResponseCode) {
                    Content = new StreamContent (new ConcatenatingStream(new[] {
                        rq.InputStream,
                        rq.ErrorStream ?? new MemoryStream (),
                    }, true, cancellationToken)),
                    RequestMessage = request,
                };
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

                if (ct.IsCancellationRequested) {
                    throw new OperationCanceledException();
                }
            });
        }
    }

    // This is a hacked up version of http://stackoverflow.com/a/3879246/5728
    class ConcatenatingStream : Stream
    {
        CancellationToken ct;
        long position;
        bool closeStreams;
        int isEnding = 0;

        IEnumerator<Stream> iterator;
        Stream current;
       
        Stream Current {
            get {
                if (current != null) return current;
                if (iterator == null) throw new ObjectDisposedException(GetType().Name);

                if (iterator.MoveNext()) {
                    current = iterator.Current;
                }

                return current;
            }
        }

        public ConcatenatingStream(IEnumerable<Stream> source, bool closeStreams, CancellationToken ct)
        {
            if (source == null) throw new ArgumentNullException("source");

            iterator = source.GetEnumerator();

            this.ct = ct;
            ct.Register (() => {
                // NB: This registration seems to not fire often, so we need
                // to also check it in Read().
                EndOfStream();
            });

            this.closeStreams = closeStreams;
        }

        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override void WriteByte(byte value) { throw new NotSupportedException(); }
        public override bool CanSeek { get { return false; } }
        public override bool CanTimeout { get { return false; } }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }

        public override void Flush() { }
        public override long Length {
            get { throw new NotSupportedException(); }
        }

        public override long Position {
            get { return position; }
            set { if (value != this.position) throw new NotSupportedException(); }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int result = 0;
            while (count > 0) {
                if (ct.IsCancellationRequested) {
                    EndOfStream();
                    throw new OperationCanceledException ();
                }

                Stream stream = Current;
                if (stream == null) break;
                int thisCount = stream.Read(buffer, offset, count);

                result += thisCount;
                count -= thisCount;
                offset += thisCount;
                if (thisCount == 0) EndOfStream();
            }

            position += result;
            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) {
                EndOfStream();
                iterator.Dispose();
                iterator = null;
                current = null;
            }

            base.Dispose(disposing);
        }

        void EndOfStream() 
        {
            if (Interlocked.CompareExchange(ref isEnding, 1, 0) == 1) {
                // Someone else is already Ending
                return;
            }

            if (closeStreams && current != null) {
                current.Close();
                current.Dispose();

                while (iterator.MoveNext ()) { iterator.Current.Dispose (); }
            }

            current = null;
        }
    }
}
