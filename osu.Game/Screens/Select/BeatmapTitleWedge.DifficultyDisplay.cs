// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapTitleWedge
    {
        public partial class DifficultyDisplay : CompositeDrawable
        {
            private const float border_weight = 2;

            [Resolved]
            private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

            [Resolved]
            private IBindable<RulesetInfo> ruleset { get; set; } = null!;

            [Resolved]
            private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

            private ModSettingChangeTracker? settingChangeTracker;

            [Resolved]
            private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

            private StarRatingDisplay starRatingDisplay = null!;
            private FillFlowContainer nameLine = null!;
            private OsuSpriteText difficultyText = null!;
            private OsuSpriteText mappedByText = null!;
            private OsuHoverContainer mapperLink = null!;
            private OsuSpriteText mapperText = null!;

            private GridContainer ratingAndNameContainer = null!;
            private DifficultyStatisticsDisplay countStatisticsDisplay = null!;
            private DifficultyStatisticsDisplay difficultyStatisticsDisplay = null!;
            private Box statContainerBackground = null!;
            private IBindable<Colour4> themeColour = null!;
            private OverlayColourProvider colourProvider = null!;

            internal Color4 MappedByTextColour => mappedByText.Colour;

            private CancellationTokenSource? cancellationSource;
            private CancellationTokenSource? countWorkSource;
            private CancellationTokenSource? countFallbackSource;
            private readonly object countFallbackLock = new object();
            private WorkingBeatmap? countFallbackBeatmap;
            private RulesetInfo? countFallbackRuleset;
            private Task<IReadOnlyList<StarDifficulty.CountStatistic>>? countFallbackTask;
            private BeatmapDifficultyCache.BindableStarDifficulty? countBindable;
            private int countFallbacksStarted;
            private int countFallbackWaitsStarted;
            internal int CountFallbacksStarted => Volatile.Read(ref countFallbacksStarted);
            internal int CountFallbackWaitsStarted => Volatile.Read(ref countFallbackWaitsStarted);

            public DifficultyDisplay()
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colourProvider)
            {
                this.colourProvider = colourProvider;
                Masking = true;
                CornerRadius = 10;
                Shear = OsuGame.SHEAR;

                InternalChildren = new Drawable[]
                {
                    new WedgeBackground(),
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new ShearAligningWrapper(ratingAndNameContainer = new GridContainer
                            {
                                Shear = -OsuGame.SHEAR,
                                AlwaysPresent = true,
                                RelativeSizeAxes = Axes.X,
                                Height = 20,
                                Margin = new MarginPadding { Vertical = 5f },
                                Padding = new MarginPadding { Left = SongSelect.WEDGE_CONTENT_MARGIN },
                                RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                ColumnDimensions = new[]
                                {
                                    new Dimension(GridSizeMode.AutoSize),
                                    new Dimension(GridSizeMode.Absolute, 6),
                                    new Dimension(),
                                },
                                Content = new[]
                                {
                                    new[]
                                    {
                                        starRatingDisplay = new StarRatingDisplay(default, animated: true)
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                        },
                                        Empty(),
                                        nameLine = new FillFlowContainer
                                        {
                                            Anchor = Anchor.CentreLeft,
                                            Origin = Anchor.CentreLeft,
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Direction = FillDirection.Horizontal,
                                            Margin = new MarginPadding { Bottom = 2f },
                                            Children = new Drawable[]
                                            {
                                                difficultyText = new TruncatingSpriteText
                                                {
                                                    Anchor = Anchor.BottomLeft,
                                                    Origin = Anchor.BottomLeft,
                                                    Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                                },
                                                mappedByText = new OsuSpriteText
                                                {
                                                    Anchor = Anchor.BottomLeft,
                                                    Origin = Anchor.BottomLeft,
                                                    Text = global::osu.Game.Localisation.ClientTranslationStrings.J5153,
                                                    Font = OsuFont.Style.Body,
                                                },
                                                mapperLink = new MapperLinkContainer
                                                {
                                                    AutoSizeAxes = Axes.Both,
                                                    Anchor = Anchor.BottomLeft,
                                                    Origin = Anchor.BottomLeft,
                                                    Child = mapperText = new TruncatingSpriteText
                                                    {
                                                        Shadow = true,
                                                        Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                                                    },
                                                },
                                            },
                                        },
                                    }
                                },
                            }),
                            new ShearAligningWrapper(new Container
                            {
                                Shear = -OsuGame.SHEAR,
                                RelativeSizeAxes = Axes.X,
                                Height = 53,
                                Padding = new MarginPadding { Bottom = border_weight, Right = border_weight },
                                Child = new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Masking = true,
                                    CornerRadius = 10 - border_weight,
                                    Shear = OsuGame.SHEAR,
                                    Children = new Drawable[]
                                    {
                                        statContainerBackground = new Box
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Colour = colourProvider.Background5.Opacity(0.8f),
                                        },
                                        new GridContainer
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Padding = new MarginPadding { Left = SongSelect.WEDGE_CONTENT_MARGIN, Right = 20f, Vertical = 7.5f },
                                            Shear = -OsuGame.SHEAR,
                                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                                            ColumnDimensions = new[]
                                            {
                                                new Dimension(),
                                                new Dimension(GridSizeMode.Absolute, 30),
                                                new Dimension(GridSizeMode.AutoSize),
                                            },
                                            Content = new[]
                                            {
                                                new[]
                                                {
                                                    countStatisticsDisplay = new DifficultyStatisticsDisplay
                                                    {
                                                        RelativeSizeAxes = Axes.X,
                                                    },
                                                    Empty(),
                                                    difficultyStatisticsDisplay = new DifficultyStatisticsDisplay(autoSize: true),
                                                }
                                            },
                                        }
                                    },
                                }
                            }),
                        }
                    },
                };

                themeColour = colourProvider.GetColourBindable(OverlayColour.Background5);
                themeColour.BindValueChanged(c => statContainerBackground.Colour = c.NewValue.Opacity(0.8f), true);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                // it is not uncommon for the beatmap and the ruleset to change in conjunction during a single update frame.
                // in that process, it is possible for the global bindable triad (beatmap / ruleset / mods) to briefly be partially invalid in combination (e.g. mods invalid for given ruleset).
                // `updateDisplay()` will initiate a difficulty calculation, and if it is allowed to run in that invalid intermediate state, it will loudly fail.
                // therefore, all changes that may initiate a difficulty calculation are debounced until the next frame to ensure the global bindable state is fully consistent -
                // and it's what you'd want to do anyway for performance reasons.
                beatmap.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));
                ruleset.BindValueChanged(_ => Scheduler.AddOnce(updateDisplay));

                mods.BindValueChanged(m =>
                {
                    settingChangeTracker?.Dispose();

                    updateDifficultyStatistics();

                    if (m.NewValue.Any())
                    {
                        settingChangeTracker = new ModSettingChangeTracker(m.NewValue);
                        settingChangeTracker.SettingChanged += _ => updateDifficultyStatistics();
                    }
                }, true);

                updateDisplay();
            }

            [Resolved]
            private ILinkHandler? linkHandler { get; set; }

            private void updateDisplay()
            {
                cancellationSource?.Cancel();
                countWorkSource?.Cancel();
                if (countBindable != null)
                    countBindable.CountStatisticsRequested -= onCountStatisticsRequested;
                cancellationSource = new CancellationTokenSource();
                resetCountFallback();

                if (beatmap.IsDefault)
                {
                    difficultyText.AsyncText = string.Empty;
                    mapperText.AsyncText = string.Empty;
                    ratingAndNameContainer.FadeOut(300, Easing.OutQuint);
                    countStatisticsDisplay.FadeOut(300, Easing.OutQuint);
                }
                else
                {
                    ratingAndNameContainer.FadeIn(300, Easing.OutQuint);
                    difficultyText.AsyncText = beatmap.Value.BeatmapInfo.DifficultyName;
                    mapperLink.Action = () => linkHandler?.HandleLink(new LinkDetails(LinkAction.OpenUserProfile, beatmap.Value.Metadata.Author));
                    mapperText.AsyncText = beatmap.Value.Metadata.Author.Username;
                }

                // Counts share the selected map's full SR/PP calculation. Use the same
                // admission delay as its carousel card, rather than starting heavy work
                // for every map that remains selected for only 150 ms while scrolling.
                // Cached results and the initial star seed still publish immediately.
                var request = difficultyCache.GetBindableDifficultyWithCountStatistics(beatmap.Value.BeatmapInfo, cancellationSource.Token,
                    SongSelect.PERFORMANCE_CALCULATION_DEBOUNCE);
                countBindable = request.Difficulty;
                countBindable.CountStatisticsRequested += onCountStatisticsRequested;
                starRatingDisplay.Current = request.Difficulty;

                onCountStatisticsRequested(request.CountStatistics);
                updateDifficultyStatistics();
            }

            private void onCountStatisticsRequested(Task<IReadOnlyList<StarDifficulty.CountStatistic>?> countStatistics)
            {
                countWorkSource?.Cancel();
                countWorkSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationSource!.Token);
                updateCountStatistics(countStatistics, countWorkSource.Token);
            }

            private void updateCountStatistics(Task<IReadOnlyList<StarDifficulty.CountStatistic>?> countStatistics, CancellationToken cancellationToken)
            {
                if (beatmap.IsDefault)
                {
                    countStatisticsDisplay.FadeOut(300, Easing.OutQuint);
                    return;
                }

                WorkingBeatmap workingBeatmap = beatmap.Value;
                RulesetInfo rulesetInfo = ruleset.Value;
                CancellationToken selectionToken = cancellationSource!.Token;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Wait for this selection's full calculation (not its initial star placeholder).
                        // A cache/persisted result without counts or a failed calculation needs one fallback.
                        var counts = await countStatistics.WaitAsync(cancellationToken).ConfigureAwait(false);
                        cancellationToken.ThrowIfCancellationRequested();

                        if (counts == null)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            Task<IReadOnlyList<StarDifficulty.CountStatistic>> fallback;
                            lock (countFallbackLock)
                            {
                                cancellationToken.ThrowIfCancellationRequested();
                                selectionToken.ThrowIfCancellationRequested();
                                if (countFallbackTask?.IsFaulted == true || countFallbackTask?.IsCanceled == true)
                                    resetCountFallbackLocked();

                                if (!ReferenceEquals(countFallbackBeatmap, workingBeatmap) || !ReferenceEquals(countFallbackRuleset, rulesetInfo) || countFallbackTask == null)
                                {
                                    resetCountFallbackLocked();
                                    countFallbackBeatmap = workingBeatmap;
                                    countFallbackRuleset = rulesetInfo;
                                    countFallbackSource = CancellationTokenSource.CreateLinkedTokenSource(selectionToken);
                                    var fallbackToken = countFallbackSource.Token;
                                    Interlocked.Increment(ref countFallbacksStarted);
                                    countFallbackTask = Task.Run<IReadOnlyList<StarDifficulty.CountStatistic>>(() =>
                                        workingBeatmap.GetPlayableBeatmap(rulesetInfo, Array.Empty<Mod>(), fallbackToken)
                                                      .GetStatistics()
                                                      .Select(s => new StarDifficulty.CountStatistic(s.Name, s.BarDisplayLength, s.Content)).ToArray(), fallbackToken);
                                }

                                fallback = countFallbackTask;
                            }

                            Interlocked.Increment(ref countFallbackWaitsStarted);
                            try
                            {
                                counts = await fallback.WaitAsync(cancellationToken).ConfigureAwait(false);
                            }
                            catch
                            {
                                if (fallback.IsFaulted || fallback.IsCanceled)
                                {
                                    lock (countFallbackLock)
                                    {
                                        if (ReferenceEquals(countFallbackTask, fallback))
                                            resetCountFallbackLocked();
                                    }
                                }

                                throw;
                            }
                        }
                        var statistics = counts.Select(s => new StatisticDifficulty.Data(s.Name, s.BarDisplayLength ?? 0,
                            s.BarDisplayLength ?? 0, 1, s.Content)).ToList();

                        Schedule(() =>
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;

                            countStatisticsDisplay.FadeIn(200, Easing.OutQuint);
                            countStatisticsDisplay.Statistics = statistics;
                        });
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                    }
                    catch (BeatmapInvalidForRulesetException)
                    {
                        Schedule(() =>
                        {
                            if (cancellationToken.IsCancellationRequested)
                                return;

                            countStatisticsDisplay.Statistics = Array.Empty<StatisticDifficulty.Data>();
                            countStatisticsDisplay.FadeOut(200, Easing.OutQuint);
                        });
                    }
                }, cancellationToken);
            }

            private void resetCountFallback()
            {
                lock (countFallbackLock)
                    resetCountFallbackLocked();
            }

            private void resetCountFallbackLocked()
            {
                countFallbackSource?.Cancel();
                countFallbackSource?.Dispose();
                countFallbackSource = null;
                countFallbackBeatmap = null;
                countFallbackRuleset = null;
                countFallbackTask = null;
            }

            private void updateDifficultyStatistics() => Scheduler.AddOnce(() =>
            {
                if (beatmap.IsDefault || ruleset.Value == null)
                {
                    difficultyStatisticsDisplay.Statistics = Array.Empty<StatisticDifficulty.Data>();
                    return;
                }

                Ruleset rulesetInstance = ruleset.Value.CreateInstance();

                var displayAttributes = rulesetInstance.GetBeatmapAttributesForDisplay(beatmap.Value.BeatmapInfo, mods.Value).ToList();
                difficultyStatisticsDisplay.Statistics = displayAttributes.Select(a => new StatisticDifficulty.Data(a)).ToList();
            });

            protected override void Dispose(bool isDisposing)
            {
                cancellationSource?.Cancel();
                countWorkSource?.Cancel();
                resetCountFallback();
                if (countBindable != null)
                    countBindable.CountStatisticsRequested -= onCountStatisticsRequested;
                base.Dispose(isDisposing);
            }

            protected override void Update()
            {
                base.Update();

                difficultyText.MaxWidth = Math.Max(nameLine.DrawWidth - mappedByText.DrawWidth - mapperText.DrawWidth - 20, 0);

                // Use difficulty colour until it gets too dark to be visible against dark backgrounds.
                Color4 col = starRatingDisplay.DisplayedStars.Value >= OsuColour.STAR_DIFFICULTY_DEFINED_COLOUR_CUTOFF ? starRatingDisplay.DisplayedDifficultyTextColour : starRatingDisplay.DisplayedDifficultyColour;

                difficultyText.Colour = col;
                mappedByText.Colour = OverlayColourProvider.IsLightTheme ? colourProvider.Content2 : col;
                countStatisticsDisplay.AccentColour = col;
                difficultyStatisticsDisplay.AccentColour = col;
            }

            private partial class MapperLinkContainer : OsuHoverContainer
            {
                private IBindable<Colour4>? themeColour;

                [BackgroundDependencyLoader]
                private void load(OverlayColourProvider? overlayColourProvider, OsuColour colours)
                {
                    TooltipText = ContextMenuStrings.ViewProfile;

                    if (overlayColourProvider != null)
                    {
                        themeColour = overlayColourProvider.GetColourBindable(OverlayColour.Light2);
                        themeColour.BindValueChanged(c =>
                        {
                            IdleColour = c.NewValue;
                            if (IsLoaded && !IsHovered)
                                ((Drawable)Content).FadeColour(c.NewValue);
                        }, true);
                    }
                    else
                    {
                        IdleColour = colours.Blue;
                    }
                }
            }
        }
    }
}
