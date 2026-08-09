// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Models;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Replays;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Tests.Beatmaps.Formats;
using osu.Game.Tests.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osu.Game.Tests.Resources;
using osu.Game.Users;
using osuTK;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class LegacyScoreExporterTest
    {
        [Test]
        public async Task TestClientModsAreConvertedDuringExport()
        {
            using var storage = new TemporaryNativeStorage("legacy-score-export");

            var exporter = new LegacyScoreExporter(storage);
            ProgressCompletionNotification? convertedNotification = null;
            exporter.PostNotification = n => convertedNotification = n as ProgressCompletionNotification;

            var ruleset = new OsuRuleset().RulesetInfo;
            var scoreInfo = TestResources.CreateTestScoreInfo(ruleset);
            scoreInfo.User = new APIUser { Id = 2, Username = "peppy" };
            scoreInfo.Mods = new Mod[]
            {
                new OsuModHidden(),
                new OsuModMosuAimAssist(),
                new OsuModMosuStaticBpm
                {
                    AdjustPitch = { Value = true },
                    SpeedChange = { Value = 1.8 },
                },
            };

            var beatmap = new TestBeatmap(ruleset);
            var score = new Score
            {
                ScoreInfo = scoreInfo,
                Replay = new Replay
                {
                    Frames =
                    {
                        new OsuReplayFrame(2000, new Vector2(256, 192), OsuAction.LeftButton),
                    }
                }
            };

            await exporter.ExportAsync(score, beatmap);

            var exportStorage = storage.GetStorageForDirectory("exports");
            var exportedFile = exportStorage.GetFiles(string.Empty, "*.osr").Single();

            using var outputStream = exportStorage.GetStream(exportedFile)!;
            var decoded = new LegacyScoreDecoderTest.TestLegacyScoreDecoder().Parse(outputStream);

            Assert.Multiple(() =>
            {
                Assert.That(decoded.ScoreInfo.Mods.OfType<OsuModHidden>().ToArray(), Has.Length.EqualTo(1));
                Assert.That(decoded.ScoreInfo.Mods.OfType<OsuModDoubleTime>().ToArray(), Has.Length.EqualTo(1));
                Assert.That(decoded.ScoreInfo.Mods.OfType<OsuModMosuAimAssist>(), Is.Empty);
                Assert.That(decoded.ScoreInfo.Mods.OfType<OsuModMosuStaticBpm>(), Is.Empty);

                var dt = decoded.ScoreInfo.Mods.OfType<OsuModDoubleTime>().Single();
                Assert.That(dt.SpeedChange.Value, Is.EqualTo(1.8).Within(0.0001));
                Assert.That(dt.AdjustPitch.Value, Is.True);

                Assert.That(convertedNotification, Is.Not.Null);
                Assert.That(convertedNotification!.Text.ToString(), Does.Contain("Client mods were converted"));
            });
        }

        [Test]
        public async Task TestClientModsAreConvertedDuringExportWithNonDefaultTargetBpm()
        {
            using var storage = new TemporaryNativeStorage("legacy-score-export-non-default-bpm");

            var exporter = new LegacyScoreExporter(storage);

            var ruleset = new OsuRuleset().RulesetInfo;
            var scoreInfo = TestResources.CreateTestScoreInfo(ruleset);
            scoreInfo.User = new APIUser { Id = 2, Username = "peppy" };
            scoreInfo.Mods = new Mod[]
            {
                new OsuModMosuStaticBpm
                {
                    TargetBpm = { Value = 220 },
                    SpeedChange = { Value = 1.46 },
                    LockDifficultyAdjust = { Value = true },
                },
            };

            var beatmap = new TestBeatmap(ruleset);
            var score = new Score
            {
                ScoreInfo = scoreInfo,
                Replay = new Replay
                {
                    Frames =
                    {
                        new OsuReplayFrame(2000, new Vector2(256, 192), OsuAction.LeftButton),
                    }
                }
            };

            await exporter.ExportAsync(score, beatmap);

            var exportStorage = storage.GetStorageForDirectory("exports");
            var exportedFile = exportStorage.GetFiles(string.Empty, "*.osr").Single();

            using var outputStream = exportStorage.GetStream(exportedFile)!;
            var decoded = new LegacyScoreDecoderTest.TestLegacyScoreDecoder().Parse(outputStream);

            Assert.Multiple(() =>
            {
                var dt = decoded.ScoreInfo.Mods.OfType<OsuModDoubleTime>().ToArray();
                Assert.That(dt, Has.Length.EqualTo(1));
                Assert.That(dt[0].SpeedChange.Value, Is.EqualTo(1.46).Within(0.0001));

                var da = decoded.ScoreInfo.Mods.OfType<OsuModDifficultyAdjust>().ToArray();
                Assert.That(da, Has.Length.EqualTo(1));
                Assert.That(da[0].ApproachRate.Value, Is.Not.Null);
                Assert.That(da[0].OverallDifficulty.Value, Is.Not.Null);

                var osuRuleset = new OsuRuleset();
                var playbackDifficulty = osuRuleset.GetAdjustedDisplayDifficulty(beatmap.BeatmapInfo, decoded.ScoreInfo.Mods);

                var hitCircle = new HitCircle();
                hitCircle.ApplyDefaults(new ControlPointInfo(), playbackDifficulty);
                double playbackPreempt = hitCircle.TimePreempt;

                var hitWindows = new OsuHitWindows();
                hitWindows.SetDifficulty(playbackDifficulty.OverallDifficulty);
                double playbackGreatWindow = hitWindows.WindowFor(HitResult.Great);

                Assert.That(playbackPreempt, Is.EqualTo(900).Within(1));
                Assert.That(playbackGreatWindow, Is.EqualTo(43.5).Within(1));
            });
        }
    }
}
