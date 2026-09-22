// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Text;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ConfigurationBackupManagerTest
    {
        [Test]
        public void TestApplyBeforeStorageInitialisationDoesNotCrash()
        {
            Assert.That(ConfigurationBackupManager.ApplyPendingRestore(null), Is.False);
        }

        [Test]
        public void TestBackupErrorStatusCanBeFormatted()
        {
            Assert.That(ForkSettingsStrings.ConfigBackupError("network failure").ToString(),
                Is.EqualTo("Configuration backup error: network failure"));
        }

        [Test]
        public void TestCreateAndApplyBackup()
        {
            using var storage = new TemporaryNativeStorage($"configuration-backup-{Guid.NewGuid():N}");
            write(storage, "input.json", "{\"mouse\":true}");
            write(storage, "game.ini", "Volume = 0.8");
            write(storage, "mosu.ini", "ForkSetting = enabled");
            write(storage, "server_profiles.json", "[{\"id\":\"default\"}]");

            ConfigBackupUpload upload = ConfigurationBackupManager.CreateUpload(storage);

            write(storage, "input.json", "old input");
            write(storage, "game.ini", "old game");
            write(storage, "mosu.ini", "old mosu");
            write(storage, "server_profiles.json", "old profiles");

            ConfigurationBackupManager.StageRestore(storage, new ConfigBackupResponse
            {
                FormatVersion = upload.FormatVersion,
                Files = upload.Files,
            });

            Assert.That(ConfigurationBackupManager.ApplyPendingRestore(storage), Is.True);
            Assert.That(read(storage, "input.json"), Is.EqualTo("{\"mouse\":true}"));
            Assert.That(read(storage, "game.ini"), Is.EqualTo("Volume = 0.8"));
            Assert.That(read(storage, "mosu.ini"), Is.EqualTo("ForkSetting = enabled"));
            Assert.That(read(storage, "server_profiles.json"), Is.EqualTo("[{\"id\":\"default\"}]"));
            Assert.That(ConfigurationBackupManager.ApplyPendingRestore(storage), Is.False);
        }

        [Test]
        public void TestMissingFileInSlotRemovesLocalFile()
        {
            using var storage = new TemporaryNativeStorage($"configuration-backup-missing-{Guid.NewGuid():N}");
            write(storage, "input.json", "local input");
            write(storage, "game.ini", "local game");

            ConfigurationBackupManager.StageRestore(storage, new ConfigBackupResponse
            {
                FormatVersion = ConfigurationBackupManager.FORMAT_VERSION,
                Files = new ConfigBackupFiles
                {
                    Game = Convert.ToBase64String(Encoding.UTF8.GetBytes("server game")),
                },
            });

            Assert.That(ConfigurationBackupManager.ApplyPendingRestore(storage), Is.True);
            Assert.That(storage.Exists("input.json"), Is.False);
            Assert.That(read(storage, "game.ini"), Is.EqualTo("server game"));
        }

        [Test]
        public void TestApplyBackupCanPreserveLocalInputConfiguration()
        {
            using var storage = new TemporaryNativeStorage($"configuration-backup-preserve-input-{Guid.NewGuid():N}");
            write(storage, "input.json", "mobile input");

            ConfigurationBackupManager.StageRestore(storage, new ConfigBackupResponse
            {
                FormatVersion = ConfigurationBackupManager.FORMAT_VERSION,
                Files = new ConfigBackupFiles
                {
                    Input = Convert.ToBase64String(Encoding.UTF8.GetBytes("desktop input")),
                    Game = Convert.ToBase64String(Encoding.UTF8.GetBytes("server game")),
                },
            });

            Assert.That(ConfigurationBackupManager.ApplyPendingRestore(storage, restoreInputConfiguration: false), Is.True);
            Assert.That(read(storage, "input.json"), Is.EqualTo("mobile input"));
            Assert.That(read(storage, "game.ini"), Is.EqualTo("server game"));
            Assert.That(ConfigurationBackupManager.ApplyPendingRestore(storage, restoreInputConfiguration: false), Is.False);
        }

        private static void write(TemporaryNativeStorage storage, string filename, string contents)
        {
            using var stream = storage.CreateFileSafely(filename);
            using var writer = new StreamWriter(stream, Encoding.UTF8);
            writer.Write(contents);
        }

        private static string read(TemporaryNativeStorage storage, string filename)
        {
            using var stream = storage.GetStream(filename, FileAccess.Read, FileMode.Open);
            using var reader = new StreamReader(stream, Encoding.UTF8);
            return reader.ReadToEnd();
        }
    }
}
