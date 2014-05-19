using System;
using System.IO;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using ModernHttpClient;

namespace HttpClient.Android
{
	[Activity (Label = "Http Client Sample", MainLauncher = true)]
	public class MainActivity : Activity
	{
		public static readonly string WisdomUrl = "http://httpbin.org/ip";
		private ProgressDialog busyDlg = null;

		protected override void OnCreate (Bundle bundle)
		{
			base.OnCreate (bundle);

			// Set our view from the "main" layout resource
			SetContentView (Resource.Layout.Main);

			// Get our button from the layout resource,
			// and attach an event to it
			Button button = FindViewById<Button> (Resource.Id.button1);

			button.Click += async delegate {
				if (FindViewById<RadioButton> (Resource.Id.radioButton1).Checked) {
					new DotNet (this).HttpSample ();
				} else if (FindViewById<RadioButton> (Resource.Id.radioButton2).Checked) {
					await new NetHttp (this).HttpSample ();
				} else if (FindViewById<RadioButton> (Resource.Id.radioButton3).Checked) {
					await new NetHttp (this).HttpSample (new NativeMessageHandler());
				}
			};
		}

		public void RenderStream (Stream stream) {
			var reader = new System.IO.StreamReader (stream);

			var intent = new Intent (this, typeof(ShowStream));
			intent.PutExtra ("string", reader.ReadToEnd ());
			StartActivity (intent);
		}

		public void Busy () {
			this.busyDlg = new ProgressDialog (this);
			this.busyDlg.SetMessage (Resources.GetString (Resource.String.busy_msg));
			this.busyDlg.SetCancelable (false);
			this.busyDlg.Show ();
		}

		public void Done () {
			if (this.busyDlg != null) {
				this.busyDlg.Dismiss ();
				this.busyDlg.Dispose ();
				this.busyDlg = null;
			}
		}
	}
}


