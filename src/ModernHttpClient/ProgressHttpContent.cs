using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Threading;

namespace ModernHttpClient
{
    public delegate void ProgressDelegate (long bytes, long totalBytes, long totalBytesExpected);

    public partial class ProgressStreamContent : StreamContent
    {
        public ProgressStreamContent(Stream stream)
            : this(new ProgressStream(stream))
        {
        }

        public ProgressStreamContent(Stream stream, int bufferSize)
            : this(new ProgressStream(stream), bufferSize)
        {
        }

        private ProgressStreamContent(ProgressStream stream)
            : base(stream)
        {
            Init(stream);
        }

        private ProgressStreamContent(ProgressStream stream, int bufferSize)
            : base(stream, bufferSize)
        {
            Init(stream);
        }

        private void Init(ProgressStream stream)
        {
            stream.ReadCallback = ReadBytes;

            Progress = delegate { };
        }

        private void Reset()
        {
            _totalBytes = 0L;
        }

        private long _totalBytes;
        private long _totalBytesExpected = -1;

        private void ReadBytes(long bytes) 
        {
            long computedLength;
            if (_totalBytesExpected == -1 && TryComputeLength(out computedLength)) 
                _totalBytesExpected = computedLength;

            // if less than zero still then change to -1
            _totalBytesExpected = Math.Max(-1, _totalBytesExpected);
            _totalBytes += bytes;

            Progress(bytes, _totalBytes, _totalBytesExpected);
        }

        private ProgressDelegate _progress;
        public ProgressDelegate Progress
        {
            get { return _progress; }
            set { 
                if (value == null) _progress = delegate { };
                else _progress = value;
            }
        }

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
        {
            Reset();
            return base.SerializeToStreamAsync(stream, context);
        }

        protected override bool TryComputeLength(out long length)
        {
            var result = base.TryComputeLength(out length);
            _totalBytesExpected = length;
            return result;
        }

        private class ProgressStream : Stream
        {
            public ProgressStream(Stream stream)
            {
                ParentStream = stream;

                ReadCallback = delegate { };
                WriteCallback = delegate { };
            }

            public Action<long> ReadCallback { get; set; }

            public Action<long> WriteCallback { get; set; }

            public Stream ParentStream { get; private set; }

            public override bool CanRead { get { return ParentStream.CanRead; } }

            public override bool CanSeek { get { return ParentStream.CanSeek; } }

            public override bool CanWrite { get { return ParentStream.CanWrite; } }

            public override bool CanTimeout { get { return ParentStream.CanTimeout; } }

            public override long Length { get { return ParentStream.Length; } }

            public override void Flush()
            {
                ParentStream.Flush();
            }

            public override Task FlushAsync(CancellationToken cancellationToken)
            {
                return ParentStream.FlushAsync(cancellationToken);
            }

            public override long Position
            {
                get { return ParentStream.Position; }
                set { ParentStream.Position = value; }
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var readCount = ParentStream.Read(buffer, offset, count);
                ReadCallback(readCount);
                return readCount;
            }

            public override long Seek(long offset, SeekOrigin origin)
            {
                return ParentStream.Seek(offset, origin);
            }

            public override void SetLength(long value)
            {
                ParentStream.SetLength(value);
            }

            public override void Write(byte[] buffer, int offset, int count)
            {
                ParentStream.Write(buffer, offset, count);
                WriteCallback(count);
            }

            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var readCount = await ParentStream.ReadAsync(buffer, offset, count, cancellationToken);
                ReadCallback(readCount);
                return readCount;
            }

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                var task = ParentStream.WriteAsync(buffer, offset, count, cancellationToken);
                WriteCallback(count);
                return task;
            }

            protected override void Dispose(bool disposing)
            {
                if (disposing) {
                    ParentStream.Dispose();
                }
            }
        }
    }
}

