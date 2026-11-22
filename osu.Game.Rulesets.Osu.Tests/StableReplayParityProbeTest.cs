using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using osu.Framework.Screens;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Online;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Osu.Tests
{
    [HeadlessTest]
    public partial class StableReplayParityProbeTest : RateAdjustedBeatmapTestScene
    {
        private bool previousStable;

        [SetUp]
        public void SaveProfile() => previousStable = MosuServerEnvironment.UsesStableProtocol;

        [TearDown]
        public void RestoreProfile() => MosuServerEnvironment.UsesStableProtocol = previousStable;

        [Test]
        public void CompareRecordedAndSimulatedJudgements()
        {
            string? replayPath = Environment.GetEnvironmentVariable("MOSU_PARITY_REPLAY");
            string? mapPath = Environment.GetEnvironmentVariable("MOSU_PARITY_BEATMAP");
            if (replayPath == null || mapPath == null)
                Assert.Ignore("Set MOSU_PARITY_REPLAY and MOSU_PARITY_BEATMAP to run the local replay parity probe.");

            Score score = null!;
            ScoreInfo recorded = null!;
            ReplayPlayer player = null!;
            var results = new List<JudgementResult>();
            AddStep("decode exact map and replay", () =>
            {
                MosuServerEnvironment.UsesStableProtocol = true;
                using var mapStream = new LineBufferedReader(File.OpenRead(mapPath!));
                var map = Decoder.GetDecoder<Beatmap>(mapStream).Decode(mapStream);
                Ruleset.Value = new OsuRuleset().RulesetInfo;
                Beatmap.Value = CreateWorkingBeatmap(map);
                using var replayStream = File.OpenRead(replayPath!);
                score = new ProbeDecoder(Beatmap.Value, Convert.ToHexString(MD5.HashData(File.ReadAllBytes(mapPath!)))).Parse(replayStream);
                recorded = score.ScoreInfo.DeepClone();
                if (Environment.GetEnvironmentVariable("MOSU_PARITY_NO_NOTELOCK") == "1")
                    foreach (var classic in score.ScoreInfo.Mods.OfType<osu.Game.Rulesets.Osu.Mods.OsuModClassic>())
                        classic.ClassicNoteLock.Value = false;
                TestContext.Progress.WriteLine($"Recorded: score={score.ScoreInfo.LegacyTotalScore}, combo={score.ScoreInfo.MaxCombo}, stats={string.Join(",", score.ScoreInfo.Statistics)}; mods={string.Join(",", score.ScoreInfo.Mods.Select(m => m.Acronym))}");
                SelectedMods.Value = score.ScoreInfo.Mods;
                player = new ReplayPlayer(score);
                player.OnLoadComplete += _ => player.GameplayState.ScoreProcessor.NewJudgement += result =>
                {
                    results.Add(result);
                    if (result.HitObject is osu.Game.Rulesets.Osu.Objects.OsuHitObject hitObject)
                        TestContext.Progress.WriteLine($"RESULT {hitObject.GetType().Name} {hitObject.StartTime} {hitObject.GetEndTime()} {hitObject.StackedPosition.X} {hitObject.StackedPosition.Y} {hitObject.Radius} {result.Type} {result.TimeOffset}");
                    if (result is osu.Game.Rulesets.Osu.Judgements.OsuSliderJudgementResult sliderResult && result.Type != Rulesets.Scoring.HitResult.Great)
                        TestContext.Progress.WriteLine($"SLIDER {result.HitObject.StartTime}: tracking={string.Join(",", sliderResult.TrackingHistory.Take(12))}");
                    if (result.Type == Rulesets.Scoring.HitResult.Miss || result.Type == Rulesets.Scoring.HitResult.Ok || result.Type == Rulesets.Scoring.HitResult.SmallTickMiss)
                        TestContext.Progress.WriteLine($"JUDGEMENT {result.Type} object={result.HitObject.GetType().Name} start={result.HitObject.StartTime} offset={result.TimeOffset}");
                };
                LoadScreen(player);
            });
            AddUntilStep("player loaded", () => player.IsCurrentScreen());
            AddStep("report clocks", () =>
            {
                // Parity must include the whole replay, even if a mechanics mismatch causes
                // the simulated health processor to fail before the recorded play did.
                player.GameplayState.ScoreProcessor.ApplyNewJudgementsWhenFailed = true;
                TestContext.Progress.WriteLine($"CLOCK test={Clock.Rate}");
            });
            AddUntilStep("simulation complete", () => player.GameplayState.HasCompleted);
            AddStep("report simulation before results substitution", () =>
            {
                var simulated = new ScoreInfo();
                player.GameplayState.ScoreProcessor.PopulateScore(simulated);
                TestContext.Progress.WriteLine($"Simulated: score={simulated.TotalScore}, combo={simulated.MaxCombo}, stats={string.Join(",", simulated.Statistics)}");
                foreach (var group in results.Where(r => r.Type.AffectsAccuracy()).GroupBy(r => r.HitObject.GetType().Name))
                    TestContext.Progress.WriteLine($"GROUP {group.Key}: {string.Join(",", group.GroupBy(r => r.Type).Select(g => $"{g.Key}={g.Count()}"))}");
                Assert.Multiple(() =>
                {
                    Assert.That(simulated.TotalScore, Is.EqualTo(recorded.LegacyTotalScore), "Recorded ScoreV1");
                    Assert.That(simulated.MaxCombo, Is.EqualTo(recorded.MaxCombo), "Recorded maximum combo");
                    foreach (var pair in recorded.Statistics.Where(p => p.Key == HitResult.Great || p.Key == HitResult.Ok || p.Key == HitResult.Meh || p.Key == HitResult.Miss))
                        Assert.That(simulated.Statistics.GetValueOrDefault(pair.Key), Is.EqualTo(pair.Value), $"Recorded {pair.Key}");
                });
            });
        }

        private class ProbeDecoder : LegacyScoreDecoder
        {
            private readonly WorkingBeatmap beatmap;
            private readonly string beatmapHash;

            public ProbeDecoder(WorkingBeatmap beatmap, string beatmapHash)
            {
                this.beatmap = beatmap;
                this.beatmapHash = beatmapHash;
            }

            protected override Ruleset GetRuleset(int rulesetId)
            {
                Assert.That(rulesetId, Is.Zero, "This probe supports osu!standard replays only.");
                return new OsuRuleset();
            }

            protected override WorkingBeatmap GetBeatmap(string md5Hash)
            {
                Assert.That(md5Hash, Is.EqualTo(beatmapHash).IgnoreCase, "Replay beatmap checksum");
                return beatmap;
            }
        }
    }
}
