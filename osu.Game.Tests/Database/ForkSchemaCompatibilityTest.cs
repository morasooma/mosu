// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Scoring;
using Realms;
using Realms.Schema;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class ForkSchemaCompatibilityTest
    {
        [TestCase(52, false)]
        [TestCase(52, true)]
        [TestCase(53, false)]
        [TestCase(53, true)]
        public void TestForkSchemaIsDowngradedAndDataIsPreserved(int sourceSchemaVersion, bool upstreamAlreadyMovedDatabase)
        {
            SynchronizationContext? previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                using var storage = new TemporaryNativeStorage($"fork-schema-compatibility-{Guid.NewGuid():N}");
                using var forkDataStore = new ForkDataStore(storage);

                const string realmFilename = "client.realm";

                string path = storage.GetFullPath(realmFilename, true);
                string sourcePath = upstreamAlreadyMovedDatabase
                    ? storage.GetFullPath("client_newer_version.realm", true)
                    : path;
                RealmConfiguration configuration = createForkSchemaConfiguration(sourcePath, (ulong)sourceSchemaVersion);
                Guid scoreId = Guid.Empty;

                if (upstreamAlreadyMovedDatabase)
                {
                    using var freshUpstreamRealm = Realm.GetInstance(new RealmConfiguration(path) { SchemaVersion = 52 });
                    freshUpstreamRealm.Write(() => freshUpstreamRealm.Add(new BeatmapSetInfo()));
                }

                using (var realm = Realm.GetInstance(configuration))
                {
                    realm.Write(() =>
                    {
                        ScoreInfo score = realm.Add(new ScoreInfo());
                        scoreId = score.ID;
                        if (sourceSchemaVersion == 52)
                            score.DynamicApi.Set(nameof(ScoreInfo.TagCoopReplayJson), "{\"Players\":[],\"Frames\":[]}");

                        if (sourceSchemaVersion == 53)
                        {
                            Assert.That(score.BeatmapInfo, Is.Not.Null);
                            score.BeatmapInfo!.DynamicApi.Set(nameof(BeatmapInfo.MaxPerformancePoints), 123.45);
                        }
                    });
                }

                using (var realmAccess = new RealmAccess(storage, "client.realm"))
                {
                    Assert.That(realmAccess.Run(realm => realm.Find<ScoreInfo>(scoreId)), Is.Not.Null);
                    if (sourceSchemaVersion == 52)
                        Assert.That(realmAccess.Run(realm => realm.Find<ScoreInfo>(scoreId)!.TagCoopReplay), Is.Not.Null);

                    if (sourceSchemaVersion == 53)
                        Assert.That(ForkDataStore.Instance!.GetPP(realmAccess.Run(realm => realm.Find<ScoreInfo>(scoreId)!.BeatmapInfo!.ID)), Is.EqualTo(123.45));
                }

                var upstreamConfiguration = new RealmConfiguration(path) { SchemaVersion = 52 };
                Assert.DoesNotThrow(() =>
                {
                    using var realm = Realm.GetInstance(upstreamConfiguration);
                    Assert.That(realm.Find<ScoreInfo>(scoreId), Is.Not.Null);
                });

                Assert.That(storage.GetFiles(string.Empty, "*_fork_schema_backup.realm"), Is.Not.Empty);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        [Test]
        public void TestForeignNewerVersionDatabaseIsMovedAsideAndCanBeConverted()
        {
            SynchronizationContext? previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                using var storage = new TemporaryNativeStorage($"foreign-newer-db-{Guid.NewGuid():N}");
                using var forkDataStore = new ForkDataStore(storage);

                const string realmFilename = "client.realm";
                string path = storage.GetFullPath(realmFilename, true);
                Guid scoreId = Guid.Empty;

                // Create a database at a newer hypothetical upstream schema version without fork-only columns.
                using (var newer = Realm.GetInstance(new RealmConfiguration(path) { SchemaVersion = 54 }))
                {
                    newer.Write(() => scoreId = newer.Add(new ScoreInfo()).ID);
                }

                using (var realmAccess = new RealmAccess(storage, realmFilename))
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(realmAccess.NewerVersionDatabaseSchemaVersion, Is.EqualTo(54));
                        Assert.That(realmAccess.NewerVersionDatabaseFilename, Is.EqualTo("client_newer_version.realm"));
                    });

                    // The game keeps running on a fresh database...
                    Assert.That(realmAccess.Run(r => r.All<ScoreInfo>().Count()), Is.Zero);

                    // Normal game startup imports the bundled triangles beatmap into this fallback database before
                    // the user can accept the conversion dialog. It must not make the fallback look like conflicting
                    // user data or the converted database would never be installed on restart.
                    realmAccess.Write(r => r.Add(new BeatmapSetInfo()));

                    // ...and the foreign database was moved aside without any fork migration taking place.
                    Assert.That(storage.Exists("client_newer_version.realm"), Is.True);
                    Assert.That(storage.GetFiles(string.Empty, "*_fork_schema_backup.realm"), Is.Empty);

                    Assert.That(realmAccess.TryConvertNewerVersionedDatabase(), Is.True);
                    Assert.That(storage.Exists("client_converted.realm"), Is.True);
                }

                // On the next startup the converted database is installed and its data is readable at the current schema.
                using (var realmAccess = new RealmAccess(storage, realmFilename))
                {
                    Assert.That(realmAccess.Run(r => r.Find<ScoreInfo>(scoreId)), Is.Not.Null);
                    Assert.That(storage.Exists("client_converted.realm"), Is.False);
                    Assert.That(storage.GetFiles(string.Empty, "*_before_converted_install.realm"), Is.Not.Empty);
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        [Test]
        public void TestForeignNewerVersionDatabaseConversionRefusedWhenUserDataExists()
        {
            SynchronizationContext? previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                using var storage = new TemporaryNativeStorage($"foreign-newer-db-conflict-{Guid.NewGuid():N}");
                using var forkDataStore = new ForkDataStore(storage);

                const string realmFilename = "client.realm";
                string path = storage.GetFullPath(realmFilename, true);

                using (var newer = Realm.GetInstance(new RealmConfiguration(path) { SchemaVersion = 54 }))
                    newer.Write(() => newer.Add(new ScoreInfo()));

                using (var realmAccess = new RealmAccess(storage, realmFilename))
                {
                    Assert.That(realmAccess.NewerVersionDatabaseSchemaVersion, Is.EqualTo(54));
                    Assert.That(realmAccess.TryConvertNewerVersionedDatabase(), Is.True);
                }

                // Simulate the user recording scores on the fresh database before the converted one gets installed.
                using (var current = Realm.GetInstance(new RealmConfiguration(path) { SchemaVersion = 52 }))
                    current.Write(() => current.Add(new ScoreInfo()));

                // The next startup must refuse to replace a database which already contains user data.
                using (var realmAccess = new RealmAccess(storage, realmFilename))
                {
                    Assert.That(realmAccess.Run(r => r.All<ScoreInfo>().Count()), Is.EqualTo(1));
                    Assert.That(storage.Exists("client_converted.realm"), Is.True);
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        [Test]
        public void TestForcedForeignDatabaseConversionOverridesScoreProtectionAndKeepsBackup()
        {
            SynchronizationContext? previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                using var storage = new TemporaryNativeStorage($"forced-foreign-newer-db-{Guid.NewGuid():N}");
                using var forkDataStore = new ForkDataStore(storage);

                const string realmFilename = "client.realm";
                string path = storage.GetFullPath(realmFilename, true);
                Guid foreignScoreId = Guid.Empty;
                Guid fallbackScoreId = Guid.Empty;

                using (var newer = Realm.GetInstance(new RealmConfiguration(path) { SchemaVersion = 54 }))
                    newer.Write(() => foreignScoreId = newer.Add(new ScoreInfo()).ID);

                // The first startup moves the foreign database aside and creates the fallback database.
                using (var realmAccess = new RealmAccess(storage, realmFilename))
                    Assert.That(realmAccess.NewerVersionDatabaseSchemaVersion, Is.EqualTo(54));

                // A score in the fallback causes normal automatic recovery to leave the backup alone.
                using (var current = Realm.GetInstance(new RealmConfiguration(path) { SchemaVersion = 52 }))
                    current.Write(() => fallbackScoreId = current.Add(new ScoreInfo()).ID);

                using (var realmAccess = new RealmAccess(storage, realmFilename))
                {
                    Assert.That(realmAccess.NewerVersionDatabaseSchemaVersion, Is.Null);
                    Assert.That(realmAccess.TryForceConvertNewerVersionedDatabase(), Is.True);
                    Assert.That(storage.Exists("client_converted.realm"), Is.True);
                    Assert.That(storage.Exists("client_forced_conversion.pending"), Is.True);
                }

                // The explicit debug action overrides score protection, while preserving the fallback as a backup.
                using (var realmAccess = new RealmAccess(storage, realmFilename))
                {
                    Assert.Multiple(() =>
                    {
                        Assert.That(realmAccess.Run(r => r.Find<ScoreInfo>(foreignScoreId)), Is.Not.Null);
                        Assert.That(realmAccess.Run(r => r.Find<ScoreInfo>(fallbackScoreId)), Is.Null);
                        Assert.That(storage.Exists("client_converted.realm"), Is.False);
                        Assert.That(storage.Exists("client_forced_conversion.pending"), Is.False);
                        Assert.That(storage.GetFiles(string.Empty, "*_before_converted_install.realm"), Is.Not.Empty);
                    });
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        [Test]
        public void TestConvertForeignTachyonDatabaseSample()
        {
            string? samplePath = Environment.GetEnvironmentVariable("OSU_FOREIGN_REALM_SAMPLE");

            if (string.IsNullOrEmpty(samplePath) || !File.Exists(samplePath))
                Assert.Ignore($"Set OSU_FOREIGN_REALM_SAMPLE to a client.realm created by a newer osu! version to run this test (got: {samplePath}).");

            SynchronizationContext? previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                using var storage = new TemporaryNativeStorage($"foreign-real-sample-{Guid.NewGuid():N}");
                using var forkDataStore = new ForkDataStore(storage);

                const string realmFilename = "client.realm";
                File.Copy(samplePath, storage.GetFullPath(realmFilename, true));

                using (var realmAccess = new RealmAccess(storage, realmFilename))
                {
                    Assert.That(realmAccess.NewerVersionDatabaseSchemaVersion, Is.Not.Null);
                    TestContext.Out.WriteLine($"Detected foreign database with schema version {realmAccess.NewerVersionDatabaseSchemaVersion}");

                    Assert.That(realmAccess.TryConvertNewerVersionedDatabase(), Is.True);
                }

                using (var realmAccess = new RealmAccess(storage, realmFilename))
                {
                    int beatmapSets = realmAccess.Run(r => r.All<BeatmapSetInfo>().Count());
                    int beatmaps = realmAccess.Run(r => r.All<BeatmapInfo>().Count());
                    int scores = realmAccess.Run(r => r.All<ScoreInfo>().Count());
                    TestContext.Out.WriteLine($"Converted database contents: {beatmapSets} beatmap sets, {beatmaps} beatmaps, {scores} scores");

                    Assert.That(beatmapSets, Is.GreaterThan(0));
                }
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        [Test]
        public void TestCurrentForkDatabaseTakesPriorityOverStaleNewerVersionBackup()
        {
            SynchronizationContext? previousContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);

            try
            {
                using var storage = new TemporaryNativeStorage($"fork-schema-stale-backup-{Guid.NewGuid():N}");
                using var forkDataStore = new ForkDataStore(storage);

                Guid currentScoreId;
                Guid staleScoreId = Guid.Empty;

                using (var staleRealm = Realm.GetInstance(createForkSchemaConfiguration(storage.GetFullPath("client_newer_version.realm", true), 53)))
                {
                    staleRealm.Write(() => staleScoreId = staleRealm.Add(new ScoreInfo()).ID);
                }

                using (var currentRealm = Realm.GetInstance(createForkSchemaConfiguration(storage.GetFullPath("client.realm", true), 52)))
                {
                    currentScoreId = Guid.Empty;
                    currentRealm.Write(() => currentScoreId = currentRealm.Add(new ScoreInfo()).ID);
                }

                using var realmAccess = new RealmAccess(storage, "client.realm");
                Assert.That(realmAccess.Run(realm => realm.Find<ScoreInfo>(currentScoreId)), Is.Not.Null);
                Assert.That(realmAccess.Run(realm => realm.Find<ScoreInfo>(staleScoreId)), Is.Null);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previousContext);
            }
        }

        [Test]
        public void TestDodgeDifficultyPersistsWithCalculatorVersion()
        {
            using var storage = new TemporaryNativeStorage($"fork-dodge-difficulty-{Guid.NewGuid():N}");
            Guid beatmapId = Guid.NewGuid();

            using (var forkDataStore = new ForkDataStore(storage))
            {
                forkDataStore.SetDodgeDifficulty(beatmapId, 6.42, 123);
                Assert.That(forkDataStore.HasDodgeDifficulty(beatmapId, 123), Is.True);
                Assert.That(forkDataStore.HasDodgeDifficulty(beatmapId, 124), Is.False);

                var beatmap = new BeatmapInfo(new RulesetInfo { ShortName = RulesetInfo.DODGE_MODE_SHORTNAME })
                {
                    ID = beatmapId,
                    StarRating = -1,
                };

                Assert.That(BeatmapDifficultyCache.GetInitialStarRating(beatmap), Is.EqualTo(6.42));
            }

            using (var reloaded = new ForkDataStore(storage))
            {
                Assert.That(reloaded.GetDodgeDifficulty(beatmapId).StarRating, Is.EqualTo(6.42));
                Assert.That(reloaded.GetDodgeDifficulty(beatmapId).DifficultyVersion, Is.EqualTo(123));
            }
        }

        [Test]
        public void TestFullDodgeDifficultyAttributesPersistWithChecksum()
        {
            using var storage = new TemporaryNativeStorage($"fork-dodge-attributes-{Guid.NewGuid():N}");
            Guid beatmapId = Guid.NewGuid();
            const string checksum = "0123456789abcdef0123456789abcdef";
            var attributes = new DifficultyAttributes
            {
                StarRating = 4.26,
                MaxCombo = 518,
            };

            using (var forkDataStore = new ForkDataStore(storage))
            {
                forkDataStore.SetDodgeDifficulty(beatmapId, checksum, attributes, 456);

                Assert.Multiple(() =>
                {
                    Assert.That(forkDataStore.HasDodgeDifficultyAttributes(beatmapId, 456, checksum), Is.True);
                    Assert.That(forkDataStore.HasDodgeDifficultyAttributes(beatmapId, 455, checksum), Is.False);
                    Assert.That(forkDataStore.HasDodgeDifficultyAttributes(beatmapId, 456, "different"), Is.False);
                    Assert.That(forkDataStore.GetDodgeDifficulty(beatmapId).AttributesJson, Does.Contain("\"max_combo\":518"));
                });
            }

            using (var reloaded = new ForkDataStore(storage))
            {
                ForkDataStore.DodgeDifficultyData persisted = reloaded.GetDodgeDifficulty(beatmapId);

                Assert.Multiple(() =>
                {
                    Assert.That(reloaded.HasDodgeDifficultyAttributes(beatmapId, 456, checksum), Is.True);
                    Assert.That(persisted.StarRating, Is.EqualTo(4.26));
                    Assert.That(persisted.BeatmapChecksum, Is.EqualTo(checksum));
                    Assert.That(persisted.HasFullAttributes, Is.True);
                });
            }
        }

        private static RealmConfiguration createForkSchemaConfiguration(string path, ulong sourceSchemaVersion)
        {
            var configuration = new RealmConfiguration(path) { SchemaVersion = sourceSchemaVersion };
            var schema = new RealmSchema.Builder();

            foreach (ObjectSchema objectSchema in configuration.Schema)
            {
                if (sourceSchemaVersion == 52 && objectSchema.Name == "Score")
                {
                    ObjectSchema.Builder scoreSchema = objectSchema.GetBuilder();
                    scoreSchema.Add(Property.Primitive(nameof(ScoreInfo.TagCoopReplayJson), RealmValueType.String, isNullable: true));
                    schema.Add(scoreSchema);
                }
                else if (sourceSchemaVersion == 53 && objectSchema.Name == "Beatmap")
                {
                    ObjectSchema.Builder beatmapSchema = objectSchema.GetBuilder();
                    beatmapSchema.Add(Property.Primitive(nameof(BeatmapInfo.MaxPerformancePoints), RealmValueType.Double));
                    schema.Add(beatmapSchema);
                }
                else
                    schema.Add(objectSchema);
            }

            configuration.Schema = schema.Build();
            return configuration;
        }
    }
}
