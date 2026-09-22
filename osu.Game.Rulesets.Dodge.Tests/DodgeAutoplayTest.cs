// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Mods;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Replays;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Mods;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeAutoplayTest
    {
        [Test]
        public void TestAutoplayDodgesCrossingBulletWithoutExceedingPlayerSpeed()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(0, 192),
                EndPosition = new Vector2(512, 192),
            };
            Beatmap<DodgeHitObject> beatmap = createBeatmap(bullet);
            List<DodgeReplayFrame> frames = generateFrames(beatmap);
            float movementSpeed = (float)(DodgeBeatmapSettings.GetPlayerSpeed(beatmap.Difficulty) / 1000);

            Assert.That(frames, Has.Count.GreaterThan(2));

            for (int i = 1; i < frames.Count; i++)
            {
                double elapsed = frames[i].Time - frames[i - 1].Time;
                float distance = (frames[i].Position - frames[i - 1].Position).Length;
                Assert.That(distance, Is.LessThanOrEqualTo(movementSpeed * elapsed + 0.1f));
            }

            for (double time = bullet.StartTime; time <= bullet.EndTime; time += 5)
            {
                Vector2 playerPosition = positionAt(frames, time);
                Assert.That(
                    DodgeBullet.IntersectsPlayer(bullet.PositionAt(time), playerPosition, bullet.BulletSize),
                    Is.False,
                    $"Collision at {time:N0} ms");
            }
        }

        [Test]
        public void TestAutoplayHandlesEmitterInsideChangedArena()
        {
            var arena = new DodgeArenaChange
            {
                StartTime = 500,
                Duration = 0,
                TargetPosition = new Vector2(96, 64),
                TargetSize = new Vector2(320, 256),
            };
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1500,
                Position = new Vector2(96, 192),
                AimPosition = new Vector2(416, 192),
                BulletCount = 5,
                SpreadAngle = 90,
            };
            Beatmap<DodgeHitObject> beatmap = createBeatmap(arena, emitter);
            List<DodgeReplayFrame> frames = generateFrames(beatmap);
            var arenaEvaluator = new DodgeArenaStateEvaluator(new[] { arena });

            foreach (DodgeReplayFrame frame in frames)
            {
                DodgeArenaState state = arenaEvaluator.Evaluate(frame.Time);
                Assert.That(
                    DodgePlayer.ClampToArena(frame.Position, state.Position, state.Size),
                    Is.EqualTo(frame.Position),
                    $"Frame outside arena at {frame.Time:N0} ms");
            }

            for (double time = emitter.StartTime; time <= emitter.EndTime; time += 5)
            {
                Vector2 playerPosition = positionAt(frames, time);

                for (int i = 0; i < emitter.EffectiveBulletCount; i++)
                {
                    Assert.That(
                        DodgeBullet.IntersectsPlayer(emitter.PositionAt(i, time), playerPosition, emitter.BulletSize),
                        Is.False,
                        $"Emitter ray {i} collided at {time:N0} ms");
                }
            }
        }

        [Test]
        public void TestAutoplayUsesWaveTrajectory()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(0, 112),
                EndPosition = new Vector2(512, 112),
                MovementType = DodgeMovementType.Sine,
                WaveAmplitude = 80,
                WaveCycles = 1,
            };
            Beatmap<DodgeHitObject> beatmap = createBeatmap(bullet);
            List<DodgeReplayFrame> frames = generateFrames(beatmap);

            for (double time = bullet.StartTime; time <= bullet.EndTime; time += 5)
            {
                Assert.That(
                    DodgeBullet.IntersectsPlayer(bullet.PositionAt(time), positionAt(frames, time), bullet.BulletSize),
                    Is.False,
                    $"Wave collision at {time:N0} ms");
            }
        }

        [Test]
        public void TestAutoplayPlansAheadForDistantOpening()
        {
            var bullets = new List<DodgeHitObject>();
            bullets.Add(new DodgeArenaChange
            {
                StartTime = 0,
                Duration = 0,
                TargetPosition = new Vector2(0, 128),
                TargetSize = new Vector2(DodgePlayfield.WIDTH, 128),
            });

            // A sweeping wall is open only on the far left. The opening is
            // farther away than the normal local lookahead, so the planner must
            // retain or recover an alternate route instead of teleporting.
            for (float x = 48; x <= DodgePlayfield.WIDTH; x += 24)
            {
                bullets.Add(new DodgeBullet
                {
                    StartTime = 2000,
                    Duration = 600,
                    Position = new Vector2(x, 96),
                    EndPosition = new Vector2(x, 288),
                });
            }

            Beatmap<DodgeHitObject> beatmap = createBeatmap(bullets.ToArray());
            List<DodgeReplayFrame> frames = generateFrames(beatmap);
            float movementSpeed = (float)(DodgeBeatmapSettings.GetPlayerSpeed(beatmap.Difficulty) / 1000);

            Assert.That(positionAt(frames, 2000).X, Is.LessThan(40), "Autoplay did not reach the distant opening in advance");

            for (int i = 1; i < frames.Count; i++)
            {
                double elapsed = frames[i].Time - frames[i - 1].Time;
                float distance = (frames[i].Position - frames[i - 1].Position).Length;
                Assert.That(distance, Is.LessThanOrEqualTo(movementSpeed * elapsed + 0.1f), $"Excess speed at {frames[i].Time:N0} ms");
            }

            for (double time = 2000; time <= 2600; time += 5)
            {
                Vector2 playerPosition = positionAt(frames, time);

                foreach (DodgeBullet bullet in bullets.OfType<DodgeBullet>())
                {
                    Assert.That(
                        DodgeBullet.IntersectsPlayer(bullet.PositionAt(time), playerPosition, bullet.BulletSize),
                        Is.False,
                        $"Sweeping wall collided at {time:N0} ms");
                }
            }
        }

        [Test]
        public void TestAutoplayIncludesMovingEmitterBurstsAndExposesRoute()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 600,
                Position = new Vector2(40, 80),
                AimPosition = new Vector2(472, 80),
                MovementEndPosition = new Vector2(40, 304),
                BulletCount = 3,
                SpreadAngle = 20,
                BurstCount = 4,
                BurstInterval = 200,
                BurstBeatDivisor = 0,
                MoveSource = true,
            };
            Beatmap<DodgeHitObject> beatmap = createBeatmap(emitter);
            var generator = new DodgeAutoGenerator(beatmap);
            generator.Generate();

            Assert.Multiple(() =>
            {
                Assert.That(generator.Route, Has.Count.GreaterThan(2));
                Assert.That(generator.Analysis.ProjectileCount, Is.EqualTo(12));
                Assert.That(generator.Route.Select(point => point.Time), Is.Ordered);
            });
        }

        [Test]
        public void TestAutoplayAvoidsBeam()
        {
            var beam = new DodgeBeam
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(200, 192),
                EndPosition = new Vector2(312, 192),
                BeamWidth = 80,
            };
            Beatmap<DodgeHitObject> beatmap = createBeatmap(beam);
            List<DodgeReplayFrame> frames = generateFrames(beatmap);
            float playerSize = DodgeBeatmapSettings.GetPlayerSize(beatmap.Difficulty);

            for (double time = beam.StartTime; time <= beam.EndTime; time += 5)
            {
                Assert.That(
                    DodgeBeam.IntersectsPlayer(
                        beam.BeamCenter,
                        beam.BeamDirection,
                        beam.PerpendicularDirection,
                        beam.BeamLength,
                        beam.BeamWidth,
                        positionAt(frames, time),
                        playerSize),
                    Is.False,
                    $"Beam collision at {time:N0} ms");
            }
        }

        [Test]
        public void TestAutoplayUsesCameraAwareEmitterLifetime()
        {
            var cameraChange = new DodgeCameraChange
            {
                StartTime = 1000,
                Position = Vector2.Zero,
                EndPosition = new Vector2(64, 0),
                Continuous = true,
            };
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(192, 192),
                BulletCount = 2,
                SpreadAngle = 0,
                ContinueUntilExit = true,
            };
            var timelineExtender = new DodgeBullet
            {
                StartTime = 10000,
                Duration = 0,
                Position = new Vector2(-100),
                EndPosition = new Vector2(-100),
            };
            Beatmap<DodgeHitObject> beatmap = createBeatmap(cameraChange, emitter, timelineExtender);
            List<DodgeReplayFrame> frames = generateFrames(beatmap);
            var camera = new DodgeCameraStateEvaluator(new[] { cameraChange });
            double continuedEndTime = DodgeGameplayTiming.GetContinuedProjectileEndTime(
                beatmap.HitObjects,
                DodgePlayfield.CONTINUED_BULLET_GRACE_PERIOD);
            double endTime = DodgeTrajectory.CalculateExitTimeWithCamera(
                emitter.StartTime,
                emitter.Duration,
                emitter.Position,
                emitter.EndPositionAt(0),
                emitter.BulletSize,
                emitter.MovementType,
                emitter.WaveAmplitude,
                emitter.WaveCycles,
                emitter.WavePhase,
                camera.Evaluate,
                continuedEndTime);
            Vector2 cameraAnchor = camera.Evaluate(emitter.StartTime);

            Assert.That(endTime, Is.GreaterThan(emitter.ExitTimeAt(0)));

            for (double time = emitter.StartTime; time <= endTime; time += 5)
            {
                float progress = (float)((time - emitter.StartTime) / emitter.Duration);
                Vector2 bulletPosition = DodgeTrajectory.PositionAtProgress(
                                             emitter.Position,
                                             emitter.EndPositionAt(0),
                                             progress,
                                             emitter.MovementType,
                                             emitter.WaveAmplitude,
                                             emitter.WaveCycles,
                                             emitter.WavePhase)
                                         + camera.Evaluate(time)
                                         - cameraAnchor;

                Assert.That(
                    DodgeBullet.IntersectsPlayer(bulletPosition, positionAt(frames, time), emitter.BulletSize),
                    Is.False,
                    $"Camera-carried emitter bullet collided at {time:N0} ms");
            }
        }

        [Test]
        public void TestCameraScrollCarriesAutoplayPlayer()
        {
            var cameraChange = new DodgeCameraChange
            {
                StartTime = 0,
                Duration = 1000,
                Position = Vector2.Zero,
                EndPosition = new Vector2(64, 0),
            };
            Beatmap<DodgeHitObject> beatmap = createBeatmap(cameraChange);
            List<DodgeReplayFrame> frames = generateFrames(beatmap);
            var camera = new DodgeCameraStateEvaluator(new[] { cameraChange });
            float movementSpeed = (float)(DodgeBeatmapSettings.GetPlayerSpeed(beatmap.Difficulty) / 1000);

            Assert.That(positionAt(frames, cameraChange.EndTime).X, Is.GreaterThan(positionAt(frames, cameraChange.StartTime).X + 32));

            for (int i = 1; i < frames.Count; i++)
            {
                double elapsed = frames[i].Time - frames[i - 1].Time;
                Vector2 cameraDelta = camera.Evaluate(frames[i].Time) - camera.Evaluate(frames[i - 1].Time);
                Vector2 cameraCarriedPosition = frames[i - 1].Position + cameraDelta;
                float controlledDistance = (frames[i].Position - cameraCarriedPosition).Length;

                Assert.That(controlledDistance, Is.LessThanOrEqualTo(movementSpeed * elapsed + 0.1f));
            }
        }

        [Test]
        public void TestAutoplayHandlesRotatingEasedEmitter()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1200,
                Position = new Vector2(256, 24),
                AimPosition = new Vector2(256, 240),
                BulletCount = 5,
                SpreadAngle = 70,
                BurstCount = 6,
                BurstInterval = 180,
                BurstRotation = 31,
                MovementEasing = DodgeMovementEasing.EaseInOut,
            };
            Beatmap<DodgeHitObject> beatmap = createBeatmap(emitter);
            var generator = new DodgeAutoGenerator(beatmap);

            generator.Generate();
            List<DodgeReplayFrame> frames = generateFrames(beatmap);
            float movementSpeed = (float)(DodgeBeatmapSettings.GetPlayerSpeed(beatmap.Difficulty) / 1000);

            Assert.That(generator.Analysis.ProjectileCount, Is.EqualTo(30));
            Assert.That(generator.Route, Has.Count.GreaterThan(2));
            Assert.That(frames, Has.Count.GreaterThan(2));
            Assert.That(frames.All(frame => float.IsFinite(frame.Position.X) && float.IsFinite(frame.Position.Y)), Is.True);

            for (int i = 1; i < frames.Count; i++)
            {
                double elapsed = frames[i].Time - frames[i - 1].Time;
                Assert.That((frames[i].Position - frames[i - 1].Position).Length,
                    Is.LessThanOrEqualTo(movementSpeed * elapsed + 0.1f));
            }
        }

        [Test]
        public void TestAutoplayStaysInsideRotatedArena()
        {
            var arena = new DodgeArenaChange
            {
                StartTime = 0,
                Duration = 0,
                TargetPosition = new Vector2(106, 152),
                TargetSize = new Vector2(300, 80),
                TargetRotation = 45,
            };
            var blockingCentre = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1200,
                Position = new Vector2(256, 192),
                EndPosition = new Vector2(256, 192),
            };
            Beatmap<DodgeHitObject> beatmap = createBeatmap(arena, blockingCentre);
            List<DodgeReplayFrame> frames = generateFrames(beatmap);
            var evaluator = new DodgeArenaStateEvaluator(new[] { arena });
            float playerSize = DodgeBeatmapSettings.GetPlayerSize(beatmap.Difficulty);

            foreach (DodgeReplayFrame frame in frames)
            {
                DodgeArenaState state = evaluator.Evaluate(frame.Time);
                Vector2 clamped = DodgePlayer.ClampToArena(
                    frame.Position,
                    state.Position,
                    state.Size,
                    playerSize,
                    state.Rotation);

                Assert.That((clamped - frame.Position).Length, Is.LessThan(0.1f), $"Frame outside rotated arena at {frame.Time:N0} ms");
            }
        }

        [Test]
        public void TestAutoplayRouteIsDeterministic()
        {
            Beatmap<DodgeHitObject> beatmap = createBeatmap(
                new DodgeEmitter
                {
                    StartTime = 1000,
                    Duration = 600,
                    Position = new Vector2(256, 32),
                    AimPosition = new Vector2(256, 300),
                    BulletCount = 5,
                    SpreadAngle = 90,
                    BurstCount = 3,
                    BurstInterval = 150,
                    BurstRotation = 17,
                },
                new DodgeBeam
                {
                    StartTime = 1300,
                    Duration = 300,
                    Position = new Vector2(80, 320),
                    EndPosition = new Vector2(432, 320),
                    BeamWidth = 32,
                });
            var first = new DodgeAutoGenerator(beatmap);
            var second = new DodgeAutoGenerator(beatmap);

            first.Generate();
            second.Generate();

            Assert.Multiple(() =>
            {
                Assert.That(second.Route, Is.EqualTo(first.Route));
                Assert.That(second.Analysis, Is.EqualTo(first.Analysis));
            });
        }

        [Test]
        public void TestAutoplayUsesGameplayHitboxInTightArena()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(10, 192),
                EndPosition = new Vector2(10, 192),
            };
            Beatmap<DodgeHitObject> beatmap = createBeatmap(
                new DodgeArenaChange
                {
                    StartTime = 0,
                    TargetPosition = new Vector2(0, 176),
                    TargetSize = new Vector2(DodgeArenaChange.MIN_SIZE),
                },
                bullet);
            beatmap.Difficulty.CircleSize = DodgeBeatmapSettings.GetCircleSize(10);
            bullet.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

            var generator = new DodgeAutoGenerator(beatmap);
            generator.Generate();
            var paddedGenerator = new DodgeAutoGenerator(beatmap, collisionSafetyMargin: 1);
            paddedGenerator.Generate();

            Assert.Multiple(() =>
            {
                Assert.That(generator.Analysis.TeleportCount, Is.Zero);
                Assert.That(generator.Analysis.CollisionCount, Is.Zero);
                Assert.That(paddedGenerator.Analysis.CollisionCount, Is.GreaterThan(0));
                Assert.That(DodgeBullet.IntersectsPlayer(
                    bullet.Position,
                    new Vector2(24, 192),
                    bullet.BulletSize,
                    DodgeBeatmapSettings.GetPlayerSize(beatmap.Difficulty)), Is.False);
            });
        }

        [Test]
        public void TestAutoplayReplayPreservesPlannedRoute()
        {
            Beatmap<DodgeHitObject> beatmap = createBeatmap(new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1200,
                Position = new Vector2(0, 192),
                EndPosition = new Vector2(512, 192),
            });
            var generator = new DodgeAutoGenerator(beatmap);
            List<DodgeReplayFrame> frames = generator.Generate().Frames.Cast<DodgeReplayFrame>().ToList();

            Assert.That(frames, Has.Count.EqualTo(generator.Route.Count + 1));

            for (int i = 0; i < generator.Route.Count; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(frames[i].Time, Is.EqualTo(generator.Route[i].Time));
                    Assert.That(frames[i].Position, Is.EqualTo(generator.Route[i].Position));
                });
            }
        }

        private static Beatmap<DodgeHitObject> createBeatmap(params DodgeHitObject[] hitObjects)
        {
            var beatmap = new Beatmap<DodgeHitObject>();
            beatmap.HitObjects.AddRange(hitObjects);
            return beatmap;
        }

        private static List<DodgeReplayFrame> generateFrames(IBeatmap beatmap)
            => new DodgeModAutoplay().CreateReplayData(beatmap, Array.Empty<Mod>())
                                     .Replay.Frames.Cast<DodgeReplayFrame>()
                                     .OrderBy(frame => frame.Time)
                                     .ToList();

        private static Vector2 positionAt(IReadOnlyList<DodgeReplayFrame> frames, double time)
        {
            if (time <= frames[0].Time)
                return frames[0].Position;

            for (int i = 1; i < frames.Count; i++)
            {
                if (time > frames[i].Time)
                    continue;

                DodgeReplayFrame previous = frames[i - 1];
                DodgeReplayFrame next = frames[i];
                float progress = (float)((time - previous.Time) / Math.Max(0.001, next.Time - previous.Time));
                return Vector2.Lerp(previous.Position, next.Position, progress);
            }

            return frames[^1].Position;
        }
    }
}
