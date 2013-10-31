using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;
using ModernHttpClient;
using System.Text;
using System.Security.Cryptography;

namespace Playground.iOS
{
    public partial class Playground_iOSViewController : UIViewController
    {
        public Playground_iOSViewController () : base ("Playground_iOSViewController", null)
        {
        }

        CancellationTokenSource currentToken;
        HttpResponseMessage resp;

        partial void cancelIt (MonoTouch.Foundation.NSObject sender)
        {
            this.currentToken.Cancel();
            if (resp != null) resp.Content.Dispose();
        }

        async partial void doIt (MonoTouch.Foundation.NSObject sender)
        {
            var client = new HttpClient(new AFNetworkHandler());
            currentToken = new CancellationTokenSource();
            var st = new Stopwatch();

            st.Start();
            try {
                resp = await client.GetAsync("https://github.com/paulcbetts/ModernHttpClient/releases/download/0.9.0/ModernHttpClient-0.9.zip", HttpCompletionOption.ResponseContentRead, currentToken.Token);
                result.Text = "Got the headers!";

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

