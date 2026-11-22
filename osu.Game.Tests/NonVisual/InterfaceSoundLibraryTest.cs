using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class InterfaceSoundLibraryTest
    {
        private TemporaryNativeStorage storage = null!;
        private InterfaceSoundLibrary library = null!;
        private static readonly string[] events = { "UI/check-on", "UI/default-select" };

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("interface-sound-library");
            library = new InterfaceSoundLibrary(storage, events);
        }

        [TearDown]
        public void TearDown() => storage.Dispose();

        [Test]
        public void TestBareZipImportAndRestart()
        {
            using var zip = archive(("UI/check-on.wav", wave()));
            var pack = library.Import(zip, "My sounds");
            Assert.That(pack.Name, Is.EqualTo("My sounds"));
            Assert.That(pack.Base, Is.EqualTo(InterfaceSoundLibrary.DefaultPack));
            var restored = new InterfaceSoundLibrary(storage, events).Find(pack.Id)!;
            Assert.That(restored.Sounds.Keys, Is.EquivalentTo(new[] { "UI/check-on" }));
            Assert.That(library.ReadSound(restored.Sounds["UI/check-on"]), Is.EqualTo(wave()));
        }

        [Test]
        public void TestManifestImportCreatesFreshIdAndMigratesRemovedBase()
        {
            var manifest = new InterfaceSoundPack { Name = "Old custom", Base = "lazer-2017" };
            manifest.Sounds["UI/check-on"] = "click.wav";
            using var zip = archive(("pack.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest))), ("click.wav", wave()));
            var pack = library.Import(zip, "unused");
            Assert.That(pack.Id, Is.Not.EqualTo(manifest.Id));
            Assert.That(pack.Base, Is.EqualTo(InterfaceSoundLibrary.DefaultPack));
        }

        [Test]
        public void TestOverrideBuiltInIsCopyAndCanBeReset()
        {
            using var input = new MemoryStream(wave());
            var pack = library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "sound.wav", input);
            Assert.That(pack.Id, Is.Not.EqualTo(InterfaceSoundLibrary.DefaultPack));
            Assert.That(pack.Base, Is.EqualTo(InterfaceSoundLibrary.DefaultPack));
            Assert.That(library.Find(InterfaceSoundLibrary.DefaultPack), Is.Null);
            library.Reset(pack.Id, "UI/check-on");
            Assert.That(library.Find(pack.Id)!.Sounds, Is.Empty);
        }

        [Test]
        public void TestOverridesUpdateSelectedCustomPackAndResetAll()
        {
            using var first = new MemoryStream(wave());
            var pack = library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "a.wav", first);
            using var second = new MemoryStream(wave());
            var updated = library.Replace(pack.Id, "UI/default-select", "b.wav", second);
            Assert.That(updated.Id, Is.EqualTo(pack.Id));
            Assert.That(updated.Sounds, Has.Count.EqualTo(2));
            library.ResetAll(pack.Id);
            Assert.That(library.Find(pack.Id)!.Sounds, Is.Empty);
        }

        [TestCase("../UI/check-on.wav")]
        [TestCase("/UI/check-on.wav")]
        [TestCase("UI\\check-on.wav")]
        [TestCase("C:/UI/check-on.wav")]
        [TestCase("Gameplay/normal-hitnormal.wav")]
        [TestCase("UI/check-on.dll")]
        public void TestUnsafeOrUnknownArchiveEntryRejected(string path)
        {
            using var zip = archive((path, wave()));
            Assert.Throws<InvalidDataException>(() => library.Import(zip, "Unsafe"));
            Assert.That(library.GetPacks(), Is.Empty);
        }

        [Test]
        public void TestDuplicateEventRejectedAtomically()
        {
            using var zip = archive(("UI/check-on.wav", wave()), ("UI/check-on.wav", wave()));
            Assert.Throws<InvalidDataException>(() => library.Import(zip, "Duplicate"));
            Assert.That(library.GetPacks(), Is.Empty);
        }

        [Test]
        public void TestMalformedAudioDoesNotChangeExistingPack()
        {
            using var valid = new MemoryStream(wave());
            var pack = library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "a.wav", valid);
            string original = pack.Sounds["UI/check-on"];
            using var invalid = new MemoryStream(new byte[500]);
            Assert.Throws<InvalidDataException>(() => library.Replace(pack.Id, "UI/check-on", "a.wav", invalid));
            Assert.That(library.Find(pack.Id)!.Sounds["UI/check-on"], Is.EqualTo(original));
        }

        [Test]
        public void TestDurationAndDecodedSizeLimits()
        {
            Assert.Throws<InvalidDataException>(() => InterfaceSoundLibrary.ValidateAudio("long.wav", wave(31)));
            Assert.Throws<InvalidDataException>(() => InterfaceSoundLibrary.ValidateAudio("empty.wav", Array.Empty<byte>()));
            Assert.Throws<InvalidDataException>(() => InterfaceSoundLibrary.ValidateAudio("big.wav", new byte[InterfaceSoundLibrary.MaximumFileBytes + 1]));
        }

        [Test]
        public void TestCorruptManifestDoesNotPreventStartup()
        {
            using (var file = storage.GetStorageForDirectory("interface-sounds").GetStream(Guid.NewGuid().ToString("N") + ".json", FileAccess.Write, FileMode.Create))
                file.Write(Encoding.UTF8.GetBytes("{\"Sounds\":null}"));
            Assert.That(library.GetPacks(), Is.Empty);
        }

        [Test]
        public void TestMp3ReplacementSurvivesRestart()
        {
            using var resources = TestResources.GetStore();
            byte[] mp3 = resources.Get("Resources/Samples/test-sample.mp3");
            using var input = new MemoryStream(mp3);
            var pack = library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "key.mp3", input);
            var restored = new InterfaceSoundLibrary(storage, events).Find(pack.Id)!;
            Assert.That(restored.Sounds["UI/check-on"], Does.EndWith(".mp3"));
            Assert.That(library.ReadSound(restored.Sounds["UI/check-on"]), Is.EqualTo(mp3));
        }

        [Test]
        public void TestArchiveEntryCountLimit()
        {
            using var input = new MemoryStream();
            using (var zip = new ZipArchive(input, ZipArchiveMode.Create, leaveOpen: true))
            {
                for (int i = 0; i <= InterfaceSoundLibrary.MaximumEntries; i++)
                    zip.CreateEntry($"empty-{i}/");
            }
            input.Position = 0;
            Assert.Throws<InvalidDataException>(() => library.Import(input, "Too many"));
            Assert.That(library.GetPacks(), Is.Empty);
        }

        [Test]
        public void TestUnknownBaseAndMissingSoundsRejected()
        {
            using var invalidBase = archive(("pack.json", Encoding.UTF8.GetBytes("{\"Base\":\"../outside\"}")));
            Assert.Throws<InvalidDataException>(() => library.Import(invalidBase, "Invalid"));
            using var nullSound = archive(("pack.json", Encoding.UTF8.GetBytes("{\"Sounds\":{\"UI/check-on\":null}}")));
            Assert.Throws<InvalidDataException>(() => library.Import(nullSound, "Invalid"));
        }

        [Test]
        public void TestMissingFileAndExecutableRejected()
        {
            var manifest = new InterfaceSoundPack();
            manifest.Sounds["UI/check-on"] = "missing.wav";
            using var zip = archive(("pack.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest))));
            Assert.Throws<InvalidDataException>(() => library.Import(zip, "Missing"));
        }

        [Test]
        public void TestPackSelectionPersists()
        {
            using (var config = new OsuConfigManager(storage))
            {
                Assert.That(config.Get<string>(OsuSetting.InterfaceSoundPack), Is.EqualTo(InterfaceSoundLibrary.DefaultPack));
                config.SetValue(OsuSetting.InterfaceSoundPack, "test-pack-id");
                config.Save();
            }
            using var restored = new OsuConfigManager(storage);
            Assert.That(restored.Get<string>(OsuSetting.InterfaceSoundPack), Is.EqualTo("test-pack-id"));
        }

        [TestCase("Gameplay/normal-hitnormal", false)]
        [TestCase("normal-hitnormal", false)]
        [TestCase("Tracks/music", false)]
        [TestCase("UI/check-on", true)]
        [TestCase("SongSelectClassic/select-difficulty", true)]
        public void TestScope(string name, bool expected) => Assert.That(InterfaceSoundManager.IsInterfaceEvent(name), Is.EqualTo(expected));

        [Test]
        public void TestPortableExportIncludesFallbacksAndNoticesAndSurvivesReexport()
        {
            using var input = new MemoryStream(wave(2));
            var pack = library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "click.wav", input);
            using var output = new MemoryStream();
            library.Export(output, pack.Id, key => (key + ".wav", wave()), new[] { Encoding.UTF8.GetBytes("Recording notice") });
            Assert.That(library.GetPacks(), Has.Count.EqualTo(1), "Export must not install a new pack");
            output.Position = 0;
            var imported = library.Import(output, "unused");
            Assert.That(imported.Id, Is.Not.EqualTo(pack.Id));
            Assert.That(imported.Sounds.Keys, Is.EquivalentTo(events));
            Assert.That(library.ReadSound(imported.Sounds["UI/check-on"]), Is.EqualTo(wave(2)));
            Assert.That(library.ReadSound(imported.Sounds["UI/default-select"]), Is.EqualTo(wave()));
            Assert.That(imported.Notices, Has.Count.EqualTo(1));
            using var reexport = new MemoryStream();
            library.Export(reexport, imported.Id, _ => throw new InvalidOperationException("All events should be in the portable pack"));
            reexport.Position = 0;
            var restored = library.Import(reexport, "again");
            Assert.That(restored.Sounds, Is.EquivalentTo(imported.Sounds));
            Assert.That(restored.Notices, Is.EqualTo(imported.Notices));
        }

        [Test]
        public void TestIntentionalSilenceIsPortable()
        {
            byte[] empty = wave(0);
            using var input = new MemoryStream(empty);
            var pack = library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "silence.wav", input);
            using var output = new MemoryStream();
            library.Export(output, pack.Id, key => (key + ".wav", wave()));
            output.Position = 0;
            Assert.That(library.ReadSound(library.Import(output, "silence").Sounds["UI/check-on"]), Is.EqualTo(empty));
            empty[32] = 0;
            Assert.Throws<InvalidDataException>(() => InterfaceSoundLibrary.ValidateAudio("bad.wav", empty));
        }

        [TestCase("Wrong.Format", 1)]
        [TestCase("Morasooma.Sounds", 2)]
        public void TestWrongPortableFormatOrVersionRejected(string format, int version)
        {
            var manifest = new InterfaceSoundPack { Format = format, Version = version };
            manifest.Sounds["UI/check-on"] = "a.wav";
            using var input = archive(("pack.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest))), ("a.wav", wave()));
            Assert.Throws<InvalidDataException>(() => library.Import(input, "invalid"));
            Assert.That(library.GetPacks(), Is.Empty);
        }

        [Test]
        public void TestCaseCollisionsSymlinksAndUnexpectedFilesRejected()
        {
            using var collision = archive(("UI/check-on.wav", wave()), ("ui/CHECK-ON.wav", wave()));
            Assert.Throws<InvalidDataException>(() => library.Import(collision, "collision"));
            using var link = new MemoryStream();
            using (var zip = new ZipArchive(link, ZipArchiveMode.Create, true))
            {
                var entry = zip.CreateEntry("UI/check-on.wav");
                entry.ExternalAttributes = unchecked((int)0xa1ff0000);
                using var file = entry.Open();
                file.Write(wave());
            }
            link.Position = 0;
            Assert.Throws<InvalidDataException>(() => library.Import(link, "link"));
            var manifest = new InterfaceSoundPack();
            manifest.Sounds["UI/check-on"] = "a.wav";
            using var extra = archive(("pack.json", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest))), ("a.wav", wave()), ("extra.wav", wave()));
            Assert.Throws<InvalidDataException>(() => library.Import(extra, "extra"));
            Assert.That(library.GetPacks(), Is.Empty);
        }

        [Test]
        public void TestModifiedContentAddressedFileRejected()
        {
            using var input = new MemoryStream(wave());
            var pack = library.Replace(InterfaceSoundLibrary.DefaultPack, "UI/check-on", "a.wav", input);
            string blob = pack.Sounds["UI/check-on"];
            File.WriteAllBytes(storage.GetFullPath("interface-sounds/files/" + blob), wave(2));
            Assert.Throws<InvalidDataException>(() => library.ReadSound(blob));
        }

        public static byte[] wave(int seconds = 1)
        {
            using var output = new MemoryStream();
            using var writer = new BinaryWriter(output);
            int length = 8000 * seconds * 2;
            writer.Write(Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + length);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
            writer.Write((short)1); writer.Write((short)1); writer.Write(8000); writer.Write(16000); writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(length);
            for (int i = 0; i < length / 2; i++)
                writer.Write((short)(Math.Sin(i * 0.2) * 5000));
            return output.ToArray();
        }

        private static MemoryStream archive(params (string Path, byte[] Data)[] entries)
        {
            var output = new MemoryStream();
            using (var zip = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var entry in entries)
                {
                    using var file = zip.CreateEntry(entry.Path).Open();
                    file.Write(entry.Data);
                }
            }
            output.Position = 0;
            return output;
        }
    }
}
