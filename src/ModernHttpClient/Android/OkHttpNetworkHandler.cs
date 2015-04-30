using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using OkHttp;
using Javax.Net.Ssl;
using System.Text.RegularExpressions;
using Java.IO;
using System.Security.Cryptography.X509Certificates;
using System.Globalization;
using Android.OS;

namespace ModernHttpClient
{
    public class NativeMessageHandler : HttpClientHandler
    {
        readonly OkHttpClient client = new OkHttpClient();
        readonly bool throwOnCaptiveNetwork;

        readonly Dictionary<HttpRequestMessage, WeakReference> registeredProgressCallbacks =
            new Dictionary<HttpRequestMessage, WeakReference>();
        readonly Dictionary<string, string> headerSeparators = 
            new Dictionary<string,string>(){ 
                {"User-Agent", " "}
            };

        public bool DisableCaching { get; set; }

        public NativeMessageHandler() : this(false, false) {}

        public NativeMessageHandler(bool throwOnCaptiveNetwork, bool customSSLVerification, NativeCookieHandler cookieHandler = null)
        {
            this.throwOnCaptiveNetwork = throwOnCaptiveNetwork;

            if (customSSLVerification) client.SetHostnameVerifier(new HostnameVerifier());
        }

        public void RegisterForProgress(HttpRequestMessage request, ProgressDelegate callback)
        {
            if (callback == null && registeredProgressCallbacks.ContainsKey(request)) {
                registeredProgressCallbacks.Remove(request);
                return;
            }

            registeredProgressCallbacks[request] = new WeakReference(callback);
        }

        ProgressDelegate getAndRemoveCallbackFromRegister(HttpRequestMessage request)
        {
            ProgressDelegate emptyDelegate = delegate { };

            lock (registeredProgressCallbacks) {
                if (!registeredProgressCallbacks.ContainsKey(request)) return emptyDelegate;

                var weakRef = registeredProgressCallbacks[request];
                if (weakRef == null) return emptyDelegate;

                var callback = weakRef.Target as ProgressDelegate;
                if (callback == null) return emptyDelegate;

                registeredProgressCallbacks.Remove(request);
                return callback;
            }
        }

        private string GetHeaderSeparator(string name)
        {
            if (headerSeparators.ContainsKey(name))
                return headerSeparators[name];
            return ",";
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var java_uri = request.RequestUri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
            var url = new Java.Net.URL(java_uri);

            var body = default(RequestBody);
            if (request.Content != null) {
                var bytes = await request.Content.ReadAsByteArrayAsync().ConfigureAwait(false);

                var contentType = String.Join (" ", request.Content.Headers.GetValues ("Content-Type"));
                body = RequestBody.Create(MediaType.Parse(contentType), bytes);
            }

            var builder = new Request.Builder()
                .Method(request.Method.Method.ToUpperInvariant(), body)
                .Url(url);

            var keyValuePairs = request.Headers
                .Union(request.Content != null ?
                    (IEnumerable<KeyValuePair<string, IEnumerable<string>>>)request.Content.Headers :
                    Enumerable.Empty<KeyValuePair<string, IEnumerable<string>>>());

            foreach (var kvp in keyValuePairs) builder.AddHeader(kvp.Key, String.Join(GetHeaderSeparator(kvp.Key), kvp.Value));

            cancellationToken.ThrowIfCancellationRequested();

            var rq = builder.Build();
            var call = client.NewCall(rq);

            // NB: Even closing a socket must be done off the UI thread. Cray!
            cancellationToken.Register(() => Task.Run(() => call.Cancel()));

            var resp = default(Response);
            try {
                resp = await call.EnqueueAsync().ConfigureAwait(false);
                var newReq = resp.Request();
                var newUri = newReq == null ? null : newReq.Uri();
                request.RequestUri = new Uri(newUri.ToString());
                if (throwOnCaptiveNetwork && newUri != null) {
                    if (url.Host != newUri.Host) {
                        throw new CaptiveNetworkException(new Uri(java_uri), new Uri(newUri.ToString()));
                    }
                }
            } catch (IOException ex) {
                if (ex.Message.ToLowerInvariant().Contains("canceled")) {
                    throw new OperationCanceledException();
                }

                throw;
            }

            var respBody = resp.Body();

            cancellationToken.ThrowIfCancellationRequested();

            var ret = new HttpResponseMessage((HttpStatusCode)resp.Code());
            ret.RequestMessage = request;
            ret.ReasonPhrase = resp.Message();
            if (respBody != null) {
                var content = new ProgressStreamContent(respBody.ByteStream(), cancellationToken);
                content.Progress = getAndRemoveCallbackFromRegister(request);
                ret.Content = content;
            } else {
                ret.Content = new ByteArrayContent(new byte[0]);
            }

            var respHeaders = resp.Headers();
            foreach (var k in respHeaders.Names()) {
                ret.Headers.TryAddWithoutValidation(k, respHeaders.Get(k));
                ret.Content.Headers.TryAddWithoutValidation(k, respHeaders.Get(k));
            }

            return ret;
        }
    }

    public static class AwaitableOkHttp
    {
        public static Task<Response> EnqueueAsync(this Call This)
        {
            var cb = new OkTaskCallback();
            This.Enqueue(cb);

            return cb.Task;
        }

        class OkTaskCallback : Java.Lang.Object, ICallback
        {
            readonly TaskCompletionSource<Response> tcs = new TaskCompletionSource<Response>();
            public Task<Response> Task { get { return tcs.Task; } }

            public void OnFailure(Request p0, Java.IO.IOException p1)
            {
                // Kind of a hack, but the simplest way to find out that server cert. validation failed
                if (p1.Message == String.Format("Hostname '{0}' was not verified", p0.Url().Host)) {
                    tcs.TrySetException(new WebException(p1.LocalizedMessage, WebExceptionStatus.TrustFailure));
                } else {
                    tcs.TrySetException(p1);
                }
            }

            public void OnResponse(Response p0)
            {
                tcs.TrySetResult(p0);
            }
        }
    }

    class HostnameVerifier : Java.Lang.Object, IHostnameVerifier
    {
        static readonly Regex cnRegex = new Regex(@"CN\s*=\s*([^,]*)", RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.Singleline);

        public bool Verify(string hostname, ISSLSession session)
        {
            return verifyServerCertificate(hostname, session) & verifyClientCiphers(hostname, session);
        }

        /// <summary>
        /// Verifies the server certificate by calling into ServicePointManager.ServerCertificateValidationCallback or,
        /// if the is no delegate attached to it by using the default hostname verifier.
        /// </summary>
        /// <returns><c>true</c>, if server certificate was verifyed, <c>false</c> otherwise.</returns>
        /// <param name="hostname"></param>
        /// <param name="session"></param>
        bool verifyServerCertificate(string hostname, ISSLSession session)
        {
            var defaultVerifier = HttpsURLConnection.DefaultHostnameVerifier;

            if (ServicePointManager.ServerCertificateValidationCallback == null) return defaultVerifier.Verify(hostname, session);

            // Convert java certificates to .NET certificates and build cert chain from root certificate
            var certificates = session.GetPeerCertificateChain();
            var chain = new System.Security.Cryptography.X509Certificates.X509Chain();
            System.Security.Cryptography.X509Certificates.X509Certificate2 root = null;
            var errors = System.Net.Security.SslPolicyErrors.None;

            // Build certificate chain and check for errors
            if (certificates == null || certificates.Length == 0) {//no cert at all
                errors = System.Net.Security.SslPolicyErrors.RemoteCertificateNotAvailable;
                goto bail;
            } 

            if (certificates.Length == 1) {//no root?
                errors = System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;
                goto bail;
            } 

            var netCerts = certificates.Select(x => new System.Security.Cryptography.X509Certificates.X509Certificate2(x.GetEncoded())).ToArray();

            for (int i = 1; i < netCerts.Length; i++) {
                chain.ChainPolicy.ExtraStore.Add(netCerts[i]);
            }

            root = netCerts[0];

            chain.ChainPolicy.RevocationFlag = System.Security.Cryptography.X509Certificates.X509RevocationFlag.EntireChain;
            chain.ChainPolicy.RevocationMode = System.Security.Cryptography.X509Certificates.X509RevocationMode.NoCheck;
            chain.ChainPolicy.UrlRetrievalTimeout = new TimeSpan(0, 1, 0);
            chain.ChainPolicy.VerificationFlags = 
                    System.Security.Cryptography.X509Certificates.X509VerificationFlags.AllowUnknownCertificateAuthority;

            if (!chain.Build(root)) {
                errors = System.Net.Security.SslPolicyErrors.RemoteCertificateChainErrors;
                goto bail;
            }

            var subject = root.Subject;
            var subjectCn = cnRegex.Match(subject).Groups[1].Value;

            if (String.IsNullOrWhiteSpace(subjectCn) || !Utility.MatchHostnameToPattern(hostname, subjectCn)) {
                errors = System.Net.Security.SslPolicyErrors.RemoteCertificateNameMismatch;
                goto bail;
            }

        bail:
            // Call the delegate to validate
            return ServicePointManager.ServerCertificateValidationCallback(this, root, chain, errors);
        }

        /// <summary>
        /// Verifies client ciphers and is only available in Mono and Xamarin products.
        /// </summary>
        /// <returns><c>true</c>, if client ciphers was verifyed, <c>false</c> otherwise.</returns>
        /// <param name="hostname"></param>
        /// <param name="session"></param>
        bool verifyClientCiphers(string hostname, ISSLSession session)
        {
            var callback = ServicePointManager.ClientCipherSuitesCallback;
            if (callback == null) return true;

            var protocol = session.Protocol.StartsWith("SSL", StringComparison.InvariantCulture) ? SecurityProtocolType.Ssl3 : SecurityProtocolType.Tls;
            var acceptedCiphers = callback(protocol, new[] { session.CipherSuite });

            return acceptedCiphers.Contains(session.CipherSuite);
        }
    }
}
