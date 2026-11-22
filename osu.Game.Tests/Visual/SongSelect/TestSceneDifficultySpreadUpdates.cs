using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Taiko;
using osu.Game.Screens.Select;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneDifficultySpreadUpdates : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [TestCase(false)]
        [TestCase(true)]
        public void TestHeaderUpdatesEveryCompletedDifficultyWithoutRecycling(bool libraryUpdate)
        {
            SpreadCache cache = null!;
            PanelBeatmapSet.SpreadDisplay spread = null!;
            BeatmapSetInfo set = null!;
            Circle[] originalDots = null!;
            AddStep("show header before import calculations finish", () =>
            {
                set = new BeatmapSetInfo();
                for (int i = 0; i < 6; i++)
                    set.Beatmaps.Add(new BeatmapInfo(new OsuRuleset().RulesetInfo)
                    {
                        BeatmapSet = set, MD5Hash = "revision-" + i, StarRating = -1
                    });
                Add(new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[] { (typeof(BeatmapDifficultyCache), cache = new SpreadCache()) },
                    Children = new Drawable[] { cache, spread = new PanelBeatmapSet.SpreadDisplay { BeatmapSet = { Value = set } } }
                });
            });
            AddUntilStep("six grey indicators", () => spread.CreatedDifficultyDots == 6
                && spread.ChildrenOfType<Circle>().All(dot => dot.Colour == colours.ForStarDifficulty(-1)));
            AddStep("capture original dots", () => originalDots = spread.ChildrenOfType<Circle>().ToArray());
            for (int i = 0; i < 6; i++)
            {
                int index = i;
                AddStep("finish difficulty " + index, () =>
                {
                    var map = set.Beatmaps[index];
                    if (libraryUpdate)
                    {
                        var updated = new BeatmapInfo(map.Ruleset) { ID = map.ID, MD5Hash = map.MD5Hash, StarRating = index + 2 };
                        cache.Invalidate(map, updated);
                    }
                    else
                        cache.Publish(map, index + 2);
                });
                AddUntilStep("header reflects completed difficulties " + index, () =>
                {
                    var dots = spread.ChildrenOfType<Circle>().ToArray();
                    return dots.Count(dot => dot.Colour == colours.ForStarDifficulty(-1)) == 5 - index
                           && Enumerable.Range(0, index + 1).All(n => dots.Any(dot => dot.Colour == colours.ForStarDifficulty(n + 2)));
                });
            }
            AddAssert("header and indicators were not recreated", () =>
                spread.ChildrenOfType<Circle>().All(originalDots.Contains) && spread.CreatedDifficultyDots == 6);
            if (!libraryUpdate)
            {
                AddStep("verify cached values", () =>
                {
                    Assert.That(cache.PreviewCount, Is.EqualTo(6));
                    for (int n = 0; n < 6; n++)
                        Assert.That(cache.GetCachedNativeStarRating(set.Beatmaps[n]), Is.EqualTo(n + 2), "cached stars " + n);
                });
                AddStep("create another header with original detached model", () =>
                {
                    var parent = (DependencyProvidingContainer)spread.Parent!;
                    parent.Remove(spread, true);
                    parent.Add(spread = new PanelBeatmapSet.SpreadDisplay { BeatmapSet = { Value = set } });
                });
                AddUntilStep("cached results seed new header", () => Enumerable.Range(0, 6)
                    .All(n => spread.ChildrenOfType<Circle>().Any(dot => dot.Colour == colours.ForStarDifficulty(n + 2))));
            }
            AddStep("reject modded and obsolete revision results", () =>
            {
                cache.Publish(set.Beatmaps[0], 10, new OsuModHardRock());
                cache.Publish(new BeatmapInfo(set.Beatmaps[0].Ruleset) { ID = set.Beatmaps[0].ID, MD5Hash = "old-revision" }, 11);
            });
            AddWaitStep("deliver ignored results", 3);
            AddAssert("no wrong colours applied", () => spread.ChildrenOfType<Circle>()
                .All(dot => dot.Colour != colours.ForStarDifficulty(10) && dot.Colour != colours.ForStarDifficulty(11)));
            AddStep("clear pooled header", () => spread.BeatmapSet.Value = null);
            AddStep("late completion after clear", () => cache.Publish(set.Beatmaps[0], 9));
        }

        [TestCase(false, false)]
        [TestCase(true, false)]
        [TestCase(false, true)]
        [TestCase(true, true)]
        public void TestHeaderRecoversAfterFullyHidden(bool recycle, bool fullCard)
        {
            PanelBeatmapSet.SpreadDisplay spread = null!;
            BeatmapSetInfo set = null!;
            Circle[] originalDots = null!;

            AddStep("show native osu header", () =>
            {
                Clear();
                Ruleset.Value = new OsuRuleset().RulesetInfo;
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ShowConvertedBeatmaps, false);
                set = new BeatmapSetInfo();
                for (int i = 0; i < 6; i++)
                    set.Beatmaps.Add(new BeatmapInfo(Ruleset.Value) { BeatmapSet = set, StarRating = i + 1 });
                if (fullCard)
                    Add(new PanelBeatmapSet
                    {
                        Item = new CarouselItem(new GroupedBeatmapSet(null, set)),
                        Expanded = { Value = true },
                    });
                else
                    Add(new PanelBeatmapSet.SpreadDisplay { BeatmapSet = { Value = set } });
            });
            AddUntilStep("header loaded", () => (spread = this.ChildrenOfType<PanelBeatmapSet.SpreadDisplay>().SingleOrDefault()!)?.IsLoaded == true);
            AddUntilStep("six indicators visible", () => spread.Alpha == 1 && spread.ChildrenOfType<Circle>().Count() == 6);
            AddStep("capture original indicators", () => originalDots = spread.ChildrenOfType<Circle>().ToArray());

            for (int i = 0; i < 3; i++)
            {
                AddStep(recycle ? "return header to pool" : "switch to incompatible mode", () =>
                {
                    if (recycle)
                        spread.BeatmapSet.Value = null;
                    else
                        Ruleset.Value = new TaikoRuleset().RulesetInfo;
                });
                AddUntilStep("header fully faded out", () => spread.Alpha == 0);
                AddAssert("hidden header excluded from layout", () => !spread.IsPresent);
                AddStep(recycle ? "reuse header for same set" : "switch back to osu", () =>
                {
                    if (recycle)
                        spread.BeatmapSet.Value = set;
                    else
                        Ruleset.Value = new OsuRuleset().RulesetInfo;
                });
                AddUntilStep("same indicators visible again", () => spread.Alpha == 1
                    && spread.ChildrenOfType<Circle>().Count() == 6
                    && spread.ChildrenOfType<Circle>().All(originalDots.Contains));
                AddUntilStep("header geometry restored", () => spread.DrawWidth > 6 * 7);
                AddAssert("no extra indicators created", () => spread.CreatedDifficultyDots == 6);
            }
        }

        private partial class SpreadCache : BeatmapDifficultyCache
        {
            public void Publish(BeatmapInfo map, double stars, params Mod[] mods)
                => OnValueComputed(new DifficultyCacheLookup(map, map.Ruleset, mods, false), new StarDifficulty(stars, 123));
        }
    }
}
