using System.Collections.Generic;
using System.Linq;
using System.Net;

#if UNIFIED
using Foundation;
#else
using MonoTouch.Foundation;
#endif

namespace ModernHttpClient
{
    public class NativeCookieHandler
    {
        public void SetCookies(IEnumerable<Cookie> cookies)
        {
            foreach (var v in cookies.Select(ToNativeCookie)) {
                NSHttpCookieStorage.SharedStorage.SetCookie(v);
            }
        }

        public List<Cookie> Cookies {
            get {
                return NSHttpCookieStorage.SharedStorage.Cookies
                    .Select(ToNetCookie)
                    .ToList();
            }
        }

        static NSHttpCookie ToNativeCookie(Cookie cookie)
        {
            return new NSHttpCookie(cookie);
        }

        static Cookie ToNetCookie(NSHttpCookie cookie)
        {
            var nc = new Cookie(cookie.Name, cookie.Value, cookie.Path, cookie.Domain);
            nc.Secure = cookie.IsSecure;

            return nc;
        }
    }
}
