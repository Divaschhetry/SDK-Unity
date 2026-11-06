using NUnit.Framework;
using Sentience.Config;

namespace Sentience.WaaS.Tests
{
    public class HttpClientTests
    {
        [Test]
        public void TestHttpClientIncludesAPIKey()
        {
            HttpClient client = new HttpClient("https://randomurl.com");
            var request = client.BuildRequest<object>("", null);
            string header = request.Item1.GetRequestHeader("X-Access-Key");
            Assert.IsTrue(header.Length > 0);
            Assert.AreEqual(SentienceConfig.GetConfig().BuilderAPIKey, header);
            Assert.IsFalse(request.Item2.Contains(header));
            Assert.IsTrue(request.Item2.Contains("X-Access-Key: [REDACTED]"));
            Assert.IsTrue(request.Item2.Contains("[REQUEST BODY REDACTED]"));
        }
    }
}