using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Models;
using osu.Game.IO.Archives;
using osu.Game.IO;
using osu.Game.Beatmaps.Formats;
using osu.Game.Extensions;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ServerBeatmapArchiveEncodingTest
    {
        [TestCase("音楽・初音ミク.wav")]
        [TestCase("песня – 日本語 🎵.wav")]
        [TestCase("cafe\u0301 - 한글.wav")]
        public void ServerExportsPreserveAudioFilenameForClientAndServer(string filename)
        {
            using var storage = new TemporaryNativeStorage("unicode-archive-export");
            var set = new BeatmapSetInfo();
            set.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = new string('a', 64) }, filename));
            foreach (var exporter in new LegacyArchiveExporter<BeatmapSetInfo>[] { new InMemoryRawExporter(storage), new InMemoryServerExporter(storage) })
            {
                using var output = new MemoryStream();
                exporter.ExportToStream(set, output, null);
                byte[] bytes = output.ToArray();
                using var server = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
                Assert.That(server.Entries.Single().FullName, Is.EqualTo(filename), "server ZIP reader");
                using var client = new ZipArchiveReader(new MemoryStream(bytes));
                Assert.That(client.Filenames.Single(), Is.EqualTo(filename), "client ZIP reader");
                using var audio = client.GetStream(filename);
                Assert.That(audio, Is.Not.Null);
                Assert.That(audio.ReadByte(), Is.EqualTo(42));
            }
        }

        [TestCase("音楽・初音ミク.wav")]
        [TestCase("песня – 日本語 🎵.wav")]
        [TestCase("cafe\u0301 - 한글.wav")]
        public void RealExportKeepsDecodedAudioReferenceResolvable(string filename)
        {
            using var storage = new TemporaryNativeStorage("unicode-beatmap-export");
            var files = storage.GetStorageForDirectory("files");
            var mapFile = new RealmFile { Hash = new string('b', 64) };
            var audioFile = new RealmFile { Hash = new string('c', 64) };
            using (var stream = files.GetStream(mapFile.GetStoragePath(), FileAccess.Write, FileMode.Create))
            using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                writer.Write($"osu file format v14\n[General]\nAudioFilename:{filename}\nMode:0\n[Metadata]\nTitle:日本語の曲\nArtist:Test\nCreator:Test\nVersion:Normal\n[Difficulty]\nHPDrainRate:5\nCircleSize:4\nOverallDifficulty:5\nApproachRate:5\nSliderMultiplier:1.4\nSliderTickRate:1\n[TimingPoints]\n0,500,4,2,0,100,1,0\n[HitObjects]\n64,192,1000,1,0,0:0:0:0:\n");
            using (var stream = files.GetStream(audioFile.GetStoragePath(), FileAccess.Write, FileMode.Create))
                stream.WriteByte(42);
            var set = new BeatmapSetInfo();
            set.Files.Add(new RealmNamedFileUsage(mapFile, "譜面.osu"));
            set.Files.Add(new RealmNamedFileUsage(audioFile, filename));
            set.Beatmaps.Add(new BeatmapInfo { Hash = mapFile.Hash, BeatmapSet = set });

            foreach (var exporter in new LegacyArchiveExporter<BeatmapSetInfo>[] { new RawBeatmapExporter(storage), new ServerBeatmapExporter(storage) })
            {
                using var output = new MemoryStream();
                exporter.ExportToStream(set, output, null);
                using var archive = new ZipArchiveReader(new MemoryStream(output.ToArray()));
                using var reader = new LineBufferedReader(archive.GetStream("譜面.osu"));
                var decoded = osu.Game.Beatmaps.Formats.Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
                Assert.That(decoded.Metadata.AudioFile, Is.EqualTo(filename));
                using var audio = archive.GetStream(decoded.Metadata.AudioFile);
                Assert.That(audio, Is.Not.Null);
                Assert.That(audio.ReadByte(), Is.EqualTo(42));
            }
        }

        private sealed class InMemoryServerExporter(osu.Framework.Platform.Storage storage) : ServerBeatmapExporter(storage)
        {
            protected override Stream GetFileContents(BeatmapSetInfo model, INamedFileUsage file) => new MemoryStream(new byte[] { 42 });
        }

        private sealed class InMemoryRawExporter(osu.Framework.Platform.Storage storage) : RawBeatmapExporter(storage)
        {
            protected override Stream GetFileContents(BeatmapSetInfo model, INamedFileUsage file) => new MemoryStream(new byte[] { 42 });
        }
    }
}
