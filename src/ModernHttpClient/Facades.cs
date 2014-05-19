using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;
using System.IO;

namespace ModernHttpClient.Portable
{
    public class NativeMessageHandler : HttpMessageHandler
    {
        const string wrongVersion = "You're referencing the Portable version in your App - you need to reference the platform (iOS/Android) version";

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="ModernHttpClient.Portable.NativeMessageHandler"/> class.
        /// </summary>
        public NativeMessageHandler(): base()
        {
            throw new Exception(wrongVersion);
        }

        /// <summary>
        /// Initializes a new instance of the <see
        /// cref="ModernHttpClient.Portable.NativeMessageHandler"/> class.
        /// </summary>
        /// <param name="throwOnCaptiveNetwork">If set to <c>true</c> throw on
        /// captive network (ie: a captive network is usually a wifi network
        /// where an authentication html form is shown instead of the real
        /// content).</param>
        public NativeMessageHandler(bool throwOnCaptiveNetwork) : base()
        {
            throw new Exception(wrongVersion);
	}

        public void RegisterForProgress(HttpRequestMessage request, ProgressDelegate callback)
        {
            throw new Exception(wrongVersion);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new Exception(wrongVersion);
        }
    }

    public class ProgressStreamContent : StreamContent 
    {
        const string wrongVersion = "You're referencing the Portable version in your App - you need to reference the platform (iOS/Android) version";

        ProgressStreamContent(Stream stream) : base(stream)
        {
            throw new Exception(wrongVersion);
        }

        ProgressStreamContent(Stream stream, int bufferSize) : base(stream, bufferSize)
        {
            throw new Exception(wrongVersion);
        }

        public ProgressDelegate Progress {
            get { throw new Exception(wrongVersion); }
            set { throw new Exception(wrongVersion); }
        }
    }

    public delegate void ProgressDelegate(long bytes, long totalBytes, long totalBytesExpected);
}
