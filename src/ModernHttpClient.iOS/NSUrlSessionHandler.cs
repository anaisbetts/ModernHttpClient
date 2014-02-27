using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using MonoTouch.Foundation;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace ModernHttpClient
{
    class InflightOperation
    {
        public HttpRequestMessage Request { get; set; }
        public TaskCompletionSource<HttpResponseMessage> FutureResponse { get; set; }
        public ByteArrayListStream ResponseBody { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class NSUrlSessionHandler : HttpMessageHandler
    {
        readonly NSUrlSession session;

        readonly Dictionary<NSUrlSessionTask, InflightOperation> inflightRequests = 
            new Dictionary<NSUrlSessionTask, InflightOperation>();

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

            var op = session.CreateDataTask(rq);

            cancellationToken.ThrowIfCancellationRequested();

            var ret = new TaskCompletionSource<HttpResponseMessage>();
            cancellationToken.Register(() => ret.TrySetCanceled());

            lock (inflightRequests) {
                inflightRequests[op] = new InflightOperation() {
                    FutureResponse = ret,
                    Request = request,
                    ResponseBody = new ByteArrayListStream(),
                    CancellationToken = cancellationToken,
                };
            }

            op.Resume();
            return await ret.Task;
        }

        class DataTaskDelegate : NSUrlSessionDataDelegate
        {
            NSUrlSessionHandler This { get; set; }

            public DataTaskDelegate(NSUrlSessionHandler that)
            {
                this.This = that;
            }

            public override void DidReceiveResponse(NSUrlSession session, NSUrlSessionDataTask dataTask, NSUrlResponse response, Action<NSUrlSessionResponseDisposition> completionHandler)
            {
                var data = getResponseForTask(dataTask);

                try {
                    if (data.CancellationToken.IsCancellationRequested) {
                        dataTask.Cancel();
                    }

                    var resp = (NSHttpUrlResponse)response;

                    var ret = new HttpResponseMessage((HttpStatusCode)resp.StatusCode) {
                        Content = new CancellableStreamContent(data.ResponseBody, () => {
                            Console.WriteLine("Cancelling!");
                            if (!data.IsCompleted) dataTask.Cancel();
                            data.IsCompleted = true;

                            data.ResponseBody.SetException(new OperationCanceledException());
                        }),
                        RequestMessage = data.Request,
                    };

                    foreach(var v in resp.AllHeaderFields) {
                        ret.Headers.TryAddWithoutValidation(v.Key.ToString(), v.Value.ToString());
                        ret.Content.Headers.TryAddWithoutValidation(v.Key.ToString(), v.Value.ToString());
                    }

                    data.FutureResponse.TrySetResult(ret);
                } catch (Exception ex) {
                    data.FutureResponse.TrySetException(ex);
                }

                completionHandler(NSUrlSessionResponseDisposition.Allow);
            }

            public override void DidCompleteWithError (NSUrlSession session, NSUrlSessionTask task, NSError error)
            {
                var data = getResponseForTask(task);
                data.IsCompleted = true;

                if (error != null) {
                    var ex = default(Exception);
                    if (error.Description.StartsWith("cancel", StringComparison.OrdinalIgnoreCase)) {
                        ex = new OperationCanceledException();
                    } else {
                        ex = new WebException(error.LocalizedDescription);
                    }

                    data.FutureResponse.TrySetException(ex);
                    data.ResponseBody.SetException(ex);
                    return;
                }

                data.ResponseBody.Complete();
            }

            public override void DidReceiveData (NSUrlSession session, NSUrlSessionDataTask dataTask, NSData byteData)
            {
                var data = getResponseForTask(dataTask);
                var bytes = byteData.ToArray();

                // NB: If we're cancelled, we still might have one more chunk 
                // of data that attempts to be delivered
                if (data.IsCompleted) return;

                data.ResponseBody.AddByteArray(bytes);
            }

            public override void WillCacheResponse (NSUrlSession session, NSUrlSessionDataTask dataTask, NSCachedUrlResponse proposedResponse, Action<NSCachedUrlResponse> completionHandler)
            {
                completionHandler(null);
            }

            InflightOperation getResponseForTask(NSUrlSessionTask task)
            {
                lock (This.inflightRequests) {
                    return This.inflightRequests[task];
                }
            }
        }
    }
            
    class ByteArrayListStream : Stream
    {
        Exception exception;
        IDisposable lockRelease = EmptyDisposable.Instance;
        readonly AsyncLock readStreamLock = new AsyncLock();
        readonly List<byte[]> bytes = new List<byte[]>();

        bool isCompleted;
        long maxLength = 0;
        long position = 0;

        public ByteArrayListStream()
        {
            // Initially we have nothing to read so Reads should be parked
            readStreamLock.LockAsync().ContinueWith(t => lockRelease = t.Result);
        }

        public override bool CanRead { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }
        public override void WriteByte(byte value) { throw new NotSupportedException(); }
        public override bool CanSeek { get { return false; } }
        public override bool CanTimeout { get { return false; } }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin)
        { 
            var result = default(long);
            switch (origin) {
            case SeekOrigin.Begin:
                result = offset;
                break;
            case SeekOrigin.Current:
                result = position + offset;
                break;
            case SeekOrigin.End:
                result = maxLength + offset;
                break;
            }
                
            return (Position = result);
        }

        public override long Position {
            get { return position; }
            set {
                if (exception != null) throw exception;

                if (value < 0 || value > maxLength) {
                    throw new ArgumentException();
                }

                // Seeking during a read? No way.
                lock (bytes) {
                    position = value;

                    // NB: If we seek back to where we have more data,
                    // unblock anyone waiting
                    if (position < maxLength) {
                        Interlocked.Exchange(ref lockRelease, EmptyDisposable.Instance).Dispose();
                    }
                }
            }
        }

        public override long Length {
            get {
                return maxLength;
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.ReadAsync(buffer, offset, count).Result;
        }

        /* OMG THIS CODE IS COMPLICATED
         *
         * Here's the core idea. We want to create a ReadAsync function that
         * reads from our list of byte arrays **until it gets to the end of
         * our current list**.
         *
         * If we're not there yet, we keep returning data, serializing access
         * to the underlying position pointer (i.e. we definitely don't want
         * people concurrently moving position along). If we try to read past
         * the end, we return the section of data we could read and complete
         * it.
         *
         * Here's where the tricky part comes in. If we're not Completed (i.e.
         * the caller still wants to add more byte arrays in the future) and
         * we're at the end of the current stream, we want to *block* the read
         * (not blocking, but async blocking whatever you know what I mean),
         * until somebody adds another byte[] to chew through, or if someone
         * rewinds the position.
         *
         * If we *are* completed, we should return zero to simply complete the
         * read, signalling we're at the end of the stream */
        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
        retry:
            int bytesRead = 0;

            if (isCompleted && position == maxLength) {
                return 0;
            }

            if (exception != null) throw exception;

            using (await readStreamLock.LockAsync()) {
                lock (bytes) {
                    int absPositionOfCurrentBuffer = 0;

                    foreach (var buf in bytes) {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (exception != null) throw exception;

                        // Get ourselves to the right buffer
                        if (position > absPositionOfCurrentBuffer + buf.Length) {
                            absPositionOfCurrentBuffer += buf.Length;
                            continue;
                        }

                        int offsetInSrcBuffer = Math.Max(0, (int)position - absPositionOfCurrentBuffer);
                        int toCopy = Math.Min(count, buf.Length - offsetInSrcBuffer);
                        Array.ConstrainedCopy(buf, offsetInSrcBuffer, buffer, offset, toCopy);

                        count -= toCopy;
                        offset += toCopy;
                        bytesRead += toCopy;
                        absPositionOfCurrentBuffer += buf.Length;

                        if (count < 0) break;
                    }

                    position += bytesRead;
                }
            }

            // If we're at the end of the stream and it's not done, prepare
            // the next read to park itself unless AddByteArray or Complete 
            // posts
            if (position >= maxLength && !isCompleted) {
                lockRelease = await readStreamLock.LockAsync();
            }

            if (bytesRead == 0 && !isCompleted) {
                // NB: There are certain race conditions where we somehow acquire
                // the lock yet are at the end of the stream, and we're not completed
                // yet. We should try again so that we can get stuck in the lock.
                goto retry;
            }

            if (cancellationToken.IsCancellationRequested) {
                Interlocked.Exchange(ref lockRelease, EmptyDisposable.Instance).Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (exception != null) {
                Interlocked.Exchange(ref lockRelease, EmptyDisposable.Instance).Dispose();

                // NB: Throwing the OperationCanceledException here that we set
                // when the CancellationStreamContent is disposed doesn't seem
                // to be recognized by async/await, so we have to lie instead.
                return 0;
            }

            return bytesRead;
        }

        public void AddByteArray(byte[] arrayToAdd)
        {
            if (exception != null) throw exception;
            if (isCompleted) throw new InvalidOperationException("Can't add byte arrays once Complete() is called");

            lock (bytes) {
                maxLength += arrayToAdd.Length;
                bytes.Add(arrayToAdd);
                Console.WriteLine("Added a new byte array, {0}: max = {1}", arrayToAdd.Length, maxLength);
            }

            Interlocked.Exchange(ref lockRelease, EmptyDisposable.Instance).Dispose();
        }

        public void Complete()
        {
            isCompleted = true;
            Interlocked.Exchange(ref lockRelease, EmptyDisposable.Instance).Dispose();
        }

        public void SetException(Exception ex)
        {
            exception = ex;
            Complete();
        }
    }

    sealed class CancellableStreamContent : StreamContent
    {
        Action onDispose;

        public CancellableStreamContent(Stream source, Action onDispose) : base(source)
        {
            this.onDispose = onDispose;
        }

        protected override void Dispose(bool disposing)
        {
            var disp = Interlocked.Exchange(ref onDispose, null);
            if (disp != null) disp();

            base.Dispose(disposing);
        }
    }

    sealed class EmptyDisposable : IDisposable
    {
        static readonly IDisposable instance = new EmptyDisposable();
        public static IDisposable Instance { get { return instance; } }

        EmptyDisposable() { }
        public void Dispose() { }
    }
}
