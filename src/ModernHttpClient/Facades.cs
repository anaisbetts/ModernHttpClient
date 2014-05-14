using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

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
    }

    public delegate void ProgressDelegate(long bytes, long totalBytes, long totalBytesExpected);
}
