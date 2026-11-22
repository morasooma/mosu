using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Online.Hinamizawa;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Hinamizawa;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneHinamizawaSongSelect : ScreenTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        [Test]
        public void TestOriginalCarouselWithRemoteDifficulties()
        {
            TestMirrorScreen screen = null!;
            BeatmapCarousel carousel = null!;
            AddStep("push mirror screen", () => Stack.Push(screen = new TestMirrorScreen()));
            AddUntilStep("screen loaded", () => screen.IsLoaded);
            AddUntilStep("catalogue loaded", () => screen.CatalogueCount == 1);
            AddStep("find original carousel", () => carousel = screen.ChildrenOfType<BeatmapCarousel>().Single());
            AddAssert("external source", () => carousel.UsesExternalSource);
            AddAssert("all difficulties", () => carousel.BeatmapSource!.Single().Beatmaps.Count, () => Is.EqualTo(2));
            AddAssert("remote audio identities differ across sets", () =>
            {
                var firstSet = new BeatmapSetInfo { OnlineID = 123 };
                var secondSet = new BeatmapSetInfo { OnlineID = 124 };
                var first = new BeatmapInfo { BeatmapSet = firstSet };
                var second = new BeatmapInfo { BeatmapSet = secondSet };
                first.Metadata.AudioFile = "hinamizawa-123.mp3";
                second.Metadata.AudioFile = "hinamizawa-124.mp3";
                firstSet.Beatmaps.Add(first);
                secondSet.Beatmaps.Add(second);
                return first.AudioEquals(second);
            }, () => Is.False);
            AddStep("choose hard", () => carousel.CurrentBeatmap = carousel.BeatmapSource!.Single().Beatmaps.Single(b => b.OnlineID == 457));
            AddUntilStep("hard selected", () => screen.SelectedDifficultyId == 457);
            AddAssert("selection did not import", () => Dependencies.Get<RealmAccess>().Run(r => r.All<BeatmapSetInfo>().Count()), () => Is.EqualTo(0));
            AddStep("exit mirror", () => Stack.Exit());
        }

        private partial class TestMirrorScreen : HinamizawaSongSelect
        {
            protected override bool LoadMirrorMedia => false;
            protected override BeatmapCatalogueProvider DefaultCatalogueProvider => BeatmapCatalogueProvider.Hinamizawa;
            protected override HinamizawaClient CreateMirrorClient(Storage storage) => new HinamizawaClient(storage.GetFullPath("cache/hinamizawa"), new CatalogueHandler());
        }

        private sealed class CatalogueHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"source\":\"local\",\"total_pages\":1,\"beatmapsets\":[{\"id\":123,\"title\":\"Remote song\",\"artist\":\"Artist\",\"creator\":\"Mapper\",\"status\":\"ranked\",\"beatmaps\":[{\"id\":456,\"beatmapset_id\":123,\"mode_int\":0,\"version\":\"Easy\",\"difficulty_rating\":1,\"status\":\"ranked\"},{\"id\":457,\"beatmapset_id\":123,\"mode_int\":0,\"version\":\"Hard\",\"difficulty_rating\":5,\"status\":\"ranked\"}]}]}"),
                });
        }
    }
}
