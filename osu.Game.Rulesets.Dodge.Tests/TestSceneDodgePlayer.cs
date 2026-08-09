// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Objects.Drawables;
using osu.Game.Rulesets.Dodge.Mods;
using osu.Game.Rulesets.Dodge.Skinning;
using osu.Game.Rulesets.Dodge.Skinning.Components;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public partial class TestSceneDodgePlayer : OsuManualInputManagerTestScene
    {
        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        [Test]
        public void TestArenaAndPlayerInput()
        {
            DrawableDodgeRuleset drawableRuleset = null!;
            Vector2 initialPosition = Vector2.Zero;

            AddStep("create dodge ruleset", () =>
            {
                Child = drawableRuleset = new DrawableDodgeRuleset(
                    new DodgeRuleset(),
                    new Beatmap<DodgeHitObject>(),
                    []);
            });
            AddUntilStep("wait for ruleset load", () => drawableRuleset.IsLoaded && drawableRuleset.Playfield.Player.IsLoaded);
            AddAssert("arena uses osu dimensions", () => DodgePlayfield.BASE_SIZE, () => Is.EqualTo(new Vector2(512, 384)));
            AddAssert("player is square", () => drawableRuleset.Playfield.Player.Size, () => Is.EqualTo(new Vector2(DodgePlayer.SIZE)));
            AddAssert("optional trail is absent by default", () => drawableRuleset.Playfield.Player.Trail.HasVisual, () => Is.False);
            AddAssert("optional graze effect is absent by default", () => drawableRuleset.Playfield.Player.GrazeEffect.HasVisual, () => Is.False);
            AddAssert("optional collision effect is absent by default", () => drawableRuleset.Playfield.Player.CollisionEffect.HasVisual, () => Is.False);
            AddStep("store initial position", () => initialPosition = drawableRuleset.Playfield.Player.Position);
            AddStep("press D", () => InputManager.PressKey(Key.D));
            AddUntilStep("player moves right", () => drawableRuleset.Playfield.Player.X > initialPosition.X);
            AddStep("release D", () => InputManager.ReleaseKey(Key.D));
            AddWaitStep("allow trail sample interval", 3);
            AddAssert("missing trail emits nothing", () => drawableRuleset.Playfield.Player.Trail.EmittedSegmentCount, () => Is.Zero);
            AddStep("press shift", () => InputManager.PressKey(Key.LShift));
            AddUntilStep("slow mode activates", () => drawableRuleset.Playfield.Player.SlowActive);
            AddStep("release shift", () => InputManager.ReleaseKey(Key.LShift));
            AddUntilStep("slow mode deactivates", () => !drawableRuleset.Playfield.Player.SlowActive);
        }

        [Test]
        public void TestConfiguredPlayerSize()
        {
            DrawableDodgeRuleset drawableRuleset = null!;

            AddStep("create ruleset with large player", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>();
                DodgeBeatmapSettings.SetPlayerSize(beatmap.Difficulty, 28);
                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, []);
            });
            AddUntilStep("wait for ruleset load", () => drawableRuleset.IsLoaded && drawableRuleset.Playfield.Player.IsLoaded);
            AddAssert("configured size applied", () => drawableRuleset.Playfield.Player.Size, () => Is.EqualTo(new Vector2(28)));
        }

        [Test]
        public void TestDodgeTextureResourcesReachPlayerRuntimeVisuals()
        {
            DodgePlayer player = null!;

            AddStep("create textured player", () =>
            {
                Child = new SkinProvidingContainer(new DodgeSkinTransformer(new DodgeTextureSkin(renderer)))
                {
                    Child = player = new DodgePlayer(),
                };
            });
            AddUntilStep("wait for textured player", () => player.IsLoaded
                                                            && player.Trail.HasVisual
                                                            && player.GrazeEffect.HasVisual
                                                            && player.CollisionEffect.HasVisual);
            AddAssert("player, trail and effects use textures", () =>
            {
                var drawables = player.ChildrenOfType<SkinnableDrawable>().ToArray();
                return drawables.Length == DodgePlayerTrail.MAX_SEGMENTS + 3
                       && drawables.All(drawable => drawable.Drawable is Sprite);
            });
            AddStep("play optional textures", () =>
            {
                player.Trail.Reset(player.Position, 0);
                player.Trail.UpdatePosition(player.Position + new Vector2(8, 0), DodgePlayerTrail.SAMPLE_INTERVAL);
                player.TriggerMissFlash();
                player.TriggerGrazeEffect();
            });
            AddAssert("trail texture emitted", () => player.Trail.EmittedSegmentCount, () => Is.EqualTo(1));
            AddAssert("collision texture played", () => player.CollisionEffect.PlayCount, () => Is.EqualTo(1));
            AddAssert("graze texture played", () => player.GrazeEffect.PlayCount, () => Is.EqualTo(1));
            AddAssert("optional textures leave hitbox unchanged", () => player.PlayerSize, () => Is.EqualTo(DodgePlayer.SIZE));
        }

        [Test]
        public void TestFullPathsModEnablesTrajectoryGuides()
        {
            DrawableDodgeRuleset drawableRuleset = null!;

            AddStep("create ruleset with full paths", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>
                {
                    HitObjects =
                    {
                        new DodgeBullet
                        {
                            StartTime = 0,
                            Duration = 2000,
                            Position = new Vector2(64, 192),
                            EndPosition = new Vector2(448, 192),
                        },
                    },
                };
                Child = drawableRuleset = new DrawableDodgeRuleset(
                    new DodgeRuleset(),
                    beatmap,
                    [new DodgeModFullPaths()]);
            });
            AddUntilStep("wait for ruleset load", () => drawableRuleset.IsLoaded);
            AddAssert("full projectile paths enabled", () => drawableRuleset.Playfield.ShowFullProjectilePaths);
            AddUntilStep("trajectory path created", () => drawableRuleset.ChildrenOfType<DrawableDodgeHitObject>()
                                                                          .SingleOrDefault()?.TrajectoryGuideCount == 1);
        }

        [Test]
        public void TestFullPathIsConsumedBehindProjectile()
        {
            DrawableDodgeRuleset drawableRuleset = null!;
            DrawableDodgeHitObject drawableBullet = null!;
            DrawableDodgeEmitter drawableEmitter = null!;
            ManualClock manualClock = null!;
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(100, 64),
                EndPosition = new Vector2(300, 64),
                ContinueUntilExit = true,
                TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.FullPath,
            };
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(100, 128),
                AimPosition = new Vector2(300, 128),
                BulletCount = 1,
                ContinueUntilExit = true,
                TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.FullPath,
            };

            AddStep("create full-path projectiles", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>
                {
                    HitObjects = { bullet, emitter },
                };
                manualClock = new ManualClock { CurrentTime = 500 };
                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, [])
                {
                    Clock = new FramedClock(manualClock),
                };
            });
            AddUntilStep("full paths loaded", () =>
            {
                drawableBullet = drawableRuleset.ChildrenOfType<DrawableDodgeHitObject>().SingleOrDefault()!;
                drawableEmitter = drawableRuleset.ChildrenOfType<DrawableDodgeEmitter>().SingleOrDefault()!;
                return drawableBullet?.TrajectoryGuideStartPosition != null
                       && drawableEmitter?.FirstTrajectoryGuideStartPosition != null;
            });
            AddAssert("bullet path initially starts at spawn", () =>
                Precision.AlmostEquals(drawableBullet.TrajectoryGuideStartPosition!.Value, bullet.Position, 0.001f));
            AddAssert("emitter path initially starts at spawn", () =>
                Precision.AlmostEquals(drawableEmitter.FirstTrajectoryGuideStartPosition!.Value, emitter.Position, 0.001f));
            AddAssert("bullet path reaches exit", () =>
                Precision.AlmostEquals(drawableBullet.TrajectoryGuideEndPosition!.Value, bullet.TrajectoryEndPosition, 0.001f));

            AddStep("advance projectiles halfway", () => manualClock.CurrentTime = 1500);
            AddUntilStep("bullet consumed travelled path", () =>
                Precision.AlmostEquals(drawableBullet.TrajectoryGuideStartPosition!.Value, bullet.PositionAt(1500), 0.001f));
            AddUntilStep("emitter consumed travelled path", () =>
                Precision.AlmostEquals(drawableEmitter.FirstTrajectoryGuideStartPosition!.Value, emitter.PositionAt(0, 1500), 0.001f));
            AddAssert("remaining bullet path still reaches exit", () =>
                Precision.AlmostEquals(drawableBullet.TrajectoryGuideEndPosition!.Value, bullet.TrajectoryEndPosition, 0.001f));
        }

        [Test]
        public void TestEmitterGeometryIsCachedUntilObjectChanges()
        {
            DrawableDodgeRuleset drawableRuleset = null!;
            DrawableDodgeEmitter drawableEmitter = null!;
            DodgeEmitter emitter = null!;
            int rebuildCount = 0;

            AddStep("create emitter", () =>
            {
                emitter = new DodgeEmitter
                {
                    StartTime = 1000,
                    Duration = 2000,
                    Position = new Vector2(32, 32),
                    AimPosition = new Vector2(192, 32),
                    BulletCount = DodgeEmitter.MAX_BULLET_COUNT,
                    SpreadAngle = 0,
                    BurstCount = DodgeEmitter.MAX_BURST_COUNT,
                    BurstBeatDivisor = 0,
                    TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.Arrow,
                    ContinueUntilExit = true,
                };
                var beatmap = new Beatmap<DodgeHitObject>
                {
                    HitObjects = { emitter },
                };
                var manualClock = new ManualClock { CurrentTime = 500 };
                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, [])
                {
                    Clock = new FramedClock(manualClock),
                };
            });
            AddUntilStep("wait for emitter cache", () =>
            {
                DrawableDodgeEmitter? found = drawableRuleset.ChildrenOfType<DrawableDodgeEmitter>().SingleOrDefault();

                if (found == null)
                    return false;

                drawableEmitter = found;
                return drawableEmitter.GeometryCacheRebuildCount > 0;
            });
            AddStep("store cache rebuild count", () => rebuildCount = drawableEmitter.GeometryCacheRebuildCount);
            AddWaitStep("run unchanged frames", 5);
            AddAssert("geometry was not rebuilt", () => drawableEmitter.GeometryCacheRebuildCount, () => Is.EqualTo(rebuildCount));
            AddAssert("normal play has no guide drawables", () => drawableEmitter.TrajectoryGuideCount, () => Is.Zero);
            AddStep("enable map-authored paths", () => emitter.TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.Path);
            AddUntilStep("map paths reuse one guide per fan ray", () => drawableEmitter.TrajectoryGuideCount == emitter.EffectiveBulletCount);
            AddAssert("authored path stops at configured duration", () =>
                Precision.AlmostEquals(drawableEmitter.FirstTrajectoryGuideEndPosition!.Value, emitter.EndPositionAt(0), 0.001f));
            AddStep("enable map-authored full paths", () => emitter.TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.FullPath);
            AddUntilStep("authored full path reaches playfield exit", () =>
                !Precision.AlmostEquals(drawableEmitter.FirstTrajectoryGuideEndPosition!.Value, emitter.EndPositionAt(0), 0.001f));
            AddStep("hide map-authored paths", () => emitter.TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.Hidden);
            AddUntilStep("map guides are released", () => drawableEmitter.TrajectoryGuideCount == 0);
            AddStep("store post-guide cache count", () => rebuildCount = drawableEmitter.GeometryCacheRebuildCount);
            AddStep("change emitter direction", () => emitter.AimPosition += new Vector2(0, 64));
            AddUntilStep("geometry cache rebuilt once", () => drawableEmitter.GeometryCacheRebuildCount == rebuildCount + 1);
            AddStep("enable full paths", () => drawableEmitter.ShowFullTrajectories = true);
            AddUntilStep("full paths reuse one guide per fan ray", () => drawableEmitter.TrajectoryGuideCount == emitter.EffectiveBulletCount);
            AddAssert("full path continues to playfield exit", () =>
                !Precision.AlmostEquals(drawableEmitter.FirstTrajectoryGuideEndPosition!.Value, emitter.EndPositionAt(0), 0.001f));
            AddStep("disable full paths", () => drawableEmitter.ShowFullTrajectories = false);
            AddUntilStep("guide drawables are released", () => drawableEmitter.TrajectoryGuideCount == 0);
        }

        [Test]
        public void TestEmitterOnlyKeepsCurrentBurstVisualsAlive()
        {
            DrawableDodgeRuleset drawableRuleset = null!;
            DrawableDodgeEmitter drawableEmitter = null!;
            ManualClock manualClock = null!;
            int batchCapacity = 0;

            AddStep("create separated bursts", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>
                {
                    HitObjects =
                    {
                        new DodgeEmitter
                        {
                            StartTime = 1000,
                            Duration = 500,
                            Position = new Vector2(32, 32),
                            AimPosition = new Vector2(192, 32),
                            BulletCount = 3,
                            BurstCount = 2,
                            BurstInterval = 2000,
                            BurstBeatDivisor = 0,
                        },
                    },
                };
                manualClock = new ManualClock { CurrentTime = 1000 };
                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, [])
                {
                    Clock = new FramedClock(manualClock),
                };
            });
            AddUntilStep("find emitter", () =>
            {
                drawableEmitter = drawableRuleset.ChildrenOfType<DrawableDodgeEmitter>().SingleOrDefault()!;
                return drawableEmitter != null;
            });
            AddUntilStep("only first burst alive", () => drawableEmitter.AliveBulletVisualCount + drawableEmitter.BatchedBulletVisualCount == 3);
            AddStep("store batch capacity", () => batchCapacity = drawableEmitter.BatchedBulletStorageCapacity);
            AddAssert("only active burst was simulated", () => drawableEmitter.LastFrameSimulatedBulletCount, () => Is.EqualTo(3));
            AddStep("seek between bursts", () => manualClock.CurrentTime = 2000);
            AddUntilStep("no bullet visuals alive", () => drawableEmitter.AliveBulletVisualCount + drawableEmitter.BatchedBulletVisualCount == 0);
            AddAssert("inactive gap has no simulation", () => drawableEmitter.ActiveSimulationCount, () => Is.Zero);
            AddStep("seek to second burst", () => manualClock.CurrentTime = 3000);
            AddUntilStep("only second burst alive", () => drawableEmitter.AliveBulletVisualCount + drawableEmitter.BatchedBulletVisualCount == 3);
            AddAssert("batch storage is reused by overlap", () => drawableEmitter.BatchedBulletStorageCapacity, () => Is.EqualTo(batchCapacity));
            AddStep("rewind to first burst", () => manualClock.CurrentTime = 1000);
            AddUntilStep("first burst restored after rewind", () => drawableEmitter.AliveBulletVisualCount + drawableEmitter.BatchedBulletVisualCount == 3);
            AddAssert("rewind reused batch storage", () => drawableEmitter.BatchedBulletStorageCapacity, () => Is.EqualTo(batchCapacity));
        }

        [Test]
        public void TestMaximumEmitterSimulatesOnlyActiveBurst()
        {
            DrawableDodgeRuleset drawableRuleset = null!;
            DrawableDodgeEmitter drawableEmitter = null!;

            AddStep("create maximum separated emitter", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>
                {
                    HitObjects =
                    {
                        new DodgeEmitter
                        {
                            StartTime = 1000,
                            Duration = 500,
                            Position = new Vector2(32, 32),
                            AimPosition = new Vector2(192, 32),
                            BulletCount = DodgeEmitter.MAX_BULLET_COUNT,
                            BurstCount = DodgeEmitter.MAX_BURST_COUNT,
                            BurstInterval = 2000,
                            BurstBeatDivisor = 0,
                        },
                    },
                };
                var manualClock = new ManualClock { CurrentTime = 1000 };
                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, [])
                {
                    Clock = new FramedClock(manualClock),
                };
            });
            AddUntilStep("find maximum emitter", () =>
            {
                drawableEmitter = drawableRuleset.ChildrenOfType<DrawableDodgeEmitter>().SingleOrDefault()!;
                return drawableEmitter != null;
            });
            AddUntilStep("one burst is alive", () => drawableEmitter.AliveBulletVisualCount + drawableEmitter.BatchedBulletVisualCount == DodgeEmitter.MAX_BULLET_COUNT);
            AddAssert("active-set excludes future bursts", () => drawableEmitter.ActiveSimulationCount, () => Is.EqualTo(DodgeEmitter.MAX_BULLET_COUNT));
            AddAssert("batch storage excludes future bursts", () => drawableEmitter.BatchedBulletStorageCapacity, () => Is.EqualTo(DodgeEmitter.MAX_BULLET_COUNT));
        }

        [Test]
        public void TestMaximumLinearFullPathEmitterKeepsCheapSteadyState()
        {
            DrawableDodgeRuleset drawableRuleset = null!;
            DrawableDodgeEmitter drawableEmitter = null!;
            ManualClock manualClock = null!;
            int geometryRebuildCount = 0;

            AddStep("create maximum full-path emitter", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>
                {
                    HitObjects =
                    {
                        new DodgeEmitter
                        {
                            StartTime = 1000,
                            Duration = 1000,
                            Position = new Vector2(64, 192),
                            AimPosition = new Vector2(448, 192),
                            BulletCount = DodgeEmitter.MAX_BULLET_COUNT,
                            BurstCount = DodgeEmitter.MAX_BURST_COUNT,
                            BurstInterval = 2000,
                            BurstBeatDivisor = 0,
                            ContinueUntilExit = true,
                            TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.FullPath,
                        },
                    },
                };
                manualClock = new ManualClock { CurrentTime = 1000 };
                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, [])
                {
                    Clock = new FramedClock(manualClock),
                };
            });
            AddUntilStep("maximum emitter loaded", () =>
            {
                drawableEmitter = drawableRuleset.ChildrenOfType<DrawableDodgeEmitter>().SingleOrDefault()!;
                return drawableEmitter?.TrajectoryGuideCount == DodgeEmitter.MAX_BULLET_COUNT
                       && drawableEmitter.ActiveSimulationCount == DodgeEmitter.MAX_BULLET_COUNT;
            });
            AddStep("store geometry rebuild count", () => geometryRebuildCount = drawableEmitter.GeometryCacheRebuildCount);
            AddRepeatStep("simulate 240 gameplay frames", () => manualClock.CurrentTime++, 240);
            AddAssert("emitter geometry remained cached", () => drawableEmitter.GeometryCacheRebuildCount, () => Is.EqualTo(geometryRebuildCount));
            AddAssert("linear guides never used buffered paths", () => drawableEmitter.TrajectoryGuideBufferedGeometryRebuildCount, () => Is.Zero);
            AddAssert("guide lookup checks only current burst", () => drawableEmitter.LastFrameTrajectoryGuideCandidateChecks,
                () => Is.LessThanOrEqualTo(DodgeEmitter.MAX_BULLET_COUNT));
            AddAssert("only active burst was simulated", () => drawableEmitter.LastFrameSimulatedBulletCount, () => Is.EqualTo(DodgeEmitter.MAX_BULLET_COUNT));
            AddAssert("circular linear bullets skip direction updates", () => drawableEmitter.LastFrameDynamicDirectionUpdateCount, () => Is.Zero);
            AddAssert("future bursts did not allocate batch storage", () => drawableEmitter.BatchedBulletStorageCapacity, () => Is.EqualTo(DodgeEmitter.MAX_BULLET_COUNT));
        }

        [Test]
        public void TestCollisionPassSkipsUpcomingLoadedObjects()
        {
            const int upcoming_count = 64;
            DrawableDodgeRuleset drawableRuleset = null!;
            ManualClock manualClock = null!;

            AddStep("create active and upcoming bullets", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>();
                beatmap.HitObjects.Add(new DodgeBullet
                {
                    StartTime = 1000,
                    Duration = 1000,
                    Position = new Vector2(32, 32),
                    EndPosition = new Vector2(480, 32),
                });

                for (int i = 0; i < upcoming_count; i++)
                {
                    beatmap.HitObjects.Add(new DodgeBullet
                    {
                        StartTime = 1500,
                        Duration = 1000,
                        Position = new Vector2(32, 64 + i % 8 * 24),
                        EndPosition = new Vector2(480, 64 + i % 8 * 24),
                    });
                }

                manualClock = new ManualClock { CurrentTime = 1000 };
                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, [])
                {
                    Clock = new FramedClock(manualClock),
                };
            });
            AddUntilStep("upcoming drawables loaded", () =>
                drawableRuleset.Playfield.RegisteredCollisionSourceCount == upcoming_count + 1);
            AddUntilStep("only active collision source processed", () =>
                drawableRuleset.Playfield.LastFrameProcessedCollisionSourceCount == 1);
            AddAssert("upcoming sources excluded from active set", () => drawableRuleset.Playfield.ActiveCollisionSourceCount, () => Is.EqualTo(1));

            AddStep("rewind before all bullets", () => manualClock.CurrentTime = 900);
            AddUntilStep("rewind visits all sources once", () =>
                drawableRuleset.Playfield.LastRewindProcessedCollisionSourceCount == upcoming_count + 1);
            AddAssert("rewind target has no active sources", () => drawableRuleset.Playfield.ActiveCollisionSourceCount, () => Is.Zero);

            AddStep("return to first bullet", () => manualClock.CurrentTime = 1000);
            AddUntilStep("forward play returns to one source", () =>
                drawableRuleset.Playfield.LastFrameProcessedCollisionSourceCount == 1);
        }

        [Test]
        public void TestExpiredBulletsStayHiddenWhileRewinding()
        {
            DrawableDodgeRuleset drawableRuleset = null!;
            ManualClock manualClock = null!;

            AddStep("create completed bullets", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>();

                for (int i = 0; i < 8; i++)
                {
                    beatmap.HitObjects.Add(new DodgeBullet
                    {
                        StartTime = 1000 + i * 100,
                        Duration = 100,
                        Position = new Vector2(32, 32 + i * 24),
                        EndPosition = new Vector2(480, 32 + i * 24),
                    });
                }

                manualClock = new ManualClock { CurrentTime = 5000 };
                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, [])
                {
                    Clock = new FramedClock(manualClock),
                };
            });
            AddUntilStep("bullets initially completed", () =>
                drawableRuleset.Playfield.AllHitObjects.All(drawable => drawable.Result.HasResult));
            AddRepeatStep("rewind through expired region", () => manualClock.CurrentTime -= 16, 30);
            AddAssert("expired bullets re-completed", () =>
                drawableRuleset.Playfield.AllHitObjects.All(drawable => drawable.Result.HasResult));
            AddAssert("expired bullet bodies hidden", () =>
                drawableRuleset.ChildrenOfType<DodgeBulletVisual>().Count(bullet => bullet.Alpha > 0), () => Is.Zero);
        }

        [Test]
        public void TestFarEmitterBulletsAreRejectedBeforeTrajectoryTests()
        {
            DrawableDodgeRuleset drawableRuleset = null!;
            DrawableDodgeEmitter drawableEmitter = null!;
            ManualClock manualClock = null!;

            AddStep("create far maximum emitter", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>
                {
                    HitObjects =
                    {
                        new DodgeEmitter
                        {
                            StartTime = 1000,
                            Duration = 1000,
                            Position = new Vector2(64, 32),
                            AimPosition = new Vector2(448, 32),
                            BulletCount = DodgeEmitter.MAX_BULLET_COUNT,
                            SpreadAngle = 0,
                        },
                    },
                };
                manualClock = new ManualClock { CurrentTime = 1000 };
                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, [])
                {
                    Clock = new FramedClock(manualClock),
                };
            });
            AddUntilStep("far bullets active", () =>
            {
                drawableEmitter = drawableRuleset.ChildrenOfType<DrawableDodgeEmitter>().SingleOrDefault()!;
                return drawableEmitter?.ActiveSimulationCount == DodgeEmitter.MAX_BULLET_COUNT;
            });
            AddStep("advance one gameplay frame", () => manualClock.CurrentTime += 16);
            AddUntilStep("emitter broad phase skips trajectory tests", () =>
                drawableEmitter.LastFrameTrajectoryIntersectionTestCount == 0);
            AddAssert("far bullets remain visible away from player", () =>
                drawableEmitter.BatchedBulletVisualCount
                + drawableEmitter.ChildrenOfType<DodgeBulletVisual>().Count(bullet => bullet.Alpha > 0),
                () => Is.EqualTo(DodgeEmitter.MAX_BULLET_COUNT));
            AddAssert("bullet batch cannot be proximity-culled", () => drawableEmitter.BulletBatchAlwaysPresent);
            AddAssert("far batch keeps projectile position", () =>
                drawableEmitter.FirstBatchedBulletPosition is Vector2 position && position.X > 64 && position.Y == 32);
            AddAssert("far bullets do not collide", () => drawableRuleset.Playfield.AllHitObjects.Single().Result.HasResult, () => Is.False);
        }

        [Test]
        public void TestSuccessfulGrazeDoesNotCreateMissingOptionalEffect()
        {
            DodgePlayfield playfield = null!;

            AddStep("create graze-enabled playfield", () => Child = playfield = new DodgePlayfield(grazeDistance: 24));
            AddUntilStep("wait for playfield load", () => playfield.IsLoaded);
            AddStep("register successful graze", () => playfield.RegisterGraze(1000, true));
            AddAssert("missing graze effect was not shown", () => playfield.Player.GrazeEffect.PlayCount, () => Is.Zero);
            AddStep("register another graze at same time", () => playfield.RegisterGraze(1000, true));
            AddAssert("same-frame trigger remains invisible", () => playfield.Player.GrazeEffect.PlayCount, () => Is.Zero);
            AddStep("register graze at later time", () => playfield.RegisterGraze(1001, true));
            AddAssert("later missing effect remains invisible", () => playfield.Player.GrazeEffect.PlayCount, () => Is.Zero);
            AddAssert("graze trigger did not change hitbox", () => playfield.Player.PlayerSize, () => Is.EqualTo(DodgePlayer.SIZE));
        }

        [Test]
        public void TestBulletCollisionJudgesMiss()
        {
            DrawableDodgeRuleset drawableRuleset = null!;

            AddStep("create colliding bullet", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>
                {
                    HitObjects =
                    {
                        new DodgeBullet
                        {
                            StartTime = 0,
                            // Keep the object active regardless of elapsed test-scene time when
                            // this visual test runs as part of the complete fixture.
                            Duration = 1_000_000,
                            Position = DodgePlayfield.BASE_SIZE / 2,
                            EndPosition = DodgePlayfield.BASE_SIZE / 2,
                        },
                    },
                };

                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, []);
            });
            AddUntilStep("wait for bullet judgement", () => drawableRuleset.Playfield.AllHitObjects.Single().Result.HasResult);
            AddAssert("collision is a miss", () => drawableRuleset.Playfield.AllHitObjects.Single().Result.Type, () => Is.EqualTo(HitResult.Miss));
            AddAssert("miss feedback triggered once", () => drawableRuleset.Playfield.MissFeedbackCount, () => Is.EqualTo(1));
            AddAssert("miss sound triggered once", () => drawableRuleset.Playfield.MissSoundPlayCount, () => Is.EqualTo(1));
        }

        [Test]
        public void TestEmitterCollisionJudgesMiss()
        {
            DrawableDodgeRuleset drawableRuleset = null!;

            AddStep("create colliding emitter", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>
                {
                    HitObjects =
                    {
                        new DodgeEmitter
                        {
                            StartTime = 0,
                            Duration = 1_000_000,
                            Position = DodgePlayfield.BASE_SIZE / 2,
                            AimPosition = DodgePlayfield.BASE_SIZE / 2,
                            BulletCount = 5,
                            SpreadAngle = 360,
                        },
                    },
                };

                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, []);
            });
            AddUntilStep("wait for emitter judgement", () => drawableRuleset.Playfield.AllHitObjects.Single().Result.HasResult);
            AddAssert("emitter collision is a miss", () => drawableRuleset.Playfield.AllHitObjects.Single().Result.Type, () => Is.EqualTo(HitResult.Miss));
            AddUntilStep("judged emitter leaves collision active set", () => drawableRuleset.Playfield.ActiveCollisionSourceCount, () => Is.Zero);
            AddAssert("emitter miss feedback triggered once", () => drawableRuleset.Playfield.MissFeedbackCount, () => Is.EqualTo(1));
            AddAssert("emitter miss sound triggered once", () => drawableRuleset.Playfield.MissSoundPlayCount, () => Is.EqualTo(1));
        }

        [Test]
        public void TestMissSoundCanBeDisabledWithoutDisablingFlash()
        {
            DodgePlayfield playfield = null!;
            bool playerFlashShown = false;

            AddStep("create playfield with miss sound disabled", () => Child = playfield = new DodgePlayfield(missSoundEnabled: false));
            AddUntilStep("wait for playfield load", () => playfield.IsLoaded);
            AddStep("trigger miss feedback", () =>
            {
                playfield.TriggerMissFeedback();
                playerFlashShown = playfield.Player.MissFlashCount == 1;
            });
            AddAssert("visual feedback remains enabled", () => playfield.MissFeedbackCount, () => Is.EqualTo(1));
            AddAssert("player flash was shown", () => playerFlashShown);
            AddAssert("missing collision skin effect was not shown", () => playfield.Player.CollisionEffect.PlayCount, () => Is.Zero);
            AddAssert("collision trigger did not change hitbox", () => playfield.Player.PlayerSize, () => Is.EqualTo(DodgePlayer.SIZE));
            AddAssert("sound was not triggered", () => playfield.MissSoundPlayCount, () => Is.Zero);
        }

        [Test]
        public void TestEmitterMissOnlyRemovesCollidingBullet()
        {
            DrawableDodgeRuleset drawableRuleset = null!;
            ManualClock manualClock = null!;

            AddStep("create spreading emitter", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>
                {
                    HitObjects =
                    {
                        new DodgeEmitter
                        {
                            StartTime = 0,
                            Duration = 2000,
                            Position = new Vector2(256, 0),
                            AimPosition = new Vector2(256, 384),
                            BulletCount = 3,
                            SpreadAngle = 90,
                        },
                    },
                };

                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, [])
                {
                    Clock = new FramedClock(manualClock = new ManualClock()),
                };
            });
            AddStep("seek bullets to player", () => manualClock.CurrentTime = 1000);
            AddUntilStep("centre bullet collides", () => drawableRuleset.Playfield.AllHitObjects.Single().Result.Type == HitResult.Miss);
            AddAssert("other emitter bullets remain visible",
                () => drawableRuleset.ChildrenOfType<DrawableDodgeEmitter>().Sum(emitter => emitter.BatchedBulletVisualCount)
                      + drawableRuleset.ChildrenOfType<DodgeBulletVisual>().Count(bullet => bullet.Alpha > 0),
                () => Is.GreaterThanOrEqualTo(2));
        }

        [Test]
        public void TestArenaChangeIsJudgedAtStart()
        {
            DrawableDodgeRuleset drawableRuleset = null!;
            ManualClock manualClock = null!;

            AddStep("create long arena change", () =>
            {
                var beatmap = new Beatmap<DodgeHitObject>
                {
                    HitObjects =
                    {
                        new DodgeArenaChange
                        {
                            StartTime = 1000,
                            Duration = 10000,
                            TargetPosition = new Vector2(64, 48),
                            TargetSize = new Vector2(384, 288),
                        },
                    },
                };

                Child = drawableRuleset = new DrawableDodgeRuleset(new DodgeRuleset(), beatmap, [])
                {
                    Clock = new FramedClock(manualClock = new ManualClock()),
                };
            });
            AddUntilStep("wait for ruleset load", () => drawableRuleset.IsLoaded);
            AddStep("seek to arena start", () => manualClock.CurrentTime = 1000);
            AddUntilStep("arena judged at start", () => drawableRuleset.Playfield.AllHitObjects.Single().Result.HasResult);
            AddAssert("arena is still moving", () => manualClock.CurrentTime < 11000);
        }

        private sealed class DodgeTextureSkin : ISkin
        {
            private readonly IRenderer renderer;

            public DodgeTextureSkin(IRenderer renderer)
            {
                this.renderer = renderer;
            }

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
                => componentName.StartsWith("dodge-", System.StringComparison.Ordinal) ? renderer.WhitePixel : null;

            public ISample? GetSample(ISampleInfo sampleInfo) => null;

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
                => null;
        }

    }
}
