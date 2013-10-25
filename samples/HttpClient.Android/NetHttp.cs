//
// This file contains the sample code to use System.Net.HttpClient
// on the iPhone to communicate using Apple's CFNetwork API
//

using System;
using System.Threading.Tasks;
using System.Net.Http;

namespace HttpClient.Android
{
	public class NetHttp
	{
		MainActivity ad;

		public NetHttp (MainActivity ad)
		{
			this.ad = ad;
		}

		public async Task HttpSample (HttpMessageHandler handler = null)
		{
			this.ad.Busy ();
			System.Net.Http.HttpClient client = (handler == null) ?
				new System.Net.Http.HttpClient () :
					new System.Net.Http.HttpClient (handler);
			var stream = await client.GetStreamAsync (MainActivity.WisdomUrl);
			this.ad.Done ();
			ad.RenderStream (stream);
		}
	}
}

