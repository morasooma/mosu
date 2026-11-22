using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneStableSingletonGrouping : BeatmapCarouselTestScene
    {
        [Cached(typeof(ISongSelect))]
        private readonly SoloSongSelect songSelect = new SoloSongSelect();

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                songSelect.Dispose();
            base.Dispose(isDisposing);
        }

        [Test]
        public void TestFastScrollingDoesNotRevealSelectedTintOnNewCards()
        {
            RemoveAllBeatmaps();
            AddStep("stable presentation", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned));
            CreateCarousel();
            AddBeatmaps(60, 1);
            SortAndGroupBy(SortMode.Title, GroupMode.None);
            AddStep("select first map", () => Carousel.CurrentGroupedBeatmap = new GroupedBeatmap(null, BeatmapSets[0].Beatmaps[0]));
            WaitForDrawablePanels();
            foreach (int position in new[] { 600, 1600, 2800, 900, 3200, 0 })
            {
                AddStep($"jump scroll to {position}", () => Scroll.ScrollTo(position, animated: false));
                AddWaitStep("materialise next range", 1);
                AddAssert("new unselected rows start in inactive colour", () =>
                {
                    var panels = GetVisiblePanels<PanelBeatmapStandalone>().Where(p => !p.Selected.Value).ToArray();
                    return panels.Length > 0 && panels.All(p => p.ChildrenOfType<LegacyMenuButtonBackground>().Single().Colour == StableCarouselLayout.Inactive);
                });
            }
        }

        [Test]
        public void TestSingletonSurvivesSelectionCollapseAndRandom()
        {
            RemoveAllBeatmaps();
            AddStep("stable presentation", () => Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned));
            CreateCarousel();
            AddStep("keep neighbouring cards materialised", () =>
            {
                Carousel.BleedTop = 1000;
                Carousel.BleedBottom = 1000;
            });
            AddBeatmaps(2, 3);
            AddStep("collection with singleton and pair", () => Carousel.AllCollections = () =>
            [
                new BeatmapCollection("Mixed", [BeatmapSets[0].Beatmaps[0].MD5Hash, BeatmapSets[1].Beatmaps[0].MD5Hash, BeatmapSets[1].Beatmaps[1].MD5Hash]),
            ]);
            SortAndGroupBy(SortMode.Title, GroupMode.Collections);
            AddStep("select singleton", () => Carousel.CurrentGroupedBeatmap = new GroupedBeatmap(mixedGroup(), BeatmapSets[0].Beatmaps[0]));
            AddUntilStep("singleton is visible without a header", () => singletonVisible());
            AddAssert("group counts three difficulties", () => grouping.GroupItems[mixedGroup()].Single(i => i.Model is GroupDefinition).NestedItemCount == 3);
            AddAssert("only pair has a set header", () => grouping.GroupItems[mixedGroup()].Count(i => i.Model is GroupedBeatmapSet) == 1);
            SelectNextSet();
            WaitForSetSelection(1);
            AddAssert("unselected singleton stays visible", singletonVisible);
            AddUntilStep("unselected singleton uses inactive card colour", () =>
                GetVisiblePanels<PanelBeatmapStandalone>().SingleOrDefault(p => p.Item?.Model is GroupedBeatmap b && b.Beatmap == BeatmapSets[0].Beatmaps[0])
                    ?.ChildrenOfType<PanelSetBackground>().Single().StableStateColour == StableCarouselLayout.Inactive);
            AddUntilStep("unselected difficulty in expanded pair stays blue", () =>
                GetVisiblePanels<PanelBeatmapStandalone>().Any(p => !p.Selected.Value && p.Item?.Model is GroupedBeatmap b
                    && b.Beatmap.BeatmapSet == BeatmapSets[1]
                    && p.ChildrenOfType<PanelSetBackground>().Single().StableStateColour == StableCarouselLayout.Expanded));
            SelectPrevSet();
            WaitForSetSelection(0, 0);
            AddUntilStep("selected singleton uses selected colour", () =>
                GetVisiblePanels<PanelBeatmapStandalone>().Any(p => p.Selected.Value
                    && p.ChildrenOfType<PanelSetBackground>().Single().StableStateColour == StableCarouselLayout.Selected));
            ToggleGroupCollapse();
            AddUntilStep("group collapsed", () => Carousel.ExpandedGroup == null);
            AddAssert("singleton hidden with group", () => !singletonVisible());
            ToggleGroupCollapse();
            AddUntilStep("singleton visible after reopening", singletonVisible);
            SelectRandomSet();
            AddUntilStep("random selects pair", () => Carousel.CurrentBeatmap?.BeatmapSet == BeatmapSets[1]);
            AddAssert("random remains inside collection", () => Carousel.CurrentGroupedBeatmap?.Group == mixedGroup());
            SelectRandomSet();
            AddUntilStep("random returns to singleton", () => Carousel.CurrentBeatmap == BeatmapSets[0].Beatmaps[0]);
            AddAssert("singleton remains visible after random", singletonVisible);
        }

        private BeatmapCarouselFilterGrouping grouping => Carousel.Filters.OfType<BeatmapCarouselFilterGrouping>().Single();
        private GroupDefinition mixedGroup() => grouping.GroupItems.Keys.Single(g => g.Title.ToString() == "Mixed");
        private bool singletonVisible() => grouping.GroupItems[mixedGroup()].Single(i => i.Model is GroupedBeatmap b && b.Beatmap == BeatmapSets[0].Beatmaps[0]).IsVisible;
    }
}
