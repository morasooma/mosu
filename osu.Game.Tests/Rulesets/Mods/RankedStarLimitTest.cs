using System;
using NUnit.Framework;
using osu.Game.Online;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.Rulesets.Mods
{
    [TestFixture]
    [NonParallelizable]
    public class RankedStarLimitTest
    {
        private bool previousThirdParty;
        private bool previousStable;
        private string previousApiUrl = null!;

        [SetUp]
        public void SetUp()
        {
            previousThirdParty = MosuServerEnvironment.IsThirdPartyServer;
            previousStable = MosuServerEnvironment.UsesStableProtocol;
            previousApiUrl = MosuServerEnvironment.ActiveApiUrl;
            MosuServerEnvironment.ActiveApiUrl = MosuServerEnvironment.PublicServerUrl;
            MosuServerEnvironment.IsThirdPartyServer = false;
            MosuServerEnvironment.UsesStableProtocol = false;
        }

        [TearDown]
        public void TearDown()
        {
            MosuServerEnvironment.IsThirdPartyServer = previousThirdParty;
            MosuServerEnvironment.UsesStableProtocol = previousStable;
            MosuServerEnvironment.ActiveApiUrl = previousApiUrl;
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        public void TestNormalBoundary(int rulesetId)
        {
            var ruleset = new RulesetInfo { OnlineID = rulesetId };
            Assert.That(ModPerformancePointHelper.IsWithinRankedStarLimit(ruleset, Array.Empty<Mod>(), 12), Is.True);
            Assert.That(ModPerformancePointHelper.IsWithinRankedStarLimit(ruleset, Array.Empty<Mod>(), 12.001), Is.False);
        }

        [TestCase(0, true)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        public void TestRelaxBoundary(int rulesetId, bool hasRelaxMode)
        {
            var ruleset = new RulesetInfo { OnlineID = rulesetId };
            Mod[] mods = { new OsuModRelax() };
            Assert.That(ModPerformancePointHelper.IsWithinRankedStarLimit(ruleset, mods, 16), Is.EqualTo(hasRelaxMode));
            Assert.That(ModPerformancePointHelper.IsWithinRankedStarLimit(ruleset, mods, 16.001), Is.False);
        }

        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        [TestCase(-1)]
        public void TestInvalidStars(double stars) =>
            Assert.That(ModPerformancePointHelper.IsWithinRankedStarLimit(new RulesetInfo { OnlineID = 0 }, Array.Empty<Mod>(), stars), Is.False);

        [TestCase("https://morasooma.net", false)]
        [TestCase("https://proxy.morasooma.net", false)]
        [TestCase("https://proxy2.morasooma.net", false)]
        [TestCase("http://127.0.0.1:18082", false)]
        [TestCase("https://osu.ppy.sh", true)]
        [TestCase("https://lazer-api.shikkesora.com", true)]
        [TestCase("https://another-server.example", true)]
        [TestCase("https://morasooma.net.example", true)]
        [TestCase("https://morasooma.net@another-server.example", true)]
        [TestCase("http://127.0.0.1:9999", true)]
        [TestCase("", true)]
        public void TestServerIdentityNotProfileFlag(string apiUrl, bool expected)
        {
            MosuServerEnvironment.ActiveApiUrl = apiUrl;
            foreach (bool thirdPartyProfile in new[] { false, true })
            {
                MosuServerEnvironment.IsThirdPartyServer = thirdPartyProfile;
                Assert.That(ModPerformancePointHelper.IsWithinRankedStarLimit(null, Array.Empty<Mod>(), 20), Is.EqualTo(expected));
            }
        }

        [Test]
        public void TestStableIsUnaffected()
        {
            MosuServerEnvironment.UsesStableProtocol = true;
            Assert.That(ModPerformancePointHelper.IsWithinRankedStarLimit(null, Array.Empty<Mod>(), 20), Is.True);
        }
    }
}
