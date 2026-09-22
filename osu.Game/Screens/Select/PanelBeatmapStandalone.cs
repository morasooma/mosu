// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Resources.Localisation.Web;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mania;

using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class PanelBeatmapStandalone : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT * 1.6f;
        public const float MANIA_HEIGHT = CarouselItem.DEFAULT_HEIGHT * 1.9f;
        public const float CLASSIC_HEIGHT = CarouselItem.DEFAULT_HEIGHT * 2f;

        private const float content_margin_left = 6.5f;
        private const float legacy_preview_content_gap = 14;

        private readonly BindableBool useLegacyPreviewLayout = new BindableBool();
        private readonly BindableBool useSkinnedLegacyCarousel = new BindableBool();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        private IBindable<StarDifficulty>? starDifficultyBindable;
        private CancellationTokenSource? starDifficultyCancellationSource;

        private PanelSetBackground beatmapBackground = null!;
        private Box classicSelectionBackground = null!;

        private ScheduledDelegate? scheduledBackgroundRetrieval;

        private FillFlowContainer contentFlow = null!;
        private OsuSpriteText titleText = null!;
        private OsuSpriteText artistText = null!;
        private PanelUpdateBeatmapButton updateButton = null!;
        private BeatmapSetOnlineStatusPill statusPill = null!;
        private BeatmapSetOnlineStatusPill importedPill = null!;

        private ConstrainedIconContainer difficultyIcon = null!;
        private StarRatingDisplay starRatingDisplay = null!;
        private StarCounter legacyStarCounter = null!;
        private SpreadDisplay spreadDisplay = null!;
        private PanelLocalRankDisplay localRank = null!;
        private OsuSpriteText keyCountText = null!;
        private OsuSpriteText additionalStatsText = null!;
        private OsuSpriteText difficultyText = null!;
        private OsuSpriteText authorText = null!;
        private FillFlowContainer mainFill = null!;

        private Box backgroundBorder = null!;
        private IBindable<Colour4> themeColour = null!;

        private BeatmapInfo beatmap => ((GroupedBeatmap)Item!.Model).Beatmap;

        internal bool ContentClearsLegacyPreview
            => contentFlow.Margin.Left >= PanelSetBackground.GetLegacyPreviewWidth(Content.DrawWidth, Content.DrawHeight);

        internal bool LegacyRankVisible => !useSkinnedLegacyCarousel.Value || localRank.Alpha > 0;

        internal bool LegacyStarsVisible => !useSkinnedLegacyCarousel.Value || legacyStarCounter.Alpha > 0;

        internal bool UsesLegacyDifficultyInactiveTint
            => !useSkinnedLegacyCarousel.Value || Selected.Value || beatmapBackground.Colour == new Color4(0, 150, 236, 255);

        internal float LegacySelectionOverlayAlpha => classicSelectionBackground.Alpha;

        internal Color4 TitleTextColour => titleText.Colour;
        internal Color4 ArtistTextColour => artistText.Colour;
        internal Color4 DifficultyTextColour => difficultyText.Colour;

        public PanelBeatmapStandalone()
        {
            PanelXOffset = 20;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ForkSongSelectOldCarouselPreviews, useLegacyPreviewLayout);
            ForkSongSelectStyleBinding.BindSkinnedLegacyCarousel(config, useSkinnedLegacyCarousel, () => songSelect is SoloSongSelect);
            useLegacyPreviewLayout.BindValueChanged(_ => ScheduleAfterChildren(updatePreviewLayoutState));
            useSkinnedLegacyCarousel.BindValueChanged(_ => ScheduleAfterChildren(updatePreviewLayoutState));

            Height = HEIGHT;

            Icon = difficultyIcon = new ConstrainedIconContainer
            {
                Size = new Vector2(12),
                Margin = new MarginPadding { Left = 4f, Right = 3f },
                Colour = colourProvider.Background5,
            };

            Background = backgroundBorder = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };

            Content.Children = new Drawable[]
            {
                beatmapBackground = new PanelSetBackground(),

                classicSelectionBackground = new Box
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Alpha = 0,
                },
                contentFlow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Spacing = new Vector2(5),
                    Margin = new MarginPadding { Left = content_margin_left },
                    Direction = FillDirection.Horizontal,
                    Children = new Drawable[]
                    {
                        localRank = new PanelLocalRankDisplay
                        {
                            Scale = new Vector2(0.8f),
                            Origin = Anchor.CentreLeft,
                            Anchor = Anchor.CentreLeft,
                        },
                        mainFill = new FillFlowContainer
                        {
                            Anchor = Anchor.CentreLeft,
                            Origin = Anchor.CentreLeft,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding { Bottom = 4.8f },
                            AutoSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                titleText = new OsuSpriteText
                                {
                                    Font = OsuFont.Style.Heading2.With(typeface: Typeface.TorusAlternate, weight: FontWeight.Bold),
                                },
                                artistText = new OsuSpriteText
                                {
                                    Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                    Padding = new MarginPadding { Top = -2 },
                                },
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    AutoSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Top = 2, Bottom = 2 },
                                    Children = new Drawable[]
                                    {
                                        statusPill = new BeatmapSetOnlineStatusPill
                                        {
                                            Animated = false,
                                            Origin = Anchor.BottomLeft,
                                            Anchor = Anchor.BottomLeft,
                                            TextSize = OsuFont.Style.Caption2.Size,
                                            Margin = new MarginPadding { Right = 4f },
                                        },
                                        importedPill = new BeatmapSetOnlineStatusPill
                                        {
                                            Animated = false,
                                            Origin = Anchor.BottomLeft,
                                            Anchor = Anchor.BottomLeft,
                                            TextSize = OsuFont.Style.Caption2.Size,
                                            Margin = new MarginPadding { Right = 4f },
                                            TextOverride = @"imported",
                                            TooltipOverride = @"Directly read from osu!stable",
                                            BackgroundColourOverride = Color4.ForestGreen,
                                            TextColourOverride = Color4.White,
                                        },
                                        updateButton = new PanelUpdateBeatmapButton
                                        {
                                            Scale = new Vector2(0.8f),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Margin = new MarginPadding { Right = 4f, Bottom = -1f },
                                        },
                                        keyCountText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Alpha = 0,
                                        },
                                        difficultyText = new OsuSpriteText
                                        {
                                            Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft,
                                            Margin = new MarginPadding { Right = 3f },
                                        },
                                        authorText = new OsuSpriteText
                                        {
                                            Colour = colourProvider.Content2,
                                            Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                            Anchor = Anchor.BottomLeft,
                                            Origin = Anchor.BottomLeft
                                        }
                                    }
                                },
                                legacyStarCounter = new StarCounter
                                {
                                    Anchor = Anchor.TopLeft,
                                    Origin = Anchor.TopLeft,
                                    Scale = new Vector2(0.42f),
                                    Alpha = 0,
                                },
                                additionalStatsText = new OsuSpriteText
                                {
                                    Colour = colourProvider.Content2,
                                    Font = OsuFont.Style.Caption2.With(weight: FontWeight.SemiBold),
                                    Alpha = 0,
                                    Margin = new MarginPadding { Top = -1f, Bottom = 1f },
                                },
                                new FillFlowContainer
                                {
                                    Anchor = Anchor.TopLeft,
                                    Origin = Anchor.TopLeft,
                                    Direction = FillDirection.Horizontal,
                                    Spacing = new Vector2(3),
                                    AutoSizeAxes = Axes.Both,
                                    Children = new Drawable[]
                                    {
                                        starRatingDisplay = new StarRatingDisplay(default, StarRatingDisplaySize.Small, animated: true)
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Scale = new Vector2(0.875f),
                                        },
                                        spreadDisplay = new SpreadDisplay
                                        {
                                            Origin = Anchor.CentreLeft,
                                            Anchor = Anchor.CentreLeft,
                                            Selected = { BindTarget = Selected },
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            // Keep the panel's text palette subscribed for its entire lifetime.
            // This used to be created from Update(), which added a new binding
            // every frame and left pooled carousel items with stale colours.
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateThemeColours(), true);
        }

        private void updateThemeColours()
        {
            artistText.Colour = authorText.Colour = additionalStatsText.Colour = Color4.White.Opacity(0.85f);
            keyCountText.Colour = Color4.White;

            if (!useSkinnedLegacyCarousel.Value)
                titleText.Colour = difficultyText.Colour = Color4.White;

            updateClassicSelectionState();
        }

        private void updatePreviewLayoutState()
        {
            difficultyIcon.Alpha = useSkinnedLegacyCarousel.Value ? 0 : 1;
            difficultyIcon.Size = useSkinnedLegacyCarousel.Value ? Vector2.Zero : new Vector2(12);
            difficultyIcon.Margin = useSkinnedLegacyCarousel.Value ? new MarginPadding() : new MarginPadding { Left = 4f, Right = 3f };
            backgroundBorder.Alpha = useSkinnedLegacyCarousel.Value ? 0 : 1;

            statusPill.Alpha = useSkinnedLegacyCarousel.Value ? 0 : 1;
            importedPill.Alpha = useSkinnedLegacyCarousel.Value ? 0 : 1;
            updateButton.Alpha = useSkinnedLegacyCarousel.Value ? 0 : 1;
            localRank.Alpha = 1;
            starRatingDisplay.Alpha = useSkinnedLegacyCarousel.Value ? 0 : 1;
            legacyStarCounter.Alpha = useSkinnedLegacyCarousel.Value ? 1 : 0;

            if (!useSkinnedLegacyCarousel.Value && starDifficultyBindable != null)
                updateAdditionalInfoText(starDifficultyBindable.Value);

            updateClassicSelectionState();

            if (Item?.Model is GroupedBeatmap groupedBeatmap)
                beatmapBackground.Beatmap = beatmaps.GetWorkingBeatmap(groupedBeatmap.Beatmap);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => updateManiaDisplay());
            mods.BindValueChanged(_ => updateManiaDisplay(), true);

            Selected.BindValueChanged(s =>
            {
                Expanded.Value = s.NewValue;
                updateClassicSelectionState();

                if (starDifficultyBindable != null)
                    computeStarRating();
            }, true);

            config.GetBindable<bool>(OsuSetting.ForkDifficultyAdditionalInfo).BindValueChanged(_ =>
            {
                if (starDifficultyBindable != null)
                    updateAdditionalInfoText(starDifficultyBindable.Value);
                else
                    additionalStatsText.Alpha = 0;
            });
        }

        protected override void PrepareForUse()
        {
            base.PrepareForUse();

            var beatmapSet = beatmap.BeatmapSet!;

            if (CarouselPreviews.Value)
                scheduledBackgroundRetrieval = Scheduler.AddDelayed(b => beatmapBackground.Beatmap = beatmaps.GetWorkingBeatmap(b), beatmap, 50);

            titleText.Text = new RomanisableString(beatmapSet.Metadata.TitleUnicode, beatmapSet.Metadata.Title);
            artistText.Text = new RomanisableString(beatmapSet.Metadata.ArtistUnicode, beatmapSet.Metadata.Artist);
            updateButton.Beatmap = beatmap;
            updateButton.BeatmapSet = beatmapSet;
            statusPill.Status = beatmap.Status;
            importedPill.Status = StablePathManager.IsStableBeatmap(beatmap.ID) ? BeatmapOnlineStatus.Ranked : BeatmapOnlineStatus.None;

            difficultyIcon.Icon = beatmap.Ruleset.CreateInstance().CreateIcon();
            difficultyIcon.Show();

            localRank.Beatmap = beatmap;
            difficultyText.Text = beatmap.DifficultyName;
            authorText.Text = BeatmapsetsStrings.ShowDetailsMappedBy(beatmap.Metadata.Author.Username);

            computeStarRating();
            spreadDisplay.Beatmap.Value = beatmap;
            Height = Item?.DrawHeight ?? HEIGHT;
            updateManiaDisplay();
            updateThemeColours();
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            scheduledBackgroundRetrieval?.Cancel();
            scheduledBackgroundRetrieval = null;
            beatmapBackground.Beatmap = null;
            updateButton.Beatmap = null;
            updateButton.BeatmapSet = null;
            importedPill.Status = BeatmapOnlineStatus.None;
            localRank.Beatmap = null;
            starDifficultyBindable = null;
            spreadDisplay.Beatmap.Value = null;

            starDifficultyCancellationSource?.Cancel();
        }

        private void computeStarRating()
        {
            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource = new CancellationTokenSource();

            if (Item == null)
                return;

            starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token,
                SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE, calculatePerformance: Selected.Value, usePersistedAdditionalInfo: true);
            starDifficultyBindable.BindValueChanged(starDifficulty =>
            {
                starRatingDisplay.Current.Value = starDifficulty.NewValue;
                legacyStarCounter.Current = (float)starDifficulty.NewValue.Stars;
                spreadDisplay.StarDifficulty.Value = starDifficulty.NewValue;
                updateAdditionalInfoText(starDifficulty.NewValue);
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            float previewWidth = PanelSetBackground.GetLegacyPreviewWidth(Content.DrawWidth, Content.DrawHeight, useSkinnedLegacyCarousel.Value);
            classicSelectionBackground.Width = Content.DrawWidth <= 0 ? 1 : Math.Max(0, 1 - previewWidth / Content.DrawWidth);

            if (useSkinnedLegacyCarousel.Value)
            {
                Color4 textColour = Selected.Value ? beatmapBackground.LegacyActiveTextColour : beatmapBackground.LegacyInactiveTextColour;

                classicSelectionBackground.Colour = beatmapBackground.LegacyMenuGlowColour;
                statusPill.Hide();
                importedPill.Hide();
                updateButton.Hide();
                starRatingDisplay.Hide();
                additionalStatsText.Hide();
                legacyStarCounter.Colour = textColour;
                legacyStarCounter.Show();
                titleText.Colour = textColour;
                artistText.Colour = textColour;
                difficultyText.Colour = textColour;
                authorText.Colour = textColour;
            }

            if (Item?.IsVisible != true)
            {
                starDifficultyCancellationSource?.Cancel();
                starDifficultyCancellationSource = null;
            }

            Height = Item?.DrawHeight ?? HEIGHT;

            // Dirty hack to make sure we don't take up spacing in parent fill flow when not displaying a rank.
            // I can't find a better way to do this.
            mainFill.Margin = new MarginPadding { Left = 1 / starRatingDisplay.Scale.X * (localRank.HasRank ? 0 : -3) };

            var diffColour = starRatingDisplay.DisplayedDifficultyColour;

            AccentColour = diffColour;
            spreadDisplay.Current.Colour = diffColour;

            beatmapBackground.Colour = useSkinnedLegacyCarousel.Value && !Selected.Value
                ? new Color4(0, 150, 236, 255)
                : Color4.White;

            backgroundBorder.Colour = diffColour;
            difficultyIcon.Colour = starRatingDisplay.DisplayedDifficultyTextColour;

            contentFlow.Margin = contentFlow.Margin with
            {
                Left = useLegacyPreviewLayout.Value || useSkinnedLegacyCarousel.Value
                    ? Math.Max(content_margin_left, PanelSetBackground.GetLegacyPreviewWidth(Content.DrawWidth, Content.DrawHeight, useSkinnedLegacyCarousel.Value) + legacy_preview_content_gap)
                    : content_margin_left
            };

        }

        private void updateClassicSelectionState()
        {
            bool classicSelected = useSkinnedLegacyCarousel.Value && Selected.Value;

            classicSelectionBackground.Colour = beatmapBackground.LegacyMenuGlowColour;
            double duration = OverlayColourProvider.ThemeTransitionDuration(DURATION / 2);
            classicSelectionBackground.FadeTo(0, duration, Easing.OutQuint);
            Color4 textColour = classicSelected ? beatmapBackground.LegacyActiveTextColour : beatmapBackground.LegacyInactiveTextColour;
            titleText.FadeColour(useSkinnedLegacyCarousel.Value ? textColour : Color4.White, duration, Easing.OutQuint);
            artistText.FadeColour(useSkinnedLegacyCarousel.Value ? textColour : Color4.White.Opacity(0.85f), duration, Easing.OutQuint);
            keyCountText.FadeColour(useSkinnedLegacyCarousel.Value ? textColour : Color4.White, duration, Easing.OutQuint);
            difficultyText.FadeColour(useSkinnedLegacyCarousel.Value ? textColour : Color4.White, duration, Easing.OutQuint);
            authorText.FadeColour(useSkinnedLegacyCarousel.Value ? textColour : Color4.White.Opacity(0.85f), duration, Easing.OutQuint);
        }

        private void updateKeyCount()
        {
            if (Item == null)
                return;

            var rulesetInstance = ruleset.Value.CreateInstance();

            if (rulesetInstance.GameplayVariants.Count() > 1)
            {
                int variant = rulesetInstance.GetVariantForBeatmap(beatmap, mods.Value);
                var variantName = rulesetInstance.GetVariantName(variant);

                keyCountText.Alpha = 1;
                keyCountText.Text = LocalisableString.Interpolate($"[{variantName}] ");
            }
            else
                keyCountText.Alpha = 0;
        }

        private void updateManiaDisplay()
        {
            updateKeyCount();
        }

        private void updateAdditionalInfoText(StarDifficulty starDiff)
        {
            if (!config.Get<bool>(OsuSetting.ForkDifficultyAdditionalInfo))
            {
                additionalStatsText.Alpha = 0;
                return;
            }

            additionalStatsText.Text = BeatmapAdditionalInfoFormatter.Format(starDiff);
            additionalStatsText.Alpha = 1;
        }

        public override MenuItem[] ContextMenuItems
        {
            get
            {
                if (Item == null)
                    return Array.Empty<MenuItem>();

                List<MenuItem> items = new List<MenuItem>();

                if (songSelect != null)
                    items.AddRange(songSelect.GetForwardActions(beatmap));

                return items.ToArray();
            }
        }
    }
}
