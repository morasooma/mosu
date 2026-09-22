// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Edit.Setup;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Objects.Drawables;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgePlayerMovementTest
    {
        [TestCase(DodgeBulletShape.Triangle, 1, 0, 90)]
        [TestCase(DodgeBulletShape.Triangle, 0, 1, 180)]
        [TestCase(DodgeBulletShape.Triangle, -1, 0, 270)]
        [TestCase(DodgeBulletShape.Diamond, 1, 0, 0)]
        [TestCase(DodgeBulletShape.Diamond, 0, 1, 90)]
        [TestCase(DodgeBulletShape.Circle, 0, 1, 0)]
        public void TestProjectileShapeFacesMovementDirection(DodgeBulletShape shape, float x, float y, float expectedRotation)
        {
            Assert.That(
                DodgeBulletVisual.CalculateRotation(shape, new Vector2(x, y)),
                Is.EqualTo(expectedRotation).Within(0.001));
        }

        [Test]
        public void TestStandardBeatmapCannotConvertToDodge()
        {
            var source = new TestBeatmap(new RulesetInfo("osu", "osu!", string.Empty, 0));
            var converter = new DodgeBeatmapConverter(source, new DodgeRuleset());

            Assert.That(source.HitObjects, Is.Not.Empty);
            Assert.That(converter.CanConvert(), Is.False);
        }

        [Test]
        public void TestSongSelectDoesNotOfferStandardBeatmapAsDodgeConvert()
        {
            var osuRuleset = new RulesetInfo("osu", "osu!", string.Empty, 0);
            var taikoRuleset = new RulesetInfo("taiko", "osu!taiko", string.Empty, 1);
            var dodgeRuleset = new RulesetInfo(RulesetInfo.DODGE_MODE_SHORTNAME, "Dodge", string.Empty, DodgeRuleset.ONLINE_ID);
            var standardBeatmap = new BeatmapInfo(osuRuleset);

            Assert.Multiple(() =>
            {
                Assert.That(standardBeatmap.AllowGameplayWithRuleset(dodgeRuleset, true), Is.False);
                Assert.That(new BeatmapInfo(dodgeRuleset).AllowGameplayWithRuleset(dodgeRuleset, true), Is.True);
                Assert.That(standardBeatmap.AllowGameplayWithRuleset(taikoRuleset, true), Is.True);
                Assert.That(standardBeatmap.AllowGameplayWithRuleset(taikoRuleset, false), Is.False);
            });
        }

        [Test]
        public void TestRepeatedConversionCreatesFreshDodgeObjectsForRetry()
        {
            var source = new Beatmap<DodgeHitObject>();
            DodgeBeatmapSettings.SetPlayerSize(source.Difficulty, 24);
            source.HitObjects.Add(new DodgeBullet
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(100, 120),
                EndPosition = new Vector2(400, 280),
                Shape = DodgeBulletShape.Diamond,
                ContinueUntilExit = true,
                Colour = Colour4.FromHex("#44AAFF"),
                OutlineThickness = 2,
                MovementType = DodgeMovementType.Sine,
                WaveAmplitude = 44,
                WaveCycles = 3,
                WavePhase = 20,
                TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.Path,
            });
            source.HitObjects.Add(new DodgeEmitter
            {
                StartTime = 2000,
                Duration = 750,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(456, 192),
                MovementEndPosition = new Vector2(256, 320),
                BulletCount = 7,
                SpreadAngle = 180,
                BurstCount = 4,
                BurstInterval = 125,
                BurstBeatDivisor = 0,
                MoveSource = true,
                Shape = DodgeBulletShape.Triangle,
                ContinueUntilExit = true,
                Colour = Colour4.FromHex("#FF5577"),
                MovementType = DodgeMovementType.Sine,
                WaveAmplitude = -28,
                WaveCycles = 4,
                WavePhase = -30,
                TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.Hidden,
            });
            source.HitObjects.Add(new DodgeArenaChange
            {
                StartTime = 3000,
                Duration = 1000,
                TargetPosition = new Vector2(50, 40),
                TargetSize = new Vector2(300, 200),
            });

            var converter = new DodgeBeatmapConverter(source, new DodgeRuleset());
            Assert.That(converter.CanConvert(), Is.True);
            IBeatmap first = converter.Convert();
            IBeatmap retry = converter.Convert();

            Assert.Multiple(() =>
            {
                Assert.That(first.HitObjects, Has.Count.EqualTo(source.HitObjects.Count));
                Assert.That(retry.HitObjects, Has.Count.EqualTo(source.HitObjects.Count));

                for (int i = 0; i < source.HitObjects.Count; i++)
                {
                    Assert.That(first.HitObjects[i], Is.Not.SameAs(source.HitObjects[i]));
                    Assert.That(retry.HitObjects[i], Is.Not.SameAs(source.HitObjects[i]));
                    Assert.That(retry.HitObjects[i], Is.Not.SameAs(first.HitObjects[i]));
                }

                Assert.That((DodgeBullet)retry.HitObjects[0], Has.Property(nameof(DodgeBullet.ContinueUntilExit)).True);
                Assert.That((DodgeBullet)retry.HitObjects[0], Has.Property(nameof(DodgeBullet.Shape)).EqualTo(DodgeBulletShape.Diamond));
                Assert.That(((DodgeBullet)retry.HitObjects[0]).Colour, Is.EqualTo(Colour4.FromHex("#44AAFF")));
                Assert.That(((DodgeBullet)retry.HitObjects[0]).OutlineThickness, Is.EqualTo(2));
                Assert.That(((DodgeBullet)retry.HitObjects[0]).MovementType, Is.EqualTo(DodgeMovementType.Sine));
                Assert.That(((DodgeBullet)retry.HitObjects[0]).WaveAmplitude, Is.EqualTo(44));
                Assert.That(((DodgeBullet)retry.HitObjects[0]).WaveCycles, Is.EqualTo(3));
                Assert.That(((DodgeBullet)retry.HitObjects[0]).WavePhase, Is.EqualTo(20));
                Assert.That(((DodgeBullet)retry.HitObjects[0]).TrajectoryGuideStyle, Is.EqualTo(DodgeTrajectoryGuideStyle.Path));
                Assert.That((DodgeEmitter)retry.HitObjects[1], Has.Property(nameof(DodgeEmitter.BulletCount)).EqualTo(7));
                Assert.That((DodgeEmitter)retry.HitObjects[1], Has.Property(nameof(DodgeEmitter.Shape)).EqualTo(DodgeBulletShape.Triangle));
                Assert.That(((DodgeEmitter)retry.HitObjects[1]).MovementEndPosition, Is.EqualTo(new Vector2(256, 320)));
                Assert.That(((DodgeEmitter)retry.HitObjects[1]).BurstCount, Is.EqualTo(4));
                Assert.That(((DodgeEmitter)retry.HitObjects[1]).BurstInterval, Is.EqualTo(125));
                Assert.That(((DodgeEmitter)retry.HitObjects[1]).BurstBeatDivisor, Is.Zero);
                Assert.That(((DodgeEmitter)retry.HitObjects[1]).MoveSource, Is.True);
                Assert.That(((DodgeEmitter)retry.HitObjects[1]).Colour, Is.EqualTo(Colour4.FromHex("#FF5577")));
                Assert.That(((DodgeEmitter)retry.HitObjects[1]).MovementType, Is.EqualTo(DodgeMovementType.Sine));
                Assert.That(((DodgeEmitter)retry.HitObjects[1]).WaveAmplitude, Is.EqualTo(-28));
                Assert.That(((DodgeEmitter)retry.HitObjects[1]).WaveCycles, Is.EqualTo(4));
                Assert.That(((DodgeEmitter)retry.HitObjects[1]).WavePhase, Is.EqualTo(-30));
                Assert.That(((DodgeEmitter)retry.HitObjects[1]).TrajectoryGuideStyle, Is.EqualTo(DodgeTrajectoryGuideStyle.Hidden));
                Assert.That((DodgeArenaChange)retry.HitObjects[2], Has.Property(nameof(DodgeArenaChange.TargetSize)).EqualTo(new Vector2(300, 200)));
                Assert.That(DodgeBeatmapSettings.GetPlayerSize(first.Difficulty), Is.EqualTo(24));
                Assert.That(DodgeBeatmapSettings.GetPlayerSize(retry.Difficulty), Is.EqualTo(24));
            });
        }

        [Test]
        public void TestOnlyDodgeAndHitResultsAreDisplayed()
        {
            var ruleset = new DodgeRuleset();

            Assert.That(ruleset.GetValidHitResults(), Is.EqualTo(new[] { HitResult.Perfect, HitResult.SmallBonus }));
            Assert.That(ruleset.GetHitResultsForDisplay().Select(result => result.result), Is.EqualTo(new[] { HitResult.Perfect, HitResult.Miss, HitResult.SmallBonus }));
            Assert.That(ruleset.GetHitResultsForDisplay().Select(result => result.displayName.ToString()), Is.EqualTo(new[] { "DODGED", "HIT", "GRAZE" }));
            Assert.That(new DodgeBullet().CreateJudgement().MaxResult, Is.EqualTo(HitResult.Perfect));
            Assert.That(new DodgeEmitter().CreateJudgement().MaxResult, Is.EqualTo(HitResult.Perfect));
            Assert.That(new DodgeArenaChange().CreateJudgement().MaxResult, Is.EqualTo(HitResult.IgnoreHit));
        }

        [Test]
        public void TestBulletSamplesAreAlwaysMuted()
        {
            var bullet = new DodgeBullet();
            bullet.Samples.Add(new HitSampleInfo(HitSampleInfo.HIT_NORMAL));

            var drawable = new DrawableDodgeHitObject(bullet);

            Assert.That(drawable.GetSamples(), Is.Empty);
            Assert.DoesNotThrow(drawable.PlaySamples);
        }

        [Test]
        public void TestBulletAppearanceUsesMapSetting()
        {
            var difficulty = new BeatmapDifficulty
            {
                ApproachRate = DodgeBeatmapSettings.GetApproachRate(1200),
                CircleSize = DodgeBeatmapSettings.GetCircleSize(24),
            };
            var bullet = new DodgeBullet();

            bullet.ApplyDefaults(new ControlPointInfo(), difficulty);

            Assert.That(bullet.TimePreempt, Is.EqualTo(1200).Within(0.001));
            Assert.That(bullet.BulletSize, Is.EqualTo(24).Within(0.001));
        }

        [Test]
        public void TestEmitterAppearanceUsesMapSetting()
        {
            var difficulty = new BeatmapDifficulty
            {
                ApproachRate = DodgeBeatmapSettings.GetApproachRate(900),
                CircleSize = DodgeBeatmapSettings.GetCircleSize(20),
            };
            var emitter = new DodgeEmitter();

            emitter.ApplyDefaults(new ControlPointInfo(), difficulty);

            Assert.That(emitter.TimePreempt, Is.EqualTo(900).Within(0.001));
            Assert.That(emitter.BulletSize, Is.EqualTo(20).Within(0.001));
        }

        [Test]
        public void TestDodgeSetupUsesCustomDifficultySection()
        {
            Assert.That(new DodgeRuleset().CreateEditorSetupSections().OfType<DodgeDifficultySection>(), Is.Not.Empty);
        }

        [Test]
        public void TestNormalMovement()
        {
            Vector2 result = DodgePlayer.CalculatePosition(new Vector2(100), Vector2.UnitX, false, 1000);

            Assert.That(result, Is.EqualTo(new Vector2(340, 100)));
        }

        [Test]
        public void TestShiftSlowsMovement()
        {
            Vector2 normal = DodgePlayer.CalculatePosition(new Vector2(100), Vector2.UnitX, false, 500);
            Vector2 slow = DodgePlayer.CalculatePosition(new Vector2(100), Vector2.UnitX, true, 500);

            Assert.That(slow.X - 100, Is.EqualTo((normal.X - 100) * DodgePlayer.SLOW_MULTIPLIER).Within(0.001f));
        }

        [Test]
        public void TestDiagonalMovementIsNormalised()
        {
            Vector2 horizontal = DodgePlayer.CalculatePosition(new Vector2(200), Vector2.UnitX, false, 100);
            Vector2 diagonal = DodgePlayer.CalculatePosition(new Vector2(200), Vector2.One, false, 100);

            Assert.That((diagonal - new Vector2(200)).Length, Is.EqualTo((horizontal - new Vector2(200)).Length).Within(0.001f));
        }

        [TestCase(-100, -100, DodgePlayer.SIZE / 2, DodgePlayer.SIZE / 2)]
        [TestCase(1000, 1000, DodgePlayfield.WIDTH - DodgePlayer.SIZE / 2, DodgePlayfield.HEIGHT - DodgePlayer.SIZE / 2)]
        public void TestPlayerStaysInsideArena(float x, float y, float expectedX, float expectedY)
        {
            Vector2 result = DodgePlayer.CalculatePosition(new Vector2(x, y), Vector2.Zero, false, 0);

            Assert.That(result, Is.EqualTo(new Vector2(expectedX, expectedY)));
        }

        [Test]
        public void TestPlayerStaysInsideChangedArena()
        {
            Vector2 result = DodgePlayer.CalculatePosition(
                new Vector2(0),
                Vector2.Zero,
                false,
                0,
                new Vector2(100, 80),
                new Vector2(200, 120));

            Assert.That(result, Is.EqualTo(new Vector2(108, 88)));
        }

        [Test]
        public void TestConfiguredPlayerSizeChangesArenaClamp()
        {
            Vector2 result = DodgePlayer.CalculatePosition(
                new Vector2(0),
                Vector2.Zero,
                false,
                0,
                new Vector2(100, 80),
                new Vector2(200, 120),
                DodgePlayer.BASE_SPEED,
                32);

            Assert.That(result, Is.EqualTo(new Vector2(116, 96)));
        }

        [Test]
        public void TestArenaChangeInterpolatesAndCanApplyImmediately()
        {
            var evaluator = new DodgeArenaStateEvaluator(new[]
            {
                new DodgeArenaChange
                {
                    StartTime = 0,
                    Duration = 1000,
                    TargetPosition = new Vector2(20, 10),
                    TargetSize = new Vector2(400, 300),
                },
                new DodgeArenaChange
                {
                    StartTime = 1000,
                    Duration = 1000,
                    TargetPosition = new Vector2(120, 60),
                    TargetSize = new Vector2(200, 100),
                },
            });

            DodgeArenaState initial = evaluator.Evaluate(-2000);
            DodgeArenaState halfway = evaluator.Evaluate(1500);

            Assert.Multiple(() =>
            {
                Assert.That(initial.Position, Is.EqualTo(new Vector2(20, 10)));
                Assert.That(initial.Size, Is.EqualTo(new Vector2(400, 300)));
                Assert.That(halfway.Position, Is.EqualTo(new Vector2(70, 35)));
                Assert.That(halfway.Size, Is.EqualTo(new Vector2(300, 200)));
            });
        }

        [Test]
        public void TestCustomMovementSpeed()
        {
            Vector2 result = DodgePlayer.CalculatePosition(
                new Vector2(100),
                Vector2.UnitX,
                false,
                500,
                Vector2.Zero,
                DodgePlayfield.BASE_SIZE,
                0.4f);

            Assert.That(result.X, Is.EqualTo(300).Within(0.001f));
        }

        [Test]
        public void TestFirstArenaChangeAfterZeroDefinesInitialState()
        {
            var evaluator = new DodgeArenaStateEvaluator(new[]
            {
                new DodgeArenaChange
                {
                    StartTime = 2000,
                    Duration = 1000,
                    TargetPosition = new Vector2(20, 10),
                    TargetSize = new Vector2(400, 300),
                },
                new DodgeArenaChange
                {
                    StartTime = 4000,
                    Duration = 1000,
                    TargetPosition = new Vector2(120, 60),
                    TargetSize = new Vector2(200, 100),
                },
            });

            DodgeArenaState beforeFirstTimingPoint = evaluator.Evaluate(-1000);
            DodgeArenaState afterFirstTimingPoint = evaluator.Evaluate(2500);
            DodgeArenaState secondChangeHalfway = evaluator.Evaluate(4500);

            Assert.Multiple(() =>
            {
                Assert.That(beforeFirstTimingPoint.Position, Is.EqualTo(new Vector2(20, 10)));
                Assert.That(beforeFirstTimingPoint.Size, Is.EqualTo(new Vector2(400, 300)));
                Assert.That(afterFirstTimingPoint.Position, Is.EqualTo(new Vector2(20, 10)));
                Assert.That(afterFirstTimingPoint.Size, Is.EqualTo(new Vector2(400, 300)));
                Assert.That(secondChangeHalfway.Position, Is.EqualTo(new Vector2(70, 35)));
                Assert.That(secondChangeHalfway.Size, Is.EqualTo(new Vector2(300, 200)));
            });
        }

        [Test]
        public void TestArenaCannotExceedBaseBounds()
        {
            var change = new DodgeArenaChange
            {
                TargetPosition = new Vector2(-100, -50),
                TargetSize = new Vector2(1000, 800),
            };

            change.ClampToBaseBounds();

            Assert.Multiple(() =>
            {
                Assert.That(change.TargetPosition, Is.EqualTo(Vector2.Zero));
                Assert.That(change.TargetSize, Is.EqualTo(DodgePlayfield.BASE_SIZE));
            });
        }

        [Test]
        public void TestBulletMovesLinearly()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(10, 20),
                EndPosition = new Vector2(110, 220),
            };

            Assert.That(bullet.PositionAt(1000), Is.EqualTo(new Vector2(10, 20)));
            Assert.That(bullet.PositionAt(1250), Is.EqualTo(new Vector2(60, 120)));
            Assert.That(bullet.PositionAt(1500), Is.EqualTo(new Vector2(110, 220)));
        }

        [Test]
        public void TestBulletDirectionIsNormalised()
        {
            var bullet = new DodgeBullet
            {
                Position = new Vector2(10, 20),
                EndPosition = new Vector2(40, 60),
            };

            Assert.That(bullet.Direction.X, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(bullet.Direction.Y, Is.EqualTo(0.8f).Within(0.0001f));
        }

        [Test]
        public void TestBulletPositionClampsToLifetime()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 500,
                Position = new Vector2(10),
                EndPosition = new Vector2(110),
            };

            Assert.That(bullet.PositionAt(0), Is.EqualTo(new Vector2(10)));
            Assert.That(bullet.PositionAt(2000), Is.EqualTo(new Vector2(110)));
        }

        [Test]
        public void TestBulletCollision()
        {
            Assert.That(DodgeBullet.IntersectsPlayer(new Vector2(100, 100), new Vector2(100, 100)), Is.True);
            Assert.That(DodgeBullet.IntersectsPlayer(new Vector2(113, 100), new Vector2(100, 100)), Is.True);
            Assert.That(DodgeBullet.IntersectsPlayer(new Vector2(115, 100), new Vector2(100, 100)), Is.False);
            Assert.That(DodgeBullet.IntersectsPlayer(new Vector2(120, 100), new Vector2(100, 100), 32), Is.True);
            Assert.That(DodgeBullet.IntersectsPlayer(new Vector2(125, 100), new Vector2(100, 100), 32), Is.False);
            Assert.That(DodgeBullet.IntersectsPlayer(new Vector2(120, 100), new Vector2(100, 100), DodgeBullet.SIZE, 32), Is.True);
            Assert.That(DodgeBullet.IntersectsPlayer(new Vector2(125, 100), new Vector2(100, 100), DodgeBullet.SIZE, 32), Is.False);
        }

        [Test]
        public void TestSweptBulletCollisionCatchesTunnelling()
        {
            Vector2 player = new Vector2(100, 100);

            Assert.That(
                DodgeBullet.IntersectsPlayerSwept(
                    new Vector2(0, 100),
                    new Vector2(200, 100),
                    player,
                    player),
                Is.True);
            Assert.That(
                DodgeBullet.IntersectsPlayerSwept(
                    new Vector2(0, 140),
                    new Vector2(200, 140),
                    player,
                    player),
                Is.False);
        }

        [Test]
        public void TestSweptCollisionAccountsForPlayerMovement()
        {
            Assert.That(
                DodgeBullet.IntersectsPlayerSwept(
                    new Vector2(100, 100),
                    new Vector2(100, 100),
                    new Vector2(0, 100),
                    new Vector2(200, 100)),
                Is.True);
        }

        [Test]
        public void TestSweptGrazeExcludesDistantPath()
        {
            Vector2 player = new Vector2(100, 100);

            Assert.That(
                DodgeBullet.IsWithinGrazeDistanceSwept(
                    new Vector2(0, 118),
                    new Vector2(200, 118),
                    player,
                    player,
                    8),
                Is.True);
            Assert.That(
                DodgeBullet.IsWithinGrazeDistanceSwept(
                    new Vector2(0, 140),
                    new Vector2(200, 140),
                    player,
                    player,
                    8),
                Is.False);
        }

        [Test]
        public void TestGrazeBandExcludesCollisionAndHasConfigurableDistance()
        {
            Vector2 player = new Vector2(100);

            Assert.That(DodgeBullet.IntersectsPlayer(new Vector2(115, 100), player), Is.False);
            Assert.That(DodgeBullet.IsWithinGrazeDistance(new Vector2(115, 100), player, 4), Is.True);
            Assert.That(DodgeBullet.IsWithinGrazeDistance(new Vector2(120, 100), player, 4), Is.False);
        }

        [Test]
        public void TestBulletCollisionIncludesCornersOfSharedCollisionBox()
        {
            Vector2 player = new Vector2(100, 100);

            // collisionDistance = (12 + 16) / 2 = 14. The player and projectile
            // visuals use a shared square collision box, so an overlapping corner
            // must not become a safe diagonal path.
            Assert.That(DodgeBullet.IntersectsPlayer(new Vector2(110, 110), player), Is.True);
            Assert.That(DodgeBullet.IntersectsPlayer(new Vector2(115, 115), player), Is.False);

            // Both endpoints are outside the box, but the swept segment crosses
            // its corner. This is the frame-to-frame equivalent of the same bug.
            Assert.That(
                DodgeBullet.IntersectsPlayerSwept(
                    new Vector2(100, 128),
                    new Vector2(128, 100),
                    player,
                    player),
                Is.True);
        }

        [Test]
        public void TestBeamSweptCollisionCatchesTunnelling()
        {
            var beam = new DodgeBeam
            {
                Position = new Vector2(200, 100),
                EndPosition = new Vector2(200, 300),
                BeamWidth = 20,
            };

            Vector2 previousPlayer = new Vector2(150, 200);
            Vector2 currentPlayer = new Vector2(250, 200);

            // Discrete endpoint checks are both false (player starts and ends outside beam)
            Assert.That(
                DodgeBeam.IntersectsPlayer(
                    beam.BeamCenter, beam.BeamDirection, beam.PerpendicularDirection,
                    beam.BeamLength, beam.BeamWidth, previousPlayer, DodgePlayer.SIZE),
                Is.False);
            Assert.That(
                DodgeBeam.IntersectsPlayer(
                    beam.BeamCenter, beam.BeamDirection, beam.PerpendicularDirection,
                    beam.BeamLength, beam.BeamWidth, currentPlayer, DodgePlayer.SIZE),
                Is.False);

            // Swept collision catches player moving across beam
            Assert.That(
                DodgeBeam.IntersectsPlayerSwept(
                    beam.BeamCenter, beam.BeamDirection, beam.PerpendicularDirection,
                    beam.BeamLength, beam.BeamWidth, previousPlayer, currentPlayer, DodgePlayer.SIZE),
                Is.True);

            // Distant movement does not collide
            Assert.That(
                DodgeBeam.IntersectsPlayerSwept(
                    beam.BeamCenter, beam.BeamDirection, beam.PerpendicularDirection,
                    beam.BeamLength, beam.BeamWidth, new Vector2(150, 50), new Vector2(250, 50), DodgePlayer.SIZE),
                Is.False);
        }

        [Test]
        public void TestBeamSweptGraze()
        {
            var beam = new DodgeBeam
            {
                Position = new Vector2(200, 100),
                EndPosition = new Vector2(200, 300),
                BeamWidth = 20,
            };

            // Player moves near beam within graze band (graze distance = 16)
            // halfWidth + playerSize/2 = 10 + 8 = 18. Graze boundary = 18 + 16 = 34.
            Vector2 previousPlayer = new Vector2(170, 150); // distance X = 30 (within 34)
            Vector2 currentPlayer = new Vector2(170, 250);

            Assert.That(
                DodgeBeam.IntersectsPlayerSwept(
                    beam.BeamCenter, beam.BeamDirection, beam.PerpendicularDirection,
                    beam.BeamLength, beam.BeamWidth, previousPlayer, currentPlayer, DodgePlayer.SIZE),
                Is.False);

            Assert.That(
                DodgeBeam.IsWithinGrazeDistanceSwept(
                    beam.BeamCenter, beam.BeamDirection, beam.PerpendicularDirection,
                    beam.BeamLength, beam.BeamWidth, previousPlayer, currentPlayer, DodgePlayer.SIZE, 16),
                Is.True);

            // Beyond graze boundary (distance X = 40 > 34)
            Assert.That(
                DodgeBeam.IsWithinGrazeDistanceSwept(
                    beam.BeamCenter, beam.BeamDirection, beam.PerpendicularDirection,
                    beam.BeamLength, beam.BeamWidth, new Vector2(160, 150), new Vector2(160, 250), DodgePlayer.SIZE, 16),
                Is.False);
        }

        [Test]
        public void TestGrazeIndicatorMatchesOuterGrazeBoundary()
        {
            Assert.That(
                DodgePlayfield.CalculateGrazeIndicatorDiameter(4, DodgeBullet.SIZE),
                Is.EqualTo(DodgePlayer.SIZE + DodgeBullet.SIZE + 8));

            Assert.That(
                DodgePlayfield.CalculateGrazeIndicatorDiameter(4, DodgeBullet.SIZE, 32),
                Is.EqualTo(32 + DodgeBullet.SIZE + 8));
        }
    }
}
