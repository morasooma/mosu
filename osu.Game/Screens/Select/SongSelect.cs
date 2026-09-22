// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Extensions;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.StateChanges;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Mods;
using osu.Game.Overlays.Volume;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Menu;
using osu.Game.Screens.Play;
using osu.Game.Screens.Ranking;
using osu.Game.Screens.Backgrounds;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osu.Game.Utils;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Screens.Select
{
    public abstract partial class SongSelect : ScreenWithBeatmapBackground, IKeyBindingHandler<GlobalAction>, ISongSelect, IHandlePresentBeatmap, IProvideCursor
    {
        /// <summary>
        /// A debounce that governs how long after a panel is selected before the rest of song select (and the game at large)
        /// updates to show that selection.
        ///
        /// This is intentionally slightly higher than key repeat, but low enough to not impede user experience.
        /// </summary>
        public const int SELECTION_DEBOUNCE = 150;

        /// <summary>
        /// A general "global" debounce to be applied to anything aggressive difficulty calculation at song select,
        /// either after selection or after a panel comes on screen. Value should be low enough that users don't complain,
        /// but otherwise as high as possible to reduce overheads.
        /// </summary>
        public const int DIFFICULTY_CALCULATION_DEBOUNCE = 150;

        private const float logo_scale = 0.4f;
        private const double fade_duration = 300;

        public const float WEDGE_CONTENT_MARGIN = CORNER_RADIUS_HIDE_OFFSET + OsuGame.SCREEN_EDGE_MARGIN;
        public const float CORNER_RADIUS_HIDE_OFFSET = 20f;
        public const float ENTER_DURATION = 600;

        /// <summary>
        /// Whether this song select instance should take control of the global track,
        /// applying looping and preview offsets.
        /// </summary>
        protected bool ControlGlobalMusic { get; init; } = true;

        /// <summary>
        /// Whether this song select instance should allow scoping down to a specific beatmap set,
        /// exposing other difficulties that are otherwise hidden by filter criteria.
        /// </summary>
        protected bool SupportScoping { init => scopedBeatmapSet.Disabled = !value; }

        /// <summary>
        /// Whether the osu! logo should be shown at the bottom-right of the screen.
        /// </summary>
        protected bool ShowOsuLogo { get; init; } = true;

        /// <summary>
        /// Whether this screen supports the stable-style chrome and fixed-width carousel.
        /// Online song selects provide their own header and footer actions, so they opt out.
        /// </summary>
        protected virtual bool SupportsStableStyle => false;

        /// <summary>
        /// Additional padding to be added to the title wedge.
        /// Generally set to show external content in this space.
        /// </summary>
        public float TopPadding { get; init; }

        private ModSelectOverlay modSelectOverlay = null!;
        private ModSpeedHotkeyHandler modSpeedHotkeyHandler = null!;

        // Blue is the most neutral choice, so I'm using that for now.
        // Purple makes the most sense to match the "gameplay" flow, but it's a bit too strong for the current design.
        // TODO: Colour scheme choice should probably be customisable by the user.
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        private BeatmapCarousel carousel = null!;
        private ScheduledDelegate? carouselPresenceRelease;

        protected FilterControl FilterControl { get; private set; } = null!;

        private BeatmapTitleWedge titleWedge = null!;
        private BeatmapDetailsArea detailsArea = null!;
        private FillFlowContainer wedgesContainer = null!;

        // Fork (ported from torii): stable-style (legacy) song select chrome. When the configured
        // song select style uses the v1 screen with skinned panels, the modern lazer chrome is
        // hidden and this chrome (top panel + leaderboard + mods readout) is shown instead.
        private Bindable<ForkSongSelectStyle> songSelectStyle = null!;
        private Bindable<GroupMode> legacyGroupCollapseWatcher = null!;
        private bool rebuildingLegacyChrome;
        private Drawable legacyTopContainer = null!;
        private Drawable legacyLeaderboardContainer = null!;
        private LegacyLeaderboard legacyLeaderboard = null!;
        private Drawable legacyModsContainer = null!;
        private Box leftGradientBackground = null!;
        private Box rightGradientBackground = null!;
        private CarouselHost carouselHost = null!;
        private InfiniteGlassBeatmapCanvas infiniteGlassCanvas = null!;
        private Container mainContent = null!;
        private SkinnableContainer skinnableContent = null!;

        internal bool SkinLayerIsAboveCarousel => skinnableContent.Depth < mainContent.Depth;

        private GridContainer mainGridContainer = null!;

        private NoResultsPlaceholder noResultsPlaceholder = null!;

        public override bool? ApplyModTrackAdjustments => true;

        public override bool ShowFooter => true;

        private Sample? errorSample;

        [Resolved]
        private OsuGameBase? game { get; set; }

        [Resolved]
        private OsuLogo? logo { get; set; }

        [Resolved]
        private BeatmapSetOverlay? beatmapOverlay { get; set; }

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private ManageCollectionsDialog? collectionsDialog { get; set; }

        [Resolved]
        private DifficultyRecommender? difficultyRecommender { get; set; }

        [Resolved]
        private ISkinSource skinSource { get; set; } = null!;

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private IOverlayManager? overlayManager { get; set; }

        [Resolved(CanBeNull = true)]
        private ScreenFooter? screenFooter { get; set; }

        private InputManager inputManager = null!;

        private readonly RealmPopulatingOnlineLookupSource onlineLookupSource = new RealmPopulatingOnlineLookupSource();

        private Bindable<bool> configBackgroundBlur = null!;
        private Bindable<bool> showConvertedBeatmaps = null!;
        private Bindable<bool> oldCarouselPreviews = null!;
        private Bindable<bool> songSelectStoryboardBackground = null!;
        private Bindable<double> carouselBackgroundDim = null!;
        private IBindable<ThemeMode>? themeMode;

        private IDisposable? modSelectOverlayRegistration;

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, OsuConfigManager config)
        {
            themeMode = OverlayColourProvider.CurrentTheme.GetBoundCopy();

            errorSample = audio.Samples.Get(@"UI/generic-error");

            AddRangeInternal(new Drawable[]
            {
                new GlobalScrollAdjustsVolume(),
                onlineLookupSource,
                mainContent = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Bottom = ScreenFooter.HEIGHT },
                    Child = new OsuContextMenuContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new PopoverContainer
                        {
                            RelativeSizeAxes = Axes.Both,
                            Children = new Drawable[]
                            {
                                leftGradientBackground = new Box
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Width = 0.6f,
                                    Depth = 2,
                                    Colour = getThemeGradient(themeMode.Value, left: true),
                                },
                                infiniteGlassCanvas = new InfiniteGlassBeatmapCanvas
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Depth = 1,
                                    ActivateItem = item => carousel.Activate(item),
                                    GetCarouselItems = () => carousel.GetCarouselItems(),
                                },
                                mainGridContainer = new GridContainer // used for max width implementation
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Content = new[]
                                    {
                                        new[]
                                        {
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                // Ensure the left components are on top of the carousel both visually (although they should never overlay)
                                                // but more importantly, for input purposes to allow the scroll-to-selection logic to override carousel's
                                                // screen-wide scroll handling.
                                                Depth = float.MinValue,
                                                Shear = OsuGame.SHEAR,
                                                Padding = new MarginPadding
                                                {
                                                    Top = -CORNER_RADIUS_HIDE_OFFSET,
                                                    Left = -CORNER_RADIUS_HIDE_OFFSET,
                                                },
                                                Children = new Drawable[]
                                                {
                                                    new Container
                                                    {
                                                        // Pad enough to only reset scroll when well into the left wedge areas.
                                                        Padding = new MarginPadding { Right = 40 },
                                                        RelativeSizeAxes = Axes.Both,
                                                        Child = new LeftSideInteractionContainer(
                                                            () => carousel.ScrollToSelection(),
                                                            delta => carousel.ScrollFromDelta(delta),
                                                            () => !songSelectStyle.Value.UsesInfiniteGlass(),
                                                            () => !songSelectStyle.Value.UsesInfiniteGlass())
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                        },
                                                    },
                                                    wedgesContainer = new FillFlowContainer
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Spacing = new Vector2(0f, 4f),
                                                        Direction = FillDirection.Vertical,
                                                        Children = new Drawable[]
                                                        {
                                                            new ShearAligningWrapper(titleWedge = new BeatmapTitleWedge
                                                            {
                                                                TopPadding = TopPadding,
                                                            }),
                                                            new ShearAligningWrapper(detailsArea = new BeatmapDetailsArea
                                                            {
                                                                // Empty leaderboard space should never turn into a screen-sized dead zone.
                                                                UseScorePanelOnlyInput = () => true,
                                                            }),
                                                        },
                                                    },
                                                }
                                            },
                                            Empty(),
                                            new Container
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Children = new Drawable[]
                                                {
                                                    rightGradientBackground = new Box
                                                    {
                                                        Anchor = Anchor.TopRight,
                                                        Origin = Anchor.TopRight,
                                                        Colour = getThemeGradient(themeMode.Value, left: false),
                                                        RelativeSizeAxes = Axes.Both,
                                                    },
                                                    new Container
                                                    {
                                                        RelativeSizeAxes = Axes.Both,
                                                        Padding = new MarginPadding
                                                        {
                                                            Top = FilterControl.HEIGHT_FROM_SCREEN_TOP + 5,
                                                            Bottom = 5,
                                                        },
                                                        Children = new Drawable[]
                                                        {
                                                            carouselHost = new CarouselHost
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                                ReceivePositionalInput = () => !songSelectStyle.Value.UsesInfiniteGlass(),
                                                                Child = carousel = createCarousel(),
                                                            },
                                                            noResultsPlaceholder = new NoResultsPlaceholder
                                                            {
                                                                RequestClearFilterText = () => FilterControl.Search(string.Empty)
                                                            }
                                                        }
                                                    },
                                                    FilterControl = new FilterControl
                                                    {
                                                        Anchor = Anchor.TopRight,
                                                        Origin = Anchor.TopRight,
                                                        RelativeSizeAxes = Axes.X,
                                                        ScopedBeatmapSet = { BindTarget = ScopedBeatmapSet },
                                                    },
                                                }
                                            },
                                        },
                                    }
                                },
                            }
                        },
                    }
                },
                skinnableContent = new SkinnableContainer(new GlobalSkinnableContainerLookup(GlobalSkinnableContainers.SongSelect))
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    // Skin-provided song-select HUD artwork must occlude the carousel, as in stable.
                    Depth = -1,
                },
                // Fork (ported from torii): stable-style song select chrome for the legacy UI mode.
                // Normalise skin coordinates independently of UIScale. A 1024x768 minimum
                // supports both 4:3 and widescreen without forcing a 16:9 letterbox.
                legacyTopContainer = new DrawSizePreservingFillContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    TargetDrawSize = new Vector2(1024, 768),
                    Alpha = 0,
                    Child = new LegacySongSelectTop
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        RelativeSizeAxes = Axes.Both,
                        FilterControl = FilterControl,
                    },
                },
                // Fork: stable-style bottom-left ranking panel (Local Ranking dropdown + scores).
                legacyLeaderboardContainer = new DrawSizePreservingFillContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    TargetDrawSize = new Vector2(1024, 768),
                    Alpha = 0,
                    Child = new OsuContextMenuContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = legacyLeaderboard = new LegacyLeaderboard
                        {
                            Anchor = Anchor.TopLeft,
                            Origin = Anchor.TopLeft,
                            RelativeSizeAxes = Axes.Both,
                            HoverScrollRequested = () => carousel.ScrollToSelection(),
                        },
                    },
                },
                // Fork: active-mods readout in the stable style, above the footer.
                legacyModsContainer = new DrawSizePreservingFillContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    TargetDrawSize = new Vector2(1024, 768),
                    Alpha = 0,
                    Child = new LegacyModsList { RelativeSizeAxes = Axes.Both },
                },
                modSpeedHotkeyHandler = new ModSpeedHotkeyHandler()
            });

            LoadComponent(modSelectOverlay = CreateModSelectOverlay());

            configBackgroundBlur = config.GetBindable<bool>(OsuSetting.SongSelectBackgroundBlur);

            // Fork (ported from torii): wire up the stable-style chrome visibility + skin rebuild.
            songSelectStyle = config.GetBindable<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle);
            songSelectStyle.BindValueChanged(e =>
            {
                if (e.NewValue.UsesStableStyle() && this.IsCurrentScreen())
                    rebuildLegacyChrome();
                else
                    updateLegacyChrome();

                updateSongSelectStylePresentation();
                infiniteGlassCanvas.SetActive(e.NewValue.UsesInfiniteGlass());

                if (ShowOsuLogo && logo != null && this.IsCurrentScreen())
                    logo.FadeTo(e.NewValue.GetSongSelectLogoAlpha(), 200, Easing.OutQuint);
            }, true);

            legacyGroupCollapseWatcher = config.GetBindable<GroupMode>(OsuSetting.SongSelectGroupMode);
            legacyGroupCollapseWatcher.BindValueChanged(_ =>
            {
                if (songSelectStyle.Value.UsesStableStyle())
                    carousel.CollapseGroupsOnNextFilter = true;
            });
            configBackgroundBlur.BindValueChanged(e =>
            {
                if (!this.IsCurrentScreen())
                    return;

                updateBackgroundDim();
            });

            oldCarouselPreviews = config.GetBindable<bool>(OsuSetting.ForkSongSelectOldCarouselPreviews);
            oldCarouselPreviews.BindValueChanged(_ =>
            {
                Scheduler.AddOnce(recreateCarousel);
            });

            carouselBackgroundDim = config.GetBindable<double>(OsuSetting.ForkSongSelectCarouselBackgroundDim);
            carouselBackgroundDim.BindValueChanged(_ =>
            {
                if (!this.IsCurrentScreen())
                    return;

                updateBackgroundDim();
            }, true);

            songSelectStoryboardBackground = config.GetBindable<bool>(OsuSetting.ForkSongSelectStoryboardBackground);
            songSelectStoryboardBackground.BindValueChanged(_ =>
            {
                if (!this.IsCurrentScreen())
                    return;

                ApplyToBackground(background =>
                {
                    background.Beatmap = Beatmap.Value;

                    if (background is SongSelectBackgroundScreen songSelectBackgroundScreen)
                        songSelectBackgroundScreen.ApplyStoryboardSettingChange(Beatmap.Value);
                });
            });

            showConvertedBeatmaps = config.GetBindable<bool>(OsuSetting.ShowConvertedBeatmaps);

            themeMode.BindValueChanged(e => updateThemeGradients(e.NewValue), true);
        }

        protected override BackgroundScreen CreateBackground() => new SongSelectBackgroundScreen(Beatmap.Value);

        private static ColourInfo getThemeGradient(ThemeMode theme, bool left)
        {
            var gradientColour = theme == ThemeMode.Light ? Color4.White : Color4.Black;

            return left
                ? ColourInfo.GradientHorizontal(gradientColour.Opacity(0.3f), gradientColour.Opacity(0f))
                : ColourInfo.GradientHorizontal(gradientColour.Opacity(0f), gradientColour.Opacity(0.5f));
        }

        private void updateThemeGradients(ThemeMode theme)
        {
            leftGradientBackground.Colour = getThemeGradient(theme, left: true);
            rightGradientBackground.Colour = getThemeGradient(theme, left: false);
        }

        private BeatmapCarousel createCarousel() => new BeatmapCarousel
        {
            SupportsStableStyle = SupportsStableStyle,
            BleedTop = FilterControl.HEIGHT_FROM_SCREEN_TOP + 5,
            BleedBottom = ScreenFooter.HEIGHT + 5,
            RelativeSizeAxes = Axes.Both,
            RequestPresentBeatmap = b => SelectAndRun(b, OnStart),
            RequestSelection = queueBeatmapSelection,
            RequestRecommendedSelection = requestRecommendedSelection,
            CustomNavigationHandler = action => infiniteGlassCanvas?.HandleNavigation(action) == true,
            NewItemsPresented = items =>
            {
                infiniteGlassCanvas?.SetItems(items);
                newItemsPresented(items);
            },
        };

        private void recreateCarousel()
        {
            var oldCarousel = carousel;
            var criteria = oldCarousel.Criteria;
            bool visuallyFocusSelected = oldCarousel.VisuallyFocusSelected;

            carouselHost.Child = carousel = createCarousel();
            carousel.VisuallyFocusSelected = visuallyFocusSelected;
            updateSongSelectStylePresentation();

            oldCarousel.Expire();

            CarouselItemsPresented = false;
            updateWedgeVisibility();

            if (criteria != null)
                criteriaChanged(criteria);
        }

        // Colour scheme for mod overlay is left as default (green) to match mods button.
        // Not sure about this, but we'll iterate based on feedback.
        protected virtual ModSelectOverlay CreateModSelectOverlay() => new UserModSelectOverlay
        {
            ShowPresets = true,
        };

        private void requestRecommendedSelection(IEnumerable<GroupedBeatmap> groupedBeatmaps)
        {
            var recommendedBeatmap = difficultyRecommender?.GetRecommendedBeatmap(groupedBeatmaps.Select(gb => gb.Beatmap)) ?? groupedBeatmaps.First().Beatmap;
            queueBeatmapSelection(groupedBeatmaps.First(bug => bug.Beatmap.Equals(recommendedBeatmap)));
        }

        /// <summary>
        /// Called when a selection is made to progress away from the song select screen.
        ///
        /// This is the default action which should be provided to <see cref="SelectAndRun"/>.
        /// </summary>
        protected abstract void OnStart();

        public override IReadOnlyList<ScreenFooterButton> CreateFooterButtons() => new ScreenFooterButton[]
        {
            new FooterButtonMods(modSelectOverlay)
            {
                Hotkey = GlobalAction.ToggleModSelection,
                Beatmap = { BindTarget = Beatmap },
                Mods = Mods,
                Ruleset = Ruleset,
                RequestDeselectAllMods = () =>
                {
                    if (modSelectOverlay.State.Value == Visibility.Visible)
                        modSelectOverlay.DeselectAll();
                    else
                        Mods.Value = Array.Empty<Mod>();
                }
            },
            new FooterButtonRandom
            {
                NextRandom = () =>
                {
                    if (!carousel.NextRandom())
                        errorSample?.Play();
                },
                PreviousRandom = () =>
                {
                    if (!carousel.PreviousRandom())
                        errorSample?.Play();
                }
            },
            new FooterButtonOptions
            {
                Hotkey = GlobalAction.ToggleBeatmapOptions,
            }
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();

            skinSource.SourceChanged += onSkinChangedWhileLegacy;

            modSelectOverlayRegistration = overlayManager?.RegisterBlockingOverlay(modSelectOverlay);

            inputManager = GetContainingInputManager()!;

            FilterControl.CriteriaChanged += criteriaChanged;

            modSelectOverlay.State.BindValueChanged(v =>
            {
                if (!this.IsCurrentScreen())
                    return;

                if (ShowOsuLogo)
                    logo?.FadeTo(v.NewValue == Visibility.Visible ? 0f : 1f, 200, Easing.OutQuint);
            });
        }

        protected override void Update()
        {
            base.Update();

            detailsArea.Height = wedgesContainer.ChildSize.Y - titleWedge.LayoutSize.Y - 4;

            if (songSelectStyle.Value.UsesStableStyleOn(SupportsStableStyle))
            {
                // Stable's menu-button-background is authored for a roughly 690px-wide panel.
                // Keep that width at 4:3 instead of shrinking the carousel to half of 1024px.
                // The remaining space belongs to the leaderboard / beatmap information area.
                mainGridContainer.ColumnDimensions = new[]
                {
                    new Dimension(),
                    new Dimension(GridSizeMode.Absolute),
                    new Dimension(GridSizeMode.Absolute, GetLegacyCarouselWidth(DrawWidth)),
                };
            }
            else
            {
                float widescreenBonusWidth = Math.Max(0, DrawWidth / DrawHeight - 2f);

                mainGridContainer.ColumnDimensions = new[]
                {
                    new Dimension(GridSizeMode.Relative, 0.5f, maxSize: 700 + widescreenBonusWidth * 100),
                    new Dimension(),
                    new Dimension(GridSizeMode.Relative, 0.5f, minSize: 500, maxSize: 700 + widescreenBonusWidth * 300),
                };
            }

            if (this.IsCurrentScreen())
                updateDebounce();
        }

        internal static float GetLegacyCarouselWidth(float viewportWidth) => Math.Min(690, Math.Max(0, viewportWidth));

        #region Selection debounce

        private BeatmapInfo? debounceQueuedSelection;
        private double debounceElapsedTime;

        private void debounceQueueSelection(BeatmapInfo beatmap)
        {
            debounceQueuedSelection = beatmap;
            debounceElapsedTime = 0;
        }

        private void updateDebounce()
        {
            if (debounceQueuedSelection == null) return;

            double elapsed = Clock.ElapsedFrameTime;

            // When a key is being held, assume the user is traversing the carousel using key repeat.
            // We want to change panels less often in this state (basically making debounce longer than initial key repeat, at least).
            double debounceInterval = inputManager.CurrentState.Keyboard.Keys.HasAnyButtonPressed ? SELECTION_DEBOUNCE * 2 : SELECTION_DEBOUNCE;

            // avoid debounce running early if there's a single long frame.
            if (!DebugUtils.IsNUnitRunning && Clock.FramesPerSecond > 0)
                elapsed = Math.Min(1000 / Clock.FramesPerSecond, elapsed);

            debounceElapsedTime += elapsed;

            if (debounceElapsedTime >= debounceInterval)
                performDebounceSelection();
        }

        private void performDebounceSelection()
        {
            if (debounceQueuedSelection == null) return;

            try
            {
                if (Beatmap.Value.BeatmapInfo.Equals(debounceQueuedSelection))
                    return;

                var workingBeatmap = beatmaps.GetWorkingBeatmap(debounceQueuedSelection);
                if (!checkBeatmapValidForSelection(workingBeatmap.BeatmapInfo))
                    return;

                Beatmap.Value = workingBeatmap;
            }
            finally
            {
                cancelDebounceSelection();
            }
        }

        private void cancelDebounceSelection()
        {
            debounceQueuedSelection = null;
            debounceElapsedTime = 0;
        }

        #endregion

        #region Audio

        [Resolved]
        private MusicController music { get; set; } = null!;

        private readonly WeakReference<ITrack?> lastTrack = new WeakReference<ITrack?>(null);

        /// <summary>
        /// Ensures some music is playing for the current track.
        /// Will resume playback from a manual user pause if the track has changed.
        /// </summary>
        private void ensurePlayingSelected()
        {
            if (!ControlGlobalMusic)
                return;

            ITrack track = music.CurrentTrack;

            bool isNewTrack = !lastTrack.TryGetTarget(out var last) || last != track;

            if (!track.IsRunning && (music.UserPauseRequested != true || isNewTrack))
            {
                Logger.Log($"Song select decided to {nameof(ensurePlayingSelected)}");

                // Only restart playback if a new track.
                // This is important so that when exiting gameplay, the track is not restarted back to the preview point.
                music.Play(isNewTrack);
            }

            lastTrack.SetTarget(track);
        }

        private bool isHandlingLooping;

        private void beginLooping()
        {
            Debug.Assert(!isHandlingLooping);

            isHandlingLooping = true;

            ensureTrackLooping(Beatmap.Value, TrackChangeDirection.None);

            music.TrackChanged += ensureTrackLooping;
        }

        private void endLooping()
        {
            // may be called multiple times during screen exit process.
            if (!isHandlingLooping)
                return;

            music.CurrentTrack.Looping = isHandlingLooping = false;

            music.TrackChanged -= ensureTrackLooping;
        }

        private void ensureTrackLooping(IWorkingBeatmap beatmap, TrackChangeDirection changeDirection)
        {
            // MusicController normally loads the track before notifying consumers, but a freshly
            // refetched working beatmap can reach us while resuming from PlayerLoader first.
            // PrepareTrackForPreview requires an already loaded track and would otherwise abort
            // OnResuming, leaving song select visible but non-interactive in the screen stack.
            if (!beatmap.TrackLoaded)
                beatmap.LoadTrack();

            beatmap.PrepareTrackForPreview(true);
        }

        #endregion

        #region Selection handling

        /// <summary>
        /// Finalises selection on the given <see cref="BeatmapInfo"/> and runs the provided action if possible.
        /// </summary>
        /// <param name="beatmap">The beatmap which should be selected. If not provided, the current globally selected beatmap will be used.</param>
        /// <param name="startAction">The action to perform if conditions are met to be able to proceed. May not be invoked if in an invalid state.</param>
        protected void SelectAndRun(BeatmapInfo beatmap, Action startAction)
        {
            if (!this.IsCurrentScreen())
                return;

            if (!checkBeatmapValidForSelection(beatmap))
                return;

            // To ensure sanity, cancel any pending selection as we are about to force a selection.
            // Carousel selection will update to the forced selection via a call of `ensureGlobalBeatmapValid` below, or when song select becomes current again.
            cancelDebounceSelection();

            // Forced refetch is important here to guarantee correct invalidation across all difficulties (editor specific).
            Beatmap.Value = beatmaps.GetWorkingBeatmap(beatmap, true);

            if (Beatmap.IsDefault)
                return;

            startAction();
        }

        /// <summary>
        /// Prepares the proposed beatmap for global selection based on a carousel user-performed action.
        /// </summary>
        /// <remarks>
        /// Calling this method will:
        /// - Immediately update the selection the carousel.
        /// - After <see cref="SELECTION_DEBOUNCE"/>, update the global beatmap. This in turn causes song select visuals (title, details, leaderboard) to update.
        ///   This debounce is intended to avoid high overheads from churning lookups while a user is changing selection via rapid keyboard operations.
        /// </remarks>
        /// <param name="groupedBeatmap">The beatmap to be selected.</param>
        private void queueBeatmapSelection(GroupedBeatmap groupedBeatmap)
        {
            if (!this.IsCurrentScreen())
                return;

            carousel.CurrentGroupedBeatmap = groupedBeatmap;

            // Debounce consideration is to avoid beatmap churn on key repeat selection.
            debounceQueueSelection(groupedBeatmap.Beatmap);
        }

        private bool ensureGlobalBeatmapValid(bool refetch = false)
        {
            if (!this.IsCurrentScreen())
                return false;

            performDebounceSelection();

            // While filtering, let's not ever attempt to change selection.
            // This will be resolved after the filter completes, see `newItemsPresented`.
            if (IsFiltering)
                return false;

            // Normal selection changes already carry current detached data. Forcing a refetch here invalidates
            // the WorkingBeatmap on every selection, causing audio, beatmap decoding and difficulty calculation
            // to be recreated continuously while scrolling.
            var currentBeatmap = beatmaps.GetWorkingBeatmap(Beatmap.Value.BeatmapInfo, refetch);
            bool validSelection = checkBeatmapValidForSelection(currentBeatmap.BeatmapInfo);

            if (validSelection)
            {
                carousel.CurrentBeatmap = currentBeatmap.BeatmapInfo;
                return true;
            }

            // If there was no beatmap selected, pick a random one.
            if (Beatmap.IsDefault)
            {
                validSelection = carousel.NextRandom();
                performDebounceSelection();
                return validSelection;
            }

            // If a previous non-default selection became non-valid, it was likely hidden or deleted.
            if (!validSelection)
            {
                // In the case a difficulty was hidden or removed, prefer selecting another difficulty from the same set.
                var activeSet = currentBeatmap.BeatmapSetInfo;

                var validBeatmaps = activeSet.Beatmaps.Where(checkBeatmapValidForSelection).ToArray();

                if (validBeatmaps.Any())
                {
                    var beatmap = difficultyRecommender?.GetRecommendedBeatmap(validBeatmaps) ?? validBeatmaps.First();
                    carousel.CurrentBeatmap = beatmap;
                    debounceQueueSelection(beatmap);
                    return true;
                }
            }

            // If all else fails, use the default beatmap.
            Beatmap.SetDefault();
            performDebounceSelection();

            return validSelection;
        }

        private bool checkBeatmapValidForSelection(BeatmapInfo beatmap)
        {
            if (!beatmap.AllowGameplayWithRuleset(Ruleset.Value, showConvertedBeatmaps.Value))
                return false;

            if (beatmap.Hidden)
                return false;

            if (beatmap.BeatmapSet == null)
            {
                if (osu.Game.Database.StablePathManager.IsStableBeatmap(beatmap.ID))
                    return true;

                return false;
            }

            if (beatmap.BeatmapSet.Protected || beatmap.BeatmapSet.DeletePending)
                return false;

            return true;
        }

        #endregion

        #region Transitions

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            this.FadeIn();
            onArrivingAtScreen();
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);

            this.FadeIn(fade_duration, Easing.OutQuint);
            onArrivingAtScreen();

            // A refetch is only required after returning from another screen, where the selected beatmap may
            // have been edited, hidden or deleted.
            ensureGlobalBeatmapValid(refetch: true);

            detailsArea.Refresh();
            legacyLeaderboard?.Refresh();

            if (ControlGlobalMusic)
            {
                // restart playback on returning to song select, regardless.
                // not sure this should be a permanent thing (we may want to leave a user pause paused even on returning)
                music.ResetTrackAdjustments();
                music.Play(requestedByUser: true);
            }
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            carousel.VisuallyFocusSelected = true;

            this.FadeOut(fade_duration, Easing.OutQuint);
            onLeavingScreen();

            base.OnSuspending(e);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            this.FadeOut(fade_duration, Easing.OutQuint);
            onLeavingScreen();

            return base.OnExiting(e);
        }

        /// <summary>
        /// Fork (ported from torii): hide/show the modern lazer song-select chrome for the
        /// stable-style UI mode. Hides the filter/sort bar and the left info + details wedges so
        /// only the carousel + stable top panel / leaderboard remain, matching osu!stable.
        /// </summary>
        private void updateLegacyChrome()
        {
            if (FilterControl == null)
                return;

            bool legacy = songSelectStyle.Value.UsesStableStyleOn(SupportsStableStyle);

            FilterControl.FadeTo(legacy ? 0 : 1, 200, Easing.OutQuint);
            wedgesContainer.FadeTo(legacy ? 0 : 1, 200, Easing.OutQuint);
            legacyTopContainer.FadeTo(legacy ? 1 : 0, 200, Easing.OutQuint);
            legacyLeaderboardContainer.FadeTo(legacy ? 1 : 0, 200, Easing.OutQuint);
            legacyModsContainer.FadeTo(legacy ? 1 : 0, 200, Easing.OutQuint);
        }

        private void updateSongSelectStylePresentation()
        {
            if (carousel == null)
                return;

            bool infiniteGlass = songSelectStyle.Value.UsesInfiniteGlass();
            BeatmapCarousel targetCarousel = carousel;

            carouselPresenceRelease?.Cancel();
            carouselPresenceRelease = null;

            // The canvas is an adapter over the regular carousel. A zero-alpha drawable is normally
            // absent from the framework update tree, which would stop the carousel's debounce timer
            // and filtering pipeline. Keep it present while visually hidden so it can continue to
            // supply filtered/sorted items and keyboard navigation to the canvas.
            //
            // AlwaysPresent must also remain set for the complete fade back from InfiniteGlass.
            // Clearing it while Alpha is still zero removes the carousel from the update tree, so
            // the fade can never advance and the regular carousel remains permanently non-interactive.
            targetCarousel.SuppressPanelPreviews = infiniteGlass;
            targetCarousel.AlwaysPresent = true;
            targetCarousel.FadeTo(infiniteGlass ? 0 : 1, 150, Easing.OutQuint);

            if (!infiniteGlass)
            {
                carouselPresenceRelease = Scheduler.AddDelayed(() =>
                {
                    if (ReferenceEquals(carousel, targetCarousel) && !songSelectStyle.Value.UsesInfiniteGlass())
                        targetCarousel.AlwaysPresent = false;

                    carouselPresenceRelease = null;
                }, 150);
            }

            leftGradientBackground.FadeTo(infiniteGlass ? 0.2f : 1, 150, Easing.OutQuint);
            rightGradientBackground.FadeTo(infiniteGlass ? 0.2f : 1, 150, Easing.OutQuint);
        }

        private partial class CarouselHost : Container
        {
            public required Func<bool> ReceivePositionalInput { private get; init; }

            public override bool PropagatePositionalInputSubTree => ReceivePositionalInput() && base.PropagatePositionalInputSubTree;
        }

        // Fork: the skin changed. Only rebuild while the stable mode is active and we are on screen.
        private void onSkinChangedWhileLegacy()
        {
            if (!songSelectStyle.Value.UsesStableStyleOn(SupportsStableStyle) || !this.IsCurrentScreen())
                return;

            // Defer a frame: SourceChanged is iterating its handler list, which includes the current
            // legacy components'. Rebuilding now would dispose objects mid-iteration.
            Schedule(rebuildLegacyChrome);
        }

        /// <summary>
        /// Fork (ported from torii): rebuild the stable chrome as if entering fresh. The children resolve
        /// skin textures one-shot in LoadComplete, so toggling mid-screen (or changing skin) must replace
        /// them to avoid stale textures. Replaces each container's Child (not the container) to keep z-order.
        /// </summary>
        private void rebuildLegacyChrome()
        {
            if (rebuildingLegacyChrome || !this.IsCurrentScreen())
                return;

            if (legacyTopContainer is not Container topContainer || legacyLeaderboardContainer is not Container leaderboardContainer)
                return;

            rebuildingLegacyChrome = true;

            try
            {
                topContainer.Child = new LegacySongSelectTop
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    RelativeSizeAxes = Axes.Both,
                    FilterControl = FilterControl,
                };

                leaderboardContainer.Child = new OsuContextMenuContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = legacyLeaderboard = new LegacyLeaderboard
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        RelativeSizeAxes = Axes.Both,
                        HoverScrollRequested = () => carousel.ScrollToSelection(),
                    },
                };
            }
            finally
            {
                rebuildingLegacyChrome = false;
            }

            updateLegacyChrome();
        }

        private void onArrivingAtScreen()
        {
            // Fork (ported from torii): apply/clear the stable chrome + aspect lock when entering.
            updateLegacyChrome();

            modSelectOverlay.Beatmap.BindTo(Beatmap);
            modSelectOverlay.Ruleset.BindTo(Ruleset);
            // required due to https://github.com/ppy/osu-framework/issues/3218
            modSelectOverlay.SelectedMods.Disabled = false;
            modSelectOverlay.SelectedMods.BindTo(Mods);

            carousel.VisuallyFocusSelected = false;

            if (ControlGlobalMusic)
            {
                // Avoid abruptly starting playback at preview point.
                // Importantly, this should be done before looping is setup to ensure we get the correct imminent `IsPlaying` state.
                if (!music.IsPlaying)
                {
                    music.DuckMomentarily(0, new DuckParameters
                    {
                        DuckDuration = 0,
                        DuckVolumeTo = 0,
                        RestoreDuration = 800,
                        RestoreEasing = Easing.OutQuint
                    });
                }

                beginLooping();
            }

            Beatmap.BindValueChanged(updateVariousState, true);
        }

        private void updateVariousState(ValueChangedEvent<WorkingBeatmap> e)
        {
            if (!this.IsCurrentScreen())
                return;

            ensureGlobalBeatmapValid();

            ensurePlayingSelected();
            updateBackgroundDim();
            updateWedgeVisibility();
            fetchOnlineInfo(force: ReferenceEquals(e.OldValue, e.NewValue));
        }

        private void onLeavingScreen()
        {
            restoreBackground();

            Beatmap.ValueChanged -= updateVariousState;

            modSelectOverlay.SelectedMods.UnbindFrom(Mods);
            modSelectOverlay.Ruleset.UnbindFrom(Ruleset);
            modSelectOverlay.Beatmap.UnbindFrom(Beatmap);

            updateWedgeVisibility();

            endLooping();
        }

        protected override void LogoArriving(OsuLogo logo, bool resuming)
        {
            base.LogoArriving(logo, resuming);

            if (!ShowOsuLogo)
                return;

            if (logo.Alpha > 0.8f && resuming)
                Footer?.StartTrackingLogo(logo, 400, Easing.OutQuint);
            else
            {
                logo.Hide();
                logo.ScaleTo(0.2f);
                Footer?.StartTrackingLogo(logo);
            }

            logo.FadeTo(songSelectStyle.Value.GetSongSelectLogoAlpha(), 240, Easing.OutQuint);
            logo.ScaleTo(logo_scale, 240, Easing.OutQuint);

            logo.Action = () =>
            {
                ensureGlobalBeatmapValid();
                SelectAndRun(Beatmap.Value.BeatmapInfo, OnStart);
                return false;
            };
        }

        protected override void LogoSuspending(OsuLogo logo)
        {
            base.LogoSuspending(logo);

            if (!ShowOsuLogo)
                return;

            Footer?.StopTrackingLogo();
        }

        protected override void LogoExiting(OsuLogo logo)
        {
            base.LogoExiting(logo);

            if (!ShowOsuLogo)
                return;

            Footer?.StopTrackingLogo();

            logo.ScaleTo(0.2f, 120, Easing.Out);
            logo.FadeOut(120, Easing.Out);
        }

        private void updateWedgeVisibility()
        {
            // Ensure we don't show an invalid selection before the carousel has finished initially filtering.
            // This avoids a flicker of a placeholder or invalid beatmap before a proper selection.
            //
            // After the carousel finishes filtering, it will attempt a selection then call this method again.
            if (!CarouselItemsPresented && !checkBeatmapValidForSelection(Beatmap.Value.BeatmapInfo))
                return;

            if (carousel.VisuallyFocusSelected)
            {
                titleWedge.Hide();
                detailsArea.Hide();
                FilterControl.Hide();
            }
            else
            {
                titleWedge.Show();
                detailsArea.Show();

                if (!songSelectStyle.Value.UsesStableStyle())
                    FilterControl.Show();
            }
        }

        private void updateBackgroundDim() => ApplyToBackground(backgroundModeBeatmap =>
        {
            backgroundModeBeatmap.Beatmap = Beatmap.Value;
            backgroundModeBeatmap.IgnoreUserSettings.Value = true;

            backgroundModeBeatmap.DimWhenUserSettingsIgnored.Value = (float)carouselBackgroundDim.Value;

            // Required to undo results screen dimming the background.
            // Probably needs more thought because this needs to be in every `ApplyToBackground` currently to restore sane defaults.
            backgroundModeBeatmap.FadeColour(Color4.White, 250);

            bool backgroundRevealActive = revealBackgroundDelegate?.State == ScheduledDelegate.RunState.Running || revealBackgroundDelegate?.State == ScheduledDelegate.RunState.Complete;
            backgroundModeBeatmap.BlurAmount.Value = configBackgroundBlur.Value && !backgroundRevealActive ? 20 : 0f;
        });

        #endregion

        #region Filtering

        /// <summary>
        /// Whether the carousel has finished initial presentation of beatmap panels.
        /// </summary>
        public bool CarouselItemsPresented { get; private set; }

        /// <summary>
        /// Whether the carousel is or will be undergoing a filter operation.
        /// </summary>
        public bool IsFiltering => carousel.IsFiltering || filterDebounce?.State == ScheduledDelegate.RunState.Waiting;

        private const double filter_delay = 250;

        private ScheduledDelegate? filterDebounce;

        private void criteriaChanged(FilterCriteria criteria)
        {
            filterDebounce?.Cancel();

            // The first filter needs to be applied immediately as this triggers the initial carousel load.
            bool isFirstFilter = filterDebounce == null;

            // Criteria change may have included a ruleset change which made the current selection invalid.
            bool isSelectionValid = checkBeatmapValidForSelection(Beatmap.Value.BeatmapInfo);

            filterDebounce = Scheduler.AddDelayed(() => carousel.Filter(criteria, !isSelectionValid), isFirstFilter || !isSelectionValid ? 0 : filter_delay);
        }

        private void newItemsPresented(IEnumerable<CarouselItem> carouselItems)
        {
            if (carousel.Criteria == null)
                return;

            CarouselItemsPresented = true;

            updateNoResultsPlaceholder();

            FilterControl.StatusText = SongSelectStrings.MatchesCount(carousel.MatchedBeatmapsCount);

            // If there's already a selection update in progress, let's not interrupt it.
            // Interrupting could cause the debounce interval to be reduced.
            //
            // `ensureGlobalBeatmapValid` is run post-selection which will resolve any pending incompatibilities (see `Beatmap` bindable callback).
            if (debounceQueuedSelection == null)
                ensureGlobalBeatmapValid();

            updateWedgeVisibility();
        }

        private void updateNoResultsPlaceholder()
        {
            int count = carousel.MatchedBeatmapsCount;

            if (count == 0)
            {
                if (noResultsPlaceholder.State.Value == Visibility.Hidden)
                {
                    // Duck audio temporarily when the no results placeholder becomes visible.
                    //
                    // Temporary ducking makes it easier to avoid scenarios where the ducking interacts badly
                    // with other global UI components (like overlays).
                    music.DuckMomentarily(400, new DuckParameters
                    {
                        DuckVolumeTo = 1,
                        DuckCutoffTo = 500,
                        DuckDuration = 250,
                        RestoreDuration = 2000,
                    });
                }

                noResultsPlaceholder.Show();
                noResultsPlaceholder.Filter = carousel.Criteria!;

                rightGradientBackground.ResizeWidthTo(3, 1000, Easing.OutPow10);
            }
            else
            {
                noResultsPlaceholder.Hide();

                rightGradientBackground.ResizeWidthTo(1, 400, Easing.OutPow10);
            }
        }

        #endregion

        #region Background reveal

        private ScheduledDelegate? revealBackgroundDelegate;

        public CursorContainer? Cursor => null;
        bool IProvideCursor.ProvidingUserCursor => revealBackgroundDelegate?.Completed == true;

        protected override bool OnHover(HoverEvent e) => true;

        protected override bool OnMouseDown(MouseDownEvent e)
        {
            var containingInputManager = GetContainingInputManager();

            // I don't know why this works, but it does.
            // If the carousel panels are hovered, hovered no longer contains the screen.
            // Maybe there's a better way of doing this, but I couldn't immediately find a good setup.
            bool mouseDownPriority = containingInputManager!.HoveredDrawables.Contains(this);

            // Touch input synthesises right clicks, which allow absolute scroll of the carousel.
            // For simplicity, disable this functionality on mobile.
            bool isTouchInput = e.CurrentState.Mouse.LastSource is ISourcedFromTouch;

            if (!carousel.AbsoluteScrolling && !isTouchInput && mouseDownPriority && revealBackgroundDelegate == null)
            {
                revealBackgroundDelegate = Scheduler.AddDelayed(() =>
                {
                    if (containingInputManager.DraggedDrawable != null)
                    {
                        revealBackgroundDelegate = null;
                        return;
                    }

                    mainContent.ResizeWidthTo(1.2f, 600, Easing.OutQuint);
                    mainContent.ScaleTo(1.2f, 600, Easing.OutQuint);
                    mainContent.FadeOut(200, Easing.OutQuint);

                    skinnableContent.ResizeWidthTo(1.2f, 600, Easing.OutQuint);
                    skinnableContent.ScaleTo(1.2f, 600, Easing.OutQuint);
                    skinnableContent.FadeOut(200, Easing.OutQuint);

                    updateBackgroundDim();

                    Footer?.Hide();
                }, 200);
            }

            return base.OnMouseDown(e);
        }

        protected override void OnMouseUp(MouseUpEvent e)
        {
            restoreBackground();
            base.OnMouseUp(e);
        }

        private void restoreBackground()
        {
            if (revealBackgroundDelegate == null)
                return;

            if (revealBackgroundDelegate.State == ScheduledDelegate.RunState.Complete)
            {
                mainContent.ResizeWidthTo(1f, 500, Easing.OutQuint);
                mainContent.ScaleTo(1, 500, Easing.OutQuint);
                mainContent.FadeIn(500, Easing.OutQuint);

                skinnableContent.ResizeWidthTo(1f, 500, Easing.OutQuint);
                skinnableContent.ScaleTo(1, 500, Easing.OutQuint);
                skinnableContent.FadeIn(500, Easing.OutQuint);

                Footer?.Show();
            }

            revealBackgroundDelegate.Cancel();
            revealBackgroundDelegate = null;

            updateBackgroundDim();
        }

        #endregion

        #region Input

        public virtual bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (!this.IsCurrentScreen()) return false;

            if (game == null)
                return false;

            var flattenedMods = ModUtils.FlattenMods(game.AvailableMods.Value.SelectMany(kv => kv.Value));

            if (!e.Repeat && screenFooter != null && !screenFooter.DefaultChromeVisible)
            {
                switch (e.Action)
                {
                    case GlobalAction.ToggleModSelection:
                        screenFooter.TriggerFooterButton(0);
                        return true;

                    case GlobalAction.SelectNextRandom:
                        screenFooter.TriggerFooterButton(1);
                        return true;

                    case GlobalAction.SelectPreviousRandom:
                        if (!carousel.PreviousRandom())
                            errorSample?.Play();
                        return true;

                    case GlobalAction.ToggleBeatmapOptions:
                        screenFooter.TriggerFooterButton(2);
                        return true;
                }
            }

            switch (e.Action)
            {
                case GlobalAction.Select:
                    // in most circumstances this is handled already by the carousel itself, but there are cases where it will not be.
                    // one of which is filtering out all visible beatmaps and attempting to start gameplay.
                    // in that case, users still expect a `Select` press to advance to gameplay anyway, using the ambient selected beatmap if there is one,
                    // which matches the behaviour resulting from clicking the osu! cookie in that scenario.
                    ensureGlobalBeatmapValid();
                    SelectAndRun(Beatmap.Value.BeatmapInfo, OnStart);
                    return true;

                case GlobalAction.IncreaseModSpeed:
                    return modSpeedHotkeyHandler.ChangeSpeed(0.05, flattenedMods);

                case GlobalAction.DecreaseModSpeed:
                    return modSpeedHotkeyHandler.ChangeSpeed(-0.05, flattenedMods);
            }

            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }

        protected override bool OnKeyDown(KeyDownEvent e)
        {
            if (e.Repeat) return false;

            switch (e.Key)
            {
                case Key.Delete:
                    if (e.ShiftPressed)
                    {
                        if (!Beatmap.IsDefault)
                            Delete(Beatmap.Value.BeatmapSetInfo);
                        return true;
                    }

                    break;
            }

            return base.OnKeyDown(e);
        }

        #endregion

        #region Online lookups

        public enum BeatmapSetLookupStatus
        {
            InProgress,
            Completed,
        }

        public class BeatmapSetLookupResult
        {
            public BeatmapSetLookupStatus Status { get; }
            public APIBeatmapSet? Result { get; }

            private BeatmapSetLookupResult(BeatmapSetLookupStatus status, APIBeatmapSet? result)
            {
                Status = status;
                Result = result;
            }

            public static BeatmapSetLookupResult InProgress() => new BeatmapSetLookupResult(BeatmapSetLookupStatus.InProgress, null);
            public static BeatmapSetLookupResult Completed(APIBeatmapSet? beatmapSet) => new BeatmapSetLookupResult(BeatmapSetLookupStatus.Completed, beatmapSet);
        }

        /// <summary>
        /// Result of the latest online beatmap set lookup.
        /// Note that this being <see langword="null"/> or <see cref="BeatmapSetLookupResult.InProgress"/> is different from
        /// being a <see cref="BeatmapSetLookupResult.Completed"/> with a <see cref="BeatmapSetLookupResult.Result"/> of null.
        /// The former indicates a lookup never occurring or being in progress, while the latter indicates a completed lookup with no result.
        /// </summary>
        [Cached(typeof(IBindable<BeatmapSetLookupResult?>))]
        private readonly Bindable<BeatmapSetLookupResult?> lastLookupResult = new Bindable<BeatmapSetLookupResult?>();

        private CancellationTokenSource? onlineLookupCancellation;
        private Task<APIBeatmapSet?>? currentOnlineLookup;

        private void fetchOnlineInfo(bool force = false)
        {
            var beatmapSetInfo = Beatmap.Value.BeatmapSetInfo;

            if (lastLookupResult.Value?.Result?.OnlineID == beatmapSetInfo.OnlineID && !force)
                return;

            onlineLookupCancellation?.Cancel();
            onlineLookupCancellation = null;

            if (beatmapSetInfo.OnlineID < 0)
            {
                lastLookupResult.Value = BeatmapSetLookupResult.Completed(null);
                return;
            }

            lastLookupResult.Value = BeatmapSetLookupResult.InProgress();
            onlineLookupCancellation = new CancellationTokenSource();
            currentOnlineLookup = onlineLookupSource.GetBeatmapSetAsync(beatmapSetInfo.OnlineID, onlineLookupCancellation.Token);
            currentOnlineLookup.ContinueWith(t =>
            {
                if (t.IsCompletedSuccessfully)
                    Schedule(() => lastLookupResult.Value = BeatmapSetLookupResult.Completed(t.GetResultSafely()));

                if (t.Exception != null)
                {
                    Logger.Log($"Error when fetching online beatmap set: {t.Exception}", LoggingTarget.Network);
                    Schedule(() => lastLookupResult.Value = BeatmapSetLookupResult.Completed(null));
                }
            });
        }

        #endregion

        #region Implementation of ISongSelect

        void ISongSelect.AddToSearch(string query) => FilterControl.AddToSearch(query);

        bool ISongSelect.CanPresentScore => true;

        void ISongSelect.PresentScore(ScoreInfo score, ScorePresentType presentType)
        {
            switch (presentType)
            {
                case ScorePresentType.Results:
                    Debug.Assert(Beatmap.Value.BeatmapInfo.Equals(score.BeatmapInfo));
                    Debug.Assert(Ruleset.Value.Equals(score.Ruleset));

                    this.Push(new SoloResultsScreen(score));
                    break;

                case ScorePresentType.Gameplay:
                    (game as OsuGame)?.PresentScore(score, presentType);
                    break;
            }
        }

        #endregion

        #region IHandlePresentBeatmap

        void IHandlePresentBeatmap.PresentBeatmap(WorkingBeatmap workingBeatmap, RulesetInfo ruleset)
        {
            unscopeBeatmapSet(restorePreviousSelection: false);
            cancelDebounceSelection();

            var beatmapInfo = workingBeatmap.BeatmapInfo;

            // Don't change the local ruleset if the user is on another ruleset and is showing converted beatmaps.
            // Eventually we probably want to check whether conversion is actually possible for the current ruleset.
            bool requiresRulesetSwitch = !beatmapInfo.Ruleset.Equals(Ruleset.Value)
                                         && (beatmapInfo.Ruleset.OnlineID > 0 || !showConvertedBeatmaps.Value);

            if (requiresRulesetSwitch)
            {
                Ruleset.Value = beatmapInfo.Ruleset;
                Beatmap.Value = workingBeatmap;

                Logger.Log($"Completing {nameof(IHandlePresentBeatmap.PresentBeatmap)} with beatmap {workingBeatmap} ruleset {beatmapInfo.Ruleset}");
            }
            else
            {
                Beatmap.Value = workingBeatmap;

                Logger.Log($"Completing {nameof(IHandlePresentBeatmap.PresentBeatmap)} with beatmap {workingBeatmap} (maintaining ruleset)");
            }
        }

        #endregion

        #region Beatmap management

        [Resolved]
        private ManageCollectionsDialog? manageCollectionsDialog { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        public virtual IEnumerable<OsuMenuItem> GetForwardActions(BeatmapInfo beatmap)
        {
            yield return new OsuMenuItem(GlobalActionKeyBindingStrings.Select, MenuItemType.Highlighted, () => SelectAndRun(beatmap, OnStart))
            {
                Icon = FontAwesome.Solid.Check
            };

            yield return new OsuMenuItemSpacer();

            if (beatmap.OnlineID > 0)
            {
                yield return new OsuMenuItem(CommonStrings.Details, MenuItemType.Standard, () => beatmapOverlay?.FetchAndShowBeatmap(beatmap.OnlineID));

                if (beatmap.GetOnlineURL(api, Ruleset.Value) is string url)
                    yield return new OsuMenuItem(CommonStrings.CopyLink, MenuItemType.Standard, () => (game as OsuGame)?.CopyToClipboard(url));
            }

            yield return new OsuMenuItemSpacer();

            foreach (var i in CreateCollectionMenuActions(beatmap))
                yield return i;
        }

        protected IEnumerable<OsuMenuItem> CreateCollectionMenuActions(BeatmapInfo beatmap)
        {
            var collectionItems = realm.Realm.All<BeatmapCollection>()
                                       .OrderBy(c => c.Name)
                                       .AsEnumerable()
                                       .Select(c => new CollectionToggleMenuItem(c.ToLive(realm), beatmap)).Cast<OsuMenuItem>().ToList();

            collectionItems.Add(new OsuMenuItem(CommonStrings.Manage, MenuItemType.Standard, () => manageCollectionsDialog?.Show()));

            yield return new OsuMenuItem(CommonStrings.Collections) { Items = collectionItems };
        }

        public void ManageCollections() => collectionsDialog?.Show();

        public void Delete(BeatmapSetInfo beatmapSet) => dialogOverlay?.Push(new BeatmapDeleteDialog(beatmapSet));

        public void RestoreAllHidden(BeatmapSetInfo beatmapSet)
        {
            foreach (BeatmapInfo beatmap in beatmapSet.Beatmaps)
                beatmaps.Restore(beatmap);
        }

        private GroupedBeatmap? beforeScopedSelection;

        private readonly Bindable<BeatmapSetInfo?> scopedBeatmapSet = new Bindable<BeatmapSetInfo?>();
        public IBindable<BeatmapSetInfo?> ScopedBeatmapSet => scopedBeatmapSet;

        public void ScopeToBeatmapSet(BeatmapSetInfo beatmapSet)
        {
            beforeScopedSelection = carousel.CurrentGroupedBeatmap;

            scopedBeatmapSet.Value = beatmapSet;
        }

        public void UnscopeBeatmapSet() => unscopeBeatmapSet(restorePreviousSelection: true);

        private void unscopeBeatmapSet(bool restorePreviousSelection)
        {
            if (scopedBeatmapSet.Value == null)
                return;

            if (beforeScopedSelection != null && restorePreviousSelection)
                queueBeatmapSelection(beforeScopedSelection);

            scopedBeatmapSet.Value = null;
            beforeScopedSelection = null;
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            if (skinSource != null)
                skinSource.SourceChanged -= onSkinChangedWhileLegacy;

            base.Dispose(isDisposing);
            modSelectOverlayRegistration?.Dispose();
        }
    }
}
