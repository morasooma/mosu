// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Screens.Select;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual.UserInterface;
using osuTK;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestScenePanelSetBackground : ThemeComparisonTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private TestWorkingBeatmap workingBeatmap = null!;
        private PanelSetBackground background = null!;
        private int previousLoadCount;

        public TestScenePanelSetBackground()
            : base(false)
        {
        }

        [SetUp]
        public void SetUp()
        {
            var beatmapInfo = new BeatmapInfo(metadata: new BeatmapMetadata
            {
                BackgroundFile = "missing-background.jpg"
            })
            {
                BeatmapSet = new BeatmapSetInfo()
            };

            workingBeatmap = new TestWorkingBeatmap(new Beatmap
            {
                BeatmapInfo = beatmapInfo
            });
        }

        [Test]
        public void TestLazyLoadingUsesToriiDelay()
        {
            AddAssert("normal centre loads immediately", () => PanelSetBackground.GetBackgroundLoadDelay(0, false), () => Is.EqualTo(0));
            AddAssert("normal one viewport delay", () => PanelSetBackground.GetBackgroundLoadDelay(1, false), () => Is.EqualTo(100));
            AddAssert("lazy centre waits 200 ms", () => PanelSetBackground.GetBackgroundLoadDelay(0, true), () => Is.EqualTo(200));
            AddAssert("lazy one viewport waits 400 ms", () => PanelSetBackground.GetBackgroundLoadDelay(1, true), () => Is.EqualTo(400));
        }

        [Test]
        public void TestReloadsWhenOldPreviewSettingChanges()
        {
            AddStep("disable skinned legacy mode", () => config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            AddStep("use modern preview", () => config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, false));
            AddStep("display", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddAssert("beatmap accepted without background hash", () => background.Beatmap == workingBeatmap);
            AddUntilStep("modern background loaded", () => background.HasLoadedBackground && !background.IsShowingLegacyPreview);
            AddAssert("working beatmap strongly released after load", () => background.RetainsWorkingBeatmap, () => Is.False);

            AddStep("record load count", () => previousLoadCount = background.BackgroundLoadCount);
            AddStep("enable old preview", () => config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, true));
            AddUntilStep("legacy background loaded", () => background.HasLoadedBackground && background.IsShowingLegacyPreview && background.BackgroundLoadCount > previousLoadCount);
            AddUntilStep("legacy preview has visible width", () => background.LegacyPreviewDrawWidthRatio > 0.05f && background.LegacyPreviewDrawWidthRatio <= 1);
            AddAssert("legacy fallback is translucent", () => background.FallbackBackgroundAlpha, () => Is.LessThan(1));

            AddStep("record load count", () => previousLoadCount = background.BackgroundLoadCount);
            AddStep("disable old preview", () => config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, false));
            AddUntilStep("modern background reloaded", () => background.HasLoadedBackground && !background.IsShowingLegacyPreview && background.BackgroundLoadCount > previousLoadCount);
        }

        [Test]
        public void TestStableStyleUsesSkinnedLegacyLayout()
        {
            AddStep("disable old preview", () => config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, false));
            AddStep("use modern style", () => config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            AddStep("display", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("modern background loaded", () => background.HasLoadedBackground && !background.IsShowingLegacyPreview);

            AddStep("enable stable style", () => config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned));
            AddUntilStep("classic preview loaded", () => background.HasLoadedBackground && background.IsShowingLegacyPreview);
            AddAssert("skinned cards enabled", () => background.SkinnedLegacyModeEnabled, () => Is.True);
            AddAssert("legacy panel corners are square", () => background.CornerRadius, () => Is.Zero);

            AddStep("use modern style", () => config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
        }

        [Test]
        public void TestCarouselPerformanceModeKeepsEnabledPreview()
        {
            AddStep("disable carousel performance mode", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPerformanceMode, false));
            AddStep("enable card previews", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, true));
            AddStep("use modern preview", () => config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, false));
            AddStep("display", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("normal background loaded", () => background.HasLoadedBackground);

            AddStep("record load count", () => previousLoadCount = background.BackgroundLoadCount);
            AddStep("enable carousel performance mode", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPerformanceMode, true));
            AddUntilStep("preview remains enabled", () => background.HasLoadedBackground && background.BackgroundLoadCount > previousLoadCount);
            AddAssert("fallback remains opaque", () => background.FallbackBackgroundAlpha, () => Is.EqualTo(1));

            AddStep("record load count", () => previousLoadCount = background.BackgroundLoadCount);
            AddStep("disable card previews", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, false));
            AddUntilStep("preview removed independently", () => !background.HasLoadedBackground);
            AddWaitStep("wait beyond normal load delay", 5);
            AddAssert("preview stays unloaded", () => background.BackgroundLoadCount, () => Is.EqualTo(previousLoadCount));

            AddStep("enable card previews", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, true));
            AddUntilStep("preview reloads in performance mode", () => background.HasLoadedBackground && background.BackgroundLoadCount > previousLoadCount);
            AddStep("disable carousel performance mode", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPerformanceMode, false));
        }

        [Test]
        public void TestCarouselPreviewsCanBeDisabledIndependently()
        {
            AddStep("disable performance mode", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPerformanceMode, false));
            AddStep("enable card previews", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, true));
            AddStep("display", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("preview loaded", () => background.HasLoadedBackground);

            AddStep("record load count", () => previousLoadCount = background.BackgroundLoadCount);
            AddStep("disable card previews", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, false));
            AddUntilStep("preview removed", () => !background.HasLoadedBackground);
            AddWaitStep("wait beyond normal load delay", 5);
            AddAssert("preview stays unloaded", () => background.BackgroundLoadCount, () => Is.EqualTo(previousLoadCount));

            AddStep("enable card previews", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, true));
            AddUntilStep("preview reloads", () => background.HasLoadedBackground && background.BackgroundLoadCount > previousLoadCount);
        }

        [Test]
        public void TestChangingPreviewResolutionReloadsPreview()
        {
            AddStep("disable performance mode", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPerformanceMode, false));
            AddStep("enable card previews", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, true));
            AddStep("set preview resolution to 100%", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPreviewResolution, 100));
            AddStep("display", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("preview loaded", () => background.HasLoadedBackground);

            AddStep("record load count", () => previousLoadCount = background.BackgroundLoadCount);
            AddStep("set preview resolution to 50%", () => config.SetValue(OsuSetting.ForkSongSelectCarouselPreviewResolution, 50));
            AddUntilStep("preview reloaded", () => background.HasLoadedBackground && background.BackgroundLoadCount > previousLoadCount);
        }

        protected override Drawable CreateContent()
        {
            return new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 0.75f,
                Height = PanelBeatmapSet.HEIGHT,
                RelativeSizeAxes = Axes.X,
                Child = background = new PanelSetBackground
                {
                    Beatmap = workingBeatmap,
                },
            };
        }
    }
}
