using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace ModernHttpClient.Portable
{
    public class NativeMessageHandler : HttpMessageHandler
    {
        public void RegisterForProgress(HttpRequestMessage request, ProgressDelegate callback)
        {
            throw new Exception("You're referencing the Portable version in your App - you need to reference the platform (iOS/Android) version");
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new Exception("You're referencing the Portable version in your App - you need to reference the platform (iOS/Android) version");
        }
    }

    public class ProgressStreamContent : StreamContent 
    {
        ProgressStreamContent(Stream stream) : base(stream)
        {
            throw new Exception("You're referencing the Portable version in your App - you need to reference the platform (iOS/Android) version");
        }

        ProgressStreamContent(Stream stream, int bufferSize) : base(stream, bufferSize)
        {
            throw new Exception("You're referencing the Portable version in your App - you need to reference the platform (iOS/Android) version");
        }

        public ProgressDelegate Progress {
            get { throw new Exception("You're referencing the Portable version in your App - you need to reference the platform (iOS/Android) version"); }
            set { throw new Exception("You're referencing the Portable version in your App - you need to reference the platform (iOS/Android) version"); }
        }
    }

    public delegate void ProgressDelegate(long bytes, long totalBytes, long totalBytesExpected);
}
