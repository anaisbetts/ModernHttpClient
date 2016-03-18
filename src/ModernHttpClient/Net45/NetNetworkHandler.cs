using System;
using System.Net.Http;
using System.Net;
using System.Threading.Tasks;
using System.Threading;

namespace ModernHttpClient
{
    public class NativeMessageHandler : HttpClientHandler
    {

        public NativeMessageHandler() : this(false, false) {}

        public NativeMessageHandler(bool throwOnCaptiveNetwork, bool customSSLVerification, NativeCookieHandler cookieHandler = null)
        {
            UseCookies = cookieHandler != null;
            if (cookieHandler != null) {
                CookieContainer = cookieHandler.CookieContainer;
            }
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string requestHost = request.RequestUri.Host;
            var response = await base.SendAsync(request, cancellationToken);
            string newRequestHost = response.RequestMessage.RequestUri.Host;
            if (requestHost != newRequestHost) {
                throw new CaptiveNetworkException(new Uri(requestHost), new Uri(newRequestHost));
            }
            return response;
        }
    }
}

