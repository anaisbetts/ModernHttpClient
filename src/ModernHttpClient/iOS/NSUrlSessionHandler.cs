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
        public ProgressDelegate Progress { get; set; }
        public ByteArrayListStream ResponseBody { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public bool IsCompleted { get; set; }
    }

    public class NativeMessageHandler : HttpMessageHandler
    {
        readonly NSUrlSession session;

        readonly Dictionary<NSUrlSessionTask, InflightOperation> inflightRequests = 
            new Dictionary<NSUrlSessionTask, InflightOperation>();

        readonly Dictionary<HttpRequestMessage, ProgressDelegate> registeredProgressCallbacks = 
            new Dictionary<HttpRequestMessage, ProgressDelegate>();

        readonly bool throwOnCaptiveNetwork;

        public NativeMessageHandler(): this(false) { }
        public NativeMessageHandler(bool throwOnCaptiveNetwork)
        {
            session = NSUrlSession.FromConfiguration(
                NSUrlSessionConfiguration.DefaultSessionConfiguration, 
                new DataTaskDelegate(this), null);

            this.throwOnCaptiveNetwork = throwOnCaptiveNetwork;
        }

        public void RegisterForProgress(HttpRequestMessage request, ProgressDelegate callback)
        {
            if (callback == null && registeredProgressCallbacks.ContainsKey(request)) {
                registeredProgressCallbacks.Remove(request);
                return;
            }

            registeredProgressCallbacks[request] = callback;
        }

        ProgressDelegate getAndRemoveCallbackFromRegister(HttpRequestMessage request)
        {
            ProgressDelegate emptyDelegate = delegate { };

            lock (registeredProgressCallbacks) {
                if (!registeredProgressCallbacks.ContainsKey(request)) return emptyDelegate;

                var callback = registeredProgressCallbacks[request];
                registeredProgressCallbacks.Remove(request);
                return callback;
            }
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
                    Progress = getAndRemoveCallbackFromRegister(request),
                    ResponseBody = new ByteArrayListStream(),
                    CancellationToken = cancellationToken,
                };
            }

            op.Resume();
            return await ret.Task.ConfigureAwait(false);
        }

        class DataTaskDelegate : NSUrlSessionDataDelegate
        {
            NativeMessageHandler This { get; set; }

            public DataTaskDelegate(NativeMessageHandler that)
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
                    var req = data.Request;

                    if (This.throwOnCaptiveNetwork && req.RequestUri.Host != resp.Url.Host) {
                        throw new CaptiveNetworkException(req.RequestUri, new Uri(resp.Url.ToString()));
                    }

                    var content = new CancellableStreamContent(data.ResponseBody, () => {
                        if (!data.IsCompleted) {
                            dataTask.Cancel();
                        }
                        data.IsCompleted = true;

                        data.ResponseBody.SetException(new OperationCanceledException());
                    });

                    content.Progress = data.Progress;

                    var ret = new HttpResponseMessage((HttpStatusCode)resp.StatusCode) {
                        Content = content,
                        RequestMessage = data.Request,
                    };

                    foreach(var v in resp.AllHeaderFields) {
                        // NB: Cocoa trolling us so hard by giving us back dummy
                        // dictionary entries
                        if (v.Key == null || v.Value == null) continue;

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
                    var ex = createExceptionForNSError(error);

                    // Pass the exception to the response
                    data.FutureResponse.TrySetException(ex);
                    data.ResponseBody.SetException(ex);
                    return;
                }

                data.ResponseBody.Complete();

                lock (This.inflightRequests) {
                    This.inflightRequests.Remove(task);
                }
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

            InflightOperation getResponseForTask(NSUrlSessionTask task)
            {
                lock (This.inflightRequests) {
                    return This.inflightRequests[task];
                }
            }

            Exception createExceptionForNSError(NSError error)
            {
                var ret = default(Exception);
                var urlError = default(NSUrlError);
                var webExceptionStatus = WebExceptionStatus.UnknownError;

                // If the domain is something other than NSUrlErrorDomain, 
                // just grab the default info
                if (error.Domain != NSError.NSUrlErrorDomain) goto leave;

                // Convert the error code into an enumeration (this is future
                // proof, rather than just casting integer)
                if (!Enum.TryParse<NSUrlError>(error.Code.ToString(), out urlError)) urlError = NSUrlError.Unknown;

                // Parse the enum into a web exception status or exception. some
                // of these values don't necessarily translate completely to
                // what WebExceptionStatus supports, so made some best guesses
                // here.  for your reading pleasure, compare these:
                //
                // Apple docs: https://developer.apple.com/library/mac/documentation/Cocoa/Reference/Foundation/Miscellaneous/Foundation_Constants/Reference/reference.html
                // .NET docs: http://msdn.microsoft.com/en-us/library/system.net.webexceptionstatus(v=vs.110).aspx
                switch(urlError) {
                case NSUrlError.Cancelled:
                case NSUrlError.UserCancelledAuthentication:
                    ret = new OperationCanceledException();
                    break;
                case NSUrlError.BadURL:
                case NSUrlError.UnsupportedURL:
                case NSUrlError.CannotConnectToHost:
                case NSUrlError.ResourceUnavailable:
                case NSUrlError.NotConnectedToInternet:
                case NSUrlError.UserAuthenticationRequired:
                    webExceptionStatus = WebExceptionStatus.ConnectFailure;
                    break;
                case NSUrlError.TimedOut:
                    webExceptionStatus = WebExceptionStatus.Timeout;
                    break;
                case NSUrlError.CannotFindHost:
                case NSUrlError.DNSLookupFailed:
                    webExceptionStatus = WebExceptionStatus.NameResolutionFailure;
                    break;
                case NSUrlError.DataLengthExceedsMaximum:
                    webExceptionStatus = WebExceptionStatus.MessageLengthLimitExceeded;
                    break;
                case NSUrlError.NetworkConnectionLost:
                    webExceptionStatus = WebExceptionStatus.ConnectionClosed;
                    break;
                case NSUrlError.HTTPTooManyRedirects:
                case NSUrlError.RedirectToNonExistentLocation:
                    webExceptionStatus = WebExceptionStatus.ProtocolError;
                    break;
                case NSUrlError.BadServerResponse:
                case NSUrlError.ZeroByteResource:
                case NSUrlError.CannotDecodeContentData:
                case NSUrlError.CannotDecodeRawData:
                case NSUrlError.CannotParseResponse:
                case NSUrlError.FileDoesNotExist:
                case NSUrlError.FileIsDirectory:
                case NSUrlError.NoPermissionsToReadFile:
                case NSUrlError.CannotLoadFromNetwork:
                case NSUrlError.CannotCreateFile:
                case NSUrlError.CannotOpenFile:
                case NSUrlError.CannotCloseFile:
                case NSUrlError.CannotWriteToFile:
                case NSUrlError.CannotRemoveFile:
                case NSUrlError.CannotMoveFile:
                case NSUrlError.DownloadDecodingFailedMidStream:
                case NSUrlError.DownloadDecodingFailedToComplete:
                    webExceptionStatus = WebExceptionStatus.ReceiveFailure;
                    break;
                case NSUrlError.SecureConnectionFailed:
                    webExceptionStatus = WebExceptionStatus.SecureChannelFailure;
                    break;
                case NSUrlError.ServerCertificateHasBadDate:
                case NSUrlError.ServerCertificateHasUnknownRoot:
                case NSUrlError.ServerCertificateNotYetValid:
                case NSUrlError.ServerCertificateUntrusted:
                case NSUrlError.ClientCertificateRejected:
                    webExceptionStatus = WebExceptionStatus.TrustFailure;
                    break;
                }

                // If we parsed a web exception status code, create an exception
                // for it
                if (webExceptionStatus != WebExceptionStatus.UnknownError) {
                    ret = new WebException(error.LocalizedDescription, webExceptionStatus);
                }

            leave:
                // If no exception generated yet, throw a normal exception with
                // the error message.
                return ret ?? new Exception(error.LocalizedDescription);
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
        int offsetInCurrentBuffer = 0;

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
            throw new NotSupportedException();
        }

        public override long Position {
            get { return position; }
            set {
                throw new NotSupportedException();
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
            int buffersToRemove = 0;

            if (isCompleted && position == maxLength) {
                return 0;
            }

            if (exception != null) throw exception;

            using (await readStreamLock.LockAsync().ConfigureAwait(false)) {
                lock (bytes) {
                    foreach (var buf in bytes) {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (exception != null) throw exception;

                        int toCopy = Math.Min(count, buf.Length - offsetInCurrentBuffer);
                        Array.ConstrainedCopy(buf, offsetInCurrentBuffer, buffer, offset, toCopy);

                        count -= toCopy;
                        offset += toCopy;
                        bytesRead += toCopy;

                        offsetInCurrentBuffer += toCopy;

                        if (offsetInCurrentBuffer >= buf.Length) {
                            offsetInCurrentBuffer = 0;
                            buffersToRemove++;
                        }

                        if (count <= 0) break;
                    }

                    // Remove buffers that we read in this operation
                    bytes.RemoveRange(0, buffersToRemove);

                    position += bytesRead;
                }
            }

            // If we're at the end of the stream and it's not done, prepare
            // the next read to park itself unless AddByteArray or Complete 
            // posts
            if (position >= maxLength && !isCompleted) {
                lockRelease = await readStreamLock.LockAsync().ConfigureAwait(false);
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
                throw exception;
            }

            if (isCompleted && position < maxLength) {
                // NB: This solves a rare deadlock 
                //
                // 1. ReadAsync called (waiting for lock release)
                // 2. AddByteArray called (release lock)
                // 3. AddByteArray called (release lock)
                // 4. Complete called (release lock the last time)
                // 5. ReadAsync called (lock released at this point, the method completed successfully) 
                // 6. ReadAsync called (deadlock on LockAsync(), because the lock is block, and there is no way to release it)
                // 
                // Current condition forces the lock to be released in the end of 5th point

                Interlocked.Exchange(ref lockRelease, EmptyDisposable.Instance).Dispose();
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
                //Console.WriteLine("Added a new byte array, {0}: max = {1}", arrayToAdd.Length, maxLength);
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

    sealed class CancellableStreamContent : ProgressStreamContent
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

            // EVIL HAX: We have to let at least one ReadAsync of the underlying
            // stream fail with OperationCancelledException before we can dispose
            // the base, or else the exception coming out of the ReadAsync will
            // be an ObjectDisposedException from an internal MemoryStream. This isn't
            // the Ideal way to fix this, but #yolo.
            Task.Run(() => base.Dispose(disposing));
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
