// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ServerExclusiveBeatmapResourcesTest
    {
        private readonly EndpointConfiguration endpoints = new EndpointConfiguration
        {
            APIUrl = "https://lazer.example.test",
        };

        [Test]
        public void TestServerResourcesBecomeAbsolute()
        {
            var set = new APIBeatmapSet
            {
                OnlineID = 2_000_000_001,
                Covers = new BeatmapSetOnlineCovers
                {
                    Cover = "/file/beatmaps/server/background/2000000002",
                    Card = "beatmaps/server/covers/2000000001.jpg",
                    List = "https://cdn.example.test/list.jpg",
                },
            };

            ServerExclusiveBeatmapResources.Resolve(set, endpoints);

            Assert.Multiple(() =>
            {
                Assert.That(set.Covers.Cover, Is.EqualTo("https://lazer.example.test/file/beatmaps/server/background/2000000002"));
                Assert.That(set.Covers.Card, Is.EqualTo("https://lazer.example.test/file/beatmaps/server/covers/2000000001.jpg"));
                Assert.That(set.Covers.List, Is.EqualTo("https://cdn.example.test/list.jpg"));
                Assert.That(set.Preview, Is.EqualTo("https://lazer.example.test/api/private/audio/beatmapset/2000000001"));
            });
        }

        [Test]
        public void TestPreviewTrackUsesApiPreview()
        {
            var set = new APIBeatmapSet
            {
                OnlineID = 2_000_000_001,
                Preview = "https://lazer.example.test/api/private/audio/beatmapset/2000000001",
            };

            Assert.That(
                PreviewTrackManager.TrackManagerPreviewTrack.GetPreviewUrl(set),
                Is.EqualTo(set.Preview));
        }

        [Test]
        public void TestOfficialPreviewFallbackIsUnchanged()
        {
            var set = new BeatmapSetInfo { OnlineID = 123 };

            Assert.That(
                PreviewTrackManager.TrackManagerPreviewTrack.GetPreviewUrl(set),
                Is.EqualTo("https://b.ppy.sh/preview/123.mp3"));
        }
    }
}
