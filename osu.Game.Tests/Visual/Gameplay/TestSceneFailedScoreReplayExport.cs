using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Input.Events;
using osu.Framework.Input.States;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.IO.Archives;
using osu.Game.Online;
using osu.Game.Overlays.Notifications;
using osu.Game.Replays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    [HeadlessTest]
    public partial class TestSceneFailedScoreReplayExport : OsuTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private BeatmapManager beatmaps = null!;
        private ScoreManager scores = null!;
        private RulesetStore rulesets = null!;
        private int exportCount;
        private int failureCount;

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio)
        {
            rulesets = new RealmRulesetStore(Realm);
            beatmaps = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default);
            scores = new ScoreManager(rulesets, () => beatmaps, LocalStorage, Realm, API)
            {
                PostNotification = notification =>
                {
                    if (notification is ProgressNotification)
                        exportCount++;
                    else if (notification is SimpleNotification)
                        failureCount++;
                }
            };
            Dependencies.Cache(Realm);
            Dependencies.Cache(scores);
        }

        [SetUpSteps]
        public void SetUpSteps() => AddStep("reset exports", () =>
        {
            exportCount = failureCount = 0;
            var storage = LocalStorage.GetStorageForDirectory("exports");
            foreach (string file in storage.GetFiles(string.Empty, "*.osr"))
                storage.Delete(file);
        });

        [TearDown]
        public void TearDown() => StablePathManager.Replace(new Dictionary<Guid, string>(), new Dictionary<Guid, string>());

        [Test]
        public void TestStableOnlyFailedScoreCanBeReadAndExported()
        {
            ScoreInfo info = null!;
            Task export = null!;

            AddStep("save failed stable-only replay", () => info = importStableScore());
            AddAssert("map is absent from normal library", () => beatmaps.QueryBeatmap(b => b.MD5Hash == info.BeatmapInfo!.MD5Hash), () => Is.Null);
            AddStep("read recorded frames", () =>
            {
                var score = scores.GetScore(info)!;
                Assert.That(score.ScoreInfo.Rank, Is.EqualTo(ScoreRank.F));
                Assert.That(score.Replay!.Frames, Has.Count.EqualTo(1));
                Assert.That(score.Replay.Frames[0].Time, Is.EqualTo(1000));
            });
            AddStep("export replay", () => export = scores.Export(info));
            AddUntilStep("export finished", () => export.IsCompleted);
            AddStep("check exported frames", () =>
            {
                export.GetAwaiter().GetResult();
                Assert.That(exportCount, Is.EqualTo(1));
                Assert.That(failureCount, Is.Zero);
                var storage = LocalStorage.GetStorageForDirectory("exports");
                using var stream = storage.GetStream(storage.GetFiles(string.Empty, "*.osr").Single())!;
                var decoded = new DatabasedLegacyScoreDecoder(rulesets, beatmaps, info.BeatmapInfo).Parse(stream);
                Assert.That(decoded.Replay!.Frames, Has.Count.EqualTo(1));
                Assert.That(decoded.ScoreInfo.Rank, Is.EqualTo(ScoreRank.F));
            });
        }

        [Test]
        public void TestDoubleF2WhileImportingOnlyExportsOnce()
        {
            var pendingImport = new TaskCompletionSource<ScoreInfo>();
            SaveFailedScoreButton button = null!;
            int imports = 0;

            AddStep("create failed score button", () => Child = button = new SaveFailedScoreButton(() =>
            {
                imports++;
                return pendingImport.Task;
            }));
            AddUntilStep("button loaded", () => button.IsLoaded);
            AddStep("press F2 twice", () =>
            {
                button.ChildrenOfType<DownloadButton>().Single().State.Value = DownloadState.NotDownloaded;
                button.OnPressed(new KeyBindingPressEvent<GlobalAction>(new InputState(), GlobalAction.ExportReplay));
                button.OnPressed(new KeyBindingPressEvent<GlobalAction>(new InputState(), GlobalAction.ExportReplay));
            });
            AddUntilStep("single import started", () => imports == 1);
            AddStep("complete import", () => pendingImport.SetResult(importStableScore()));
            AddUntilStep("export completed", () => LocalStorage.GetStorageForDirectory("exports").GetFiles(string.Empty, "*.osr").Any());
            AddWaitStep("allow duplicate callbacks", 3);
            AddAssert("only one export", () => exportCount, () => Is.EqualTo(1));
            AddAssert("no export errors", () => failureCount, () => Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestMissingOrMismatchedStableMapDoesNotCrashExport(bool mismatchedHash)
        {
            ScoreInfo info = null!;
            Task export = null!;
            AddStep("save replay then invalidate map", () =>
            {
                info = importStableScore();
                if (mismatchedHash)
                    Realm.Write(r => r.Find<BeatmapInfo>(info.BeatmapInfo!.ID)!.MD5Hash = new string('0', 32));
                else
                    StablePathManager.Replace(new Dictionary<Guid, string>(), new Dictionary<Guid, string>());
                info = scores.Query(s => s.ID == info.ID)!;
            });
            AddStep("export without synchronous exception", () => export = scores.Export(info));
            AddUntilStep("export handled", () => export.IsCompleted);
            AddStep("failure reported without unhandled task", () =>
            {
                export.GetAwaiter().GetResult();
                Assert.That(failureCount, Is.EqualTo(1));
                Assert.That(exportCount, Is.Zero);
                Assert.That(LocalStorage.GetStorageForDirectory("exports").GetFiles(string.Empty, "*.osr"), Is.Empty);
            });
        }

        private ScoreInfo importStableScore()
        {
            const string filename = "stable-only.osu";
            using (var writer = new StreamWriter(LocalStorage.CreateFileSafely(filename)))
            {
                writer.Write("""
                             osu file format v14

                             [General]
                             Mode: 0

                             [Metadata]
                             Title: Stable replay regression
                             Artist: Test
                             Creator: Test
                             Version: Failed

                             [Difficulty]
                             HPDrainRate:5
                             CircleSize:4
                             OverallDifficulty:5
                             ApproachRate:5
                             SliderMultiplier:1.4
                             SliderTickRate:1

                             [TimingPoints]
                             0,500,4,2,0,100,1,0

                             [HitObjects]
                             256,192,1000,1,0,0:0:0:0:
                             """);
            }

            var set = new BeatmapSetInfo();
            var info = new BeatmapInfo(new OsuRuleset().RulesetInfo) { BeatmapSet = set };
            set.Beatmaps.Add(info);
            using (var stream = LocalStorage.GetStream(filename)!)
            {
                info.MD5Hash = stream.ComputeMD5Hash();
                info.Hash = stream.ComputeSHA2Hash();
            }
            StablePathManager.Replace(new Dictionary<Guid, string> { [info.ID] = LocalStorage.GetFullPath(filename) }, new Dictionary<Guid, string>());
            var working = beatmaps.GetWorkingBeatmap(info);
            var score = new Score
            {
                ScoreInfo = new ScoreInfo
                {
                    Ruleset = info.Ruleset,
                    BeatmapInfo = info,
                    BeatmapHash = info.Hash,
                    Rank = ScoreRank.F,
                    Date = DateTimeOffset.UtcNow,
                    Statistics = new Dictionary<HitResult, int> { [HitResult.Miss] = 1 },
                    MaximumStatistics = new Dictionary<HitResult, int> { [HitResult.Great] = 1 },
                },
                Replay = new Replay { Frames = { new OsuReplayFrame(1000, new Vector2(256, 192)) } }
            };
            using var replayStream = new MemoryStream();
            new LegacyScoreEncoder(score, working.Beatmap).Encode(replayStream);
            byte[] replayBytes = replayStream.ToArray();
            // Reproduce the original lookup failure: the file exists, but a plain
            // database lookup cannot see a map played directly from stable.
            using (var originalDecodeStream = new MemoryStream(replayBytes))
                Assert.Throws<LegacyScoreDecoder.BeatmapNotFoundException>(() => new DatabasedLegacyScoreDecoder(rulesets, beatmaps).Parse(originalDecodeStream));

            using var archive = new ByteArrayArchiveReader(replayBytes, "failed.osr");
            return scores.Import(score.ScoreInfo, archive)!.Value.Detach();
        }
    }
}
