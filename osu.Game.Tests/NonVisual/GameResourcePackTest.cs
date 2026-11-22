using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class GameResourcePackTest
    {
        private TemporaryNativeStorage storage = null!;
        private ResourceStore<byte[]> catalogue = null!;
        private GameResourcePackLibrary library = null!;
        private byte[] png = null!;

        [SetUp]
        public void SetUp()
        {
            storage = new TemporaryNativeStorage("morars-contract");
            using var image = new Image<Rgba32>(12, 12);
            image[1, 1] = new Rgba32(255, 255, 255);
            using var memory = new MemoryStream();
            image.SaveAsPng(memory);
            png = memory.ToArray();
            catalogue = new ResourceStore<byte[]>(new DataStore(new Dictionary<string, byte[]>
            {
                ["Textures/Icons/example.png"] = png,
                ["Fonts/Nunito/Nunito-Regular.bin"] = new byte[] { 1, 2, 3 },
                ["Shaders/sh_Test.fs"] = Encoding.UTF8.GetBytes("trusted shader"),
            }));
            library = new GameResourcePackLibrary(storage, catalogue);
        }

        [TearDown]
        public void TearDown() { catalogue.Dispose(); storage.Dispose(); }

        private Dictionary<string, byte[]> files() => new Dictionary<string, byte[]>
        {
            ["Textures/Icons/example.png"] = png,
            ["Notices/Morasooma/LICENSE.txt"] = Encoding.UTF8.GetBytes("Original-resource terms; open licences retained."),
        };

        private static MemoryStream archive(Dictionary<string, byte[]> files, Action<GameResourcePackLibrary.Manifest>? mutate = null)
        {
            var manifest = new GameResourcePackLibrary.Manifest();
            foreach (var (path, data) in files) manifest.Files.Add(path, Convert.ToHexStringLower(SHA256.HashData(data)));
            mutate?.Invoke(manifest);
            var memory = new MemoryStream();
            using (var zip = new ZipArchive(memory, ZipArchiveMode.Create, true))
            {
                foreach (var (path, data) in files)
                {
                    using var stream = zip.CreateEntry(path).Open();
                    stream.Write(data);
                }
                using var entry = zip.CreateEntry("pack.json").Open();
                entry.Write(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(manifest)));
            }
            memory.Position = 0;
            return memory;
        }

        [Test]
        public void TestInstallReopenAndEffectiveExport()
        {
            using var input = archive(files());
            var pack = library.Install(input);
            using var restored = library.Open(pack.Id);
            Assert.That(restored.Get("Textures/Icons/example.png"), Is.EqualTo(png));
            Assert.That(restored.Get("Shaders/sh_Test.fs"), Is.Null);
            Assert.That(library.Installed().Single().Id, Is.EqualTo(pack.Id));
            using var output = new MemoryStream();
            library.Export(output, "My resources", catalogue, new Dictionary<string, byte[]>
            {
                ["Notices/Morasooma/LICENSE.txt"] = Encoding.UTF8.GetBytes("licence"),
            });
            Assert.That(library.Validate(output).Manifest.Files.Keys, Does.Not.Contain("Shaders/sh_Test.fs"));
        }

        [TestCase("Shaders/sh_Test.fs")]
        [TestCase("ShaderOverrides/Shaders/sh_Test.fs")]
        [TestCase("shaders/sh_Test.fs")]
        [TestCase("SHADERS/sh_Test.fs")]
        [TestCase("Shaders/sh_Test.fs.png")]
        [TestCase("Textures/Icons/sh_Test.fs")]
        [TestCase("Notices/sh_Test.fs")]
        [TestCase("Textures/Icons/evil.dll")]
        [TestCase("Textures/Icons/../evil.png")]
        [TestCase("Textures/Icons/unknown.png")]
        [TestCase("Textures\\Icons\\example.png")]
        [TestCase("/Textures/Icons/example.png")]
        public void TestRejectForbiddenPaths(string path)
        {
            var data = files(); data.Add(path, png);
            using var input = archive(data);
            Assert.Throws<InvalidDataException>(() => library.Install(input));
            Assert.That(library.Installed(), Is.Empty);
            Assert.That(storage.GetStorageForDirectory("ResourcePacks").GetFiles("", ".install-*"), Is.Empty);
        }

        [Test]
        public void TestRejectChangedHash()
        {
            using var input = archive(files(), m => m.Files["Textures/Icons/example.png"] = new string('0', 64));
            Assert.Throws<InvalidDataException>(() => library.Validate(input));
        }

        [Test]
        public void TestRejectChangedBuiltinFontDescriptor()
        {
            var data = files(); data.Add("Fonts/Nunito/Nunito-Regular.bin", new byte[] { 4, 5, 6 });
            using var input = archive(data);
            Assert.Throws<InvalidDataException>(() => library.Validate(input));
        }

        [TestCase("duplicate-path")]
        [TestCase("case-collision")]
        [TestCase("symlink")]
        [TestCase("animated-png")]
        [TestCase("truncated-image")]
        [TestCase("duplicate-json")]
        public void TestMalformedArchiveRejectedWithoutInstallation(string fault)
        {
            var data = files();
            if (fault == "animated-png")
            {
                // An APNG control chunk must be rejected even if frame one is valid.
                byte[] chunk = new byte[12];
                Encoding.ASCII.GetBytes("acTL").CopyTo(chunk, 4);
                data["Textures/Icons/example.png"] = png[..8].Concat(chunk).Concat(png[8..]).ToArray();
            }
            if (fault == "truncated-image") data["Textures/Icons/example.png"] = png[..^8];
            using var input = archive(data);
            if (fault is "duplicate-path" or "case-collision" or "symlink" or "duplicate-json")
            {
                using (var zip = new ZipArchive(input, ZipArchiveMode.Update, leaveOpen: true))
                {
                    if (fault == "symlink") zip.GetEntry("Textures/Icons/example.png")!.ExternalAttributes = unchecked((int)0xa1ff0000);
                    else if (fault == "duplicate-json")
                    {
                        var old = zip.GetEntry("pack.json")!;
                        string json;
                        using (var reader = new StreamReader(old.Open())) json = reader.ReadToEnd();
                        old.Delete();
                        using var writer = new StreamWriter(zip.CreateEntry("pack.json").Open());
                        writer.Write("{\"Version\":1," + json[1..]);
                    }
                    else
                    {
                        string path = fault == "duplicate-path" ? "Textures/Icons/example.png" : "textures/Icons/example.png";
                        using var output = zip.CreateEntry(path).Open();
                        output.Write(png);
                    }
                }
                input.Position = 0;
            }
            Assert.That(() => library.Install(input), Throws.Exception);
            Assert.That(library.Installed(), Is.Empty);
            Assert.That(storage.GetStorageForDirectory("ResourcePacks").GetFiles("", ".install-*"), Is.Empty);
        }

        [Test]
        public void TestCancelledExportDoesNotReportCompletion()
        {
            using var output = new MemoryStream();
            bool completed = false;
            Assert.Throws<OperationCanceledException>(() => library.Export(output, "Cancelled", catalogue,
                new Dictionary<string, byte[]>(), p => completed |= p >= 1, new CancellationToken(true)));
            Assert.That(completed, Is.False);
        }

        [Test]
        public void TestCancelledImportDoesNotInstall()
        {
            using var input = archive(files());
            Assert.Throws<OperationCanceledException>(() => library.Install(input, cancellationToken: new CancellationToken(true)));
            Assert.That(library.Installed(), Is.Empty);
        }

        [Test]
        public void TestStoreRejectsShaderEvenIfLocalArchiveIsTampered()
        {
            string path = storage.GetFullPath("tampered.morars");
            var data = files(); data.Add("Shaders/sh_Test.fs", Encoding.UTF8.GetBytes("untrusted"));
            using (var input = archive(data)) using (var file = File.Create(path)) input.CopyTo(file);
            using var store = new GameResourcePackStore(path);
            Assert.That(store.Get("Shaders/sh_Test.fs"), Is.Null);
            Assert.That(store.GetAvailableResources().Any(p => p.Contains("Shader", StringComparison.OrdinalIgnoreCase)), Is.False);
        }

        private sealed class DataStore : IResourceStore<byte[]>
        {
            private readonly Dictionary<string, byte[]> files;
            public DataStore(Dictionary<string, byte[]> files) => this.files = files;
            public byte[] Get(string name) => files.GetValueOrDefault(name)!;
            public System.Threading.Tasks.Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => System.Threading.Tasks.Task.FromResult(Get(name));
            public Stream GetStream(string name) => Get(name) is byte[] data ? new MemoryStream(data, false) : null!;
            public IEnumerable<string> GetAvailableResources() => files.Keys;
            public void Dispose() { }
        }
    }
}
