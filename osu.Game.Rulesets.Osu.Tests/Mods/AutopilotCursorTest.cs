using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Online;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    public class AutopilotCursorTest
    {
        private OsuModAutopilot mod = null!;
        private DrawableOsuRuleset ruleset = null!;
        private List<OsuReplayFrame> frames = null!;
        private string previousApiUrl = null!;

        [SetUp]
        public void SetUp()
        {
            previousApiUrl = MosuServerEnvironment.ActiveApiUrl;
            MosuServerEnvironment.ActiveApiUrl = MosuServerEnvironment.PublicServerUrl;
            var beatmap = new OsuBeatmap();
            beatmap.HitObjects.Add(new HitCircle { StartTime = 1000, Position = new Vector2(100, 100) });
            beatmap.HitObjects.Add(new HitCircle { StartTime = 1700, Position = new Vector2(400, 200) });
            beatmap.HitObjects.Add(new Slider
            {
                StartTime = 2200,
                Position = new Vector2(100, 100),
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(200, 0) }, 200),
                RepeatCount = 1,
            });
            beatmap.HitObjects.Add(new Spinner { StartTime = 5000, Duration = 1000 });
            foreach (var obj in beatmap.HitObjects)
                obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            mod = new OsuModAutopilot();
            ruleset = new DrawableOsuRuleset(new OsuRuleset(), beatmap, new Mod[] { mod });
            mod.ApplyToDrawableRuleset(ruleset);
            frames = new OsuAutoGenerator(beatmap, new Mod[] { mod }) { DelayedMovements = true }
                     .Generate().Frames.Cast<OsuReplayFrame>().ToList();
        }

        [TearDown]
        public void TearDown()
        {
            MosuServerEnvironment.ActiveApiUrl = previousApiUrl;
            ruleset.Dispose();
        }

        [Test]
        public void TestInterpolatesBetweenGeneratedFrames()
        {
            var (start, end) = movingFrames().First();
            double time = (start.Time + end.Time) / 2;
            Vector2 expected = (start.Position + end.Position) / 2;

            assertPosition(time, expected);
            Assert.That(mod.GetCursorPosition(time), Is.Not.EqualTo(start.Position));
            Assert.That(mod.GetCursorPosition(time), Is.Not.EqualTo(end.Position));
        }

        [TestCase(30)]
        [TestCase(60)]
        [TestCase(165)]
        [TestCase(240)]
        [TestCase(1000)]
        public void TestFollowsClockAtDifferentUpdateRates(int updatesPerSecond)
        {
            for (double time = frames[0].Time; time <= frames[^1].Time; time += 1000.0 / updatesPerSecond)
                assertPosition(time, expectedPosition(time));
        }

        [Test]
        public void TestCatchesUpAfterStallAndCanSeekBackwards()
        {
            foreach (double time in new[] { -2000, 990, 3500, 5900, 10000, 1300, 1000, -1000, 1700 })
                assertPosition(time, expectedPosition(time));
        }

        [Test]
        public void TestExactObjectPositions()
        {
            foreach (var obj in ruleset.Beatmap.HitObjects.Where(h => h is not Spinner))
                assertPosition(obj.StartTime, obj.StackedPosition);
        }

        [Test]
        public void TestDelayedMovementKeepsCursorOnCircleForLateTap()
        {
            assertPosition(1050, ruleset.Beatmap.HitObjects[0].StackedPosition);
        }

        [Test]
        public void TestReapplyingResetsPlaybackPosition()
        {
            mod.GetCursorPosition(10000);
            mod.ApplyToDrawableRuleset(ruleset);
            assertPosition(900, expectedPosition(900));
        }

        [Test]
        public void TestEmptyBeatmap()
        {
            using var emptyRuleset = new DrawableOsuRuleset(new OsuRuleset(), new OsuBeatmap());
            mod.ApplyToDrawableRuleset(emptyRuleset);
            Assert.That(mod.GetCursorPosition(0), Is.Null);
            Assert.That(mod.GetCursorPosition(1000), Is.Null);
        }

        [Test]
        public void TestDuplicateFrameTimesHaveFinitePositions()
        {
            ruleset.Beatmap.HitObjects.Insert(1, new HitCircle { StartTime = 1000, Position = new Vector2(300, 100) });
            ruleset.Beatmap.HitObjects[1].ApplyDefaults(ruleset.Beatmap.ControlPointInfo, ruleset.Beatmap.Difficulty);
            mod.ApplyToDrawableRuleset(ruleset);
            foreach (double time in new[] { 999.9, 1000, 1000.1, 1000 })
            {
                Vector2 position = mod.GetCursorPosition(time)!.Value;
                Assert.That(float.IsFinite(position.X) && float.IsFinite(position.Y), Is.True);
            }
        }

        [TestCase("https://morasooma.net", true)]
        [TestCase("https://proxy.morasooma.net", true)]
        [TestCase("https://proxy2.morasooma.net", true)]
        [TestCase("http://127.0.0.1:18082", true)]
        [TestCase("https://osu.ppy.sh", false)]
        [TestCase("https://lazer-api.shikkesora.com", false)]
        [TestCase("https://other.example", false)]
        public void TestServerSelectsMovementPolicy(string apiUrl, bool smooth)
        {
            MosuServerEnvironment.ActiveApiUrl = apiUrl;
            mod.ApplyToDrawableRuleset(ruleset);
            var generated = new OsuAutoGenerator(ruleset.Beatmap, ruleset.Mods) { DelayedMovements = smooth }
                            .Generate().Frames.Cast<OsuReplayFrame>().ToList();

            // The first call to the original lazer algorithm always selects the first frame.
            double time = movingFrames().First().end.Time;
            Vector2? position = mod.GetCursorPosition(time);
            if (smooth)
                assertPosition(time, expectedPosition(time));
            else
                Assert.That(position, Is.EqualTo(generated[0].Position));
        }

        [TestCase("https://osu.ppy.sh")]
        [TestCase("https://lazer-api.shikkesora.com")]
        public void TestOtherServersMatchOriginalLazerAlgorithm(string apiUrl)
        {
            MosuServerEnvironment.ActiveApiUrl = apiUrl;
            mod.ApplyToDrawableRuleset(ruleset);
            var originalFrames = new OsuAutoGenerator(ruleset.Beatmap, ruleset.Mods)
                                 .Generate().Frames.Cast<OsuReplayFrame>().ToList();
            int index = -1;
            var times = Enumerable.Range(0, 7000).Select(i => (double)i - 1000)
                                  .Concat(new[] { 10000.0, 10000, 1000, -1000, 5900 })
                                  .Concat(Enumerable.Repeat(10000.0, originalFrames.Count));
            foreach (double time in times)
            {
                Vector2? expected = null;
                if (index != originalFrames.Count - 1
                    && (index < 0 || Math.Abs(originalFrames[index + 1].Time - time) <= Math.Abs(originalFrames[index].Time - time)))
                    expected = originalFrames[++index].Position;

                Assert.That(mod.GetCursorPosition(time), Is.EqualTo(expected), $"original lazer update at {time}ms");
            }
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TestPolicyIsCapturedForPlayAndReevaluatedOnApply(bool startOnMosu)
        {
            MosuServerEnvironment.ActiveApiUrl = startOnMosu ? MosuServerEnvironment.PublicServerUrl : "https://osu.ppy.sh";
            mod.ApplyToDrawableRuleset(ruleset);
            MosuServerEnvironment.ActiveApiUrl = startOnMosu ? "https://osu.ppy.sh" : MosuServerEnvironment.PublicServerUrl;
            double time = movingFrames().First().end.Time;
            Assert.That(mod.GetCursorPosition(time), Is.EqualTo(startOnMosu ? expectedPosition(time) : frames[0].Position));

            mod.ApplyToDrawableRuleset(ruleset);
            Assert.That(mod.GetCursorPosition(time), Is.EqualTo(startOnMosu ? frames[0].Position : expectedPosition(time)));
        }

        private IEnumerable<(OsuReplayFrame start, OsuReplayFrame end)> movingFrames()
            => frames.Zip(frames.Skip(1), (start, end) => (start, end))
                     .Where(pair => pair.end.Time > pair.start.Time && pair.start.Position != pair.end.Position);

        private Vector2 expectedPosition(double time)
        {
            if (time < frames[0].Time)
                return frames[0].Position;

            int index = frames.FindLastIndex(f => f.Time <= time);
            if (index == frames.Count - 1)
                return frames[index].Position;

            var start = frames[index];
            var end = frames[index + 1];
            return Vector2.Lerp(start.Position, end.Position, (float)((time - start.Time) / (end.Time - start.Time)));
        }

        private void assertPosition(double time, Vector2 expected)
        {
            Vector2? actual = mod.GetCursorPosition(time);
            Assert.That(actual.HasValue, Is.True);
            Assert.That(Vector2.Distance(actual!.Value, expected), Is.LessThan(0.001f), $"cursor at {time}ms");
        }
    }
}
