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
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;

using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
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
    public partial class PanelBeatmap : Panel
    {
        public const float HEIGHT = CarouselItem.DEFAULT_HEIGHT;
        public const float MANIA_HEIGHT = CarouselItem.DEFAULT_HEIGHT * 1.3f;
        public const float CLASSIC_HEIGHT = CarouselItem.DEFAULT_HEIGHT * 1.3f;

        private StarCounter starCounter = null!;
        private StableStarCounter legacyStarCounter = null!;
        private FillFlowContainer modernDifficultyFlow = null!;
        private ConstrainedIconContainer difficultyIcon = null!;
        private OsuSpriteText additionalStatsText = null!;
        private OsuSpriteText variantText = null!;
        private StarRatingDisplay starRatingDisplay = null!;
        private PanelLocalRankDisplay localRank = null!;
        private OsuSpriteText difficultyText = null!;
        private OsuSpriteText authorText = null!;
        private FillFlowContainer mainFill = null!;

        private IBindable<StarDifficulty>? starDifficultyBindable;
        internal IBindable<StarDifficulty>? TrackedDifficulty => starDifficultyBindable;
        private CancellationTokenSource? starDifficultyCancellationSource;

        private Box backgroundBorder = null!;
        private Box modernBackground = null!;
        private Box backgroundDifficultyTint = null!;
        private LegacyMenuButtonBackground legacyMenuButtonBackground = null!;
        private IBindable<Colour4> themeColour = null!;
        private IBindable<Colour4> themeContentColour = null!;
        private OverlayColourProvider colourProvider = null!;

        private readonly BindableBool useSkinnedLegacyCarousel = new BindableBool();
        private readonly Bindable<ForkSongSelectStyle> songSelectStyle = new Bindable<ForkSongSelectStyle>();

        private TrianglesV2 triangles = null!;

        [Resolved]
        private IRulesetStore rulesets { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        private BeatmapInfo beatmap => ((GroupedBeatmap)Item!.Model).Beatmap;

        public PanelBeatmap()
        {
            PanelXOffset = 60;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, OsuConfigManager config)
        {
            this.colourProvider = colourProvider;
            config.BindWith(OsuSetting.ForkSongSelectStyle, songSelectStyle);
            songSelectStyle.BindValueChanged(style => useSkinnedLegacyCarousel.Value = style.NewValue.UsesStableStyleOn(songSelect is SoloSongSelect), true);

            Height = HEIGHT;

            Icon = difficultyIcon = new ConstrainedIconContainer
            {
                Size = new Vector2(9f),
                Margin = new MarginPadding { Left = 2.5f, Right = 1.5f },
                Colour = colourProvider.Background5,
            };

            Background = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new SongSelectBackdropSurface
                    {
                        IsActive = () => !useSkinnedLegacyCarousel.Value,
                        GetCornerRadius = () => TopLevelContent.CornerRadius,
                    },
                    backgroundBorder = new Box { RelativeSizeAxes = Axes.Both },
                },
            };

            Content.Children = new Drawable[]
            {
                legacyMenuButtonBackground = new LegacyMenuButtonBackground(),
                modernBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4),
                },
                backgroundDifficultyTint = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                },
                triangles = new TrianglesV2
                {
                    ScaleAdjust = 1.2f,
                    Thickness = 0.01f,
                    Velocity = 0.3f,
                    RelativeSizeAxes = Axes.Both,
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    Spacing = new Vector2(5),
                    Margin = new MarginPadding { Left = 6.5f },
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
                            AutoSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Bottom = 3.5f },
                            Children = new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    Direction = FillDirection.Horizontal,
                                    AutoSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Bottom = 4 },
                                    Children = new Drawable[]
                                    {
                                        variantText = new OsuSpriteText
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
                                additionalStatsText = new OsuSpriteText
                                {
                                    Colour = colourProvider.Content2,
                                    Font = OsuFont.Style.Caption2.With(weight: FontWeight.SemiBold),
                                    Alpha = 0,
                                    Margin = new MarginPadding { Top = -2f, Bottom = 1f },
                                },
                                legacyStarCounter = new StableStarCounter
                                {
                                    Alpha = 0,
                                },
                                modernDifficultyFlow = new FillFlowContainer
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
                                        starCounter = new StarCounter
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            Scale = new Vector2(0.4f)
                                        }
                                    },
                                }
                            }
                        }
                    }
                }
            };

            useSkinnedLegacyCarousel.BindValueChanged(legacy =>
            {
                difficultyIcon.Alpha = legacy.NewValue ? 0 : 1;
                difficultyIcon.Size = legacy.NewValue ? Vector2.Zero : new Vector2(9f);
                difficultyIcon.Margin = legacy.NewValue ? new MarginPadding() : new MarginPadding { Left = 2.5f, Right = 1.5f };
                legacyMenuButtonBackground.Alpha = legacy.NewValue ? 1 : 0;
                backgroundBorder.Alpha = legacy.NewValue ? 0 : 1;
                modernBackground.Alpha = legacy.NewValue ? 0 : 1;
                backgroundDifficultyTint.Alpha = legacy.NewValue ? 0 : 1;
                triangles.Alpha = legacy.NewValue ? 0 : 1;
                localRank.Alpha = 1;
                localRank.UseLegacyStyle = legacy.NewValue;
                modernDifficultyFlow.Alpha = starRatingDisplay.Alpha = starCounter.Alpha = legacy.NewValue ? 0 : 1;
                legacyStarCounter.Alpha = legacy.NewValue ? 1 : 0;

                if (legacy.NewValue)
                {
                    mainFill.Padding = new MarginPadding { Bottom = 1 };
                    difficultyText.Font = OsuFont.Style.Body.With(weight: FontWeight.Bold);
                    authorText.Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold);
                }
                else
                {
                    mainFill.Padding = new MarginPadding { Bottom = 3.5f };
                    difficultyText.Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold);
                    authorText.Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold);
                }

                if (!legacy.NewValue && starDifficultyBindable != null)
                    updateAdditionalInfoText(starDifficultyBindable.Value);
            }, true);

            themeColour = colourProvider.GetColourBindable(OverlayColour.Background3);
            themeColour.BindValueChanged(_ => updateThemeColours());
            themeContentColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeContentColour.BindValueChanged(_ => updateThemeColours(), true);
        }

        private void updateThemeColours()
        {
            modernBackground.Colour = ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4);
            authorText.Colour = additionalStatsText.Colour = colourProvider.Content2;
            variantText.Colour = difficultyText.Colour = colourProvider.Content1;

            if (!useSkinnedLegacyCarousel.Value)
                difficultyText.Colour = colourProvider.Content1;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ruleset.BindValueChanged(_ => updateManiaDisplay());
            mods.BindValueChanged(_ => updateManiaDisplay(), true);
            Selected.BindValueChanged(_ =>
            {
                if (starDifficultyBindable != null)
                    computeStarRating();
            });

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

            difficultyIcon.Icon = getRulesetIcon(beatmap.Ruleset);

            localRank.Active = true;
            localRank.Beatmap = beatmap;
            
            if (osu.Game.Database.StablePathManager.IsStableBeatmap(beatmap.ID))
            {
                variantText.AsyncText = "импорт ";
                variantText.Colour = osu.Framework.Graphics.Colour4.LimeGreen;
                variantText.Alpha = 1;
            }
            else
            {
                variantText.Alpha = 0;
            }

            difficultyText.AsyncText = beatmap.DifficultyName;
            authorText.AsyncText = BeatmapsetsStrings.ShowDetailsMappedBy(beatmap.Metadata.Author.Username);

            updateThemeColours();
            computeStarRating();
            Height = Item?.DrawHeight ?? HEIGHT;
            updateManiaDisplay();
        }

        private Drawable getRulesetIcon(RulesetInfo rulesetInfo)
        {
            var rulesetInstance = rulesets.GetRuleset(rulesetInfo.ShortName)?.CreateInstance();

            if (rulesetInstance is null)
                return new SpriteIcon { Icon = FontAwesome.Regular.QuestionCircle };

            return rulesetInstance.CreateIcon();
        }

        protected override void FreeAfterUse()
        {
            base.FreeAfterUse();

            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource = null;
            starDifficultyBindable = null;
            difficultyText.AsyncText = authorText.AsyncText = variantText.AsyncText = additionalStatsText.AsyncText = string.Empty;
            starRatingDisplay.Current.Value = default;
            starCounter.Current = 0;
            legacyStarCounter.Current = 0;
            additionalStatsText.Alpha = 0;
            localRank.Beatmap = null;
            localRank.Active = false;
        }

        protected override void Dispose(bool isDisposing)
        {
            // Parent teardown does not necessarily return this panel to its pool.
            // Cancel before base disposal destroys the text and difficulty displays.
            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource = null;
            starDifficultyBindable = null;
            base.Dispose(isDisposing);
        }

        private void computeStarRating()
        {
            if (IsDisposed)
                return;

            starDifficultyCancellationSource?.Cancel();
            starDifficultyCancellationSource = new CancellationTokenSource();

            if (Item == null)
                return;

            starDifficultyBindable = difficultyCache.GetBindableDifficulty(beatmap, starDifficultyCancellationSource.Token,
                Selected.Value ? SongSelect.PERFORMANCE_CALCULATION_DEBOUNCE : SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE,
                calculatePerformance: Selected.Value, usePersistedAdditionalInfo: true);
            var requestedBindable = starDifficultyBindable;
            var requestedSource = starDifficultyCancellationSource;
            requestedBindable.BindValueChanged(starDifficulty =>
            {
                if (IsDisposed || !ReferenceEquals(starDifficultyBindable, requestedBindable) || requestedSource.IsCancellationRequested)
                    return;

                starRatingDisplay.Current.Value = starDifficulty.NewValue;
                starCounter.Current = legacyStarCounter.Current = (float)starDifficulty.NewValue.Stars;
                updateAdditionalInfoText(starDifficulty.NewValue);
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            if (useSkinnedLegacyCarousel.Value)
            {
                Color4 textColour = Selected.Value ? legacyMenuButtonBackground.ActiveTextColour : legacyMenuButtonBackground.InactiveTextColour;
                difficultyText.Colour = textColour;
                authorText.Colour = textColour;
                additionalStatsText.Hide();
                localRank.Show();
                legacyStarCounter.Colour = textColour;
            }

            if (!useSkinnedLegacyCarousel.Value)
            {
                // Keep the difficulty-coloured backing behind the icon and text.
                backgroundBorder.Alpha = OverlayTransparency.SongSelectActive ? 0.25f : 1f;
                modernBackground.Alpha = OverlayTransparency.SongSelectActive ? 0.15f : 1f;
            }

            localRank.Active = Item?.IsVisible == true;

            if (Item?.IsVisible != true)
            {
                starDifficultyCancellationSource?.Cancel();
                starDifficultyCancellationSource = null;
            }
            else if (starDifficultyCancellationSource == null)
            {
                // A refilter/collapse can hide an item without recycling its panel.
                // Its cancelled preview must be restarted when the same item becomes visible again.
                computeStarRating();
            }

            Height = Item?.DrawHeight ?? HEIGHT;

            // Dirty hack to make sure we don't take up spacing in parent fill flow when not displaying a rank.
            // I can't find a better way to do this.
            var newMargin = useSkinnedLegacyCarousel.Value
                ? new MarginPadding()
                : new MarginPadding { Left = 1 / starRatingDisplay.Scale.X * (localRank.HasRank ? 0 : -3) };
            if (!mainFill.Margin.Equals(newMargin))
                mainFill.Margin = newMargin;

            var diffColour = starRatingDisplay.DisplayedDifficultyColour;

            if (AccentColour != diffColour)
            {
                AccentColour = diffColour;
                starCounter.Colour = diffColour;

                backgroundBorder.Colour = diffColour;
                backgroundDifficultyTint.Colour = ColourInfo.GradientHorizontal(diffColour.Opacity(0.25f), diffColour.Opacity(0f));

                triangles.Colour = ColourInfo.GradientVertical(diffColour.Opacity(0.25f), diffColour.Opacity(0f));
            }

            if (difficultyIcon.Colour != starRatingDisplay.DisplayedDifficultyTextColour)
            {
                difficultyIcon.Colour = starRatingDisplay.DisplayedDifficultyTextColour;
            }
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

                variantText.Alpha = 1;
                variantText.AsyncText = LocalisableString.Interpolate($"[{variantName}] ");
            }
            else
                variantText.Alpha = 0;
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

            additionalStatsText.AsyncText = BeatmapAdditionalInfoFormatter.Format(starDiff);
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
