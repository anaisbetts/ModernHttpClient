using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ModernHttpClient
{
    // This is a hacked up version of http://stackoverflow.com/a/3879246/5728
    class ConcatenatingStream : Stream
    {
        readonly CancellationTokenSource cts = new CancellationTokenSource();
        readonly Action onDispose;

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

        public ConcatenatingStream(IEnumerable<Func<Stream>> source, bool closeStreams, Task blockUntil = null, Action onDispose = null)
        {
            if (source == null) throw new ArgumentNullException("source");

            iterator = source.Select(x => x()).GetEnumerator();

            this.closeStreams = closeStreams;
            this.blockUntil = blockUntil;
            this.onDispose = onDispose;
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

        public override async Task<int> ReadAsync (byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            int result = 0;

            if (blockUntil != null) {
                await blockUntil.ContinueWith(_ => {}, cancellationToken);
            }

            while (count > 0) {
                if (cancellationToken.IsCancellationRequested) {
                    throw new OperationCanceledException();
                }

                if (cts.IsCancellationRequested) {
                    throw new OperationCanceledException();
                }

                Stream stream = Current;
                if (stream == null) break;

                var thisCount = default(int);
                thisCount = await stream.ReadAsync(buffer, offset, count, cancellationToken);

                result += thisCount;
                count -= thisCount;
                offset += thisCount;
                if (thisCount == 0) EndOfStream();
            }
                            
            if (cancellationToken.IsCancellationRequested) {
                throw new OperationCanceledException();
            }
                            
            if (cts.IsCancellationRequested) {
                throw new OperationCanceledException();
            }

            position += result;
            return result;
        }

        public override int Read (byte[] buffer, int offset, int count)
        {
            return readInternal(buffer, offset, count);
        }

        int readInternal(byte[] buffer, int offset, int count, CancellationToken ct = default(CancellationToken))
        {
            int result = 0;

            if (blockUntil != null) {
                blockUntil.Wait(cts.Token);
            }

            while (count > 0) {
                if (ct.IsCancellationRequested) {
                    throw new OperationCanceledException();
                }

                if (cts.IsCancellationRequested) {
                    throw new OperationCanceledException();
                }

                Stream stream = Current;
                if (stream == null) break;

                var thisCount = default(int);
                thisCount = stream.Read(buffer, offset, count);

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
            if (Interlocked.CompareExchange(ref isEnding, 1, 0) == 1) {
                return;
            }

            if (disposing) {
                cts.Cancel();

                while (Current != null) {
                    EndOfStream();
                }

                iterator.Dispose();
                iterator = null;
                current = null;

                if (onDispose != null) onDispose();
            }

            base.Dispose(disposing);
        }

        void EndOfStream() 
        {
            if (closeStreams && current != null) {
                current.Close();
                current.Dispose();
            }

            current = null;
        }
    }
}

