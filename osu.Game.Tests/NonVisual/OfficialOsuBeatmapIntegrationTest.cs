// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class OfficialOsuBeatmapIntegrationTest
    {
        [Test]
        public void TestOfficialTokenUpdatePreservesGameIni()
        {
            using var storage = new TemporaryNativeStorage("official-osu-token-store");

            using (var stream = storage.CreateFileSafely("game.ini"))
            using (var writer = new StreamWriter(stream))
            {
                writer.WriteLine("# official config");
                writer.WriteLine("Username = peppy");
                writer.WriteLine("Token = old-access|123|old-refresh");
                writer.WriteLine("UnknownSetting = keep-me");
                writer.WriteLine("VolumeUniversal = 0.42");
            }

            using var config = new OsuConfigManager(storage);

            Assert.That(config.ReadOriginalGameCredentials(), Is.EqualTo(("peppy", "old-access|123|old-refresh")));
            Assert.That(config.TryWriteOriginalGameToken("new-access|456|new-refresh"), Is.True);

            using var updatedStream = storage.GetStream("game.ini");
            using var reader = new StreamReader(updatedStream!);
            string updated = reader.ReadToEnd();

            Assert.Multiple(() =>
            {
                Assert.That(updated, Does.Contain("# official config"));
                Assert.That(updated, Does.Contain("Username = peppy"));
                Assert.That(updated, Does.Contain("Token = new-access|456|new-refresh"));
                Assert.That(updated, Does.Contain("UnknownSetting = keep-me"));
                Assert.That(updated, Does.Contain("VolumeUniversal = 0.42"));
                Assert.That(updated, Does.Not.Contain("old-access"));
            });
        }

        [Test]
        public void TestOfficialEndpointHasNoRealtimeEndpoints()
        {
            var endpoints = new OfficialOsuEndpointConfiguration();

            Assert.Multiple(() =>
            {
                Assert.That(endpoints.APIUrl, Is.EqualTo("https://osu.ppy.sh"));
                Assert.That(endpoints.APIClientID, Is.EqualTo("5"));
                Assert.That(endpoints.MetadataUrl, Is.Empty);
                Assert.That(endpoints.MultiplayerUrl, Is.Empty);
                Assert.That(endpoints.SpectatorUrl, Is.Empty);
                Assert.That(endpoints.BeatmapSubmissionServiceUrl, Is.Null);
            });
        }

        [Test]
        public void TestRouterOnlyUsesOfficialApiForNormalBeatmaps()
        {
            using var storage = new TemporaryNativeStorage("official-osu-router");
            using var config = new OsuConfigManager(storage);
            config.SetValue(OsuSetting.ForkUseOfficialBeatmapService, true);
            var primary = new DummyAPIAccess();
            var official = new OfficialOsuBeatmapApi(config);
            official.ConnectionState.Value = OfficialOsuBeatmapApiState.Connected;

            var router = new BeatmapApiProvider(primary, official);
            var normalSet = new BeatmapSetInfo { OnlineID = 123 };
            var serverSet = new BeatmapSetInfo { OnlineID = BeatmapApiProvider.SERVER_EXCLUSIVE_ID_THRESHOLD };

            Assert.Multiple(() =>
            {
                Assert.That(router.SelectProvider(new DownloadBeatmapSetRequest(normalSet, false)), Is.SameAs(official));
                Assert.That(router.SelectProvider(new DownloadBeatmapSetRequest(serverSet, false)), Is.SameAs(primary));
                Assert.That(router.SelectProvider(new GetMeRequest()), Is.SameAs(primary));
            });
        }

        [Test]
        public void TestOfficialMetadataChangeDetection()
        {
            var localSet = new BeatmapSetInfo { OnlineID = 123, Status = BeatmapOnlineStatus.Pending };
            var localBeatmap = new BeatmapInfo
            {
                OnlineID = 456,
                BeatmapSet = localSet,
                Status = BeatmapOnlineStatus.Pending,
                MD5Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                LastOnlineUpdate = new System.DateTimeOffset(2026, 7, 1, 0, 0, 0, System.TimeSpan.Zero),
            };
            var request = new GetBeatmapRequest(localBeatmap);
            var unchanged = new APIBeatmap
            {
                OnlineID = 456,
                OnlineBeatmapSetID = 123,
                Status = BeatmapOnlineStatus.Pending,
                Checksum = localBeatmap.MD5Hash,
                LastUpdated = localBeatmap.LastOnlineUpdate.Value,
                BeatmapSet = new APIBeatmapSet { OnlineID = 123, Status = BeatmapOnlineStatus.Pending },
            };

            Assert.Multiple(() =>
            {
                Assert.That(BeatmapApiProvider.officialMetadataChanged(request, unchanged), Is.False);
                Assert.That(BeatmapApiProvider.officialMetadataChanged(request, withChecksum(unchanged, "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb")), Is.True);
                Assert.That(BeatmapApiProvider.officialMetadataChanged(request, withStatus(unchanged, BeatmapOnlineStatus.Ranked)), Is.True);
                Assert.That(BeatmapApiProvider.officialMetadataChanged(request, withLastUpdated(unchanged, localBeatmap.LastOnlineUpdate.Value.AddMinutes(1))), Is.True);
            });
        }

        private static APIBeatmap withChecksum(APIBeatmap source, string checksum) => new APIBeatmap
        {
            OnlineID = source.OnlineID,
            OnlineBeatmapSetID = source.OnlineBeatmapSetID,
            Status = source.Status,
            Checksum = checksum,
            LastUpdated = source.LastUpdated,
            BeatmapSet = source.BeatmapSet,
        };

        private static APIBeatmap withStatus(APIBeatmap source, BeatmapOnlineStatus status)
        {
            var result = withChecksum(source, source.Checksum);
            result.Status = status;
            return result;
        }

        private static APIBeatmap withLastUpdated(APIBeatmap source, System.DateTimeOffset lastUpdated)
        {
            var result = withChecksum(source, source.Checksum);
            result.LastUpdated = lastUpdated;
            return result;
        }
    }
}
