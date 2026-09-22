// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Difficulty;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Dodge.Tests
{
    /// <summary>
    /// A local regression runner for beatmaps exported from lazer. The maps are
    /// intentionally not checked into the repository; point
    /// <c>DODGE_REGRESSION_MAP_DIR</c> at a directory containing matching
    /// <c>.osu</c> and <c>.ruleset.json</c> files to enable it.
    /// </summary>
    [TestFixture]
    public class DodgeExportedMapRegressionTest
    {
        [Test]
        [Category("ExternalRegression")]
        public void TestExportedMaps()
        {
            string? mapDirectory = Environment.GetEnvironmentVariable("DODGE_REGRESSION_MAP_DIR");

            if (string.IsNullOrWhiteSpace(mapDirectory) || !Directory.Exists(mapDirectory))
            {
                Assert.Ignore("Set DODGE_REGRESSION_MAP_DIR to run exported Dodge maps.");
                return;
            }

            string[] beatmapFiles = Directory.GetFiles(mapDirectory, "*.osu", SearchOption.AllDirectories);
            string? mapFilter = Environment.GetEnvironmentVariable("DODGE_REGRESSION_MAP_FILTER");

            if (!string.IsNullOrWhiteSpace(mapFilter))
            {
                beatmapFiles = beatmapFiles.Where(path =>
                                                   Path.GetFileNameWithoutExtension(path).Contains(
                                                       mapFilter,
                                                       StringComparison.OrdinalIgnoreCase))
                                           .ToArray();
            }

            Assert.That(beatmapFiles, Is.Not.Empty, $"No .osu files found below {mapDirectory}.");

            TestContext.Progress.WriteLine("map\tthreats\trelevant_patterns\teffective_patterns\tmean_urgency\tpeak_urgency\tpeak_patterns\tfallbacks\tcollisions\tbranch\taction\tpath_raw\tpeak_path\tmovement_raw\tpeak_movement\treading_raw\tpeak_reading\tdifficult_sections\tmovement\tpath\treading\tstars\tss_pp\tmean_pressure\tpeak_pressure\tweighted_pressure\tunclearable\tgenerate_ms\tallocated_mb");

            foreach (string beatmapFile in beatmapFiles.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            {
                string sidecarFile = Path.ChangeExtension(beatmapFile, ".ruleset.json");
                Assert.That(File.Exists(sidecarFile), Is.True, $"Missing Dodge sidecar for {beatmapFile}.");

                IBeatmap beatmap = DecodeBeatmap(beatmapFile, sidecarFile);
                var generator = new DodgeAutoGenerator(beatmap);

                long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
                var stopwatch = Stopwatch.StartNew();
                generator.Generate();
                stopwatch.Stop();
                long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;

                DodgeAutoplayAnalysis analysis = generator.Analysis;
                DodgeDifficultyAttributes difficulty = DodgeAutoplayDifficultyEvaluator.FromAnalysis(analysis);
                difficulty.MaxCombo = DodgeDifficultyCalculator.CalculateMaxCombo(beatmap);
                var fullComboScore = new ScoreInfo
                {
                    MaxCombo = difficulty.MaxCombo,
                    Statistics = new Dictionary<HitResult, int>
                    {
                        [HitResult.Perfect] = difficulty.MaxCombo,
                    },
                };
                var performance = (DodgePerformanceAttributes)new DodgePerformanceCalculator().Calculate(fullComboScore, difficulty);
                string mapName = Path.GetFileNameWithoutExtension(beatmapFile);

                TestContext.Progress.WriteLine(
                    $"{mapName}\t{analysis.ProjectileCount}\t{analysis.RelevantPatternCount}\t{analysis.EffectiveReadingPatternCount:N2}\t{analysis.MeanThreatUrgency:N3}\t{analysis.PeakThreatUrgency:N3}\t{analysis.PeakConcurrentPatterns}\t{analysis.TeleportCount}\t{analysis.CollisionCount}\t{analysis.BranchDifficulty:N3}\t{analysis.ActionDifficulty:N3}\t{analysis.PathDifficulty:N3}\t{analysis.PeakPathDifficulty:N3}\t{analysis.MovementDifficulty:N3}\t{analysis.PeakMovementDifficulty:N3}\t{analysis.ReadingDifficulty:N3}\t{analysis.PeakReadingDifficulty:N3}\t{analysis.DifficultSectionCount:N2}\t{difficulty.MovementDifficulty:N3}\t{difficulty.PathDifficulty:N3}\t{difficulty.ReadingDifficulty:N3}\t{difficulty.StarRating:N3}\t{performance.Total:N1}\t{analysis.MeanPressure:N3}\t{analysis.PeakPressure:N3}\t{analysis.WeightedPressure:N3}\t{difficulty.IsUnclearable}\t{stopwatch.Elapsed.TotalMilliseconds:N1}\t{allocatedBytes / 1024d / 1024d:N1}");

                foreach (DodgeAutoplayRoutePoint point in generator.Route.Where((point, index) =>
                             point.UsedFallback && (index == 0 || !generator.Route[index - 1].UsedFallback)))
                {
                    TestContext.Progress.WriteLine($"  fallback start: {point.Time:N1} ms at ({point.Position.X:N1}, {point.Position.Y:N1})");
                }

                Assert.Multiple(() =>
                {
                    Assert.That(generator.Route, Is.Not.Empty, $"No route generated for {mapName}.");
                    Assert.That(generator.Route.Select(point => point.Time), Is.Ordered, $"Route times are unordered for {mapName}.");
                    Assert.That(generator.Route.All(point => float.IsFinite(point.Position.X) && float.IsFinite(point.Position.Y)),
                        Is.True,
                        $"Route contains a non-finite position for {mapName}.");
                    Assert.That(double.IsFinite(difficulty.StarRating), Is.True, $"Star rating is not finite for {mapName}.");

                    if (mapName.Contains("Dead Air", StringComparison.OrdinalIgnoreCase)
                        || mapName.Contains("Cotton Candy", StringComparison.OrdinalIgnoreCase)
                        || mapName.Contains("NikoN1nja - #9", StringComparison.OrdinalIgnoreCase))
                    {
                        Assert.That(analysis.TeleportCount, Is.Zero, $"Known-clear route used fallback movement for {mapName}.");
                        Assert.That(analysis.CollisionCount, Is.Zero, $"Known-clear route collided for {mapName}.");
                    }
                });
            }
        }

        internal static IBeatmap DecodeBeatmap(string beatmapFile, string sidecarFile)
        {
            Beatmap compatibility;

            using (var stream = File.OpenRead(beatmapFile))
            using (var reader = new LineBufferedReader(stream))
                compatibility = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);

            using var sidecar = File.OpenRead(sidecarFile);
            IBeatmap decoded = DodgeBeatmapSerializer.DecodeCompatibilityBeatmap(compatibility, sidecar);
            decoded.BeatmapInfo.Ruleset = new DodgeRuleset().RulesetInfo;

            foreach (DodgeHitObject hitObject in decoded.HitObjects.OfType<DodgeHitObject>())
                hitObject.ApplyDefaults(decoded.ControlPointInfo, decoded.Difficulty);

            return decoded;
        }

    }
}
