using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    public partial class TestSceneOsuModSegmentRate : OsuModTestScene
    {
        [TestCase(false)]
        [TestCase(true)]
        public void TestGlitchChangesActualGameplayClockWithoutChangingPitch(bool baked)
            => run(new OsuModGlitch { BakedTiming = { Value = baked }, Amount = { Value = 100 }, Thirds = { Value = false }, Fifths = { Value = false } });

        [TestCase(FlatlineDirection.Harder, false)]
        [TestCase(FlatlineDirection.Easier, false)]
        [TestCase(FlatlineDirection.Both, false)]
        [TestCase(FlatlineDirection.Harder, true)]
        [TestCase(FlatlineDirection.Easier, true)]
        [TestCase(FlatlineDirection.Both, true)]
        public void TestFlatlineChangesActualGameplayClock(FlatlineDirection direction, bool baked)
            => run(new OsuModFlatline { BakedTiming = { Value = baked }, Direction = { Value = direction } });

        [Test]
        public void TestMappedBreakOverlayAndSkip(
            [Values(false, true)] bool flatline,
            [Values(false, true)] bool baked)
        {
            ModSegmentRate rate = flatline ? new OsuModFlatline() : new OsuModGlitch { Amount = { Value = 100 }, Thirds = { Value = false }, Fifths = { Value = false } };
            rate.BakedTiming.Value = baked;
            CreateModTest(new ModTestData
            {
                Mod = rate,
                CreateBeatmap = () =>
                {
                    var beatmap = new OsuBeatmap();
                    beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                    for (int i = 0; i < 32; i++)
                        beatmap.HitObjects.Add(new HitCircle { StartTime = 2000 + i * 250, Position = new Vector2(100 + i % 2 * 30, 100) });
                    beatmap.Breaks.Add(new BreakPeriod(11000, 20000));
                    for (int i = 0; i < 40; i++)
                        beatmap.HitObjects.Add(new HitCircle { StartTime = 21000 + i * 80, Position = new Vector2(70 + i % 2 * 350, 100 + i % 3 * 80) });
                    return beatmap;
                },
                PassCondition = () => true,
            });

            AddStep("get applied tempo mod", () => rate = Player.GameplayState.Mods.OfType<ModSegmentRate>().Single());
            AddAssert("non-neutral tempo plan", () => rate.RatePlan.Segments.Count > 0);
            AddAssert("break moved in baked mode only", () =>
                (Math.Abs(Player.GameplayState.Beatmap.Breaks.Single().StartTime - 11000) > 100) == baked);
            AddStep("seek into mapped break", () =>
            {
                var period = Player.GameplayState.Beatmap.Breaks.Single();
                Player.GameplayClockContainer.Stop();
                Player.GameplayClockContainer.Seek(period.StartTime + 100);
            });
            AddUntilStep("tracker uses playable break", () =>
                this.ChildrenOfType<BreakTracker>().Single().CurrentBreak.Value?.StartTime
                == Player.GameplayState.Beatmap.Breaks.Single().StartTime);
            AddUntilStep("break overlay visible", () => this.ChildrenOfType<BreakOverlay>().Single().IsVisualVisible);
            AddStep("resume for authorised skip", () => Player.GameplayClockContainer.Start());
            AddUntilStep("clock running", () => Player.GameplayClockContainer.IsRunning);
            AddStep("skip mapped break", () =>
            {
                var tracker = this.ChildrenOfType<BreakTracker>().Single();
                var period = tracker.CurrentBreak.Value!;
                Assert.That(((MasterGameplayClockContainer)Player.GameplayClockContainer).SkipBreak(period, tracker.GetBreakIndex(period), false), Is.True);
                Player.GameplayClockContainer.Stop();
            });
            AddAssert("skip uses mapped end", () => Player.GameplayClockContainer.CurrentTime,
                () => Is.EqualTo(Player.GameplayState.Beatmap.Breaks.Single().EndTime - MasterGameplayClockContainer.MINIMUM_SKIP_TIME).Within(150));
            AddStep("seek after mapped break", () => Player.GameplayClockContainer.Seek(Player.GameplayState.Beatmap.Breaks.Single().EndTime + 100));
            AddUntilStep("break overlay hidden", () => !this.ChildrenOfType<BreakOverlay>().Single().IsVisualVisible);
        }

        [Test]
        public void TestHiddenSliderFadeTransforms(
            [Values(false, true)] bool flatline,
            [Values(false, true)] bool hiddenFirst)
        {
            ModSegmentRate rate = flatline ? new OsuModFlatline() : new OsuModGlitch { Amount = { Value = 100 } };
            var hidden = new OsuModHidden();
            CreateModTest(new ModTestData
            {
                Mods = hiddenFirst ? new Mod[] { hidden, rate } : new Mod[] { rate, hidden },
                CreateBeatmap = () =>
                {
                    var beatmap = new OsuBeatmap();
                    beatmap.Difficulty.ApproachRate = 10;
                    beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                    beatmap.HitObjects.Add(new HitCircle { StartTime = 2000, Position = new Vector2(100) });
                    beatmap.HitObjects.Add(new Slider
                    {
                        StartTime = 4000,
                        Position = new Vector2(200),
                        RepeatCount = 3,
                        Path = new SliderPath(new[]
                        {
                            new PathControlPoint(Vector2.Zero),
                            new PathControlPoint(new Vector2(200, 0)),
                        }, 200),
                    });
                    for (int i = 0; i < 40; i++)
                        beatmap.HitObjects.Add(new HitCircle { StartTime = 7000 + i * 80, Position = new Vector2(70 + i % 2 * 350, 100 + i % 3 * 80) });
                    return beatmap;
                },
                PassCondition = () => true,
            });
            AddStep("seek to slider fade", () =>
            {
                var slider = Player.GameplayState.Beatmap.HitObjects.OfType<Slider>().Single();
                Player.GameplayClockContainer.Stop();
                // HD at AR10: fade-in 180ms, fade-out 135ms. Halfway through fade-out.
                Player.GameplayClockContainer.Seek(slider.StartTime - slider.TimePreempt + 180 + 67.5);
            });
            AddUntilStep("slider head loaded", () => Player.DrawableRuleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Any());
            AddAssert("head fades at hidden timing", () =>
                Player.DrawableRuleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Single().HeadCircle.Alpha,
                () => Is.EqualTo(0.5f).Within(0.05f));
        }

        private void run(ModSegmentRate mod)
        {
            bool observedChangedRate = false;
            int lastLoggedSecond = -1;
            CreateModTest(new ModTestData
            {
                Mod = mod,
                CreateBeatmap = () =>
                {
                    var beatmap = new OsuBeatmap();
                    beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
                    for (int i = 0; i < 16; i++)
                        beatmap.HitObjects.Add(new HitCircle { StartTime = 2000 + i * 250, Position = new Vector2(100 + i % 2 * 30, 100) });
                    for (int i = 0; i < 40; i++)
                        beatmap.HitObjects.Add(new HitCircle { StartTime = 6000 + i * 80, Position = new Vector2(70 + i % 2 * 350, 100 + i % 3 * 80) });
                    return beatmap;
                },
                PassCondition = () =>
                {
                    var actual = Player.GameplayState.Mods.OfType<ModSegmentRate>().Single();
                    double time = Player.GameplayClockContainer.CurrentTime;
                    double sourceTime = actual.BakedTiming.Value ? actual.RatePlan.Unmap(time) : time;
                    double expected = actual.ApplyToRate(time);
                    double expectedTempo = actual.RatePlan.RateAt(sourceTime);
                    if ((int)(time / 1000) != lastLoggedSecond)
                    {
                        lastLoggedSecond = (int)(time / 1000);
                        TestContext.Out.WriteLine($"{mod.Acronym} {time:F0}ms expected={expected:F3} actual={Player.GameplayClockContainer.Rate:F3} frequency={Player.GameplayClockContainer.AdjustmentsFromMods.AggregateFrequency.Value:F3} segments={actual.RatePlan.Segments.Count}");
                    }
                    // Do not assert a boundary frame: audio adjustments are propagated on the audio thread.
                    if (actual.RatePlan.Segments.Any(s => sourceTime > s.Start + 50 && sourceTime < s.End - 50)
                        && Math.Abs(expectedTempo - 1) > 0.01
                        && Math.Abs(Player.GameplayClockContainer.AdjustmentsFromMods.AggregateTempo.Value - expectedTempo) < 0.01
                        && Math.Abs(Player.GameplayClockContainer.Rate - expected) < 0.01)
                    {
                        observedChangedRate = true;
                        Assert.That(Player.GameplayClockContainer.AdjustmentsFromMods.AggregateFrequency.Value, Is.EqualTo(1).Within(0.0001));
                    }
                    return observedChangedRate && Player.ScoreProcessor.JudgedHits >= 8;
                },
            });
        }
    }
}
