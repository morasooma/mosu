using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Edit;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Editor
{
    [TestFixture]
    public class RealtimeMappingRecorderTest
    {
        private static EditorBeatmap createBeatmap()
            => new EditorBeatmap(new Beatmap { BeatmapInfo = new BeatmapInfo { Ruleset = new OsuRuleset().RulesetInfo } });

        private static HitObject create(double heldDuration, double endTime, params Vector2[] points)
            => RealtimeMappingRecorder.CreateRecordedObject(createBeatmap(),
                1000, endTime, heldDuration, new Vector2(100, 200), points);

        [TestCase(-200)]
        [TestCase(0)]
        [TestCase(200)]
        public void TimeCorrectionPreservesCapturedCursorPosition(double offset)
        {
            var beatmap = createBeatmap();
            var positionAtPress = new Vector2(321, 123);
            var circle = (HitCircle)RealtimeMappingRecorder.CreateSnappedRecordedObject(
                beatmap, 1137, 1180, offset, positionAtPress, new[] { Vector2.Zero });
            Assert.That(circle.Position, Is.EqualTo(positionAtPress));
            Assert.That(circle.StartTime, Is.EqualTo(beatmap.SnapTime(1137 - offset, null)));
        }

        [TestCase(-200)]
        [TestCase(200)]
        public void SliderTimeCorrectionPreservesCapturedGeometry(double offset)
        {
            var beatmap = createBeatmap();
            var positionAtPress = new Vector2(321, 123);
            var path = new[] { Vector2.Zero, new Vector2(50, 60), new Vector2(200, 0) };
            var slider = (Slider)RealtimeMappingRecorder.CreateSnappedRecordedObject(
                beatmap, 1137, 1750, offset, positionAtPress, path);
            Assert.That(slider.Position, Is.EqualTo(positionAtPress));
            Assert.That(slider.Path.ControlPoints[0].Position, Is.EqualTo(Vector2.Zero));
            Assert.That(slider.Path.ControlPoints[^1].Position, Is.EqualTo(path[^1]));
            Assert.That(slider.StartTime, Is.EqualTo(beatmap.SnapTime(1137 - offset, null)));
        }

        [Test]
        public void RecordingUsesGameplayTypedBeatmap()
        {
            Assert.DoesNotThrow(() => new osu.Game.Rulesets.Osu.UI.DrawableOsuRuleset(
                new OsuRuleset(), new osu.Game.Rulesets.Osu.Beatmaps.OsuBeatmap()));
        }

        [Test]
        public void OccupiedSnappedSlotRejectsDuplicatesButNotNextSlot()
        {
            var objects = new HitObject[] { new HitCircle { StartTime = 1000 } };
            Assert.That(RealtimeMappingRecorder.IsTimeSlotOccupied(objects, 1000), Is.True);
            Assert.That(RealtimeMappingRecorder.IsTimeSlotOccupied(objects, 1125), Is.False);
            Assert.That(RealtimeMappingRecorder.IsTimeSlotOccupied(Array.Empty<HitObject>(), 1000), Is.False);
        }

        [Test]
        public void RecordedObjectsHaveNormalHitsounds()
        {
            Assert.That(create(50, 1125, Vector2.Zero).Samples, Has.Count.EqualTo(1));
            Assert.That(create(500, 1500, Vector2.Zero, new Vector2(200, 0)).Samples, Has.Count.EqualTo(1));
        }

        [Test]
        public void RecordedSliderUsesEditorSplineSmoothing()
        {
            var slider = (Slider)create(500, 1500, Vector2.Zero, new Vector2(30, 10),
                new Vector2(60, 35), new Vector2(100, 60), new Vector2(150, 70), new Vector2(200, 60));
            Assert.That(slider.Path.ControlPoints[0].Type,
                Is.EqualTo(osu.Game.Rulesets.Objects.Types.PathType.BSpline(4)));
            Assert.That(slider.Path.ControlPoints[^1].Position, Is.EqualTo(new Vector2(200, 60)));
        }

        [Test]
        public void ShortTapIsCircle()
        {
            var obj = create(50, 1125, Vector2.Zero, new Vector2(100, 0));
            Assert.That(obj, Is.TypeOf<HitCircle>());
            Assert.That(obj.StartTime, Is.EqualTo(1000));
            Assert.That(((HitCircle)obj).Position, Is.EqualTo(new Vector2(100, 200)));
        }

        [Test]
        public void StationaryHoldIsCircle()
            => Assert.That(create(500, 1500, Vector2.Zero), Is.TypeOf<HitCircle>());

        [Test]
        public void ZeroSnappedDurationIsCircle()
            => Assert.That(create(150, 1000, Vector2.Zero, new Vector2(100, 0)), Is.TypeOf<HitCircle>());

        [TestCase(1)]
        [TestCase(100)]
        [TestCase(10000)]
        public void SliderKeepsSnappedEndAtVelocityLimits(float distance)
        {
            var slider = (Slider)create(500, 1500, Vector2.Zero, new Vector2(distance, 0));
            Assert.That(slider.EndTime, Is.EqualTo(1500).Within(0.001));
            Assert.That(slider.SliderVelocityMultiplier, Is.InRange(0.1, 10));
            Assert.That(slider.Position, Is.EqualTo(new Vector2(100, 200)));
            Assert.That(slider.RepeatCount, Is.Zero);
        }

        [Test]
        public void SliderEndSurvivesEditorReprocessing()
        {
            var beatmap = createBeatmap();
            var slider = (Slider)RealtimeMappingRecorder.CreateRecordedObject(beatmap,
                1000, 1625, 600, Vector2.Zero,
                new[] { Vector2.Zero, new Vector2(50, 50), new Vector2(150, 0) });
            slider.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
            Assert.That(slider.EndTime, Is.EqualTo(1625).Within(0.001));
            Assert.That(slider.Path.ControlPoints.Count, Is.GreaterThanOrEqualTo(2));
        }
    }
}
