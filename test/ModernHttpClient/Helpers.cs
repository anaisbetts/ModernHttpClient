using System;
using System.Threading.Tasks;
using System.Threading;

namespace ModernHttpClient.Tests
{
    public static class Helpers
    {
        public static T WaitInUnitTestRunner<T>(this Task<T> This)
        {
            // NB: We have to fool async/await into not deadlocking the
            // runner. Let Dreams Soar
            var exception = default(Exception);
            var ret = default(T);

            var t = new Thread(() => {
                try {
                    ret = This.Result;
                } catch (Exception ex) {
                    exception = ex;
                }
            });

            t.Start();
            t.Join();

            if (exception != null) {
                throw exception;
            }

            return ret;
        }
    }
}

