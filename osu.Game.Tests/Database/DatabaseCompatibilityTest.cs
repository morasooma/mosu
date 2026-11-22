using System;
using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Database;
using Realms;

namespace osu.Game.Tests.Database
{
    [TestFixture]
    public class DatabaseCompatibilityTest
    {
        [Test]
        public void TestMissingDatabaseIsNotCreatedByInspection()
        {
            using var storage = new TemporaryNativeStorage($"database-check-{Guid.NewGuid():N}");
            RealmAccess.CheckDatabaseCompatibility(storage, "client.realm");
            Assert.That(storage.Exists("client.realm"), Is.False);
        }

        [TestCase(51, false)]
        [TestCase(52, false)]
        [TestCase(53, true)]
        [TestCase(100, true)]
        public void TestInspectionPreservesDatabase(int version, bool blocked)
        {
            using var storage = new TemporaryNativeStorage($"database-check-{Guid.NewGuid():N}");
            string path = storage.GetFullPath("client.realm", true);
            using (Realm.GetInstance(new RealmConfiguration(path) { SchemaVersion = (ulong)version }))
            {
            }

            byte[] original = File.ReadAllBytes(path);
            DateTime modified = File.GetLastWriteTimeUtc(path);
            if (blocked)
                Assert.Throws<DatabaseCompatibilityException>(() => RealmAccess.CheckDatabaseCompatibility(storage, "client.realm"));
            else
                Assert.DoesNotThrow(() => RealmAccess.CheckDatabaseCompatibility(storage, "client.realm"));

            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
            Assert.That(File.GetLastWriteTimeUtc(path), Is.EqualTo(modified));
            Assert.That(storage.GetFiles("", "*_newer_version.realm"), Is.Empty);
        }

        [Test]
        public void TestRealmAccessDoesNotReplaceNewerDatabaseEvenWithRecoveryBackup()
        {
            using var storage = new TemporaryNativeStorage($"database-check-{Guid.NewGuid():N}");
            string path = storage.GetFullPath("client.realm", true);
            using (Realm.GetInstance(new RealmConfiguration(path) { SchemaVersion = 53 }))
            {
            }
            using (Realm.GetInstance(new RealmConfiguration(storage.GetFullPath("client_newer_version.realm", true)) { SchemaVersion = 52 }))
            {
            }

            byte[] original = File.ReadAllBytes(path);
            Assert.Throws<DatabaseCompatibilityException>(() =>
            {
                using var access = new RealmAccess(storage, "client.realm");
            });
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }

        [Test]
        public void TestMissingCurrentDatabaseWithNewerBackupBlocksStartup()
        {
            using var storage = new TemporaryNativeStorage($"database-check-{Guid.NewGuid():N}");
            string path = storage.GetFullPath("client_newer_version.realm", true);
            using (Realm.GetInstance(new RealmConfiguration(path) { SchemaVersion = 53 }))
            {
            }

            byte[] original = File.ReadAllBytes(path);
            Assert.Throws<DatabaseCompatibilityException>(() => RealmAccess.CheckStartupDatabaseCompatibility(storage, "client.realm"));
            Assert.That(storage.Exists("client.realm"), Is.False);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }

        [Test]
        public void TestUnreadableDatabaseIsNotReset()
        {
            using var storage = new TemporaryNativeStorage($"database-check-{Guid.NewGuid():N}");
            string path = storage.GetFullPath("client.realm", true);
            File.WriteAllText(path, "Not a Realm database");
            byte[] original = File.ReadAllBytes(path);
            Assert.Throws<DatabaseCompatibilityException>(() => RealmAccess.CheckDatabaseCompatibility(storage, "client.realm"));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(original));
        }
    }
}
