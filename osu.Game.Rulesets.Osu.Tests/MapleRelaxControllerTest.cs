using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class MapleRelaxControllerTest
    {
        private static readonly Vector2 centre = new Vector2(256, 192);

        [Test]
        public void TestMersenneTwisterKnownSequence()
        {
            var random = new MapleRelaxRandom(5489);
            uint[] expected = { 3499211612, 581869302, 3890346734, 3586334585, 545404204 };
            Assert.That(expected.Select(_ => random.NextUInt()), Is.EqualTo(expected));
        }

        [Test]
        public void TestNormalDistributionAndSeedReproducibility()
        {
            var random = new MapleRelaxRandom(1234);
            var duplicate = new MapleRelaxRandom(1234);
            double[] samples = Enumerable.Range(0, 50000).Select(_ => random.NextNormal()).ToArray();
            Assert.That(samples, Is.EqualTo(samples.Select(_ => duplicate.NextNormal()).ToArray()));
            Assert.That(samples.Average(), Is.EqualTo(0).Within(0.025));
            Assert.That(samples.Average(x => x * x), Is.EqualTo(1).Within(0.035));
        }

        [TestCase(0.75)]
        [TestCase(1)]
        [TestCase(1.5)]
        public void TestRecoveredSamplingFormulaAndOffsetClockSemantics(double rate)
        {
            var mod = maple();
            mod.MapleOffset.Value = 7;
            var samples = new Queue<double>(new[] { 1.0, -1.0 });
            var events = new List<RelaxController.RelaxInputEvent>();
            var controller = create(mod, events, () => samples.Dequeue(), circle(1000));
            controller.Update(900, rate, centre);
            Assert.That(controller.ScheduledPressTime, Is.EqualTo(1017));
            controller.Update(1016, rate, centre);
            Assert.That(events, Is.Empty);
            controller.Update(1017, rate, centre);
            Assert.That(events.Count, Is.EqualTo(1));
            // Adjusted time is 1010. Hold=(80-20/2.5)*rate. Release compares to RAW time.
            double release = 1010 + Math.Truncate(72 * rate);
            Assert.That(controller.NextReleaseTime, Is.EqualTo(release));
            controller.Update(release - 0.1, rate, centre);
            Assert.That(events.Count, Is.EqualTo(1));
            controller.Update(release, rate, centre);
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[1].IsPress, Is.False);
        }

        [Test]
        public void TestRateAppliedAfterInitialisationRescalesExistingHoldSample()
        {
            var events = new List<RelaxController.RelaxInputEvent>();
            var controller = create(maple(), events, () => 0, circle(1000));
            controller.Update(800, 1, centre);
            controller.Update(1000, 0.75, centre);
            Assert.That(controller.NextReleaseTime, Is.EqualTo(1060));
        }

        [Test]
        public void TestWaitLateUsesInnerNinetyPercentRadius()
        {
            HitCircle note = circle(1000);
            var events = new List<RelaxController.RelaxInputEvent>();
            var controller = create(maple(), events, () => 0, note);
            controller.Update(1000, 1, centre + new Vector2((float)note.Radius * 0.95f, 0));
            Assert.That(events, Is.Empty);
            controller.Update(1020, 1, centre);
            Assert.That(events.Single().Time, Is.EqualTo(1020));
        }

        [Test]
        public void TestWithoutWaitLateCanPressOffTargetAfterOneDeviation()
        {
            var mod = maple();
            mod.MapleWaitLate.Value = false;
            var events = new List<RelaxController.RelaxInputEvent>();
            var controller = create(mod, events, () => 0, circle(1000));
            controller.Update(1000, 1, Vector2.Zero);
            Assert.That(events, Is.Empty);
            controller.Update(1011, 1, Vector2.Zero);
            Assert.That(events.Count, Is.EqualTo(1));
        }

        [Test]
        public void TestExpiredWindowAdvancesWithOffTargetPress()
        {
            var events = new List<RelaxController.RelaxInputEvent>();
            HitCircle note = circle(1000);
            var controller = create(maple(), events, () => 0, note);
            controller.Update(900, 1, Vector2.Zero);
            controller.Update(1200, 1, Vector2.Zero);
            Assert.That(events.Single().IsPress, Is.True);
            Assert.That(controller.Target, Is.Null);
        }

        [Test]
        public void TestDirectionPredictionCanPressBeforeSampledTime()
        {
            var mod = maple();
            var events = new List<RelaxController.RelaxInputEvent>();
            HitCircle first = circle(1000);
            HitCircle next = circle(1300);
            next.Position = centre + new Vector2(150, 0);
            var controller = create(mod, events, () => 1, first, next);
            controller.Update(980, 1, centre);
            controller.Update(990, 1, centre + new Vector2((float)first.Radius * 0.8f, 0));
            Assert.That(events.Single().Time, Is.EqualTo(990));
            Assert.That(events[0].TargetStartTime, Is.EqualTo(1000));
        }

        [Test]
        public void TestDirectionPredictionToggle()
        {
            var mod = maple();
            mod.MapleDirectionPrediction.Value = false;
            HitCircle first = circle(1000);
            HitCircle next = circle(1300);
            next.Position = centre + new Vector2(150, 0);
            var events = new List<RelaxController.RelaxInputEvent>();
            var controller = create(mod, events, () => 1, first, next);
            controller.Update(980, 1, centre);
            controller.Update(990, 1, centre + new Vector2((float)first.Radius * 0.8f, 0));
            Assert.That(events, Is.Empty);
        }

        [TestCase(0.75)]
        [TestCase(1)]
        [TestCase(1.5)]
        public void TestIndependentButtonsAndRateScaledSameFingerSpacing(double rate)
        {
            var events = new List<RelaxController.RelaxInputEvent>();
            var notes = Enumerable.Range(0, 6).Select(i => circle(1000 + i * 60 * rate)).ToArray();
            var controller = create(maple(), events, () => 0, notes);
            for (double time = 900; time <= notes[^1].StartTime; time += 1)
                controller.Update(time, rate, centre);
            var presses = events.Where(e => e.IsPress).ToArray();
            Assert.That(presses.Length, Is.EqualTo(notes.Length));
            Assert.That(presses.Select(p => p.Action), Is.EqualTo(new[]
            {
                OsuAction.LeftButton, OsuAction.RightButton, OsuAction.LeftButton,
                OsuAction.RightButton, OsuAction.LeftButton, OsuAction.RightButton,
            }));
            controller.Update(2000, rate, centre);
            Assert.That(events.Count(e => !e.IsPress), Is.EqualTo(presses.Length));
        }

        [Test]
        public void TestSlowNotesUseConfiguredPrimaryButton()
        {
            var mod = maple();
            mod.MaplePrimaryButton.Value = MapleRelaxButton.Right;
            mod.MapleSecondaryButton.Value = MapleRelaxButton.Left;
            var events = new List<RelaxController.RelaxInputEvent>();
            var controller = create(mod, events, () => 0, circle(1000), circle(1500));
            controller.Update(1000, 1, centre);
            controller.Update(1100, 1, centre);
            controller.Update(1500, 1, centre);
            Assert.That(events.Where(e => e.IsPress).Select(e => e.Action), Is.All.EqualTo(OsuAction.RightButton));
        }

        [Test]
        public void TestSliderHoldsToEndPlusSeparateOffsetAndSpinnerNeedsNoAim()
        {
            var slider = new Slider
            {
                StartTime = 1000, Position = centre,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }, 100),
            };
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            var events = new List<RelaxController.RelaxInputEvent>();
            var controller = create(maple(), events, () => 0, slider);
            controller.Update(1000, 1, centre);
            Assert.That(controller.NextReleaseTime, Is.EqualTo(slider.EndTime + 65));
            controller.Update(slider.EndTime, 1, Vector2.Zero);
            Assert.That(events.Count, Is.EqualTo(1));
            controller.Update(slider.EndTime + 65, 1, Vector2.Zero);
            Assert.That(events.Count, Is.EqualTo(2));
            events.Clear();
            var spinner = new Spinner { StartTime = 1000, EndTime = 1500 };
            spinner.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            controller = create(maple(), events, () => 0, spinner);
            controller.Update(1000, 1, Vector2.Zero);
            Assert.That(events.Single().IsPress, Is.True);
            Assert.That(controller.NextReleaseTime, Is.EqualTo(1565));
        }

        [Test]
        public void TestInitialisingInsideSpinnerStillHolds()
        {
            var spinner = new Spinner { StartTime = 1000, EndTime = 2000 };
            spinner.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            var events = new List<RelaxController.RelaxInputEvent>();
            var controller = create(maple(), events, () => 0, spinner);
            controller.Update(1500, 1, Vector2.Zero);
            Assert.That(events.Single().IsPress, Is.True);
            Assert.That(controller.NextReleaseTime, Is.EqualTo(2065));
        }

        [TestCase(false, OsuAction.RightButton)]
        [TestCase(true, OsuAction.LeftButton)]
        public void TestSliderAlternationOverride(bool enabled, OsuAction expected)
        {
            var mod = maple();
            mod.MapleSliderAlternationOverride.Value = enabled;
            var slider = new Slider
            {
                StartTime = 1000, Position = centre,
                Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(140, 0) }, 140),
            };
            slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            HitCircle next = circle(slider.EndTime + 100);
            var events = new List<RelaxController.RelaxInputEvent>();
            var controller = create(mod, events, () => 0, slider, next);
            controller.Update(1000, 1, centre);
            controller.Update(slider.EndTime + 80, 1, centre);
            controller.Update(next.StartTime, 1, centre);
            Assert.That(events.Last(e => e.IsPress).Action, Is.EqualTo(expected));
        }

        [Test]
        public void TestLowestHoldUsesHalfGapAndCapsAtTwentyFive()
        {
            var mod = maple();
            mod.MapleLowestHoldTimes.Value = true;
            var events = new List<RelaxController.RelaxInputEvent>();
            var controller = create(mod, events, () => 0, circle(1000), circle(1020));
            controller.Update(1000, 1, centre);
            Assert.That(controller.NextReleaseTime, Is.EqualTo(1010));
            controller.Update(1010, 1, centre);
            controller.Update(1020, 1, centre);
            Assert.That(controller.NextReleaseTime, Is.EqualTo(1045));
        }

        [Test]
        public void TestRewindReleasesAndReinitialises()
        {
            var events = new List<RelaxController.RelaxInputEvent>();
            var controller = create(maple(), events, () => 0, circle(1000));
            controller.Update(1000, 1, centre);
            controller.Update(800, 1, centre);
            Assert.That(events.Count, Is.EqualTo(2));
            Assert.That(events[1].IsPress, Is.False);
            controller.Update(1000, 1, centre);
            Assert.That(events.Count(e => e.IsPress), Is.EqualTo(2));
        }

        private static OsuModMosuRelax maple() => new OsuModMosuRelax { Algorithm = { Value = MosuRelaxAlgorithm.Maple } };

        private static HitCircle circle(double time)
        {
            var circle = new HitCircle { StartTime = time, Position = centre };
            circle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());
            return circle;
        }

        private static MapleRelaxController create(OsuModMosuRelax mod, List<RelaxController.RelaxInputEvent> events, Func<double> normal,
                                                   params OsuHitObject[] objects)
        {
            var result = new MapleRelaxController(mod, objects, normal);
            result.InputChanged = (pressed, action, time, target) => events.Add(new RelaxController.RelaxInputEvent(pressed, action, time, target));
            return result;
        }
    }
}
