//
// This file contains the sample code to use System.Net.WebRequest
// on Android to communicate with HTTP servers
//
// Author:
//   Miguel de Icaza
//

using System;
using System.Net;
using System.Diagnostics;

namespace HttpClient.Android
{
	public class DotNet {
		MainActivity ad;
		
		public DotNet (MainActivity ad)
		{
			this.ad = ad;
		}
		
		//
		// Asynchronous HTTP request
		//
		public void HttpSample ()
		{
			this.ad.Busy ();
			var request = WebRequest.Create (MainActivity.WisdomUrl);
			request.BeginGetResponse (FeedDownloaded, request);
		}
		
		//
		// Invoked when we get the stream back from the twitter feed
		// We parse the RSS feed and push the data into a 
		// table.
		//
		void FeedDownloaded (IAsyncResult result)
		{
			var request = result.AsyncState as HttpWebRequest;
			
			try {
				var response = request.EndGetResponse (result);
				this.ad.RenderStream (response.GetResponseStream ());
			} catch (Exception e) {
				Debug.WriteLine (e);
			} finally {
				this.ad.Done ();
			}
		}
	}
}
