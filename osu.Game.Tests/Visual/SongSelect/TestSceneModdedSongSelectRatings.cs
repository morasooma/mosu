using System;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneModdedSongSelectRatings : BeatmapCarouselTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [SetUp]
        public void ResetSetting() => Schedule(() => config.SetValue(OsuSetting.ForkSongSelectModdedRatings, false));

        [Test]
        public void DefaultUsesSavedRatingsAndToggleStopsExperimentalWork()
        {
            BeatmapInfo map = null!;
            SongSelectRatingWork old = null!;
            CreateCarousel();
            AddBeatmaps(3, 1);
            AddStep("sort with DT while option is off", () =>
            {
                map = BeatmapSets[0].Beatmaps[0];
                Carousel.Filter(new FilterCriteria { Sort = SortMode.Difficulty, Ruleset = map.Ruleset, Mods = new Mod[] { new OsuModDoubleTime() } });
            });
            WaitForFiltering();
            AddAssert("no experimental worker by default", () => getWork() == null);
            AddAssert("no estimate snapshot", () => Carousel.Criteria!.Ratings == null);
            AddAssert("saved NM stars retained", () => Carousel.Criteria!.GetSortStars(map) == map.StarRating);
            AddStep("enable option live", () => config.SetValue(OsuSetting.ForkSongSelectModdedRatings, true));
            WaitForFiltering();
            AddAssert("experimental worker started", () => getWork() != null);
            AddStep("publish experimental result", () =>
            {
                old = getWork()!;
                old.Accept(new BeatmapDifficultyCache.DifficultyCacheLookup(map, map.Ruleset, new Mod[] { new OsuModDoubleTime() }),
                    new StarDifficulty(new DifficultyAttributes { StarRating = 17 }));
            });
            AddUntilStep("exact result used", () => Carousel.Criteria!.GetSortStars(map), () => Is.EqualTo(17));
            AddStep("disable option live", () => config.SetValue(OsuSetting.ForkSongSelectModdedRatings, false));
            WaitForFiltering();
            AddAssert("worker detached", () => getWork() == null);
            AddAssert("snapshot discarded", () => Carousel.Criteria!.Ratings == null);
            AddAssert("saved rating restored", () => Carousel.Criteria!.GetSortStars(map) == map.StarRating);
            AddStep("publish late completion and resume screen", () =>
            {
                old.Accept(new BeatmapDifficultyCache.DifficultyCacheLookup(map, map.Ruleset, new Mod[] { new OsuModDoubleTime() }),
                    new StarDifficulty(new DifficultyAttributes { StarRating = 18 }));
                Carousel.SetRatingWorkEnabled(false);
                Carousel.SetRatingWorkEnabled(true);
            });
            WaitForFiltering();
            AddAssert("resume cannot override disabled option", () => getWork() == null && Carousel.Criteria!.Ratings == null);
        }

        [Test]
        public void ExactPublicationRefreshesCarouselAndModChangeDropsIt()
        {
            SongSelectRatingWork old = null!;
            BeatmapInfo map = null!;
            CreateCarousel();
            AddBeatmaps(3, 1);
            AddStep("enable experimental setting", () => config.SetValue(OsuSetting.ForkSongSelectModdedRatings, true));
            WaitForFiltering();
            AddStep("enable mod-aware SR sorting", () =>
            {
                map = BeatmapSets[0].Beatmaps[0];
                Carousel.Filter(new FilterCriteria { Sort = SortMode.Difficulty, Ruleset = map.Ruleset, Mods = new Mod[] { new OsuModDoubleTime() } });
            });
            WaitForFiltering();
            AddStep("publish exact visible result", () =>
            {
                old = getWork()!;
                old.Accept(new BeatmapDifficultyCache.DifficultyCacheLookup(map, map.Ruleset, new Mod[] { new OsuModDoubleTime() }),
                    new StarDifficulty(new DifficultyAttributes { StarRating = 17 }));
            });
            AddUntilStep("snapshot re-filter sees exact result", () => Carousel.Criteria!.GetStars(map), () => Is.EqualTo(17));
            AddStep("switch mods to HR", () => Carousel.Filter(new FilterCriteria
            {
                Sort = SortMode.Difficulty, Ruleset = map.Ruleset, Mods = new Mod[] { new OsuModHardRock() }
            }));
            WaitForFiltering();
            AddAssert("old exact number not inherited", () => Carousel.Criteria!.GetStars(map) != 17);
            AddStep("obsolete DT completion ignored", () => old.Accept(new BeatmapDifficultyCache.DifficultyCacheLookup(map, map.Ruleset, new Mod[] { new OsuModDoubleTime() }),
                new StarDifficulty(new DifficultyAttributes { StarRating = 18 })));
            AddWaitStep("allow publication", 3);
            AddAssert("old result still absent", () => Carousel.Criteria!.GetStars(map) != 18);
            AddStep("leave song select", () => Carousel.SetRatingWorkEnabled(false));
            AddAssert("background work detached", () => getWork() == null);
            AddStep("resume song select", () => Carousel.SetRatingWorkEnabled(true));
            WaitForFiltering();
            AddAssert("fresh generation resumed", () => getWork() != null);
        }

        private SongSelectRatingWork? getWork() => (SongSelectRatingWork?)typeof(BeatmapCarousel)
            .GetField("ratingWork", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(Carousel)!;
    }
}
