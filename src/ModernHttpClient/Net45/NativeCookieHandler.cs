using System;
using System.Net.Http;
using System.Net;
using System.Collections.Generic;
using System.Linq;

namespace ModernHttpClient
{
    public class NativeCookieHandler
    {
        internal CookieContainer CookieContainer { get; private set; }
        CookieCollection _currentCollection;

        public NativeCookieHandler()
        {
            CookieContainer = new CookieContainer();
            _currentCollection = new CookieCollection();
        }

        public void SetCookies(IEnumerable<Cookie> cookies)
        {
            _currentCollection = new CookieCollection();
            foreach (var cookie in cookies) {
                _currentCollection.Add(cookie);
            }

            CookieContainer.Add(_currentCollection);
        }

        public List<Cookie> Cookies {
            get {
                return CookieCollection.ToList();
            }
        }

        IEnumerable<Cookie> CookieCollection {
            get {
                foreach (var cookie in _currentCollection) {
                    if (cookie is Cookie) {
                        yield return (Cookie)cookie;
                    }
                }
            }
        }
    }
}

