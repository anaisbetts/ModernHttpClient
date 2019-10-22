using System.Collections.Generic;
using System.Linq;
using System.Net;
using Java.Net;

namespace ModernHttpClient
{
    public class NativeCookieHandler
    {
        readonly CookieManager cookieManager = new CookieManager();

        public NativeCookieHandler()
        {
            CookieHandler.Default = cookieManager; //set cookie manager if using NativeCookieHandler
        }

        public void SetCookies(IEnumerable<Cookie> cookies)
        {
            foreach (var nc in cookies.Select(ToNativeCookie)) {
                cookieManager.CookieStore.Add(new URI(nc.Domain), nc);
            }
        }
            
        public List<Cookie> Cookies {
            get {
                return cookieManager.CookieStore.Cookies
                    .Select(ToNetCookie)
                    .ToList();
            }
        }

        static HttpCookie ToNativeCookie(Cookie cookie)
        {
            var nc = new HttpCookie(cookie.Name, cookie.Value);
            nc.Domain = cookie.Domain;
            nc.Path = cookie.Path;
            nc.Secure = cookie.Secure;

            return nc;
        }

        static Cookie ToNetCookie(HttpCookie cookie)
        {
            var nc = new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain);
            nc.Secure = cookie.Secure;

            return nc;
        }
    }
}
