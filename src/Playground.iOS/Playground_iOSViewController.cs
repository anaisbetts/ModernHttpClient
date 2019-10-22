using System;
using CoreGraphics;
using Foundation;
using UIKit;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;
using ModernHttpClient;
using System.Text;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net;
using System.Linq;

namespace Playground.iOS
{
    public partial class Playground_iOSViewController : UIViewController
    {
        public Playground_iOSViewController () : base ("Playground_iOSViewController", null)
        {
            /*
            Task.Run (async () => {
                var client = new HttpClient(new NSUrlSessionHandler());

                var item = new { MyProperty = "Property Value" };
                var content = new StringContent(JsonConvert.SerializeObject(item), Encoding.UTF8, "application/json");
                var result = await client.PostAsync("http://requestb.in/1aj9b9c1", content);

                result.EnsureSuccessStatusCode();
            });
            */

            //Here we accept any certificate and just print the cert's data.
            ServicePointManager.ServerCertificateValidationCallback += (sender, certificate, chain, sslPolicyErrors) => {
                System.Diagnostics.Debug.WriteLine("Callback Server Certificate: " + sslPolicyErrors);

                foreach(var el in chain.ChainElements) {
                    System.Diagnostics.Debug.WriteLine(el.Certificate.GetCertHashString());
                    System.Diagnostics.Debug.WriteLine(el.Information);
                }

                return true;
            };

        }

        CancellationTokenSource currentToken;
        HttpResponseMessage resp;

        partial void cancelIt (Foundation.NSObject sender)
        {
            this.currentToken.Cancel();
            if (resp != null) resp.Content.Dispose();
        }

        void HandleDownloadProgress(long bytes, long totalBytes, long totalBytesExpected)
        {
            Console.WriteLine("Downloading {0}/{1}", totalBytes, totalBytesExpected);

            BeginInvokeOnMainThread(() => {
                var progressPercent = (float)totalBytes / (float)totalBytesExpected;
                progress.SetProgress(progressPercent, animated: true);
            });
        }

        async partial void doIt (Foundation.NSObject sender)
        {
            var handler = new NativeMessageHandler();
            var client = new HttpClient(handler);

            currentToken = new CancellationTokenSource();
            var st = new Stopwatch();

            st.Start();
            try {
                handler.DisableCaching = true;
                var url = "https://github.com/paulcbetts/ModernHttpClient/releases/download/0.9.0/ModernHttpClient-0.9.zip";
             

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                handler.RegisterForProgress(request, HandleDownloadProgress);

                //if using NTLM authentication pass the credentials below
                //handler.Credentials = new NetworkCredential("user","pass");

                resp = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, currentToken.Token);
                result.Text = "Got the headers!";

                Console.WriteLine("Status code: {0}", resp.StatusCode);

                Console.WriteLine("Reason: {0}", resp.ReasonPhrase);

                Console.WriteLine("Headers");
                foreach (var v in resp.Headers) {
                    Console.WriteLine("{0}: {1}", v.Key, String.Join(",", v.Value));
                }

                Console.WriteLine("Content Headers");
                foreach (var v in resp.Content.Headers) {
                    Console.WriteLine("{0}: {1}", v.Key, String.Join(",", v.Value));
                }

                var bytes = await resp.Content.ReadAsByteArrayAsync();
                result.Text = String.Format("Read {0} bytes", bytes.Length);

                var md5 = MD5.Create();
                var md5Result = md5.ComputeHash(bytes);
                md5sum.Text = ToHex(md5Result, false);
            } catch (Exception ex) {
                result.Text = ex.ToString();
            } finally {
                st.Stop();
                result.Text = (result.Text ?? "") + String.Format("\n\nTook {0} milliseconds", st.ElapsedMilliseconds);
            }
        }

        public override bool ShouldAutorotateToInterfaceOrientation (UIInterfaceOrientation toInterfaceOrientation)
        {
            // Return true for supported orientations
            return (toInterfaceOrientation != UIInterfaceOrientation.PortraitUpsideDown);
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
