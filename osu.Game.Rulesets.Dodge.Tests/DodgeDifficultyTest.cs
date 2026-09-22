// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Difficulty;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Mods;
using osu.Game.Rulesets.Dodge.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeDifficultyTest
    {
        [Test]
        public void TestEmptyMapHasNoDifficulty()
        {
            DodgeDifficultyAttributes attributes = DodgeAutoplayDifficultyEvaluator.Calculate(new Beatmap<DodgeHitObject>());

            Assert.That(attributes.StarRating, Is.Zero);
        }

        [Test]
        public void TestDenseEmitterIsHarderThanSingleSafeBullet()
        {
            var safeBullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(0, 32),
                EndPosition = new Vector2(512, 32),
            };
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(512, 192),
                BulletCount = 16,
                SpreadAngle = 360,
            };

            DodgeDifficultyAttributes easy = DodgeAutoplayDifficultyEvaluator.Calculate(createBeatmap(safeBullet));
            DodgeDifficultyAttributes hard = DodgeAutoplayDifficultyEvaluator.Calculate(createBeatmap(emitter));

            Assert.That(easy.StarRating, Is.GreaterThan(0));
            Assert.That(hard.StarRating, Is.GreaterThan(easy.StarRating));
            Assert.That(hard.ProjectileRate, Is.GreaterThan(easy.ProjectileRate));
            Assert.That(hard.PeakActiveProjectiles, Is.GreaterThan(easy.PeakActiveProjectiles));
        }

        [Test]
        public void TestRulesetDifficultyCalculatorUsesAutoplayAnalysis()
        {
            Beatmap<DodgeHitObject> beatmap = createBeatmap(new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(0, 192),
                EndPosition = new Vector2(512, 192),
            });
            var ruleset = new DodgeRuleset();
            var calculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));

            var attributes = (DodgeDifficultyAttributes)calculator.Calculate();

            Assert.That(attributes.StarRating, Is.GreaterThan(0));
            Assert.That(attributes.ProjectileRate, Is.GreaterThan(0));
            Assert.That(attributes.MaxCombo, Is.EqualTo(1));
        }

        [Test]
        public void TestMaxComboUsesGameplayJudgements()
        {
            Beatmap<DodgeHitObject> beatmap = createBeatmap(
                new DodgeBullet(),
                new DodgeEmitter { BurstCount = 4 },
                new DodgeBeam(),
                new DodgeArenaChange(),
                new DodgeCameraChange());

            Assert.That(DodgeDifficultyCalculator.CalculateMaxCombo(beatmap), Is.EqualTo(6));
        }

        [Test]
        public void TestTimedDifficultyReusesOneFullAnalysisAndProvidesLivePerformance()
        {
            Beatmap<DodgeHitObject> beatmap = createBeatmap(
                new DodgeBullet
                {
                    StartTime = 1000,
                    Duration = 1000,
                    Position = new Vector2(0, 64),
                    EndPosition = new Vector2(512, 64),
                },
                new DodgeEmitter
                {
                    StartTime = 2000,
                    Duration = 1000,
                    Position = new Vector2(256, 192),
                    AimPosition = new Vector2(512, 192),
                    BulletCount = 8,
                    SpreadAngle = 180,
                });
            beatmap.BeatmapInfo.StarRating = 4;
            var ruleset = new DodgeRuleset();
            var calculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));

            var timed = calculator.CalculateTimed();
            var first = (DodgeDifficultyAttributes)timed[0].Attributes;
            var last = (DodgeDifficultyAttributes)timed[1].Attributes;
            var liveScore = new ScoreInfo
            {
                MaxCombo = first.MaxCombo,
                Statistics = new Dictionary<HitResult, int>
                {
                    [HitResult.Perfect] = first.MaxCombo,
                },
            };
            var livePerformance = (DodgePerformanceAttributes)new DodgePerformanceCalculator().Calculate(liveScore, first);

            Assert.Multiple(() =>
            {
                Assert.That(timed, Has.Count.EqualTo(2));
                Assert.That(calculator.AutoplayCalculationCount, Is.EqualTo(1));
                Assert.That(first.StarRating, Is.LessThan(last.StarRating));
                Assert.That(first.MovementDifficulty, Is.GreaterThan(0).And.LessThan(last.MovementDifficulty));
                Assert.That(first.PathDifficulty, Is.GreaterThan(0).And.LessThan(last.PathDifficulty));
                Assert.That(first.RelevantPatternCount, Is.GreaterThan(0).And.LessThanOrEqualTo(last.RelevantPatternCount));
                Assert.That(first.EffectiveReadingPatternCount, Is.GreaterThan(0).And.LessThan(last.EffectiveReadingPatternCount));
                Assert.That(first.MaxCombo, Is.EqualTo(1));
                Assert.That(last.MaxCombo, Is.EqualTo(2));
                Assert.That(livePerformance.Total, Is.GreaterThan(0));
            });
        }

        [Test]
        [NonParallelizable]
        public void TestTimedDifficultyUsesForkDatabaseAttributesUntilMapChanges()
        {
            SynchronizationContext? previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                using var storage = new TemporaryNativeStorage($"dodge-live-difficulty-{Guid.NewGuid():N}");
                using var store = new ForkDataStore(storage);
                Beatmap<DodgeHitObject> beatmap = createBeatmap(
                    new DodgeBullet
                    {
                        StartTime = 1000,
                        Duration = 1000,
                        Position = new Vector2(0, 64),
                        EndPosition = new Vector2(512, 64),
                    },
                    new DodgeEmitter
                    {
                        StartTime = 2000,
                        Duration = 1000,
                        Position = new Vector2(256, 192),
                        AimPosition = new Vector2(512, 192),
                        BulletCount = 8,
                        SpreadAngle = 180,
                    });
                beatmap.BeatmapInfo.ID = Guid.NewGuid();
                beatmap.BeatmapInfo.MD5Hash = "0123456789abcdef0123456789abcdef";
                var ruleset = new DodgeRuleset();
                var initialCalculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));

                var initialTimed = initialCalculator.CalculateTimed();
                var initial = (DodgeDifficultyAttributes)initialTimed[^1].Attributes;

                Assert.That(initialCalculator.AutoplayCalculationCount, Is.EqualTo(1));
                Assert.That(initialCalculator.PersistentCacheHitCount, Is.Zero);
                Assert.That(store.HasDodgeDifficultyAttributes(
                    beatmap.BeatmapInfo.ID,
                    initialCalculator.Version,
                    beatmap.BeatmapInfo.MD5Hash), Is.True);

                var cachedFullCalculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));
                var cachedFull = (DodgeDifficultyAttributes)cachedFullCalculator.Calculate();

                Assert.Multiple(() =>
                {
                    Assert.That(cachedFullCalculator.AutoplayCalculationCount, Is.Zero);
                    Assert.That(cachedFullCalculator.PersistentCacheHitCount, Is.EqualTo(1));
                    Assert.That(cachedFull.StarRating, Is.EqualTo(initial.StarRating).Within(0.0001));
                    Assert.That(cachedFull.MovementDifficulty, Is.EqualTo(initial.MovementDifficulty).Within(0.0001));
                    Assert.That(cachedFull.PathDifficulty, Is.EqualTo(initial.PathDifficulty).Within(0.0001));
                    Assert.That(cachedFull.ReadingDifficulty, Is.EqualTo(initial.ReadingDifficulty).Within(0.0001));
                    Assert.That(cachedFull.MaxCombo, Is.EqualTo(initial.MaxCombo));
                });

                var cachedRateCalculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));
                var cachedRate = (DodgeDifficultyAttributes)cachedRateCalculator.Calculate(new[] { new DodgeModDoubleTime() });

                Assert.Multiple(() =>
                {
                    Assert.That(cachedRateCalculator.AutoplayCalculationCount, Is.Zero);
                    Assert.That(cachedRateCalculator.PersistentCacheHitCount, Is.EqualTo(1));
                    Assert.That(cachedRate.StarRating, Is.GreaterThan(initial.StarRating));
                });

                var cachedCalculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));
                var cachedTimed = cachedCalculator.CalculateTimed();
                var cachedFinal = (DodgeDifficultyAttributes)cachedTimed[^1].Attributes;

                Assert.Multiple(() =>
                {
                    Assert.That(cachedCalculator.AutoplayCalculationCount, Is.Zero);
                    Assert.That(cachedCalculator.PersistentCacheHitCount, Is.EqualTo(1));
                    Assert.That(cachedFinal.StarRating, Is.EqualTo(initial.StarRating).Within(0.0001));
                    Assert.That(cachedFinal.MovementDifficulty, Is.EqualTo(initial.MovementDifficulty).Within(0.0001));
                    Assert.That(cachedFinal.PathDifficulty, Is.EqualTo(initial.PathDifficulty).Within(0.0001));
                    Assert.That(cachedFinal.ReadingDifficulty, Is.EqualTo(initial.ReadingDifficulty).Within(0.0001));
                });

                beatmap.BeatmapInfo.MD5Hash = "fedcba9876543210fedcba9876543210";
                var changedMapCalculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));
                changedMapCalculator.CalculateTimed();

                Assert.Multiple(() =>
                {
                    Assert.That(changedMapCalculator.PersistentCacheHitCount, Is.Zero);
                    Assert.That(changedMapCalculator.AutoplayCalculationCount, Is.EqualTo(1));
                });
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        [Test]
        public void TestLargerPlayerIncreasesDifficulty()
        {
            Beatmap<DodgeHitObject> smallPlayerMap = createBeatmap(createEmitter());
            Beatmap<DodgeHitObject> largePlayerMap = createBeatmap(createEmitter());
            DodgeBeatmapSettings.SetPlayerSize(smallPlayerMap.Difficulty, 8);
            DodgeBeatmapSettings.SetPlayerSize(largePlayerMap.Difficulty, 32);

            double smallPlayerStars = DodgeAutoplayDifficultyEvaluator.Calculate(smallPlayerMap).StarRating;
            double largePlayerStars = DodgeAutoplayDifficultyEvaluator.Calculate(largePlayerMap).StarRating;

            Assert.That(largePlayerStars, Is.GreaterThan(smallPlayerStars));

            static DodgeEmitter createEmitter() => new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(512, 192),
                BulletCount = 12,
                SpreadAngle = 360,
            };
        }

        [Test]
        public void TestRateModsChangeDifficulty()
        {
            Beatmap<DodgeHitObject> beatmap = createBeatmap(new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(512, 192),
                BulletCount = 12,
                SpreadAngle = 180,
            });
            var ruleset = new DodgeRuleset();
            var calculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));

            double normal = calculator.Calculate().StarRating;
            double halfTime = calculator.Calculate(new[] { new DodgeModHalfTime() }).StarRating;
            double doubleTime = calculator.Calculate(new[] { new DodgeModDoubleTime() }).StarRating;

            Assert.That(halfTime, Is.LessThan(normal));
            Assert.That(doubleTime, Is.GreaterThan(normal));
            Assert.That(doubleTime / normal, Is.EqualTo(Math.Pow(1.5, 1.35)).Within(0.001));
        }

        [Test]
        public void TestStaticBpmChangesDifficulty()
        {
            Beatmap<DodgeHitObject> beatmap = createBeatmap(new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(512, 192),
                BulletCount = 12,
                SpreadAngle = 180,
            });
            beatmap.BeatmapInfo.BPM = 120;
            var ruleset = new DodgeRuleset();
            var calculator = new DodgeDifficultyCalculator(ruleset.RulesetInfo, new FlatWorkingBeatmap(beatmap));
            var staticBpm = new DodgeModMosuStaticBpm();
            staticBpm.TargetBpm.Value = 180;

            double normal = calculator.Calculate().StarRating;
            double adjusted = calculator.Calculate(new[] { staticBpm }).StarRating;

            Assert.That(adjusted, Is.GreaterThan(normal));
            Assert.That(adjusted / normal, Is.EqualTo(Math.Pow(1.5, 1.35)).Within(0.001));
        }

        [Test]
        public void TestPlannerFallbackDoesNotClaimMapIsUnclearable()
        {
            var arena = new DodgeArenaChange
            {
                StartTime = 0,
                Duration = 0,
                TargetPosition = new Vector2(240, 176),
                TargetSize = new Vector2(32),
            };
            var blockingBullet = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 4000,
                Position = new Vector2(256, 192),
                EndPosition = new Vector2(256, 192),
            };

            DodgeDifficultyAttributes attributes = DodgeAutoplayDifficultyEvaluator.Calculate(createBeatmap(arena, blockingBullet));

            Assert.That(attributes.AutoplayTeleports, Is.EqualTo(1));
            Assert.That(attributes.AutoplayCollisions, Is.EqualTo(1));
            Assert.That(attributes.IsUnclearable, Is.False);
            Assert.That(attributes.StarRating, Is.LessThanOrEqualTo(15));
        }

        [Test]
        public void TestDecorativeOffscreenBulletsDoNotInflateStars()
        {
            Beatmap<DodgeHitObject> baseMap = createBeatmap(createCrossingBullet());
            var decoratedObjects = new List<DodgeHitObject> { createCrossingBullet() };

            for (int i = 0; i < 100; i++)
            {
                decoratedObjects.Add(new DodgeBullet
                {
                    StartTime = 1000,
                    Duration = 1000,
                    Position = new Vector2(-200 - i, -200),
                    EndPosition = new Vector2(-200 - i, -200),
                });
            }

            DodgeDifficultyAttributes baseline = DodgeAutoplayDifficultyEvaluator.Calculate(baseMap);
            DodgeDifficultyAttributes decorated = DodgeAutoplayDifficultyEvaluator.Calculate(createBeatmap(decoratedObjects.ToArray()));

            Assert.Multiple(() =>
            {
                Assert.That(decorated.ProjectileRate, Is.GreaterThan(baseline.ProjectileRate * 50));
                Assert.That(decorated.RelevantPatternCount, Is.EqualTo(baseline.RelevantPatternCount));
                Assert.That(decorated.ReadingDifficulty, Is.EqualTo(baseline.ReadingDifficulty).Within(0.001));
                Assert.That(decorated.StarRating, Is.EqualTo(baseline.StarRating).Within(0.15));
            });

            static DodgeBullet createCrossingBullet() => new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(0, 192),
                EndPosition = new Vector2(512, 192),
            };
        }

        [Test]
        public void TestAppearanceDurationDoesNotAffectObjectDerivedReadingDifficulty()
        {
            Beatmap<DodgeHitObject> readable = createBeatmap(createEmitter());
            Beatmap<DodgeHitObject> reactive = createBeatmap(createEmitter());
            readable.Difficulty.ApproachRate = 2;
            reactive.Difficulty.ApproachRate = 8;

            DodgeDifficultyAttributes readableDifficulty = DodgeAutoplayDifficultyEvaluator.Calculate(readable);
            DodgeDifficultyAttributes reactiveDifficulty = DodgeAutoplayDifficultyEvaluator.Calculate(reactive);

            Assert.Multiple(() =>
            {
                Assert.That(reactiveDifficulty.ReadingDifficulty, Is.EqualTo(readableDifficulty.ReadingDifficulty).Within(0.001));
                Assert.That(reactiveDifficulty.StarRating, Is.EqualTo(readableDifficulty.StarRating).Within(0.001));
            });

            static DodgeEmitter createEmitter() => new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(512, 192),
                BulletCount = 12,
                SpreadAngle = 180,
            };
        }

        [Test]
        public void TestSimultaneousBulletWallIsOneReadingPattern()
        {
            var bullets = Enumerable.Range(0, 16)
                                    .Select(index => (DodgeHitObject)new DodgeBullet
                                    {
                                        StartTime = 1000,
                                        Duration = 1000,
                                        Position = new Vector2(0, index * 24),
                                        EndPosition = new Vector2(512, index * 24),
                                    })
                                    .ToArray();

            DodgeDifficultyAttributes difficulty = DodgeAutoplayDifficultyEvaluator.Calculate(createBeatmap(bullets));

            Assert.That(difficulty.RelevantPatternCount, Is.EqualTo(1));
        }

        [Test]
        public void TestRepeatedBurstsIncreaseObjectDerivedReadingDifficulty()
        {
            Beatmap<DodgeHitObject> single = createBeatmap(createEmitter(1));
            Beatmap<DodgeHitObject> repeated = createBeatmap(createEmitter(4));

            DodgeDifficultyAttributes singleDifficulty = DodgeAutoplayDifficultyEvaluator.Calculate(single);
            DodgeDifficultyAttributes repeatedDifficulty = DodgeAutoplayDifficultyEvaluator.Calculate(repeated);

            Assert.Multiple(() =>
            {
                Assert.That(singleDifficulty.RelevantPatternCount, Is.EqualTo(1));
                Assert.That(repeatedDifficulty.RelevantPatternCount, Is.EqualTo(4));
                Assert.That(repeatedDifficulty.ReadingDifficulty, Is.GreaterThan(singleDifficulty.ReadingDifficulty));
            });

            static DodgeEmitter createEmitter(int burstCount) => new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(512, 192),
                BulletCount = 8,
                SpreadAngle = 360,
                BurstCount = burstCount,
                BurstInterval = 150,
                BurstRotation = 15,
            };
        }

        [Test]
        public void TestRapidSimilarBulletsShareReadingPatternFamily()
        {
            var bullets = Enumerable.Range(0, 4)
                                    .Select(index => (DodgeHitObject)new DodgeBullet
                                    {
                                        StartTime = 1000 + index * 60,
                                        Duration = 1000,
                                        Position = new Vector2(0, 192),
                                        EndPosition = new Vector2(512, 192),
                                    })
                                    .ToArray();

            DodgeDifficultyAttributes difficulty = DodgeAutoplayDifficultyEvaluator.Calculate(createBeatmap(bullets));

            Assert.That(difficulty.RelevantPatternCount, Is.EqualTo(1));
        }

        [Test]
        public void TestFasterThreatHasHigherUrgencyAndReadingDifficulty()
        {
            var slowGenerator = new DodgeAutoGenerator(createBeatmap(createBullet(4000)));
            var fastGenerator = new DodgeAutoGenerator(createBeatmap(createBullet(500)));
            slowGenerator.Generate();
            fastGenerator.Generate();

            DodgeDifficultyAttributes slow = DodgeAutoplayDifficultyEvaluator.FromAnalysis(slowGenerator.Analysis);
            DodgeDifficultyAttributes fast = DodgeAutoplayDifficultyEvaluator.FromAnalysis(fastGenerator.Analysis);

            Assert.Multiple(() =>
            {
                Assert.That(fastGenerator.Analysis.MeanThreatUrgency, Is.GreaterThan(slowGenerator.Analysis.MeanThreatUrgency));
                Assert.That(fast.ReadingDifficulty, Is.GreaterThan(slow.ReadingDifficulty));
            });

            static DodgeBullet createBullet(double duration) => new DodgeBullet
            {
                StartTime = 1000,
                Duration = duration,
                Position = new Vector2(0, 192),
                EndPosition = new Vector2(512, 192),
            };
        }

        [Test]
        public void TestSymmetricRadialCoverageIsNotMaximumDirectionComplexity()
        {
            double parallel = DodgeAutoGenerator.CalculateDirectionComplexity(1 / 12.0, 1);
            double crossing = DodgeAutoGenerator.CalculateDirectionComplexity(0.5, 1);
            double radial = DodgeAutoGenerator.CalculateDirectionComplexity(1, 1);

            Assert.Multiple(() =>
            {
                Assert.That(crossing, Is.GreaterThan(parallel));
                Assert.That(crossing, Is.GreaterThan(radial));
                Assert.That(radial, Is.EqualTo(0).Within(0.0001));
            });
        }

        [Test]
        public void TestCurvedTrajectoryIncreasesReadingDifficulty()
        {
            Beatmap<DodgeHitObject> linear = createBeatmap(createBullet(DodgeMovementType.Linear));
            Beatmap<DodgeHitObject> curved = createBeatmap(createBullet(DodgeMovementType.Sine));

            DodgeDifficultyAttributes linearDifficulty = DodgeAutoplayDifficultyEvaluator.Calculate(linear);
            DodgeDifficultyAttributes curvedDifficulty = DodgeAutoplayDifficultyEvaluator.Calculate(curved);

            Assert.That(curvedDifficulty.ReadingDifficulty, Is.GreaterThan(linearDifficulty.ReadingDifficulty));

            static DodgeBullet createBullet(DodgeMovementType movementType) => new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(0, 192),
                EndPosition = new Vector2(512, 192),
                MovementType = movementType,
                WaveAmplitude = 64,
                WaveCycles = 2,
            };
        }

        [Test]
        public void TestSinglePeakHasLimitedInfluenceOnLongMap()
        {
            double baseline = DodgeAutoGenerator.AggregateSectionDifficulty(Enumerable.Repeat(0.3, 100));
            double withPeak = DodgeAutoGenerator.AggregateSectionDifficulty(Enumerable.Repeat(0.3, 99).Append(1));
            double isolatedPeak = DodgeAutoGenerator.AggregateSectionDifficulty(Enumerable.Repeat(0.0, 99).Append(1));
            double sustained = DodgeAutoGenerator.AggregateSectionDifficulty(Enumerable.Repeat(0.3, 50).Concat(Enumerable.Repeat(0.8, 50)));

            Assert.Multiple(() =>
            {
                Assert.That(withPeak - baseline, Is.LessThanOrEqualTo(0.05));
                Assert.That(isolatedPeak, Is.LessThanOrEqualTo(0.05));
                Assert.That(sustained, Is.GreaterThan(withPeak + 0.2));
            });
        }

        [Test]
        public void TestDifficultSectionCountTracksSustainedDifficulty()
        {
            double isolated = DodgeAutoGenerator.CalculateDifficultSectionCount(
                Enumerable.Repeat(0.0, 9).Append(1));
            double repeated = DodgeAutoGenerator.CalculateDifficultSectionCount(
                Enumerable.Repeat(1.0, 10));

            Assert.Multiple(() =>
            {
                Assert.That(isolated, Is.EqualTo(1).Within(0.001));
                Assert.That(repeated, Is.EqualTo(10).Within(0.001));
            });
        }

        [Test]
        public void TestFewerSafePathsIncreaseDifficulty()
        {
            Beatmap<DodgeHitObject> openMap = createBeatmap(new DodgeBullet
            {
                StartTime = 1000,
                Duration = 2000,
                Position = new Vector2(-200),
                EndPosition = new Vector2(-200),
            });
            Beatmap<DodgeHitObject> corridorMap = createBeatmap(
                new DodgeBeam
                {
                    StartTime = 1000,
                    Duration = 2000,
                    Position = new Vector2(0, 80),
                    EndPosition = new Vector2(512, 80),
                    BeamWidth = 144,
                },
                new DodgeBeam
                {
                    StartTime = 1000,
                    Duration = 2000,
                    Position = new Vector2(0, 304),
                    EndPosition = new Vector2(512, 304),
                    BeamWidth = 144,
                });

            DodgeDifficultyAttributes open = DodgeAutoplayDifficultyEvaluator.Calculate(openMap);
            DodgeDifficultyAttributes corridor = DodgeAutoplayDifficultyEvaluator.Calculate(corridorMap);

            Assert.Multiple(() =>
            {
                Assert.That(corridor.IsUnclearable, Is.False);
                Assert.That(corridor.PathDifficulty, Is.GreaterThan(open.PathDifficulty + 0.4));
                Assert.That(corridor.StarRating, Is.GreaterThan(open.StarRating));
            });
        }

        [Test]
        public void TestRotatingEasedEmitterProducesFiniteStarRating()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1200,
                Position = new Vector2(256, 24),
                AimPosition = new Vector2(256, 260),
                BulletCount = 7,
                SpreadAngle = 100,
                BurstCount = 8,
                BurstInterval = 125,
                BurstRotation = 24,
                MovementEasing = DodgeMovementEasing.EaseOut,
            };

            DodgeDifficultyAttributes attributes = DodgeAutoplayDifficultyEvaluator.Calculate(createBeatmap(emitter));

            Assert.That(attributes.ProjectileRate, Is.GreaterThan(0));
            Assert.That(attributes.PeakActiveProjectiles, Is.GreaterThan(0));
            Assert.That(attributes.StarRating, Is.GreaterThan(0).And.LessThanOrEqualTo(15));
            Assert.That(double.IsFinite(attributes.StarRating), Is.True);
        }

        private static Beatmap<DodgeHitObject> createBeatmap(params DodgeHitObject[] hitObjects)
        {
            var beatmap = new Beatmap<DodgeHitObject>();
            beatmap.HitObjects.AddRange(hitObjects);
            return beatmap;
        }
    }
}
