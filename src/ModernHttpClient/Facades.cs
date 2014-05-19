using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace ModernHttpClient.Portable
{
    public class NativeMessageHandler : HttpMessageHandler
    {
		private const string WrongVersion = "You're referencing the Portable version in your App - you need to reference the platform (iOS/Android) version";

		/// <summary>
		/// Initializes a new instance of the <see cref="ModernHttpClient.Portable.NativeMessageHandler"/> class.
		/// </summary>
		public NativeMessageHandler(): base()
		{
			throw new Exception(WrongVersion);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ModernHttpClient.Portable.NativeMessageHandler"/> class.
		/// </summary>
		/// <param name="throwOnCaptiveNetwork">If set to <c>true</c> throw on captive network (ie: a captive network is usually a wifi network where an authentication html form is shown instead of the real content).</param>
		public NativeMessageHandler(bool throwOnCaptiveNetwork): base()
		{
			throw new Exception(WrongVersion);
		}

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
			throw new Exception(WrongVersion);
        }
    }
}