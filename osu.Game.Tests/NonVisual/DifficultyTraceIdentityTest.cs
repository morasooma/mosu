using System;
using System.Diagnostics.Tracing;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Statistics;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class DifficultyTraceIdentityTest
    {
        [Test]
        public void TestIdentityIncludesChecksumRulesetAndModsButNotFullPreviewKind()
        {
            using var listener = new Listener();
            using var cache = new BeatmapDifficultyCache();
            var ruleset = new OsuRuleset().RulesetInfo;
            var map = new BeatmapInfo { ID = Guid.NewGuid(), MD5Hash = "original", Ruleset = ruleset };
            long full = id(cache, new BeatmapDifficultyCache.DifficultyCacheLookup(map, ruleset, null, true));
            long preview = id(cache, new BeatmapDifficultyCache.DifficultyCacheLookup(map, ruleset, null, false));
            Assert.That(preview, Is.EqualTo(full));
            Assert.That(id(cache, new BeatmapDifficultyCache.DifficultyCacheLookup(map, ruleset, new Mod[] { new OsuModHardRock() })), Is.Not.EqualTo(full));
            var revised = new BeatmapInfo { ID = map.ID, MD5Hash = "revised", Ruleset = ruleset };
            Assert.That(id(cache, new BeatmapDifficultyCache.DifficultyCacheLookup(revised, ruleset, null)), Is.Not.EqualTo(full));
            Assert.That(id(cache, new BeatmapDifficultyCache.DifficultyCacheLookup(map, new osu.Game.Rulesets.RulesetInfo { ShortName = "different" }, null)), Is.Not.EqualTo(full));
            for (int i = 0; i < 257; i++)
                _ = id(cache, new BeatmapDifficultyCache.DifficultyCacheLookup(new BeatmapInfo { ID = Guid.NewGuid(), Ruleset = ruleset }, ruleset, null));
            // Eviction may give the same key a fresh ID, but never reuse an old ID for a
            // different key. Equal ID is proof of equality, unlike an integer hash.
            Assert.That(id(cache, new BeatmapDifficultyCache.DifficultyCacheLookup(map, ruleset, null)), Is.GreaterThan(full));
        }

        private static long id(BeatmapDifficultyCache cache, BeatmapDifficultyCache.DifficultyCacheLookup key)
            => (long)typeof(BeatmapDifficultyCache).GetMethod("getTraceKeyId", BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(cache, new object[] { key })!;

        private sealed class Listener : EventListener
        {
            protected override void OnEventSourceCreated(EventSource source)
            {
                if (source.Name == StutterTraceEventSource.PROVIDER_NAME)
                    EnableEvents(source, EventLevel.Informational);
            }
        }
    }
}
