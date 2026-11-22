using NUnit.Framework;
using osu.Game.Online;
using osu.Game.Online.Metadata;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ClientBeatmapDownloadPolicyTest
    {
        [TestCase("https://morasooma.net", "https://morasooma.net/metadata", true)]
        [TestCase("https://proxy.morasooma.net", "https://proxy.morasooma.net/metadata", true)]
        [TestCase("https://proxy2.morasooma.net", "https://proxy2.morasooma.net/metadata", true)]
        [TestCase("https://MORASOOMA.NET:443/", "https://morasooma.net/metadata", true)]
        [TestCase("https://osu.ppy.sh", "https://osu.ppy.sh/metadata", false)]
        [TestCase("https://morasooma.net.attacker.example", "https://morasooma.net.attacker.example/metadata", false)]
        [TestCase("https://attacker.example", "https://morasooma.net/metadata", false)]
        [TestCase("https://morasooma.net", "https://attacker.example/metadata", false)]
        [TestCase("https://morasooma.net", "https://proxy2.morasooma.net/metadata", false)]
        [TestCase("http://morasooma.net", "http://morasooma.net/metadata", false)]
        [TestCase("https://morasooma.net:8443", "https://morasooma.net:8443/metadata", false)]
        [TestCase("https://evil@morasooma.net", "https://morasooma.net/metadata", false)]
        [TestCase("https://morasooma.net", "https://evil@morasooma.net/metadata", false)]
        [TestCase("https://morasooma.net?redirect=evil", "https://morasooma.net/metadata", false)]
        [TestCase("https://morasooma.net", "https://morasooma.net/metadata?redirect=evil", false)]
        [TestCase("https://morasooma.net/#evil", "https://morasooma.net/metadata", false)]
        [TestCase("https://morasooma.net", "https://morasooma.net/metadata#evil", false)]
        [TestCase("https://morasooma.net/other", "https://morasooma.net/metadata", false)]
        [TestCase("https://morasooma.net", "https://morasooma.net/signalr/metadata", false)]
        [TestCase("https://morasooma.net", "https://morasooma.net/metadata/other", false)]
        [TestCase("https://morasooma.net.", "https://morasooma.net./metadata", false)]
        [TestCase("file:///morasooma.net", "file:///metadata", false)]
        [TestCase("", "", false)]
        [TestCase("not a url", "not a url", false)]
        [TestCase("http://127.0.0.1:18083", "http://127.0.0.1:18083/metadata", false)]
        [TestCase("http://localhost:18082", "http://localhost:18082/metadata", false)]
        public void StrictOriginsAndEndpoints(string api, string metadata, bool expected)
        {
            var endpoints = new EndpointConfiguration { APIUrl = api, MetadataUrl = metadata };
            Assert.That(ClientBeatmapDownloadPolicy.IsTrusted(endpoints, false, false), Is.EqualTo(expected));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void CustomAndStableProfilesAreRejectedEvenWithTrustedUrls(bool custom, bool stable)
        {
            var endpoints = new EndpointConfiguration { APIUrl = "https://morasooma.net", MetadataUrl = "https://morasooma.net/metadata" };
            Assert.That(ClientBeatmapDownloadPolicy.IsTrusted(endpoints, custom, stable), Is.False);
        }

        [Test]
        public void LocalGatewayIsDebugOnly()
        {
            var endpoints = new EndpointConfiguration { APIUrl = "http://127.0.0.1:18082", MetadataUrl = "http://127.0.0.1:18082/metadata" };
#if DEBUG
            Assert.That(ClientBeatmapDownloadPolicy.IsTrusted(endpoints, false, false), Is.True);
#else
            Assert.That(ClientBeatmapDownloadPolicy.IsTrusted(endpoints, false, false), Is.False);
#endif
        }
    }
}
