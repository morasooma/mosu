using System;
using System.Linq;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Development;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Carousel;
using osu.Game.Overlays;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Visual.UserInterface;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestScenePanelBackgroundRetrieval : ThemeComparisonTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private readonly BlockingCache cache = new BlockingCache();
        private Panel panel = null!;
        private bool standalone;
        private BeatmapSetInfo set = null!;
        private int updateFrames;
        private int blockedAtFrame;

        public TestScenePanelBackgroundRetrieval()
            : base(false)
        {
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset retrieval probe", () =>
            {
                cache.Release.Set();
                config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, true);
                set = TestResources.CreateTestBeatmapSetInfo();
                cache.Calls = 0;
                cache.Entered.Reset();
                cache.Release.Reset();
                cache.CalledOnUpdate = false;
                cache.PlayableCalls = 0;
                standalone = false;
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestColdRetrievalDoesNotBlockUpdateAndPublishesOnCompletion(bool standalone)
        {
            createPanel(standalone);
            AddUntilStep("cold worker entered", () => cache.Entered.IsSet);
            AddStep("record Update progress", () => blockedAtFrame = updateFrames);
            AddWaitStep("updates continue while lookup is blocked", 5);
            AddAssert("Update advances", () => updateFrames > blockedAtFrame);
            AddAssert("artwork-only lookup off Update", () => !cache.CalledOnUpdate && cache.PlayableCalls == 0);
            AddAssert("background not prematurely published", () => getBackground().Beatmap == null);
            AddStep("release lookup", () => cache.Release.Set());
            AddUntilStep("correct source published", () => getBackground().Beatmap?.BeatmapInfo.ID == set.Beatmaps[0].ID);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestReturnedPanelDoesNotPublishLateLookup(bool standalone)
        {
            createPanel(standalone);
            AddUntilStep("cold worker entered", () => cache.Entered.IsSet);
            AddStep("return panel and release lookup", () =>
            {
                panel.Return();
                cache.Release.Set();
            });
            AddWaitStep("let completion reach scheduler", 10);
            AddAssert("returned panel remains empty", () => getBackground().Beatmap == null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestDisposedPanelDoesNotPublishLateLookup(bool standalone)
        {
            createPanel(standalone);
            AddUntilStep("cold worker entered", () => cache.Entered.IsSet);
            AddStep("dispose panel and release lookup", () =>
            {
                ContentContainer.Clear();
                cache.Release.Set();
            });
            AddUntilStep("panel disposed", () => panel is ProbeStandalonePanel standalonePanel ? standalonePanel.Disposed : ((ProbePanel)panel).Disposed);
            AddWaitStep("allow worker to finish", 10);
            AddAssert("disposed panel still has no source", () => getBackground().Beatmap == null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestReplacementDoesNotPublishPreviousSet(bool standalone)
        {
            createPanel(standalone);
            BeatmapSetInfo replacement = null!;
            AddUntilStep("old lookup entered", () => cache.Entered.IsSet);
            AddStep("reuse for another set", () =>
            {
                replacement = TestResources.CreateTestBeatmapSetInfo();
                panel.Return();
                panel.Item = createItem(replacement);
                prepareReplacement();
                cache.Release.Set();
            });
            AddUntilStep("replacement published", () => getBackground().Beatmap?.BeatmapInfo.ID == replacement.Beatmaps[0].ID);
            AddAssert("both lookups off Update", () => cache.Calls == 2 && !cache.CalledOnUpdate);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestDisablingPreviewsRejectsInFlightLookup(bool standalone)
        {
            createPanel(standalone);
            AddUntilStep("cold worker entered", () => cache.Entered.IsSet);
            AddStep("disable previews and release lookup", () =>
            {
                config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, false);
                cache.Release.Set();
            });
            AddWaitStep("allow completion to reach scheduler", 10);
            AddAssert("disabled panel has no source", () => getBackground().Beatmap == null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestDisabledPreviewsDoNotStartLookup(bool standalone)
        {
            createPanel(standalone);
            AddStep("cancel first request", () =>
            {
                panel.Return();
                cache.Release.Set();
            });
            AddWaitStep("first request drains", 5);
            int calls = 0;
            AddStep("prepare disabled panel", () =>
            {
                calls = cache.Calls;
                config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, false);
                panel.Item = createItem(set);
                prepareReplacement();
            });
            AddWaitStep("wait beyond retrieval admission", 10);
            AddAssert("no additional lookup", () => cache.Calls == calls && getBackground().Beatmap == null);
        }

        [Test]
        public void TestStandaloneReplacementWithoutReturnDoesNotPublishPreviousDifficulty()
        {
            createPanel(true);
            BeatmapSetInfo replacement = null!;
            AddUntilStep("old lookup entered", () => cache.Entered.IsSet);
            AddStep("replace active item without return", () =>
            {
                replacement = TestResources.CreateTestBeatmapSetInfo();
                panel.Item = createItem(replacement);
                cache.Release.Set();
            });
            AddUntilStep("replacement published", () => getBackground().Beatmap?.BeatmapInfo.ID == replacement.Beatmaps[0].ID);
            AddAssert("both lookups use worker artwork path", () => cache.Calls == 2 && !cache.CalledOnUpdate && cache.PlayableCalls == 0);
        }

        [Test]
        public void TestStandaloneLayoutChangesDoNotRefetchArtwork()
        {
            createPanel(true);
            AddUntilStep("lookup entered", () => cache.Entered.IsSet);
            AddStep("complete lookup", () => cache.Release.Set());
            AddUntilStep("source published", () => getBackground().Beatmap?.BeatmapInfo.ID == set.Beatmaps[0].ID);
            AddStep("switch preview layout", () => config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, true));
            AddWaitStep("layout updated", 5);
            AddStep("restore preview layout", () => config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, false));
            AddWaitStep("layout restored", 5);
            AddAssert("layout does not retrieve playable or repeat artwork", () => cache.Calls == 1 && cache.PlayableCalls == 0);
        }

        [Test]
        public void TestStandaloneLayoutRestoresMissingWeakSource()
        {
            createPanel(true);
            AddUntilStep("lookup entered", () => cache.Entered.IsSet);
            AddStep("complete lookup", () => cache.Release.Set());
            AddUntilStep("source published", () => getBackground().Beatmap?.BeatmapInfo.ID == set.Beatmaps[0].ID);
            AddStep("simulate collected source and switch layout", () =>
            {
                getBackground().Beatmap = null;
                config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, true);
            });
            AddUntilStep("source restored for new layout", () => getBackground().Beatmap?.BeatmapInfo.ID == set.Beatmaps[0].ID);
            AddAssert("replacement uses artwork worker", () => cache.Calls == 2 && cache.PlayableCalls == 0 && !cache.CalledOnUpdate);
        }

        [Test]
        public void TestStandaloneEnablingPreviewsRestartsCancelledLookup()
        {
            createPanel(true);
            AddUntilStep("lookup entered", () => cache.Entered.IsSet);
            AddStep("disable and complete stale lookup", () =>
            {
                config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, false);
                cache.Release.Set();
            });
            AddWaitStep("stale completion drains", 5);
            AddAssert("disabled source cleared", () => getBackground().Beatmap == null);
            AddStep("enable previews", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, true));
            AddUntilStep("current source published", () => getBackground().Beatmap?.BeatmapInfo.ID == set.Beatmaps[0].ID);
            AddAssert("fresh artwork lookup off Update", () => cache.Calls == 2 && !cache.CalledOnUpdate && cache.PlayableCalls == 0);
        }

        private void createPanel(bool standalone) => AddStep("create panel", () =>
        {
            this.standalone = standalone;
            CreateThemedContent(OverlayColourScheme.Aquamarine);
        });

        private CarouselItem createItem(BeatmapSetInfo beatmapSet)
            => new CarouselItem(standalone ? new GroupedBeatmap(null, beatmapSet.Beatmaps[0]) : new GroupedBeatmapSet(null, beatmapSet));

        private void prepareReplacement()
        {
            if (panel is ProbeStandalonePanel standalonePanel)
                standalonePanel.PrepareReplacement();
            else
                ((ProbePanel)panel).PrepareReplacement();
        }

        private PanelSetBackground getBackground() => panel.ChildrenOfType<PanelSetBackground>().Single();

        protected override Drawable CreateContent() => new DependencyProvidingContainer
        {
            RelativeSizeAxes = Axes.Both,
            CachedDependencies = new (Type, object)[] { (typeof(IWorkingBeatmapCache), cache) },
            Child = panel = createContentPanel(),
        };

        private Panel createContentPanel()
        {
            Panel created = standalone ? new ProbeStandalonePanel() : new ProbePanel();
            created.Item = createItem(set);
            created.Width = 0.8f;
            created.Anchor = created.Origin = Anchor.Centre;
            return created;
        }

        protected override void Update()
        {
            base.Update();
            updateFrames++;
        }

        protected override void Dispose(bool isDisposing)
        {
            cache.Release.Set();
            base.Dispose(isDisposing);
        }

        private partial class ProbePanel : PanelBeatmapSet
        {
            public bool Disposed => IsDisposed;
            public void PrepareReplacement() => base.PrepareForUse();
        }

        private partial class ProbeStandalonePanel : PanelBeatmapStandalone
        {
            public bool Disposed => IsDisposed;
            public void PrepareReplacement() => base.PrepareForUse();
        }

        private sealed class BlockingCache : IWorkingBeatmapCache
        {
            public readonly ManualResetEventSlim Entered = new ManualResetEventSlim();
            public readonly ManualResetEventSlim Release = new ManualResetEventSlim();
            public int Calls;
            public int PlayableCalls;
            public volatile bool CalledOnUpdate;

            public WorkingBeatmap GetWorkingBeatmap(BeatmapInfo? info)
            {
                Interlocked.Increment(ref PlayableCalls);
                return retrieve(info);
            }

            public WorkingBeatmap GetCarouselBackgroundBeatmap(BeatmapInfo? info) => retrieve(info);

            private WorkingBeatmap retrieve(BeatmapInfo? info)
            {
                Interlocked.Increment(ref Calls);
                CalledOnUpdate |= ThreadSafety.IsUpdateThread;
                Entered.Set();
                // Report a synchronous regression through assertions instead of
                // blocking the same Update thread that must release the test gate.
                if (!ThreadSafety.IsUpdateThread && !Release.Wait(TimeSpan.FromSeconds(3)))
                    throw new TimeoutException("Background retrieval test gate was not released.");
                return new TestWorkingBeatmap(new Beatmap { BeatmapInfo = info! });
            }

            public void Invalidate(BeatmapSetInfo beatmapSetInfo) { }
            public void Invalidate(BeatmapInfo beatmapInfo) { }
        }
    }
}
