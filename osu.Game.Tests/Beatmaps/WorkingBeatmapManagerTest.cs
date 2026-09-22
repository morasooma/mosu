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
            rulesetStore = rulesets;
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
