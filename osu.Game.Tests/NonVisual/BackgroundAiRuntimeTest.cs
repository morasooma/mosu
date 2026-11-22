using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Graphics.Backgrounds.AI;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    [NonParallelizable]
    public class BackgroundAiRuntimeTest
    {
        [TestCase(false)]
        [TestCase(true)]
        public async Task TestDownloadExtractOfflineRepairAndChecksumFailure(bool zip)
        {
            using var storage = new TemporaryNativeStorage("optional-ai-runtime-" + Guid.NewGuid().ToString("N"));
            byte[] library = { 1, 2, 3, 4, 5 };
            string entry = "runtime/lib/" + (zip ? "onnxruntime.dll" : "libonnxruntime.so.1.23.2");
            var files = new Dictionary<string, byte[]>
            {
                [entry] = library,
                ["runtime/LICENSE"] = new byte[] { 10 },
                ["runtime/ThirdPartyNotices.txt"] = new byte[] { 20 },
                ["runtime/../../escape"] = new byte[] { 30 },
                ["runtime/include/unneeded.h"] = new byte[] { 40 },
            };
            using var bytes = new MemoryStream();
            if (zip)
            {
                using var archive = new ZipArchive(bytes, ZipArchiveMode.Create, leaveOpen: true);
                foreach (var file in files)
                {
                    using var output = archive.CreateEntry(file.Key).Open();
                    output.Write(file.Value);
                }
            }
            else
            {
                using var gzip = new GZipStream(bytes, CompressionLevel.Fastest, leaveOpen: true);
                using var tar = new TarWriter(gzip, leaveOpen: true);
                foreach (var file in files)
                {
                    using var input = new MemoryStream(file.Value);
                    tar.WriteEntry(new PaxTarEntry(TarEntryType.RegularFile, file.Key) { DataStream = input });
                }
            }
            var payload = bytes.ToArray();
            var release = new BackgroundAiRuntime.Release(zip ? "runtime.zip" : "runtime.tgz", payload.Length,
                Convert.ToHexString(SHA256.HashData(payload)), entry, library.Length, Convert.ToHexString(SHA256.HashData(library)));
            using var handler = new DownloadHandler(payload);
            string directory = storage.GetFullPath("runtime");
            var source = new Uri("https://runtime.example/package");
            string path = await BackgroundAiRuntime.Install(directory, release, handler, source, CancellationToken.None);
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(library));
            Assert.That(Directory.GetFiles(Path.GetDirectoryName(path)!), Has.Length.EqualTo(3), "Only native runtime and licenses should be installed.");
            Assert.That(File.Exists(storage.GetFullPath("escape")), Is.False);
            Assert.That(await BackgroundAiRuntime.Install(directory, release, handler, source, CancellationToken.None), Is.EqualTo(path));
            Assert.That(handler.Requests, Is.EqualTo(1), "Verified installed runtime must work offline.");
            File.WriteAllBytes(path, new byte[] { 9 });
            await BackgroundAiRuntime.Install(directory, release, handler, source, CancellationToken.None);
            Assert.That(handler.Requests, Is.EqualTo(2));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(library));
            File.Delete(path);
            handler.Payload = new byte[payload.Length];
            Assert.ThrowsAsync<InvalidDataException>(async () => await BackgroundAiRuntime.Install(directory, release, handler, source, CancellationToken.None));
            Assert.That(Directory.GetDirectories(directory, ".runtime-*"), Is.Empty);
            Assert.That(File.Exists(path), Is.False);
        }

        private sealed class DownloadHandler(byte[] payload) : HttpMessageHandler
        {
            public byte[] Payload = payload;
            public int Requests;
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Requests++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Payload) });
            }
        }
    }
}
