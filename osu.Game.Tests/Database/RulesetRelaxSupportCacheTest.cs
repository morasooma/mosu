using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    [NonParallelizable]
    public class RulesetRelaxSupportCacheTest
    {
        [SetUp]
        public void SetUp()
        {
            CountingRelaxRuleset.Discoveries = 0;
            CountingNoRelaxRuleset.Discoveries = 0;
        }

        private static RulesetInfo info<T>(string shortName) where T : Ruleset => new RulesetInfo
        {
            ShortName = shortName,
            InstantiationInfo = typeof(T).AssemblyQualifiedName!,
            Available = true,
        };

        [Test]
        public void TestCachesBothSupportedAndUnsupportedRulesetsAcrossWrappers()
        {
            var cache = new RulesetRelaxSupportCache();
            for (int i = 0; i < 100; i++)
            {
                Assert.That(cache.SupportsRelax(info<CountingRelaxRuleset>("relax")), Is.True);
                Assert.That(cache.SupportsRelax(info<CountingNoRelaxRuleset>("no-relax")), Is.False);
            }

            Assert.That(CountingRelaxRuleset.Discoveries, Is.EqualTo(1));
            Assert.That(CountingNoRelaxRuleset.Discoveries, Is.EqualTo(1));
        }

        [Test]
        public void TestNewPassRediscoversCapabilities()
        {
            var ruleset = info<CountingRelaxRuleset>("relax");
            Assert.That(new RulesetRelaxSupportCache().SupportsRelax(ruleset), Is.True);
            Assert.That(new RulesetRelaxSupportCache().SupportsRelax(ruleset), Is.True);
            Assert.That(CountingRelaxRuleset.Discoveries, Is.EqualTo(2));
        }

        [Test]
        public void TestFailuresAreNotCached()
        {
            var cache = new RulesetRelaxSupportCache();
            var ruleset = info<CountingRelaxRuleset>("relax");
            ruleset.Available = false;
            Assert.Throws<RulesetLoadException>(() => cache.SupportsRelax(ruleset));
            ruleset.Available = true;
            Assert.That(cache.SupportsRelax(ruleset), Is.True);
            Assert.That(CountingRelaxRuleset.Discoveries, Is.EqualTo(1));
        }

        [Test]
        public void TestFailedRulesetDoesNotBlockOtherRulesets()
        {
            var cache = new RulesetRelaxSupportCache();
            var broken = info<CountingRelaxRuleset>("broken");
            broken.InstantiationInfo = "Missing.Ruleset, Missing.Assembly";
            Assert.Throws<RulesetLoadException>(() => cache.SupportsRelax(broken));
            Assert.That(cache.SupportsRelax(info<CountingNoRelaxRuleset>("no-relax")), Is.False);
            Assert.That(cache.SupportsRelax(info<CountingRelaxRuleset>("relax")), Is.True);
        }

        [Test]
        public void TestDiscoveryAllocationsScaleWithRulesetsNotBeatmaps()
        {
            const int maps = 10000;
            var ruleset = info<CountingRelaxRuleset>("relax");
            _ = ruleset.CreateInstance().GetModsFor(ModType.Automation).OfType<ModRelax>().Any();
            _ = new RulesetRelaxSupportCache().SupportsRelax(ruleset);

            var watch = Stopwatch.StartNew();
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < maps; i++)
                if (!ruleset.CreateInstance().GetModsFor(ModType.Automation).OfType<ModRelax>().Any())
                    throw new InvalidOperationException();
            long uncachedBytes = GC.GetAllocatedBytesForCurrentThread() - start;
            double uncachedMs = watch.Elapsed.TotalMilliseconds;

            CountingRelaxRuleset.Discoveries = 0;
            watch.Restart();
            start = GC.GetAllocatedBytesForCurrentThread();
            var cache = new RulesetRelaxSupportCache();
            for (int i = 0; i < maps; i++)
                if (!cache.SupportsRelax(ruleset))
                    throw new InvalidOperationException();
            long cachedBytes = GC.GetAllocatedBytesForCurrentThread() - start;
            double cachedMs = watch.Elapsed.TotalMilliseconds;

            TestContext.WriteLine($"PP capability discovery, {maps} maps: uncached={uncachedBytes} B / {uncachedMs:0.###} ms; cached={cachedBytes} B / {cachedMs:0.###} ms");
            Assert.That(CountingRelaxRuleset.Discoveries, Is.EqualTo(1));
            Assert.That(cachedBytes, Is.LessThan(uncachedBytes / 100));
        }

        public class CountingRelaxRuleset : OsuRuleset
        {
            public static int Discoveries;

            public override IEnumerable<Mod> GetModsFor(ModType type)
            {
                Discoveries++;
                return base.GetModsFor(type);
            }
        }

        public class CountingNoRelaxRuleset : OsuRuleset
        {
            public static int Discoveries;

            public override IEnumerable<Mod> GetModsFor(ModType type)
            {
                Discoveries++;
                return Array.Empty<Mod>();
            }
        }
    }
}
