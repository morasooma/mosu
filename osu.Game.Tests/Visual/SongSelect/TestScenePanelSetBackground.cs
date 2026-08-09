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
        public void TestReloadsWhenOldPreviewSettingChanges()
        {
            AddStep("disable skinned legacy mode", () => config.SetValue(OsuSetting.ForkSongSelectSkinnedLegacyCarousel, false));
            AddStep("use modern preview", () => config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, false));
            AddStep("display", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddAssert("beatmap accepted without background hash", () => background.Beatmap == workingBeatmap);
            AddUntilStep("modern background loaded", () => background.HasLoadedBackground && !background.IsShowingLegacyPreview);

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
        public void TestSkinnedLegacyModeEnablesClassicPreviewLayout()
        {
            AddStep("disable old preview", () => config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, false));
            AddStep("disable skinned legacy mode", () => config.SetValue(OsuSetting.ForkSongSelectSkinnedLegacyCarousel, false));
            AddStep("display", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("modern background loaded", () => background.HasLoadedBackground && !background.IsShowingLegacyPreview);

            AddStep("record load count", () => previousLoadCount = background.BackgroundLoadCount);
            AddStep("enable skinned legacy mode", () => config.SetValue(OsuSetting.ForkSongSelectSkinnedLegacyCarousel, true));
            AddUntilStep("classic preview loaded", () => background.HasLoadedBackground && background.IsShowingLegacyPreview && background.BackgroundLoadCount > previousLoadCount);
            AddAssert("skinned mode enabled", () => background.SkinnedLegacyModeEnabled);
            AddAssert("panel corners are square", () => background.CornerRadius, () => Is.Zero);
            AddUntilStep("classic preview has visible width", () => background.LegacyPreviewDrawWidthRatio > 0.05f && background.LegacyPreviewDrawWidthRatio <= 1);

            AddStep("disable skinned legacy mode", () => config.SetValue(OsuSetting.ForkSongSelectSkinnedLegacyCarousel, false));
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
