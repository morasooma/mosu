// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class TrustedDomainOnlineStoreTest
    {
        [Test]
        public void TestCurrentServerUrlIsUnchanged()
        {
            const string url = "https://morasooma.net/file/news/image";

            Assert.That(TrustedDomainOnlineStore.ResolveLookupUrl(url), Is.EqualTo(url));
        }

        [Test]
        public void TestStoredServerFileUsesCurrentEndpoint()
        {
            const string storedUrl = "https://retired-server.invalid/file/news/image?size=2";
            string expected = $"{MosuServerEnvironment.ServerUrl}/file/news/image?size=2";

            Assert.That(TrustedDomainOnlineStore.ResolveLookupUrl(storedUrl), Is.EqualTo(expected));
        }

        [Test]
        public void TestUntrustedExternalUrlIsBlocked()
        {
            Assert.That(TrustedDomainOnlineStore.ResolveLookupUrl("https://example.invalid/image.png"), Is.Empty);
        }
    }
}
