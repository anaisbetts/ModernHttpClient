using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ModernHttpClient
{
    public class NativeMessageHandler : HttpClientHandler
    {
        const string wrongVersion = "You're referencing the Portable version in your App - you need to reference the platform (iOS/Android) version";

        readonly bool throwOnCaptiveNetwork;
        readonly NativeCookieHandler cookieHandler;

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="ModernHttpClient.Portable.NativeMessageHandler"/> class.
        /// </summary>
        public NativeMessageHandler(): this(false, false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="ModernHttpClient.Portable.NativeMessageHandler"/> class.
        /// </summary>
        /// <param name="throwOnCaptiveNetwork">If set to <c>true</c> throw on
        /// captive network (ie: a captive network is usually a wifi network
        /// where an authentication html form is shown instead of the real
        /// content).</param>
        /// <param name="customSSLVerification">Enable custom SSL certificate 
        /// verification via ServicePointManager. Disabled by default for 
        /// performance reasons (i.e. the OS default certificate verification 
        /// will take place)</param>
        /// <param name="cookieHandler">Enable native cookie handling.
        /// </param>
        public NativeMessageHandler(bool throwOnCaptiveNetwork, bool customSSLVerification, NativeCookieHandler cookieHandler = null) : base()
        {
            this.throwOnCaptiveNetwork = throwOnCaptiveNetwork;
            this.cookieHandler = cookieHandler;

            UseCookies = cookieHandler != null;
            if (cookieHandler != null) {
                CookieContainer = cookieHandler.CookieContainer;
            }
        }

        public bool DisableCaching { get; set; }

        public void RegisterForProgress(HttpRequestMessage request, ProgressDelegate callback)
        {
            throw new Exception(wrongVersion);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var reqUri = request.RequestUri;
            var response = await base.SendAsync(request, cancellationToken);
            var newUri = response.RequestMessage.RequestUri;
            if (throwOnCaptiveNetwork && reqUri.Host != newUri.Host) {
                throw new CaptiveNetworkException(reqUri, newUri);
            }
            cookieHandler.Add(reqUri);
            cookieHandler.Add(newUri);
            return response;
        }
    }
}