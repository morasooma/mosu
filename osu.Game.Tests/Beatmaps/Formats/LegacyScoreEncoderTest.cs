// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO.Legacy;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Beatmaps.Formats
{
    public class LegacyScoreEncoderTest
    {
        [TestCase(1, 3)]
        [TestCase(1, 0)]
        [TestCase(0, 3)]
        public void TestCatchMergesFruitAndDropletMisses(int missCount, int largeTickMissCount)
        {
            var ruleset = new CatchRuleset().RulesetInfo;
            var scoreInfo = TestResources.CreateTestScoreInfo(ruleset);
            var beatmap = new TestBeatmap(ruleset);

            scoreInfo.Statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 50,
                [HitResult.LargeTickHit] = 5,
                [HitResult.Miss] = missCount,
                [HitResult.LargeTickMiss] = largeTickMissCount
            };

            var score = new Score { ScoreInfo = scoreInfo };
            var decodedAfterEncode = encodeThenDecode(LegacyBeatmapDecoder.LATEST_VERSION, score, beatmap);

            Assert.That(decodedAfterEncode.ScoreInfo.GetCountMiss(), Is.EqualTo(missCount + largeTickMissCount));
        }

        [Test]
        public void TestFailPreserved()
        {
            var ruleset = new OsuRuleset().RulesetInfo;
            var scoreInfo = TestResources.CreateTestScoreInfo();
            var beatmap = new TestBeatmap(ruleset);

            scoreInfo.Rank = ScoreRank.F;

            var score = new Score { ScoreInfo = scoreInfo };
            var decodedAfterEncode = encodeThenDecode(LegacyBeatmapDecoder.LATEST_VERSION, score, beatmap);

            Assert.That(decodedAfterEncode.ScoreInfo.Rank, Is.EqualTo(ScoreRank.F));
        }

        [Test]
        public void TestScoreWithMissIsNotPerfect()
        {
            var ruleset = new OsuRuleset().RulesetInfo;
            var scoreInfo = TestResources.CreateTestScoreInfo(ruleset);
            var beatmap = new TestBeatmap(ruleset);

            scoreInfo.Statistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 2,
                [HitResult.Miss] = 1,
            };

            scoreInfo.MaximumStatistics = new Dictionary<HitResult, int>
            {
                [HitResult.Great] = 3
            };

            // Hit -> Miss -> Hit
            scoreInfo.Combo = 1;
            scoreInfo.MaxCombo = 1;

            using (var ms = new MemoryStream())
            {
                new LegacyScoreEncoder(new Score { ScoreInfo = scoreInfo }, beatmap).Encode(ms, true);

                ms.Seek(0, SeekOrigin.Begin);

                using (var sr = new SerializationReader(ms))
                {
                    sr.ReadByte(); // ruleset id
                    sr.ReadInt32(); // version
                    sr.ReadString(); // beatmap hash
                    sr.ReadString(); // username
                    sr.ReadString(); // score hash
                    sr.ReadInt16(); // count300
                    sr.ReadInt16(); // count100
                    sr.ReadInt16(); // count50
                    sr.ReadInt16(); // countGeki
                    sr.ReadInt16(); // countKatu
                    sr.ReadInt16(); // countMiss
                    sr.ReadInt32(); // total score
                    sr.ReadInt16(); // max combo
                    bool isPerfect = sr.ReadBoolean(); // full combo

                    Assert.That(isPerfect, Is.False);
                }
            }
        }

        [Test]
        public void TestLockedClientRateModExportsDifficultyAdjust()
        {
            var ruleset = new OsuRuleset();
            var scoreInfo = TestResources.CreateTestScoreInfo(ruleset.RulesetInfo);
            var beatmap = new TestBeatmap(ruleset.RulesetInfo);

            scoreInfo.Mods = new Mod[]
            {
                new TestClientDoubleTime
                {
                    SpeedChange = { Value = 1.25 },
                    LockDifficultyAdjust = { Value = true }
                }
            };

            Mod[] exportMods = LegacyScoreExportModConverter.GetExportMods(ruleset, beatmap, scoreInfo.Mods, out bool convertedClientMods);

            Assert.Multiple(() =>
            {
                Assert.That(convertedClientMods, Is.True);
                var exportedRateMod = exportMods.OfType<ModDoubleTime>().SingleOrDefault();
                Assert.That(exportedRateMod, Is.Not.Null);
                Assert.That(exportedRateMod!.LockDifficultyAdjust.Value, Is.False);

                var difficultyAdjust = exportMods.OfType<OsuModDifficultyAdjust>().SingleOrDefault();
                Assert.That(difficultyAdjust, Is.Not.Null);

                var sourceDifficulty = ruleset.GetAdjustedDisplayDifficulty(beatmap.BeatmapInfo, scoreInfo.Mods);
                var exportedDifficulty = ruleset.GetAdjustedDisplayDifficulty(beatmap.BeatmapInfo, exportMods);

                Assert.That(exportedDifficulty.ApproachRate, Is.EqualTo(sourceDifficulty.ApproachRate).Within(0.01));

                var sourceHitCircle = createAdjustedHitCircle(beatmap, scoreInfo.Mods);
                var exportedHitCircle = createAdjustedHitCircle(beatmap, exportMods);
                var greatOnlyExportHitCircle = createAdjustedHitCircle(beatmap, createGreatOnlyExportMods(ruleset, beatmap, 1.25));

                Assert.That(exportedHitCircle.TimePreempt, Is.EqualTo(sourceHitCircle.TimePreempt).Within(0.0001));
                Assert.That(calculateWindowError(sourceHitCircle, exportedHitCircle), Is.LessThan(calculateWindowError(sourceHitCircle, greatOnlyExportHitCircle)));
            });
        }

        private static IReadOnlyCollection<Mod> createGreatOnlyExportMods(OsuRuleset ruleset, TestBeatmap beatmap, double rate)
        {
            var rateMod = (ModDoubleTime)ruleset.CreateModFromAcronym("DT")!;
            rateMod.SpeedChange.Value = rate;

            var difficultyAdjust = (OsuModDifficultyAdjust)ruleset.CreateModFromAcronym("DA")!;
            difficultyAdjust.ApproachRate.Value = computeApproachRate(beatmap.Difficulty.ApproachRate, rate);
            difficultyAdjust.OverallDifficulty.Value = computeGreatOnlyOverallDifficulty(beatmap.Difficulty.OverallDifficulty, rate);

            return new Mod[] { rateMod, difficultyAdjust };
        }

        private static HitCircle createAdjustedHitCircle(TestBeatmap beatmap, IReadOnlyCollection<Mod> mods)
        {
            var difficulty = new BeatmapDifficulty(beatmap.Difficulty);

            foreach (var mod in mods.OfType<IApplicableToDifficulty>())
                mod.ApplyToDifficulty(difficulty);

            var hitCircle = new HitCircle();
            hitCircle.ApplyDefaults(new ControlPointInfo(), difficulty);

            foreach (var mod in mods.OfType<IApplicableToHitObject>())
                mod.ApplyToHitObject(hitCircle);

            return hitCircle;
        }

        private static double calculateWindowError(HitCircle expected, HitCircle actual)
            => new[] { HitResult.Great, HitResult.Ok, HitResult.Meh }
               .Sum(result => Math.Abs(expected.HitWindows.WindowFor(result) - actual.HitWindows.WindowFor(result)));

        private static float computeApproachRate(float approachRate, double rate)
        {
            var preemptRange = new DifficultyRange(1800, 1200, 450);
            double targetPreempt = IBeatmapDifficultyInfo.DifficultyRangeInt(approachRate, preemptRange) * rate;
            return (float)IBeatmapDifficultyInfo.InverseDifficultyRange(targetPreempt, preemptRange);
        }

        private static float computeGreatOnlyOverallDifficulty(float overallDifficulty, double rate)
        {
            var greatRange = new DifficultyRange(80, 50, 20);
            double targetGreatWindow = (Math.Floor(IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, greatRange)) - 0.5) * rate;
            return (float)((79.5 - targetGreatWindow) / 6);
        }

        private class TestClientDoubleTime : OsuModDoubleTime
        {
            public override ModType Type => ModType.Mosu;
        }

        private static Score encodeThenDecode(int beatmapVersion, Score score, TestBeatmap beatmap)
        {
            var encodeStream = new MemoryStream();

            var encoder = new LegacyScoreEncoder(score, beatmap);
            encoder.Encode(encodeStream);

            var decodeStream = new MemoryStream(encodeStream.GetBuffer());

            var decoder = new LegacyScoreDecoderTest.TestLegacyScoreDecoder(beatmapVersion);
            var decodedAfterEncode = decoder.Parse(decodeStream);
            return decodedAfterEncode;
        }
    }
}
