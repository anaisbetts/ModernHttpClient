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

        protected override void OnCreate (Bundle bundle)
        {
            base.OnCreate (bundle);

            // Set our view from the "main" layout resource
            SetContentView (Resource.Layout.Main);

            // Get our button from the layout resource,
            // and attach an event to it
            var button = FindViewById<Button>(Resource.Id.doIt);
            var result = FindViewById<TextView>(Resource.Id.result);
            
            button.Click += async (o, e) => {
                var client = new HttpClient(new OkHttpNetworkHandler());
                var cts = new CancellationTokenSource();
                var st = new Stopwatch();

                Task.Delay(1000).ContinueWith(_ => cts.Cancel());

                st.Start();
                try {
                    var resp = await client.GetAsync("https://github.com/paulcbetts/ModernHttpClient/releases/download/0.9.0/ModernHttpClient-0.9.zip", HttpCompletionOption.ResponseContentRead, cts.Token);
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


