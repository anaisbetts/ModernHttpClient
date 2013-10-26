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

namespace Playground.Android
{
    [Activity (Label = "Playground.Android", MainLauncher = true)]
    public class MainActivity : Activity
    {
        int count = 1;

        CancellationTokenSource currentToken;
        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            // Set our view from the "main" layout resource
            SetContentView (Resource.Layout.Main);

            // Get our button from the layout resource,
            // and attach an event to it
            var button = FindViewById<Button>(Resource.Id.doIt);
            var cancel = FindViewById<Button>(Resource.Id.cancelButton);
            var result = FindViewById<TextView>(Resource.Id.result);

            cancel.Click += (o, e) => {
                Console.WriteLine("Canceled token {0:x8}", this.currentToken.Token.GetHashCode());
                this.currentToken.Cancel();
            };

            button.Click += async (o, e) => {
                var client = new HttpClient(new OkHttpNetworkHandler());
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
            };
        }
    }
}


