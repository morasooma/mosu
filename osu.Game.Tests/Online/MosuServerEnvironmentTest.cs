// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class MosuServerEnvironmentTest
    {
        [Test]
        public void TestPublicServerUrl() =>
            Assert.That(MosuServerEnvironment.PublicServerUrl, Is.EqualTo("https://morasooma.net"));

        [Test]
        public void TestPrimaryServerUrl() =>
            Assert.That(MosuServerEnvironment.GetServerUrl(false), Is.EqualTo(MosuServerEnvironment.ServerUrl));

        [Test]
        public void TestProxyServerUrl() =>
            Assert.That(MosuServerEnvironment.GetServerUrl(true), Is.EqualTo(MosuServerEnvironment.ProxyServerUrl));

        [Test]
        public void TestUpdateFeedUsesConfiguredServer() =>
            Assert.That(MosuServerEnvironment.UpdateUrl, Is.EqualTo(MosuServerEnvironment.ServerUrl + MosuServerEnvironment.UpdateFeedPath));
    }
}
