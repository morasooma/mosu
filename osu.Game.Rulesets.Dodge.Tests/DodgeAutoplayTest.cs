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
