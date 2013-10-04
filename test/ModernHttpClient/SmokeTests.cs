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
        public void ExecuteSimpleGet()
        {
            var fixture = new HttpClient(new AFNetworkHandler());
            var result = fixture.GetAsync("https://api.github.com/octocat").WaitInUnitTestRunner();

            Assert.AreEqual(HttpStatusCode.OK, result.StatusCode);
        }
    }
}

