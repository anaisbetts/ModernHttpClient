using System;
using System.Threading.Tasks;
using System.Threading;

namespace ModernHttpClient.Tests
{
    public static class Helpers
    {
        public static T WaitInUnitTestRunner<T>(this Task<T> This, TimeSpan? timeout = null)
        {
            // NB: We have to fool async/await into not deadlocking the
            // runner. Let Dreams Soar
            var exception = default(Exception);
            var ret = default(T);

            timeout = timeout ?? TimeSpan.FromSeconds(30);
            var timeoutTask = Task.Delay(timeout.Value).ContinueWith(_ => {
                if (This.IsFaulted || This.IsCompleted || This.IsFaulted) return;
                throw new TimeoutException();
            });

            var t = new Thread(() => {
                try {
                    Task.WaitAny(timeoutTask, This);
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

