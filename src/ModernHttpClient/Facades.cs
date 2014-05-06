using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace ModernHttpClient.Portable
{
    public class NativeMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new Exception("You're referencing the Portable version in your App - you need to reference the platform (iOS/Android) version");
        }
    }
}

