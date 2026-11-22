using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeLoopMotionTest
    {
        [TestCase(DodgeMovementType.PingPong)]
        [TestCase(DodgeMovementType.OrbitClockwise)]
        [TestCase(DodgeMovementType.OrbitCounterclockwise)]
        public void TestCyclesCloseAndRepeat(DodgeMovementType type)
        {
            Vector2 start = new Vector2(100, 150);
            Vector2 end = new Vector2(300, 150);
            Assert.Multiple(() =>
            {
                Assert.That((position(0) - start).Length, Is.LessThan(0.001));
                Assert.That((position(0.5f) - end).Length, Is.LessThan(0.001));
                Assert.That((position(1) - start).Length, Is.LessThan(0.001));
                Assert.That((position(3.25f) - position(0.25f)).Length, Is.LessThan(0.001));
            });
            Vector2 position(float progress) => DodgeTrajectory.PositionAtProgress(start, end, progress, type, 0, 1, 0);
        }

        [TestCase(DodgeMovementType.OrbitClockwise, -1)]
        [TestCase(DodgeMovementType.OrbitCounterclockwise, 1)]
        public void TestCircleDirectionAndRadius(DodgeMovementType type, int ySign)
        {
            Vector2 centre = new Vector2(200, 150);
            Vector2 quarter = DodgeTrajectory.PositionAtProgress(new Vector2(100, 150), new Vector2(300, 150), 0.25f, type, 0, 1, 0);
            Assert.That((quarter - (centre + new Vector2(0, 100 * ySign))).Length, Is.LessThan(0.001));
            foreach (Vector2 point in DodgeTrajectory.CreatePathVertices(new Vector2(100, 150), new Vector2(300, 150), 1, type, 0, 1, 0))
                Assert.That((point - centre).Length, Is.EqualTo(100).Within(0.001));
        }

        [TestCase(DodgeMovementType.PingPong)]
        [TestCase(DodgeMovementType.OrbitClockwise)]
        [TestCase(DodgeMovementType.OrbitCounterclockwise)]
        public void TestContinueUntilExitCannotMakeLoopInfinite(DodgeMovementType type)
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000, Duration = 4000, Position = new Vector2(100), EndPosition = new Vector2(300),
                ContinueUntilExit = true, MovementType = type, WaveCycles = 4,
            };
            DodgeWorldGeometry.ApplyArenaTimeline(new DodgeHitObject[] { bullet, new DodgeArenaChange { WorldSpace = true } });
            Assert.That(bullet.MovementEndTime, Is.EqualTo(5000));
            Assert.That((bullet.PositionAt(9000) - bullet.PositionAt(5000)).Length, Is.LessThan(0.001));
            var emitter = new DodgeEmitter { StartTime = 1000, Duration = 4000, ContinueUntilExit = true, MovementType = type };
            Assert.That(emitter.ExitTimeAt(0, 0), Is.EqualTo(5000));
        }

        [Test]
        public void TestTranslationKeepsPatternShapeAndRotationKeepsDistances()
        {
            Vector2 first = new Vector2(100, 150);
            Vector2 second = new Vector2(300, 200);
            Vector2 travel = new Vector2(100, 50);
            Vector2 centre = (first + second) / 2;
            for (int i = 0; i <= 100; i++)
            {
                float progress = i / 100f;
                Vector2 translatedFirst = DodgeLoopMotion.Position(first, first + travel, progress, DodgeMovementType.OrbitClockwise, 3, 0, DodgeMovementEasing.Linear);
                Vector2 translatedSecond = DodgeLoopMotion.Position(second, second + travel, progress, DodgeMovementType.OrbitClockwise, 3, 0, DodgeMovementEasing.Linear);
                Assert.That((translatedSecond - translatedFirst - (second - first)).Length, Is.LessThan(0.001));
                Vector2 rotatedFirst = DodgeLoopMotion.Position(first, centre * 2 - first, progress, DodgeMovementType.OrbitClockwise, 3, 0, DodgeMovementEasing.Linear);
                Vector2 rotatedSecond = DodgeLoopMotion.Position(second, centre * 2 - second, progress, DodgeMovementType.OrbitClockwise, 3, 0, DodgeMovementEasing.Linear);
                Assert.That((rotatedSecond - rotatedFirst).Length, Is.EqualTo((second - first).Length).Within(0.001));
            }
        }

        [Test]
        public void TestCollisionSamplingAndGuideDoNotTreatClosedCircleAsStraightLine()
        {
            var vertices = DodgeTrajectory.CreatePathVertices(new Vector2(100), new Vector2(300, 100), 1,
                DodgeMovementType.OrbitClockwise, 0, 4, 0);
            Assert.That(vertices.Count, Is.GreaterThan(100));
            Assert.That(vertices.Max(p => p.Y) - vertices.Min(p => p.Y), Is.GreaterThan(190));
            Assert.That(DodgeTrajectory.SuggestedSubdivisionCount(0, 1000, 0, 1000, DodgeMovementType.OrbitClockwise, 4), Is.GreaterThan(1));
        }

        [TestCase(DodgeEmitterLoopScope.TranslatePattern)]
        [TestCase(DodgeEmitterLoopScope.RotatePattern)]
        public void TestEmitterLoopRemainsProceduralWhenFanIsEdited(DodgeEmitterLoopScope scope)
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000, Duration = 4000, Position = new Vector2(256, 192),
                AimPosition = new Vector2(356, 192), BulletCount = 8, SpreadAngle = 360,
                BurstCount = 2, BurstInterval = 500, BurstBeatDivisor = 0,
                MovementType = DodgeMovementType.OrbitClockwise, WaveCycles = 2,
                LoopScope = scope, LoopOffset = new Vector2(128, 32),
            };
            emitter.BulletCount = 12;
            emitter.AimPosition = emitter.Position + new Vector2(150, 0);
            foreach (int burst in Enumerable.Range(0, emitter.EffectiveBurstCount))
            {
                for (int ray = 0; ray < emitter.EffectiveBulletCount; ray++)
                {
                    var points = emitter.TrajectoryPointsAt(burst, ray);
                    Assert.That(points.Start, Is.EqualTo(emitter.EndPositionAt(burst, ray)));
                    if (scope == DodgeEmitterLoopScope.TranslatePattern)
                        Assert.That(points.End - points.Start, Is.EqualTo(emitter.LoopOffset));
                    else
                    {
                        Assert.That(((points.Start + points.End) / 2 - emitter.SourcePositionAt(burst)).Length, Is.LessThan(0.001));
                        Assert.That((emitter.PositionAt(burst, ray, emitter.EmissionTimeAt(burst) + 250) - emitter.SourcePositionAt(burst)).Length,
                            Is.EqualTo(150).Within(0.001));
                    }
                }
            }
            var clone = (DodgeEmitter)DodgeHitObjectCloner.Clone(emitter);
            Assert.Multiple(() =>
            {
                Assert.That(clone.LoopScope, Is.EqualTo(scope));
                Assert.That(clone.LoopOffset, Is.EqualTo(emitter.LoopOffset));
                Assert.That(clone.BulletCount, Is.EqualTo(12));
                Assert.That(clone.BurstCount, Is.EqualTo(2));
                Assert.That(clone.AimPosition, Is.EqualTo(emitter.AimPosition));
            });
        }

        [Test]
        public void TestLoopModesSurviveSidecarRoundTrip()
        {
            var beatmap = new Beatmap<DodgeHitObject>();
            beatmap.HitObjects.Add(new DodgeBullet
            {
                StartTime = 1000, Duration = 8000, Position = new Vector2(64), EndPosition = new Vector2(300),
                MovementType = DodgeMovementType.OrbitCounterclockwise, WaveCycles = 8, WavePhase = 90,
            });
            beatmap.HitObjects.Add(new DodgeEmitter
            {
                StartTime = 2000, Duration = 6000, Position = new Vector2(80), AimPosition = new Vector2(250),
                MovementType = DodgeMovementType.PingPong, WaveCycles = 6,
                LoopScope = DodgeEmitterLoopScope.TranslatePattern, LoopOffset = new Vector2(90, -30),
            });
            using var stream = new MemoryStream();
            DodgeBeatmapSerializer.Serialize(beatmap, stream);
            stream.Position = 0;
            DodgeHitObject[] decoded = DodgeBeatmapSerializer.DeserializeHitObjects(stream).ToArray();
            var bullet = decoded.OfType<DodgeBullet>().Single();
            var emitter = decoded.OfType<DodgeEmitter>().Single();
            Assert.Multiple(() =>
            {
                Assert.That(bullet.MovementType, Is.EqualTo(DodgeMovementType.OrbitCounterclockwise));
                Assert.That(bullet.Duration, Is.EqualTo(8000));
                Assert.That(bullet.WaveCycles, Is.EqualTo(8));
                Assert.That(bullet.WavePhase, Is.EqualTo(90));
                Assert.That(emitter.MovementType, Is.EqualTo(DodgeMovementType.PingPong));
                Assert.That(emitter.Duration, Is.EqualTo(6000));
                Assert.That(emitter.WaveCycles, Is.EqualTo(6));
                Assert.That(emitter.LoopScope, Is.EqualTo(DodgeEmitterLoopScope.TranslatePattern));
                Assert.That(emitter.LoopOffset, Is.EqualTo(new Vector2(90, -30)));
            });
        }
    }
}
