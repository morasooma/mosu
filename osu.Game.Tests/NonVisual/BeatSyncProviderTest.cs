using System;
using System.Reflection;
using osu.Game.Rulesets;
using NUnit.Framework;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class BeatSyncProviderTest
    {
        [Test]
        public void TestBeforeBeatmapInitialisation()
        {
            using var game = new OsuGameBase();
            Assert.That(((IBeatSyncProvider)game).ControlPoints, Is.Null);
            Assert.That(((IBeatSyncProvider)game).CurrentAmplitudes, Is.EqualTo(ChannelAmplitudes.Empty));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestCompletedBeatmapLoad(bool failed)
        {
            using var game = new OsuGameBase();
            var beatmap = new Beatmap();
            var working = new NullableTestWorkingBeatmap(beatmap, failed);

            typeof(OsuGameBase).GetProperty("Beatmap", BindingFlags.Instance | BindingFlags.NonPublic)!
                               .SetValue(game, new Bindable<WorkingBeatmap>(working));

            // BeatmapLoaded also reports true for a completed load which returned null.
            Assert.That(working.BeatmapLoaded, Is.True);
            Assert.That(((IBeatSyncProvider)game).ControlPoints,
                failed ? Is.Null : Is.SameAs(beatmap.ControlPointInfo));
            Assert.That(((IBeatSyncProvider)game).CurrentAmplitudes, Is.EqualTo(ChannelAmplitudes.Empty));
        }

        [Test]
        public void TestUnavailableRulesetDoesNotPreventTimingDecode()
        {
            var info = new BeatmapInfo
            {
                Ruleset = new RulesetInfo { ShortName = "missing", Available = false },
            };
            var resources = new WorkingBeatmapCache(null!, null!, null!, null!);
            var type = typeof(WorkingBeatmapCache).GetNestedType("BeatmapManagerWorkingBeatmap", BindingFlags.NonPublic)!;
            var working = (WorkingBeatmap)Activator.CreateInstance(type, info, resources)!;

            // No file yields an empty beatmap. This must not attempt to instantiate the missing ruleset first.
            Assert.That(working.Beatmap, Is.Not.Null);
            Assert.That(working.Beatmap.ControlPointInfo, Is.Not.Null);
        }

        private class NullableTestWorkingBeatmap : TestWorkingBeatmap
        {
            private readonly bool failed;

            public NullableTestWorkingBeatmap(IBeatmap beatmap, bool failed)
                : base(beatmap)
            {
                this.failed = failed;
            }

            protected override IBeatmap GetBeatmap() => failed ? throw new RulesetLoadException("Ruleset not available") : base.GetBeatmap();
        }
    }
}
