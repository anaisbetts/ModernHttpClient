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
            var headers = request.Headers as IEnumerable<KeyValuePair<string, IEnumerable<string>>>;
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
                Url = NSUrl.FromString(request.RequestUri.AbsoluteUri),
            };

            var host = request.RequestUri.GetLeftPart(UriPartial.Authority);
            var op = default(AFHTTPRequestOperation);
            var err = default(NSError);
            var handler = new AFHTTPClient(new NSUrl(host));

            // NB: I have no idea how async methods affects object lifetime and
            // GC'ing of local variables, soooooooo....
            lock (pins) { pins[rq] = new object[] { op, handler, }; }

            var blockingTcs = new TaskCompletionSource<bool>();
            var retBox = new HttpResponseMessage[1];
            try {
                op = await enqueueOperation(handler, new AFHTTPRequestOperation(rq), cancellationToken, () => blockingTcs.SetResult(true), ex => {
                    if (ex is ApplicationException) {
                        err = (NSError)ex.Data["err"];
                    }

                    retBox[0].ReasonPhrase = (err != null ? err.LocalizedDescription : null);
                });
            } catch (ApplicationException ex) {
                op = (AFHTTPRequestOperation)ex.Data["op"];
                err = (NSError)ex.Data["err"];
            }

            var resp = (NSHttpUrlResponse)op.Response;

            if (err != null && resp == null && err.Domain == NSError.NSUrlErrorDomain && err.Code == -1009) {
                throw new WebException (err.LocalizedDescription, WebExceptionStatus.NameResolutionFailure);
            }

            if (op.IsCancelled) {
                lock (pins) { pins.Remove(rq); }
                throw new TaskCanceledException();
            }

            var respData = op.ResponseData;
            var httpContent = new StreamContent (
                new ConcatenatingStream(new Func<Stream>[] { 
                    () => new MemoryStream(), 
                    () => respData == null || op.ResponseData.Length == 0 ? Stream.Null : op.ResponseData.AsStream() 
                },
                true,
                cancellationToken,
                blockingTcs.Task));

            var ret = new HttpResponseMessage((HttpStatusCode)resp.StatusCode) {
                Content = httpContent,
                RequestMessage = request,
                ReasonPhrase = (err != null ? err.LocalizedDescription : null),
            };

            retBox[0] = ret;

            foreach(var v in resp.AllHeaderFields) {
                ret.Headers.TryAddWithoutValidation(v.Key.ToString(), v.Value.ToString());
            }

            lock (pins) { pins.Remove(rq); }
            return ret;
        }

        Task<AFHTTPRequestOperation> enqueueOperation(AFHTTPClient handler, AFHTTPRequestOperation operation, CancellationToken cancelToken, Action onCompleted, Action<Exception> onError)
        {
            var tcs = new TaskCompletionSource<AFHTTPRequestOperation>();
            if (cancelToken.IsCancellationRequested) {
                tcs.SetCanceled();
                return tcs.Task;
            }

            bool completed = false;

            operation.SetDownloadProgressBlock((a, b, c) => {
                // NB: We're totally cheating here, we just happen to know
                // that we're guaranteed to have response headers after the
                // first time we get progress.
                if (completed) return;

                completed = true;
                tcs.SetResult(operation);
            });

            operation.SetCompletionBlockWithSuccess(
                (op, _) => onCompleted(),
                (op, err) => {
                    var ex = new ApplicationException();
                    ex.Data.Add("op", op);
                    ex.Data.Add("err", err);

                    onCompleted();
                    if (completed) {
                        onError(ex);
                        return;
                    }

                    // NB: Secret Handshake is Secret
                    completed = true;
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

    // This is a hacked up version of http://stackoverflow.com/a/3879246/5728
    class ConcatenatingStream : Stream
    {
        CancellationToken ct;
        long position;
        bool closeStreams;
        int isEnding = 0;
        Task blockUntil;

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

        public ConcatenatingStream(IEnumerable<Func<Stream>> source, bool closeStreams, CancellationToken ct, Task blockUntil = null)
        {
            if (source == null) throw new ArgumentNullException("source");

            iterator = source.Select(x => x()).GetEnumerator();

            this.ct = ct;
            ct.Register (() => {
                // NB: This registration seems to not fire often, so we need
                // to also check it in Read().
                EndOfStream();
            });

            this.closeStreams = closeStreams;
            this.blockUntil = blockUntil;
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

            if (blockUntil != null) {
                // XXX: Because of https://github.com/mono/mono/pull/792, we can't
                // actually use the CancellationToken here.
                //blockUntil.Wait(ct);
                blockUntil.Wait();
            }

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
