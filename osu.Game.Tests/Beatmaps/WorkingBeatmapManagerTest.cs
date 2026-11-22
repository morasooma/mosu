// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Extensions;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Models;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Beatmaps
{
    [HeadlessTest]
    public partial class WorkingBeatmapManagerTest : OsuTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private BeatmapManager beatmaps = null!;

        private BeatmapSetInfo importedSet = null!;

        private RulesetStore rulesetStore = null!;

        private readonly APIUser mapper = new APIUser
        {
            Id = 8128,
            Username = "separate-set-mapper",
        };

        [BackgroundDependencyLoader]
        private void load(GameHost host, AudioManager audio, RulesetStore rulesets)
        {
            rulesetStore = new RealmRulesetStore(Realm);
            Dependencies.Cache(beatmaps = new BeatmapManager(LocalStorage, Realm, null, audio, Resources, host, Beatmap.Default));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("import beatmap", () =>
            {
                beatmaps.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                importedSet = beatmaps.GetAllUsableBeatmapSets()
                                      .First(set => set.Beatmaps.Any(beatmap => beatmap.Ruleset.OnlineID == 0));
            });
        }

        [Test]
        public void TestGetWorkingBeatmap() => AddStep("run test", () =>
        {
            Assert.That(beatmaps.GetWorkingBeatmap(importedSet.Beatmaps.First()), Is.Not.Null);
        });

        [Test]
        public void TestCarouselArtworkDoesNotPopulatePlayableCache() => AddStep("resolve preview without gameplay graph", () =>
        {
            var info = importedSet.Beatmaps.First();
            var preview = ((IWorkingBeatmapCache)beatmaps).GetCarouselBackgroundBeatmap(info);
            Assert.That(preview.BeatmapInfo.ID, Is.EqualTo(info.ID));
            Assert.That(preview.TrackLoaded, Is.False);

            var playable = beatmaps.GetWorkingBeatmap(info);
            Assert.That(playable, Is.Not.SameAs(preview));
            Assert.That(playable.Beatmap, Is.Not.Null);
            Assert.That(beatmaps.GetWorkingBeatmap(info), Is.SameAs(playable));
        });

        [Test]
        public void TestCarouselArtworkHandlesMissingBackground() => AddStep("missing artwork is optional", () =>
        {
            var preview = ((IWorkingBeatmapCache)beatmaps).GetCarouselBackgroundBeatmap(new BeatmapInfo());
            Assert.That(preview.GetPanelBackground(), Is.Null);
            Assert.That(preview.GetLegacyPreviewBackground(50), Is.Null);
            Assert.That(preview.TrackLoaded, Is.False);
        });

        [Test]
        public void TestCarouselArtworkForEmptySetUsesDefault() => AddStep("empty set keeps nullable cache contract", () =>
        {
            var info = new BeatmapSetInfo().Beatmaps.MinBy(b => b.OnlineID);
            Assert.That(((IWorkingBeatmapCache)beatmaps).GetCarouselBackgroundBeatmap(info), Is.SameAs(beatmaps.GetWorkingBeatmap(null)));
        });

        [Test]
        public void TestCarouselArtworkHandlesNullMetadata() => AddStep("corrupt metadata has no artwork", () =>
        {
            var info = importedSet.Beatmaps.First();
            var metadata = info.Metadata;
            try
            {
                Realm.Write(r => r.Find<BeatmapInfo>(info.ID)!.Metadata = null!);
                var preview = ((IWorkingBeatmapCache)beatmaps).GetCarouselBackgroundBeatmap(info);
                Assert.That(preview.GetPanelBackground(), Is.Null);
            }
            finally
            {
                // The visual fixture reuses imported maps between its test cases.
                // Corruption injection must not poison subsequent gameplay tests.
                Realm.Write(r => r.Find<BeatmapInfo>(info.ID)!.Metadata = r.Add(metadata));
            }
        });

        [Test]
        public void TestDecodeWithUnavailableRuleset() => AddStep("decode without installed ruleset", () =>
        {
            var info = importedSet.Beatmaps.First().Clone();
            info.Ruleset = new RulesetInfo
            {
                ShortName = "unavailable-test-ruleset",
                Available = false,
            };

            var working = beatmaps.GetWorkingBeatmap(info);
            Assert.That(working.Beatmap, Is.Not.Null);
            Assert.That(working.Beatmap.ControlPointInfo.TimingPoints, Is.Not.Empty);
        });

        [TestCase("")]
        [TestCase("stale-online-checksum")]
        public void TestQueryOnlineBeatmapUsesMatchChecksumBeforeMetadataProcessing(string onlineChecksum) => AddStep("resolve fresh import using match checksum", () =>
        {
            var info = importedSet.Beatmaps.First();
            string originalOnlineChecksum = info.OnlineMD5Hash;

            try
            {
                Realm.Write(r => r.Find<BeatmapInfo>(info.ID)!.OnlineMD5Hash = onlineChecksum);

                Assert.Multiple(() =>
                {
                    Assert.That(beatmaps.QueryOnlineBeatmapId(info.OnlineID, info.MD5Hash)?.ID, Is.EqualTo(info.ID));
                    Assert.That(beatmaps.QueryOnlineBeatmapId(info.OnlineID, new string('0', 32)), Is.Null, "A different match revision must still be rejected.");
                    Assert.That(beatmaps.QueryOnlineBeatmapId(int.MaxValue, info.MD5Hash), Is.Null, "The online ID must still match.");
                    Assert.That(beatmaps.QueryOnlineBeatmapId(info.OnlineID), Is.Null, "Without a match checksum, online metadata remains authoritative.");
                });

                var working = beatmaps.GetWorkingBeatmap(beatmaps.QueryOnlineBeatmapId(info.OnlineID, info.MD5Hash)!);
                Assert.That(working.Beatmap.HitObjects, Is.Not.Empty, "The newly imported file must actually decode.");
                Assert.That(working.BeatmapInfo.MD5Hash, Is.EqualTo(info.MD5Hash));

                Realm.Write(r => r.Find<BeatmapSetInfo>(info.BeatmapSet!.ID)!.DeletePending = true);
                Assert.That(beatmaps.QueryOnlineBeatmapId(info.OnlineID, info.MD5Hash), Is.Null, "Deleted imports must not become playable.");
            }
            finally
            {
                Realm.Write(r =>
                {
                    r.Find<BeatmapInfo>(info.ID)!.OnlineMD5Hash = originalOnlineChecksum;
                    r.Find<BeatmapSetInfo>(info.BeatmapSet!.ID)!.DeletePending = false;
                });
            }
        });

        [Test]
        public void TestQueryOnlineBeatmapFallsBackToChecksumVerifiedStableBeatmap() => AddStep("run test", () =>
        {
            string beatmapPath = Path.GetTempFileName();

            try
            {
                File.WriteAllText(beatmapPath, "stable-only beatmap");
                string checksum;
                using (var stream = File.OpenRead(beatmapPath))
                    checksum = stream.ComputeMD5Hash();

                var set = new BeatmapSetInfo { OnlineID = 654321 };
                var stableBeatmap = new BeatmapInfo
                {
                    OnlineID = 7654321,
                    MD5Hash = checksum,
                    BeatmapSet = set,
                    Ruleset = rulesetStore.AvailableRulesets.First(),
                };
                set.Beatmaps.Add(stableBeatmap);

                StablePathManager.Replace(
                    new Dictionary<Guid, string> { [stableBeatmap.ID] = beatmapPath },
                    new Dictionary<Guid, string>(),
                    [set]);

                Assert.Multiple(() =>
                {
                    Assert.That(beatmaps.QueryOnlineBeatmapId(stableBeatmap.OnlineID, checksum)?.ID, Is.EqualTo(stableBeatmap.ID));
                    Assert.That(beatmaps.QueryOnlineBeatmapId(stableBeatmap.OnlineID, new string('0', 32)), Is.Null);
                });

                File.Delete(beatmapPath);
                Assert.That(beatmaps.QueryOnlineBeatmapId(stableBeatmap.OnlineID, checksum), Is.Null);
            }
            finally
            {
                StablePathManager.Replace(new Dictionary<Guid, string>(), new Dictionary<Guid, string>());
                File.Delete(beatmapPath);
            }
        });

        [Test]
        public void TestCreateAndReuseSeparateRulesetSet() => AddStep("run test", () =>
        {
            WorkingBeatmap reference = beatmaps.GetWorkingBeatmap(importedSet.Beatmaps.First());
            RulesetInfo catchRuleset = rulesetStore.AvailableRulesets.Single(ruleset => ruleset.OnlineID == 2);

            WorkingBeatmap first = beatmaps.CreateNewDifficultyInSeparateRulesetSet(importedSet, reference, catchRuleset, mapper);
            BeatmapSetInfo firstSet = first.BeatmapSetInfo;
            RealmNamedFileUsage originFile = firstSet.Files.Single(file => RulesetBeatmapSetOrigin.IsOriginFilename(file.Filename));
            RulesetBeatmapSetOrigin origin;

            using (Stream stream = first.GetStream(originFile.File.GetStoragePath())!)
            using (var reader = new StreamReader(stream))
                origin = JsonConvert.DeserializeObject<RulesetBeatmapSetOrigin>(reader.ReadToEnd())!;

            Assert.Multiple(() =>
            {
                Assert.That(firstSet.ID, Is.Not.EqualTo(importedSet.ID));
                Assert.That(firstSet.OnlineID, Is.LessThanOrEqualTo(0));
                Assert.That(first.BeatmapInfo.OnlineID, Is.LessThanOrEqualTo(0));
                Assert.That(firstSet.Beatmaps, Has.Count.EqualTo(1));
                Assert.That(firstSet.Beatmaps.Single().Ruleset.OnlineID, Is.EqualTo(catchRuleset.OnlineID));
                Assert.That(firstSet.Files.Any(file => RulesetBeatmapSetOrigin.IsOriginFilename(file.Filename)), Is.True);
                Assert.That(firstSet.Files.Count(file => file.Filename.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)), Is.EqualTo(1));
                Assert.That(firstSet.Files.Any(file => file.Filename.EndsWith(".ruleset.json", StringComparison.OrdinalIgnoreCase)), Is.False);
                Assert.That(firstSet.GetFile(first.Metadata.BackgroundFile), Is.Not.Null);
                Assert.That(first.Metadata.AudioFile, Is.EqualTo(reference.Metadata.AudioFile));
                Assert.That(first.Metadata.BackgroundFile, Is.EqualTo(reference.Metadata.BackgroundFile));
                Assert.That(first.Beatmap.ControlPointInfo.TimingPoints.Count, Is.EqualTo(reference.Beatmap.ControlPointInfo.TimingPoints.Count));
                Assert.That(first.Metadata.Author.OnlineID, Is.EqualTo(mapper.OnlineID));
                Assert.That(first.Metadata.Author.Username, Is.EqualTo(mapper.Username));
                Assert.That(origin.OriginalAuthorOnlineID, Is.EqualTo(reference.Metadata.Author.OnlineID));
                Assert.That(origin.OriginalAuthorUsername, Is.EqualTo(reference.Metadata.Author.Username));
                Assert.That(origin.MapperOnlineID, Is.EqualTo(mapper.OnlineID));
                Assert.That(origin.MapperUsername, Is.EqualTo(mapper.Username));
            });

            origin.Version = 1;
            origin.MapperOnlineID = 0;
            origin.MapperUsername = string.Empty;
            byte[] legacyOriginData = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(origin));

            using (var legacyOriginStream = new MemoryStream(legacyOriginData, false))
                beatmaps.AddFile(firstSet, legacyOriginStream, originFile.Filename);

            Realm.Write(realm =>
            {
                BeatmapSetInfo managedSet = realm.Find<BeatmapSetInfo>(firstSet.ID)!;

                foreach (BeatmapInfo beatmap in managedSet.Beatmaps)
                {
                    beatmap.Metadata.Author.OnlineID = reference.Metadata.Author.OnlineID;
                    beatmap.Metadata.Author.Username = reference.Metadata.Author.Username;
                }
            });

            BeatmapSetInfo legacySet = beatmaps.FindSeparateRulesetBeatmapSet(importedSet, catchRuleset)!;
            string legacyOriginPath = Realm.Run(realm =>
            {
                BeatmapSetInfo managedSet = realm.Find<BeatmapSetInfo>(legacySet.ID)!;
                return managedSet.Files.Single(file => RulesetBeatmapSetOrigin.IsOriginFilename(file.Filename)).File.GetStoragePath();
            });
            RulesetBeatmapSetOrigin storedLegacyOrigin;

            using (Stream stream = beatmaps.GetWorkingBeatmap(legacySet.Beatmaps.First()).GetStream(legacyOriginPath)!)
            using (var reader = new StreamReader(stream))
                storedLegacyOrigin = JsonConvert.DeserializeObject<RulesetBeatmapSetOrigin>(reader.ReadToEnd())!;

            Assert.That(storedLegacyOrigin.Version, Is.EqualTo(1));
            Assert.That(legacySet.Beatmaps.All(beatmap => beatmap.Metadata.Author.Username == reference.Metadata.Author.Username), Is.True);

            WorkingBeatmap second = beatmaps.CreateNewDifficultyInSeparateRulesetSet(importedSet, reference, catchRuleset, mapper);

            Assert.Multiple(() =>
            {
                Assert.That(second.BeatmapSetInfo.ID, Is.EqualTo(firstSet.ID));
                Assert.That(second.BeatmapSetInfo.Beatmaps, Has.Count.EqualTo(2));
                Assert.That(beatmaps.FindSeparateRulesetBeatmapSet(importedSet, catchRuleset)?.ID, Is.EqualTo(firstSet.ID));
                Assert.That(second.BeatmapSetInfo.Beatmaps.All(beatmap => beatmap.Metadata.Author.OnlineID == mapper.OnlineID), Is.True);
                Assert.That(second.BeatmapSetInfo.Beatmaps.All(beatmap => beatmap.Metadata.Author.Username == mapper.Username), Is.True);
            });
        });

        [Test]
        public void TestExtractRulesetDifficultiesFromMixedSet() => AddStep("run test", () =>
        {
            WorkingBeatmap reference = beatmaps.GetWorkingBeatmap(importedSet.Beatmaps.First());
            RulesetInfo catchRuleset = rulesetStore.AvailableRulesets.Single(ruleset => ruleset.OnlineID == 2);
            int originalSourceDifficultyCount = importedSet.Beatmaps.Count;
            int originalRulesetDifficultyCount = importedSet.Beatmaps.Count(beatmap => beatmap.Ruleset.OnlineID == catchRuleset.OnlineID);
            int existingTargetDifficultyCount = beatmaps.FindSeparateRulesetBeatmapSet(importedSet, catchRuleset)?.Beatmaps.Count ?? 0;
            WorkingBeatmap mixedDifficulty = beatmaps.CreateNewDifficulty(importedSet, reference, catchRuleset);
            string[] extractedDifficultyNames = importedSet.Beatmaps
                                                          .Where(beatmap => beatmap.Ruleset.OnlineID == catchRuleset.OnlineID)
                                                          .Select(beatmap => beatmap.DifficultyName)
                                                          .ToArray();

            WorkingBeatmap extracted = beatmaps.ExtractRulesetDifficultiesToSeparateSet(importedSet, catchRuleset, mapper);
            BeatmapSetInfo sourceAfterExtraction = Realm.Run(realm => realm.Find<BeatmapSetInfo>(importedSet.ID)!.Detach());

            Assert.Multiple(() =>
            {
                Assert.That(sourceAfterExtraction.Beatmaps, Has.Count.EqualTo(originalSourceDifficultyCount - originalRulesetDifficultyCount));
                Assert.That(sourceAfterExtraction.Beatmaps, Has.None.Matches<BeatmapInfo>(beatmap => beatmap.Ruleset.OnlineID == catchRuleset.OnlineID));
                Assert.That(extracted.BeatmapSetInfo.ID, Is.Not.EqualTo(importedSet.ID));
                Assert.That(extracted.BeatmapSetInfo.Beatmaps, Has.Count.EqualTo(existingTargetDifficultyCount + extractedDifficultyNames.Length));
                Assert.That(extracted.BeatmapSetInfo.Beatmaps.Select(beatmap => beatmap.DifficultyName), Does.Contain(mixedDifficulty.BeatmapInfo.DifficultyName));
                Assert.That(extracted.BeatmapSetInfo.Beatmaps.Select(beatmap => beatmap.DifficultyName), Is.SupersetOf(extractedDifficultyNames));
                Assert.That(extracted.BeatmapInfo.OnlineID, Is.LessThanOrEqualTo(0));
                Assert.That(beatmaps.FindSeparateRulesetBeatmapSet(importedSet, catchRuleset)?.ID, Is.EqualTo(extracted.BeatmapSetInfo.ID));
                Assert.That(extracted.BeatmapSetInfo.Beatmaps.All(beatmap => beatmap.Metadata.Author.OnlineID == mapper.OnlineID), Is.True);
            });
        });

        [Test]
        public void TestCachedRetrievalNoFiles() => AddStep("run test", () =>
        {
            var beatmap = importedSet.Beatmaps.First();

            Assert.That(beatmap.BeatmapSet?.Files, Is.Empty);

            var first = beatmaps.GetWorkingBeatmap(beatmap);
            var second = beatmaps.GetWorkingBeatmap(beatmap);

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.BeatmapInfo.BeatmapSet?.Files, Has.Count.GreaterThan(0));
        });

        [Test]
        public void TestCachedManagedRetrievalAvoidsDetachingAgain() => AddStep("cached managed lookup", () =>
        {
            Realm.Run(realm =>
            {
                var managed = realm.Find<BeatmapInfo>(importedSet.Beatmaps.First().ID)!;
                var first = beatmaps.GetWorkingBeatmap(managed);
                // Warm cache enumeration before measuring. This path used to copy the entire
                // managed set/files even though GetWorkingBeatmap returned the cached instance.
                _ = beatmaps.GetWorkingBeatmap(managed);
                long start = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10; i++)
                    _ = beatmaps.GetWorkingBeatmap(managed);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
                TestContext.WriteLine($"Ten managed cached lookups allocated {allocated} B");
                Assert.That(allocated, Is.LessThan(16 * 1024));
                Assert.That(beatmaps.GetWorkingBeatmap(managed), Is.SameAs(first));
                Assert.That(first.BeatmapInfo.IsManaged, Is.False);
            });
        });

        [Test]
        public void TestCachedRetrievalWithFiles() => AddStep("run test", () =>
        {
            var beatmap = Realm.Run(r => r.Find<BeatmapInfo>(importedSet.Beatmaps.First().ID)!.Detach());

            Assert.That(beatmap.BeatmapSet?.Files, Has.Count.GreaterThan(0));

            var first = beatmaps.GetWorkingBeatmap(beatmap);
            var second = beatmaps.GetWorkingBeatmap(beatmap);

            Assert.That(first, Is.SameAs(second));
            Assert.That(first.BeatmapInfo.BeatmapSet?.Files, Has.Count.GreaterThan(0));
        });

        [Test]
        public void TestForcedRefetchRetrievalNoFiles() => AddStep("run test", () =>
        {
            var beatmap = importedSet.Beatmaps.First();

            Assert.That(beatmap.BeatmapSet?.Files, Is.Empty);

            var first = beatmaps.GetWorkingBeatmap(beatmap);
            var second = beatmaps.GetWorkingBeatmap(beatmap, true);
            Assert.That(first, Is.Not.SameAs(second));
        });

        [Test]
        public void TestForcedRefetchRetrievalWithFiles() => AddStep("run test", () =>
        {
            var beatmap = Realm.Run(r => r.Find<BeatmapInfo>(importedSet.Beatmaps.First().ID)!.Detach());

            Assert.That(beatmap.BeatmapSet?.Files, Has.Count.GreaterThan(0));

            var first = beatmaps.GetWorkingBeatmap(beatmap);
            var second = beatmaps.GetWorkingBeatmap(beatmap, true);
            Assert.That(first, Is.Not.SameAs(second));
        });

        [Test]
        public void TestSavePreservesCollections() => AddStep("run test", () =>
        {
            var beatmap = Realm.Run(r => r.Find<BeatmapInfo>(importedSet.Beatmaps.First().ID)!.Detach());

            var working = beatmaps.GetWorkingBeatmap(beatmap);

            Assert.That(working.BeatmapInfo.BeatmapSet?.Files, Has.Count.GreaterThan(0));

            string initialHash = working.BeatmapInfo.MD5Hash;

            var preserveCollection = new BeatmapCollection("test contained");
            preserveCollection.BeatmapMD5Hashes.Add(initialHash);

            var noNewCollection = new BeatmapCollection("test not contained");

            Realm.Write(r =>
            {
                r.Add(preserveCollection);
                r.Add(noNewCollection);
            });

            Assert.That(preserveCollection.BeatmapMD5Hashes, Does.Contain(initialHash));
            Assert.That(noNewCollection.BeatmapMD5Hashes, Does.Not.Contain(initialHash));

            beatmaps.Save(working.BeatmapInfo, working.GetPlayableBeatmap(new OsuRuleset().RulesetInfo));

            string finalHash = working.BeatmapInfo.MD5Hash;

            Assert.That(finalHash, Is.Not.SameAs(initialHash));

            Assert.That(preserveCollection.BeatmapMD5Hashes, Does.Not.Contain(initialHash));
            Assert.That(preserveCollection.BeatmapMD5Hashes, Does.Contain(finalHash));
            Assert.That(noNewCollection.BeatmapMD5Hashes, Does.Not.Contain(finalHash));
        });
    }
}
