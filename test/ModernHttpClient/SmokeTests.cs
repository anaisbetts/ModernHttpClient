using System;
using NUnit.Framework;
using System.Net.Http;
using System.Net;

namespace ModernHttpClient.Tests
{
    [TestFixture]
    public class SmokeTests
    {
        [Test]
        public async void ExecuteSimpleGet()
        {
            var fixture = new HttpClient(new AFNetworkHandler());
            var result = await fixture.GetAsync("https://api.github.com/octocat");

            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
        }
    }
}

