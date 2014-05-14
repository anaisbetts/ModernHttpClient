using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Threading;

namespace ModernHttpClient
{
    public delegate void ProgressDelegate (long bytes, long totalBytes, long totalBytesExpected);
}

