using System;
using System.Globalization;

namespace ModernHttpClient
{
    internal static class Utility
    {
        public static bool MatchHostnameToPattern(string hostname, string pattern)
        {
            // check if this is a pattern
            int index = pattern.IndexOf('*');
            if (index == -1) {
                // not a pattern, do a direct case-insensitive comparison
                return (String.Compare(hostname, pattern, true, CultureInfo.InvariantCulture) == 0);
            }

            // check pattern validity
            // A "*" wildcard character MAY be used as the left-most name component in the certificate.

            // unless this is the last char (valid)
            if (index != pattern.Length - 1) {
                // then the next char must be a dot .'.
                if (pattern[index + 1] != '.') {
                    return false;
                }
            }

            // only one (A) wildcard is supported
            int i2 = pattern.IndexOf('*', index + 1);
            if (i2 != -1) return false;

            // match the end of the pattern
            string end = pattern.Substring(index + 1);
            int length = hostname.Length - end.Length;
            // no point to check a pattern that is longer than the hostname
            if (length <= 0) return false;

            if (String.Compare(hostname, length, end, 0, end.Length, true, CultureInfo.InvariantCulture) != 0) {
                return false;
            }

            // special case, we start with the wildcard
            if (index == 0) {
                // ensure we hostname non-matched part (start) doesn't contain a dot
                int i3 = hostname.IndexOf('.');
                return ((i3 == -1) || (i3 >= (hostname.Length - end.Length)));
            }

            // match the start of the pattern
            string start = pattern.Substring(0, index);
            return (String.Compare(hostname, 0, start, 0, start.Length, true, CultureInfo.InvariantCulture) == 0);
        }
    }
}
