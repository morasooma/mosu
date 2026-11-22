using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Select.Hinamizawa;
using osu.Game.Tests.Visual;
using osu.Game.Scoring;
using osu.Game.Database;

namespace osu.Game.Tests.Beatmaps
{
    public partial class HinamizawaWorkingBeatmapTest : ScreenTestScene
    {
        protected override bool UseFreshStoragePerRun => true;
        private string directory = null!;
        private HinamizawaWorkingBeatmap working = null!;

        private void SetUpArchive()
        {
            directory = Path.Combine(Path.GetTempPath(), "hinai-working-" + Guid.NewGuid());
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory, "map.osu"), """
                osu file format v14

                [General]
                AudioFilename: audio.wav
                Mode: 0

                [Metadata]
                Title: Mirror song
                Artist: Artist
                Creator: Mapper
                Version: Hard
                BeatmapID: 456
                BeatmapSetID: 123

                [Difficulty]
                HPDrainRate: 5
                CircleSize: 4
                OverallDifficulty: 5
                ApproachRate: 5
                SliderMultiplier: 1.4
                SliderTickRate: 1

                [TimingPoints]
                0,500,4,2,1,100,1,0

                [HitObjects]
                256,192,1000,1,0,0:0:0:0:
                256,192,2000,1,0,0:0:0:0:
                """);
        }

        [Test]
        public void TestPlayableWithoutImportingSet()
        {
            AddStep("create archive", SetUpArchive);
            AddStep("create working map", () => working = HinamizawaWorkingBeatmap.Create(directory,
                new APIBeatmap { OnlineID = 456, OnlineBeatmapSetID = 123, RulesetID = 0 }, new OsuRuleset().RulesetInfo,
                Dependencies.Get<AudioManager>(), Dependencies.Get<GameHost>()));
            AddAssert("decoded objects", () => working.Beatmap.HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("gameplay conversion", () => working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo).HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("hashes computed", () => working.BeatmapInfo.MD5Hash.Length == 32 && working.BeatmapInfo.Hash.Length == 64);
            AddAssert("correct difficulty", () => working.BeatmapInfo.OnlineID, () => Is.EqualTo(456));
            AddAssert("path resolved", () => working.BeatmapInfo.Path, () => Is.EqualTo("map.osu"));
            AddAssert("no set imported", () => Dependencies.Get<RealmAccess>().Run(r => r.All<BeatmapSetInfo>().Count()), () => Is.EqualTo(0));
            AddAssert("custom skin loads", () => working.Skin != null);
            System.Threading.Tasks.Task? scoreAnchor = null;
            AddStep("create score metadata anchor", () =>
            {
                var realm = Dependencies.Get<RealmAccess>();
                realm.Write(r =>
                {
                    if (!r.All<RulesetInfo>().Any(info => info.ShortName == working.BeatmapInfo.Ruleset.ShortName))
                        r.Add(new OsuRuleset().RulesetInfo);
                });
                scoreAnchor = HinamizawaPlayer.EnsureScoreBeatmapAsync(realm, working.BeatmapInfo);
            });
            AddUntilStep("anchor stored", () => scoreAnchor?.IsCompleted == true);
            AddAssert("anchor succeeded", () => scoreAnchor?.IsCompletedSuccessfully == true);
            AddAssert("anchor has no library set", () => Dependencies.Get<RealmAccess>().Run(r => r.Find<BeatmapInfo>(working.BeatmapInfo.ID)?.BeatmapSet == null));
            AddAssert("still no set imported", () => Dependencies.Get<RealmAccess>().Run(r => r.All<BeatmapSetInfo>().Count()), () => Is.EqualTo(0));
            AddStep("release archive", TearDownArchive);
        }

        [Test]
        public void TestMissingCatalogueChecksumStillComputesActualHashesAndVerifiesIdentity()
        {
            AddStep("create archive", SetUpArchive);
            AddStep("accept missing expected checksum", () => working = HinamizawaWorkingBeatmap.Create(directory,
                new APIBeatmap { OnlineID = 456, OnlineBeatmapSetID = 123, RulesetID = 0, Checksum = string.Empty }, new OsuRuleset().RulesetInfo,
                Dependencies.Get<AudioManager>(), Dependencies.Get<GameHost>()));
            AddAssert("actual hashes computed", () => working.BeatmapInfo.MD5Hash.Length == 32 && working.BeatmapInfo.Hash.Length == 64);
            AddStep("release archive", TearDownArchive);
        }

        [Test]
        public void TestWrongRulesetRejectedEvenWithoutCatalogueChecksum()
        {
            AddStep("create archive", SetUpArchive);
            AddAssert("reject wrong mode", () =>
            {
                Assert.Throws<InvalidDataException>(() => HinamizawaWorkingBeatmap.Create(directory,
                    new APIBeatmap { OnlineID = 456, OnlineBeatmapSetID = 123, RulesetID = 1 }, new OsuRuleset().RulesetInfo,
                    Dependencies.Get<AudioManager>(), Dependencies.Get<GameHost>()));
                return true;
            });
            AddStep("release archive", TearDownArchive);
        }

        [Test]
        public void TestChecksumMismatchRejected()
        {
            AddStep("create archive", SetUpArchive);
            AddAssert("reject stale archive", () =>
            {
                Assert.Throws<InvalidDataException>(() => HinamizawaWorkingBeatmap.Create(directory,
                    new APIBeatmap { OnlineID = 456, OnlineBeatmapSetID = 123, Checksum = "wrong" }, new OsuRuleset().RulesetInfo,
                    Dependencies.Get<AudioManager>(), Dependencies.Get<GameHost>()));
                return true;
            });
            AddStep("release archive", TearDownArchive);
        }

        [TearDown]
        public void TearDownArchive()
        {
            working?.Dispose();
            working = null!;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }
}
