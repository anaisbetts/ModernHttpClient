using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.UIKit;
using System.Net.Http;
using System.Threading;
using System.Diagnostics;
using ModernHttpClient;

namespace Playground.iOS
{
    public partial class Playground_iOSViewController : UIViewController
    {
        public Playground_iOSViewController () : base ("Playground_iOSViewController", null)
        {
        }

        CancellationTokenSource currentToken;

        partial void cancelIt (MonoTouch.Foundation.NSObject sender)
        {
            this.currentToken.Cancel();
        }

        async partial void doIt (MonoTouch.Foundation.NSObject sender)
        {
            var client = new HttpClient(new AFNetworkHandler());
            currentToken = new CancellationTokenSource();
            var st = new Stopwatch();

            st.Start();
            try {
                var resp = await client.GetAsync("https://github.com/paulcbetts/ModernHttpClient/releases/download/0.9.0/ModernHttpClient-0.9.zip", HttpCompletionOption.ResponseHeadersRead, currentToken.Token);
                result.Text = "Got the headers!";

                var bytes = await resp.Content.ReadAsByteArrayAsync();
                result.Text = String.Format("Read {0} bytes", bytes.Length);
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
    }
}

