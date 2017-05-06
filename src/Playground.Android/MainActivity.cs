using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using System.Net.Http;
using ModernHttpClient;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Net;
using System.Linq;
using System.IO;

namespace Playground.Android
{
    [Activity (Label = "Playground.Android", MainLauncher = true)]
    public class MainActivity : Activity
    {
        int count = 1;

        CancellationTokenSource currentToken;

        ProgressBar progress;

        void HandleDownloadProgress(long bytes, long totalBytes, long totalBytesExpected)
        {
            Console.WriteLine("Downloading {0}/{1}", totalBytes, totalBytesExpected);

            RunOnUiThread(() => {
                progress.Max = 10000;

                var progressPercent = (float)totalBytes / (float)totalBytesExpected;
                var progressOffset = Convert.ToInt32(progressPercent * 10000);

                Console.WriteLine(progressOffset);
                progress.Progress = progressOffset;
            });
        }

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            // Set our view from the "main" layout resource
            SetContentView (Resource.Layout.Main);

            //Here we accept any certificate and just print the cert's data.
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => {
                System.Diagnostics.Debug.WriteLine("Callback Server Certificate: " + sslPolicyErrors);

                foreach(var el in chain.ChainElements) {
                    System.Diagnostics.Debug.WriteLine(el.Certificate.GetCertHashString());
                    System.Diagnostics.Debug.WriteLine(el.Information);
                }

                return true;
            };

            // Get our button from the layout resource,
            // and attach an event to it
            var button = FindViewById<Button>(Resource.Id.doIt);
            var cancel = FindViewById<Button>(Resource.Id.cancelButton);
            var result = FindViewById<TextView>(Resource.Id.result);
            var hashView = FindViewById<TextView>(Resource.Id.md5sum);
            var status = FindViewById<TextView>(Resource.Id.status);
            progress = FindViewById<ProgressBar>(Resource.Id.progress);

            var resp = default(HttpResponseMessage);

            cancel.Click += (o, e) => {
                Console.WriteLine("Canceled token {0:x8}", this.currentToken.Token.GetHashCode());
                this.currentToken.Cancel();
                if (resp != null) resp.Content.Dispose();
            };

            button.Click += async (o, e) => {
                var handler = new NativeMessageHandler();
                var client = new HttpClient(handler);

                currentToken = new CancellationTokenSource();
                var st = new Stopwatch();

                client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("ModernHttpClient", "1.0"));
                client.DefaultRequestHeaders.UserAgent.Add(new System.Net.Http.Headers.ProductInfoHeaderValue("MyTest", "2.0"));
                handler.DisableCaching = true;

                st.Start();
                try {
                    //var url = "https://tv.eurosport.com";
                    //var url = "https://github.com/downloads/nadlabak/android/cm-9.1.0a-umts_sholes.zip";
                    var url = "https://github.com/paulcbetts/ModernHttpClient/releases/download/0.9.0/ModernHttpClient-0.9.zip";

                    var request = new HttpRequestMessage(HttpMethod.Get, url);
                    handler.RegisterForProgress(request, HandleDownloadProgress);

                    resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, currentToken.Token);
                    result.Text = "Got the headers!";

                    status.Text = string.Format("HTTP {0}: {1}", (int)resp.StatusCode, resp.ReasonPhrase);

                    foreach (var v in resp.Headers) {
                        Console.WriteLine("{0}: {1}", v.Key, String.Join(",", v.Value));
                    }

                    var stream = await resp.Content.ReadAsStreamAsync();

                    var ms = new MemoryStream();
                    await stream.CopyToAsync(ms, 4096, currentToken.Token);
                    var bytes = ms.ToArray();

                    result.Text = String.Format("Read {0} bytes", bytes.Length);

                    var md5 = MD5.Create();
                    var hash = md5.ComputeHash(bytes);
                    hashView.Text = ToHex(hash, false);
                } catch (Exception ex) {
                    result.Text = ex.ToString();
                } finally {
                    st.Stop();
                    result.Text = (result.Text ?? "") + String.Format("\n\nTook {0} milliseconds", st.ElapsedMilliseconds);
                }
            };
        }

        public static string ToHex(byte[] bytes, bool upperCase)
        {
            var result = new StringBuilder(bytes.Length*2);

            for (int i = 0; i < bytes.Length; i++)
                result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));

            return result.ToString();
        }
    }
}


