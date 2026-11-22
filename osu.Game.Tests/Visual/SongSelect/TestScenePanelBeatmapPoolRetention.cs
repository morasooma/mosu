#nullable disable

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Graphics.Carousel;
using osu.Game.Overlays;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestScenePanelBeatmapPoolRetention : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [TestCase(false)]
        [TestCase(true)]
        public void TestReturnedPanelDoesNotRetainFullResultOrAcceptStaleCompletion(bool standalone)
        {
            DrawablePool<PanelBeatmap> regularPool = null;
            DrawablePool<PanelBeatmapStandalone> standalonePool = null;
            Panel panel = null;
            BeatmapInfo beatmap = null;
            BeatmapDifficultyCache.BindableStarDifficulty oldBindable = null;
            DifficultyAttributes attributes = new DifficultyAttributes { StarRating = 5, MaxCombo = 321 };
            PerformanceAttributes performance = new PerformanceAttributes { Total = 100 };
            var counts = new[] { new StarDifficulty.CountStatistic("old counts", null, "3") };
            var oldResult = new StarDifficulty(attributes, performance, counts);

            AddStep("load pool", () =>
            {
                beatmap = TestResources.CreateTestBeatmapSetInfo().Beatmaps.First();
                if (standalone)
                    Add(standalonePool = new DrawablePool<PanelBeatmapStandalone>(0, 1));
                else
                    Add(regularPool = new DrawablePool<PanelBeatmap>(0, 1));
            });
            AddStep("get panel", () =>
            {
                panel = standalone
                    ? standalonePool.Get(p => p.Item = new CarouselItem(new GroupedBeatmap(null, beatmap)))
                    : regularPool.Get(p => p.Item = new CarouselItem(new GroupedBeatmap(null, beatmap)));
                Add(panel);
            });
            AddUntilStep("bindable attached", () => getBindable(panel) != null);
            AddStep("publish full result", () =>
            {
                oldBindable = (BeatmapDifficultyCache.BindableStarDifficulty)getBindable(panel);
                oldBindable.Value = oldResult;
            });
            AddAssert("all display holders contain old result", () =>
                ReferenceEquals(getDisplay(panel).Current.Value.DifficultyAttributes, attributes)
                && (!standalone || ReferenceEquals(((PanelBeatmapStandalone)panel).ChildrenOfType<PanelBeatmapStandalone.SpreadDisplay>().Single()
                                                               .StarDifficulty.Value.PerformanceAttributes, performance)));
            AddStep("return panel", () => Remove(panel, false));
            AddAssert("no attributes, performance or counts retained by returned display holders", () =>
                !panel.IsInUse && getDisplay(panel).Current.Value.DifficultyAttributes == null
                && getDisplay(panel).Current.Value.PerformanceAttributes == null && getDisplay(panel).Current.Value.CountStatistics == null
                && (!standalone || ((PanelBeatmapStandalone)panel).ChildrenOfType<PanelBeatmapStandalone.SpreadDisplay>().Single()
                                         .StarDifficulty.Value is { DifficultyAttributes: null, PerformanceAttributes: null, CountStatistics: null }));
            AddStep("simulate stale completion", () => oldBindable.Value = new StarDifficulty(attributes, new PerformanceAttributes { Total = 200 }, counts));
            AddAssert("stale completion cannot restore old references", () =>
                getDisplay(panel).Current.Value.DifficultyAttributes == null
                && (!standalone || ((PanelBeatmapStandalone)panel).ChildrenOfType<PanelBeatmapStandalone.SpreadDisplay>().Single()
                                         .StarDifficulty.Value.PerformanceAttributes == null));
            AddStep("reuse same panel for next map", () =>
            {
                var nextBeatmap = TestResources.CreateTestBeatmapSetInfo().Beatmaps.First();
                Panel reused = standalone
                    ? standalonePool.Get(p => p.Item = new CarouselItem(new GroupedBeatmap(null, nextBeatmap)))
                    : regularPool.Get(p => p.Item = new CarouselItem(new GroupedBeatmap(null, nextBeatmap)));
                Assert.That(reused, Is.SameAs(panel));
                Add(reused);
            });
            AddUntilStep("new bindable attached", () => getBindable(panel) != null && !ReferenceEquals(oldBindable, getBindable(panel)));
            AddStep("late old completion after reuse", () => oldBindable.Value = new StarDifficulty(attributes, new PerformanceAttributes { Total = 300 }, counts));
            AddAssert("old request cannot overwrite reused display", () => getDisplay(panel).Current.Value.DifficultyAttributes == null);
            AddStep("publish next value", () => ((BeatmapDifficultyCache.BindableStarDifficulty)getBindable(panel)).Value = new StarDifficulty(2.5, 12));
            AddAssert("new panel displays new stars and no old references", () =>
                getDisplay(panel).Current.Value is { Stars: 2.5, MaxCombo: 12, DifficultyAttributes: null, PerformanceAttributes: null, CountStatistics: null }
                && (!standalone || ((PanelBeatmapStandalone)panel).ChildrenOfType<PanelBeatmapStandalone.SpreadDisplay>().Single()
                                         .StarDifficulty.Value is { Stars: 2.5, MaxCombo: 12, DifficultyAttributes: null, PerformanceAttributes: null, CountStatistics: null }));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestVisiblePanelRestartsDifficultyAfterTemporaryCollapse(bool standalone)
        {
            Panel panel = null;
            CarouselItem item = null;
            BeatmapDifficultyCache.BindableStarDifficulty previous = null;

            AddStep("show difficulty", () =>
            {
                item = new CarouselItem(new GroupedBeatmap(null, TestResources.CreateTestBeatmapSetInfo().Beatmaps.First()));
                panel = standalone ? new PanelBeatmapStandalone { Item = item } : new PanelBeatmap { Item = item };
                Add(panel);
            });
            AddUntilStep("request attached", () => getBindable(panel) != null);
            AddStep("temporarily collapse item", () =>
            {
                previous = (BeatmapDifficultyCache.BindableStarDifficulty)getBindable(panel);
                item.IsVisible = false;
            });
            AddUntilStep("hidden request cancelled", () => previous.CancellationToken.IsCancellationRequested);
            AddStep("reveal same item without recycling", () => item.IsVisible = true);
            AddUntilStep("visible request restarted", () => getBindable(panel) is BeatmapDifficultyCache.BindableStarDifficulty current
                && !ReferenceEquals(current, previous) && !current.CancellationToken.IsCancellationRequested);
            AddStep("complete resumed preview", () =>
                ((BeatmapDifficultyCache.BindableStarDifficulty)getBindable(panel)).Value = new StarDifficulty(6.25, 123));
            AddAssert("stars update without leaving view", () => getDisplay(panel).Current.Value.Stars == 6.25);
            AddStep("late cancelled completion", () => previous.Value = new StarDifficulty(9, 999));
            AddAssert("cancelled preview cannot overwrite", () => getDisplay(panel).Current.Value.Stars == 6.25);
        }

        [Test]
        public void TestDisposedPanelCancelsDifficultyBeforeDisposingText()
        {
            PanelBeatmap panel = null;
            Container holder = null;
            BeatmapDifficultyCache.BindableStarDifficulty bindable = null;
            StarRatingDisplay display = null;
            AddStep("load panel with additional info", () =>
            {
                Dependencies.Get<OsuConfigManager>().SetValue(OsuSetting.ForkDifficultyAdditionalInfo, true);
                Add(holder = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = panel = new PanelBeatmap
                    {
                        Item = new CarouselItem(new GroupedBeatmap(null, TestResources.CreateTestBeatmapSetInfo().Beatmaps.First())),
                    },
                });
            });
            AddUntilStep("difficulty request attached", () => panel.TrackedDifficulty != null);
            AddStep("dispose while a result is outstanding", () =>
            {
                bindable = (BeatmapDifficultyCache.BindableStarDifficulty)panel.TrackedDifficulty;
                display = getDisplay(panel);
                panel.TopLevelContent.Add(new DisposalProbe(() =>
                    Assert.That(bindable.CancellationToken.IsCancellationRequested, Is.True, "cancel before child disposal")));
                // Tear down the parent without returning the panel to its pool first.
                Remove(holder, false);
                holder.Dispose();
            });
            AddAssert("request cancelled and detached", () => bindable.CancellationToken.IsCancellationRequested && panel.TrackedDifficulty == null);
            AddStep("simulate late difficulty result", () => bindable.Value = new StarDifficulty(9, 999));
            AddAssert("disposed display was not changed", () => display.Current.Value.Stars != 9);
        }

        private partial class DisposalProbe : Drawable
        {
            private readonly Action onDisposing;

            public DisposalProbe(Action onDisposing) => this.onDisposing = onDisposing;

            protected override void Dispose(bool isDisposing)
            {
                if (!IsDisposed)
                    onDisposing();
                base.Dispose(isDisposing);
            }
        }

        private static osu.Framework.Bindables.IBindable<StarDifficulty> getBindable(Panel panel)
            => panel is PanelBeatmapStandalone standalone ? standalone.TrackedDifficulty : ((PanelBeatmap)panel).TrackedDifficulty;

        private static StarRatingDisplay getDisplay(Panel panel) => panel.ChildrenOfType<StarRatingDisplay>().Single();
    }
}
