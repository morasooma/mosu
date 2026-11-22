// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using osu.Framework.IO.Network;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ServerExclusiveBeatmapResourcesTest
    {
        private readonly EndpointConfiguration endpoints = new EndpointConfiguration
        {
            APIUrl = "https://lazer.example.test",
        };

        [TestCase(null, false)]
        [TestCase("", false)]
        [TestCase("   ", false)]
        [TestCase("mosu-exclusive", true)]
        [TestCase("g0v0-exclusive", true)]
        [TestCase("foo G0V0-EXCLUSIVE bar", true)]
        [TestCase("server-exclusive", true)]
        [TestCase("not-g0v0-exclusive", false)]
        [TestCase("g0v0-exclusive-other", false)]
        [TestCase("  foo MOSU-EXCLUSIVE bar  ", true)]
        [TestCase("not-mosu-exclusive", false)]
        [TestCase("mosu-exclusive-other", false)]
        [TestCase("foo\tmosu-exclusive", false)]
        public void TestExclusiveTagRequiresSpaceDelimitedWholeToken(string? tags, bool expected)
        {
            Assert.That(new BeatmapMetadata { Tags = tags! }.IsServerExclusive(), Is.EqualTo(expected));
        }

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

        [TestCase("/file/beatmaps/server/background/2000000002", "https://lazer.example.test/file/beatmaps/server/background/2000000002")]
        [TestCase("//cdn.example.test/cover.jpg", "https://cdn.example.test/cover.jpg")]
        [TestCase("https://cdn.example.test/cover.jpg", "https://cdn.example.test/cover.jpg")]
        public void TestCoverUrlResolution(string value, string expected)
        {
            Assert.That(ServerExclusiveBeatmapResources.ResolveUrl(value, endpoints), Is.EqualTo(expected));
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

        [TestCase("details")]
        [TestCase("lookup")]
        [TestCase("batch")]
        [TestCase("search")]
        [TestCase("profile")]
        [TestCase("most-played")]
        public void TestRequestPostProcessingResolvesExclusiveResources(string route)
        {
            const string setJson = """
                {"id":2000000001,"covers":{"cover@2x":"/file/beatmaps/server/background/2000000002"},
                 "beatmaps":[{"id":2000000002}],"preview_url":""}
                """;
            using var api = new DummyAPIAccess();
            api.Endpoints.APIUrl = endpoints.APIUrl;
            APIRequest request;
            object response;
            switch (route)
            {
                case "details":
                    (request, response) = prepare(new GetBeatmapSetRequest(2000000001), setJson);
                    break;
                case "lookup":
                    (request, response) = prepare(new GetBeatmapRequest(2000000002), "{\"beatmapset\":" + setJson + "}");
                    break;
                case "batch":
                    (request, response) = prepare(new GetBeatmapsRequest([2000000002]), "{\"beatmaps\":[{\"beatmapset\":" + setJson + "}]}");
                    break;
                case "search":
                    (request, response) = prepare(new SearchBeatmapSetsRequest("", new RulesetInfo { OnlineID = 0 }), "{\"beatmapsets\":[" + setJson + "]}");
                    break;
                case "profile":
                    (request, response) = prepare(new GetUserBeatmapsRequest(42, BeatmapSetType.Loved, new PaginationParameters()), "[" + setJson + "]");
                    break;
                default:
                    (request, response) = prepare(new GetUserMostPlayedBeatmapsRequest(42, new PaginationParameters()), "[{\"beatmapset\":" + setJson + "}]");
                    break;
            }
            request.AttachAPI(api);
            request.GetType().GetMethod("PostProcess", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(request, null);
            APIBeatmapSet set = response switch
            {
                APIBeatmapSet single => single,
                APIBeatmap map => map.BeatmapSet!,
                GetBeatmapsResponse batch => batch.Beatmaps[0].BeatmapSet!,
                SearchBeatmapSetsResponse search => System.Linq.Enumerable.First(search.BeatmapSets),
                List<APIBeatmapSet> profile => profile[0],
                List<APIUserMostPlayedBeatmap> mostPlayed => mostPlayed[0].BeatmapSet,
                _ => throw new System.InvalidOperationException(),
            };
            Assert.That(set.Covers.Cover, Does.StartWith(endpoints.APIUrl + "/file/"));
            Assert.That(set.Preview, Is.EqualTo(endpoints.APIUrl + "/api/private/audio/beatmapset/2000000001"));

            (APIRequest, object) prepare<T>(APIRequest<T> req, string json) where T : class
            {
                using var web = new OsuJsonWebRequest<T>();
                typeof(WebRequest).GetProperty(nameof(WebRequest.ResponseStream))!.SetValue(web,
                    new MemoryStream(Encoding.UTF8.GetBytes(json)));
                typeof(JsonWebRequest<T>).GetMethod("ProcessResponse", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(web, null);
                typeof(APIRequest).GetField("WebRequest", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(req, web);
                return (req, web.ResponseObject!);
            }
        }

        [Test]
        public void TestMissingCoversUseArchiveBackgroundAndOfficialResourcesStayUnchanged()
        {
            var exclusive = new APIBeatmapSet { OnlineID = 2000000001, Beatmaps = [new APIBeatmap { OnlineID = 2000000002 }] };
            var official = new APIBeatmapSet { OnlineID = 123, Preview = "//b.ppy.sh/preview/123.mp3" };
            ServerExclusiveBeatmapResources.ResolveResponse(new[] { exclusive, official }, endpoints);
            Assert.That(exclusive.Covers.Cover, Is.EqualTo(endpoints.APIUrl + "/file/beatmaps/server/background/2000000002"));
            Assert.That(official.Preview, Is.EqualTo("//b.ppy.sh/preview/123.mp3"));
            Assert.That(official.Covers.Cover, Is.Null);
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
