// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Updater;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class MobileUpdateNotifierTest
    {
        [TestCase("2026.901.0", "2026.902.0", true)]
        [TestCase("2026.901.0", "2026.901.1", true)]
        [TestCase("2026.901.0", "2026.901.0", false)]
        [TestCase("2026.901.0", "2026.821.0", false)]
        [TestCase("local release", "2026.902.0", false)]
        [TestCase("2026.901.0", "invalid", false)]
        public void TestNewerVersionComparison(string currentVersion, string candidateVersion, bool expected)
        {
            Assert.That(MobileUpdateNotifier.isNewerVersion(currentVersion, candidateVersion), Is.EqualTo(expected));
        }

        [Test]
        public void TestServerManifestCreatesVersionedDownloadUrl()
        {
            AndroidUpdateManifest manifest = createManifest();

            bool success = MobileUpdateNotifier.tryGetAndroidDownloadUrl(
                "https://morasooma.net/client/updates/",
                "stable",
                manifest,
                out string? url);

            Assert.Multiple(() =>
            {
                Assert.That(success, Is.True);
                Assert.That(url, Is.EqualTo("https://morasooma.net/client/updates/mosu-2026.902.0-android-arm64.apk"));
            });
        }

        [TestCase("channel", "tachyon")]
        [TestCase("platform", "windows")]
        [TestCase("architecture", "x86_64")]
        [TestCase("file_name", "https://example.com/malicious.apk")]
        [TestCase("file_name", "../malicious.apk")]
        [TestCase("sha256", "invalid")]
        public void TestInvalidManifestIsRejected(string property, string value)
        {
            AndroidUpdateManifest manifest = createManifest();

            switch (property)
            {
                case "channel":
                    manifest.Channel = value;
                    break;

                case "platform":
                    manifest.Platform = value;
                    break;

                case "architecture":
                    manifest.Architecture = value;
                    break;

                case "file_name":
                    manifest.FileName = value;
                    break;

                case "sha256":
                    manifest.Sha256 = value;
                    break;
            }

            Assert.That(MobileUpdateNotifier.tryGetAndroidDownloadUrl(
                "https://morasooma.net/client/updates",
                "stable",
                manifest,
                out _), Is.False);
        }

        [Test]
        public void TestManifestJsonContract()
        {
            const string json = """
                                {
                                  "schema_version": 1,
                                  "channel": "stable",
                                  "platform": "android",
                                  "architecture": "arm64-v8a",
                                  "version": "2026.902.0",
                                  "application_version": 2026090200,
                                  "file_name": "mosu-2026.902.0-android-arm64.apk",
                                  "sha256": "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                                  "size": 123456,
                                  "published_at": "2026-09-02T00:00:00Z"
                                }
                                """;

            AndroidUpdateManifest? manifest = JsonConvert.DeserializeObject<AndroidUpdateManifest>(json);

            Assert.Multiple(() =>
            {
                Assert.That(manifest, Is.Not.Null);
                Assert.That(manifest!.Version, Is.EqualTo("2026.902.0"));
                Assert.That(manifest.ApplicationVersion, Is.EqualTo(2026090200));
                Assert.That(manifest.FileName, Is.EqualTo("mosu-2026.902.0-android-arm64.apk"));
            });
        }

        private static AndroidUpdateManifest createManifest() => new AndroidUpdateManifest
        {
            SchemaVersion = 1,
            Channel = "stable",
            Platform = "android",
            Architecture = "arm64-v8a",
            Version = "2026.902.0",
            ApplicationVersion = 2026090200,
            FileName = "mosu-2026.902.0-android-arm64.apk",
            Sha256 = new string('a', 64),
            Size = 123456,
            PublishedAt = "2026-09-02T00:00:00Z"
        };
    }
}
