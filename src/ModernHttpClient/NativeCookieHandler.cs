using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;

namespace ModernHttpClient
{
    public class NativeCookieHandler
    {
        static readonly char[] PortDelimiters = new[] { ',', '"' };

        readonly HashSet<string> authorities = new HashSet<string>(StringComparer.Ordinal);
        internal readonly CookieContainer CookieContainer = new CookieContainer();

        public void SetCookies(IEnumerable<Cookie> cookies)
        {
            foreach (var cookie in cookies) {
                var uriSb = new StringBuilder();
                uriSb.Append(cookie.Secure ? "https://" : "http://");
                uriSb.Append(cookie.Domain);
                if (cookie.Port.Length > 0) {
                    var ports = cookie.Port.Split(PortDelimiters, StringSplitOptions.RemoveEmptyEntries);
                    if (ports.Length > 0) uriSb.Append(':').Append(ports[0]);
                }
                uriSb.Append(cookie.Path);

                Uri uri;
                if (!Uri.TryCreate(uriSb.ToString(), UriKind.Absolute, out uri)) {
                    throw new CookieException();
                }

                CookieContainer.Add(uri, cookie);
                Add(uri);
            }
        }

        public IReadOnlyList<Cookie> Cookies {
            get {
                var cookies = new CookieCollection();
                foreach (var uri in authorities.Select(a => new Uri("https://" + a))) {
                    cookies.Add(CookieContainer.GetCookies(uri));
                }
                return cookies.Cast<Cookie>().ToList();
            }
        }

        internal void Add(Uri uri)
        {
            authorities.Add(uri.Authority);
        }
    }
}