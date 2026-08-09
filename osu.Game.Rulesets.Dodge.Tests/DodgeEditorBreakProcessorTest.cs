// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeEditorBreakProcessorTest
    {
        [Test]
        public void ArenaChangeDoesNotCreateAutomaticBreakBeforeFirstProjectile()
        {
            EditorBeatmap beatmap = createBeatmap(
                new DodgeArenaChange
                {
                    StartTime = 0,
                    Duration = 500,
                },
                new DodgeBullet
                {
                    StartTime = 5000,
                    Duration = 1000,
                });

            process(beatmap);

            Assert.That(beatmap.Breaks.Select(b => (b.StartTime, b.EndTime)), Is.Empty);
        }

        [Test]
        public void AutomaticBreakDoesNotAppearWhileContinuedBulletIsMoving()
        {
            EditorBeatmap beatmap = createBeatmap(
                new DodgeBullet
                {
                    StartTime = 1000,
                    Duration = 1000,
                    Position = new Vector2(256, 192),
                    EndPosition = new Vector2(266, 192),
                    ContinueUntilExit = true,
                },
                new DodgeBullet
                {
                    StartTime = 10_000,
                    Duration = 1000,
                });

            process(beatmap);

            Assert.That(beatmap.Breaks.Select(b => (b.StartTime, b.EndTime)), Is.Empty);
        }

        [Test]
        public void AutomaticBreakDoesNotAppearWhileContinuedEmitterBulletsAreMoving()
        {
            EditorBeatmap beatmap = createBeatmap(
                new DodgeEmitter
                {
                    StartTime = 1000,
                    Duration = 1000,
                    Position = new Vector2(256, 192),
                    AimPosition = new Vector2(266, 192),
                    BulletCount = 3,
                    ContinueUntilExit = true,
                },
                new DodgeBullet
                {
                    StartTime = 10_000,
                    Duration = 1000,
                });

            process(beatmap);

            Assert.That(beatmap.Breaks.Select(b => (b.StartTime, b.EndTime)), Is.Empty);
        }

        [Test]
        public void AutomaticBreakStillAppearsAfterProjectilesHaveCleared()
        {
            EditorBeatmap beatmap = createBeatmap(
                new DodgeBullet
                {
                    StartTime = 1000,
                    Duration = 1000,
                },
                new DodgeBullet
                {
                    StartTime = 10_000,
                    Duration = 1000,
                });

            process(beatmap);

            Assert.That(beatmap.Breaks, Has.Count.EqualTo(1));
            Assert.That(beatmap.Breaks[0].StartTime, Is.GreaterThanOrEqualTo(2200));
            Assert.That(beatmap.Breaks[0].EndTime, Is.LessThanOrEqualTo(9250));
        }

        private static EditorBeatmap createBeatmap(params DodgeHitObject[] hitObjects)
        {
            var controlPoints = new ControlPointInfo();
            controlPoints.Add(0, new TimingControlPoint { BeatLength = 500 });

            var beatmap = new Beatmap
            {
                ControlPointInfo = controlPoints,
                BeatmapInfo = { Ruleset = new DodgeRuleset().RulesetInfo },
            };

            beatmap.HitObjects.AddRange(hitObjects);
            return new EditorBeatmap(beatmap);
        }

        private static void process(EditorBeatmap beatmap)
        {
            var processor = new EditorBeatmapProcessor(beatmap, new DodgeRuleset());
            processor.PreProcess();
            processor.PostProcess();
        }
    }
}
