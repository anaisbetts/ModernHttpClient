//
// This file contains the sample code to use System.Net.HttpClient
// on the iPhone to communicate using Apple's CFNetwork API
//

using System;
using System.Threading.Tasks;
using System.Net.Http;

namespace HttpClient
{
	public class NetHttp
	{
		AppDelegate ad;

		public NetHttp (AppDelegate ad)
		{
			this.ad = ad;
		}

		public async Task HttpSample (HttpMessageHandler handler)
		{
			var client = new System.Net.Http.HttpClient (handler);
			ad.RenderStream (await client.GetStreamAsync (Application.WisdomUrl));
		}
	}
}

