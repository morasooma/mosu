using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Scoring;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneLegacyDifficultyPresentation : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private readonly SoloSongSelect songSelect = new SoloSongSelect();

        [TestCase(false)]
        [TestCase(true)]
        public void TestLegacyStarsExcludeModernDifficultyIndicators(bool standalone)
        {
            Panel panel = null!;
            var config = Dependencies.Get<OsuConfigManager>();

            AddStep("create legacy difficulty", () =>
            {
                config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned);
                var item = new CarouselItem(new GroupedBeatmap(null, TestResources.CreateTestBeatmapSetInfo().Beatmaps.First()));
                panel = standalone ? new PanelBeatmapStandalone { Item = item } : new PanelBeatmap { Item = item };
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(ISongSelect), songSelect) },
                    Child = panel,
                };
            });
            AddUntilStep("legacy stars visible", () => panel.ChildrenOfType<StableStarCounter>().SingleOrDefault()?.IsPresent == true);
            AddAssert("modern star pill hidden", () => !panel.ChildrenOfType<StarRatingDisplay>().Single().IsPresent);
            AddAssert("ordinary stars hidden", () => panel.ChildrenOfType<StarCounter>().Where(s => s is not StableStarCounter).All(s => !s.IsPresent));
            AddAssert("rank uses legacy presentation", () => panel.ChildrenOfType<PanelLocalRankDisplay>().Single().UseLegacyStyle);
            if (standalone)
            {
                AddStep("receive late difficulty calculation", () => panel.ChildrenOfType<PanelBeatmapStandalone.SpreadDisplay>().Single().StarDifficulty.Value = new StarDifficulty(5, 100));
                AddWaitStep("finish spread fade", 30);
                AddAssert("modern spread cannot reappear below stable stars", () => !panel.ChildrenOfType<PanelBeatmapStandalone.SpreadDisplay>().Single().Parent!.IsPresent);
            }
            AddStep("select difficulty", () => panel.Selected.Value = true);
            AddWaitStep("allow selection and difficulty updates", 3);
            AddAssert("selection does not reveal modern pill", () => !panel.ChildrenOfType<StarRatingDisplay>().Single().IsPresent);
            AddStep("switch to modern", () => config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            AddUntilStep("modern pill restored", () => panel.ChildrenOfType<StarRatingDisplay>().Single().IsPresent);
            AddAssert("legacy stars hidden", () => !panel.ChildrenOfType<StableStarCounter>().Single().IsPresent);
            AddAssert("modern rank restored", () => !panel.ChildrenOfType<PanelLocalRankDisplay>().Single().UseLegacyStyle);
            AddStep("switch back to legacy", () => config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned));
            AddUntilStep("legacy restored without modern pill", () => panel.ChildrenOfType<StableStarCounter>().Single().IsPresent
                && !panel.ChildrenOfType<StarRatingDisplay>().Single().IsPresent);
        }

        [Test]
        public void TestRankTexturesAndStyleChangesWithoutRankChange()
        {
            PanelLocalRankDisplay rank = null!;
            AddStep("create rank", () => Child = rank = new PanelLocalRankDisplay());

            foreach (var scoreRank in Enum.GetValues<ScoreRank>())
            {
                AddStep($"display {scoreRank}", () => rank.ApplySnapshot(scoreRank, rank.Generation));
                AddUntilStep("modern badge loaded", () => rank.ChildrenOfType<DrawableRank>().Any(r => r.IsPresent));
                AddStep("switch rank to legacy", () => rank.UseLegacyStyle = true);
                AddUntilStep("legacy texture loaded", () => rank.ChildrenOfType<PanelLocalRankDisplay.LegacyCarouselRank>().Any(s => s.IsPresent
                    && s.TextureName == $"ranking-{scoreRank}-small" && (scoreRank == ScoreRank.F || s.Sprite.Texture != null)));
                AddUntilStep("modern badge removed", () => !rank.ChildrenOfType<DrawableRank>().Any(r => r.IsPresent));
                AddAssert("rank value preserved", () => rank.CurrentRank == scoreRank);
                AddStep("switch rank to modern", () => rank.UseLegacyStyle = false);
                AddUntilStep("modern badge restored", () => rank.ChildrenOfType<DrawableRank>().Any(r => r.IsPresent));
                AddUntilStep("legacy texture removed", () => !rank.ChildrenOfType<PanelLocalRankDisplay.LegacyCarouselRank>().Any(s => s.IsPresent));
            }

            AddStep("clear rank", () => rank.ApplySnapshot(null, rank.Generation));
            AddUntilStep("no rank displayed", () => rank.CurrentRank == null && rank.ChildrenOfType<UpdateableRank>().Single().Alpha == 0);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                songSelect.Dispose();

            base.Dispose(isDisposing);
        }
    }
}
