// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Platform;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Timing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.IO.Archives;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osu.Game.Screens.Ranking;
using osu.Game.Skinning;
using osu.Game.Users;
using osu.Game.Utils;
using osuTK.Graphics;

namespace osu.Game.Screens.Play
{
    [Cached]
    public abstract partial class Player : ScreenWithBeatmapBackground, ISamplePlaybackDisabler, ILocalUserPlayInfo
    {
        /// <summary>
        /// The delay upon completion of the beatmap before displaying the results screen.
        /// </summary>
        public const double RESULTS_DISPLAY_DELAY = 1000.0;

        /// <summary>
        /// Raised after <see cref="StartGameplay"/> is called.
        /// </summary>
        public event Action OnGameplayStarted;

        public override bool AllowUserExit => false; // handled by HoldForMenuButton

        /// <summary>
        /// Raised after all gameplay has finished.
        /// </summary>
        public event Action OnShowingResults;

        protected override bool PlayExitSound => !isRestarting;

        protected override UserActivity InitialActivity => new UserActivity.InSoloGame(Beatmap.Value.BeatmapInfo, Ruleset.Value);

        public override float BackgroundParallaxAmount => 0.1f;

        public override bool HideOverlaysOnEnter => true;

        public override bool HideMenuCursorOnNonMouseInput => true;

        public override bool RequiresPortraitOrientation
        {
            get
            {
                if (!LoadedBeatmapSuccessfully)
                    return false;

                return DrawableRuleset!.RequiresPortraitOrientation;
            }
        }

        protected override OverlayActivation InitialOverlayActivationMode => OverlayActivation.UserTriggered;

        // We are managing our own adjustments (see OnEntering/OnExiting).
        public override bool? ApplyModTrackAdjustments => false;

        private readonly IBindable<bool> gameActive = new Bindable<bool>(true);
        private readonly BindableBool customApproachRateEnabled = new BindableBool();
        private readonly BindableFloat customApproachRate = new BindableFloat();
        private readonly Bindable<string> customUsername = new Bindable<string>();

        [Cached]
        private readonly GameplayIntegrityTracker gameplayIntegrityTracker = new GameplayIntegrityTracker();

        private readonly Bindable<bool> samplePlaybackDisabled = new Bindable<bool>();

        /// <summary>
        /// Whether gameplay should pause when the game window focus is lost.
        /// </summary>
        protected virtual bool PauseOnFocusLost => true;

        public Action<bool> PrepareLoaderForRestart;

        private bool isRestarting;
        private bool skipExitTransition;

        private readonly Bindable<bool> storyboardReplacesBackground = new Bindable<bool>();

        public IBindable<bool> LocalUserPlaying => localUserPlaying;

        private readonly Bindable<bool> localUserPlaying = new Bindable<bool>();
        private readonly Bindable<LocalUserPlayingState> playingState = new Bindable<LocalUserPlayingState>();

        public int RestartCount;

        /// <summary>
        /// Whether the <see cref="HUDOverlay"/> is currently visible.
        /// </summary>
        public IBindable<bool> ShowingOverlayComponents = new Bindable<bool>();

        /// <summary>
        /// A flag which can be checked to decide whether we are in a state where settings that affect
        /// game balance should be allowed to be applied at the current point in time.
        /// </summary>
        public virtual bool AllowCriticalSettingsAdjustment { get; } = true;

        // Should match PlayerLoader for consistency. Cached here for the rare case we push a Player
        // without the loading screen (one such usage is the skin editor's scene library).
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Resolved]
        private ScoreManager scoreManager { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; }

        [Resolved]
        private MusicController musicController { get; set; }

        [Resolved]
        private OsuGameBase game { get; set; }

        public GameplayState GameplayState { get; private set; }

        protected ISkinSource GameplaySkinSource { get; private set; } = null!;

        private Ruleset ruleset;

        public BreakOverlay BreakOverlay;

        private LetterboxOverlay letterboxOverlay;

        /// <summary>
        /// Whether the gameplay is currently in a break.
        /// </summary>
        public readonly IBindable<bool> IsBreakTime = new BindableBool();

        private BreakTracker breakTracker;
        private bool useInstantGameplayStart;
        private bool frameStablePlaybackDisabledForExtremeRate;
        private SpecialGameplayRateMode specialGameplayRateMode;
        private bool specialGameplayClockStarted;

        private const double max_safe_frame_stable_rate = 5;

        protected SkipOverlay SkipIntroOverlay { get; private set; }
        private SkipOverlay skipOutroOverlay;
        private Container breakSkipOverlayContainer;
        private SkipOverlay breakSkipOverlay;

        protected BreakPeriod CurrentBreak => breakTracker.CurrentBreak.Value;

        protected ScoreProcessor ScoreProcessor { get; private set; }

        /// <summary>
        /// When <see langword="true"/>, all mod score multipliers are forced to 1.00x
        /// because the score is being played in Ranked Play mode.
        /// Override this to return <see langword="true"/> in Ranked Play gameplay screens.
        /// </summary>
        protected virtual bool IsRankedPlaySession => false;

        protected HealthProcessor HealthProcessor { get; private set; }

        protected DrawableRuleset DrawableRuleset { get; private set; }

        protected HUDOverlay HUDOverlay { get; private set; }

        public bool LoadedBeatmapSuccessfully => DrawableRuleset?.Objects.Any() == true;

        protected GameplayClockContainer GameplayClockContainer { get; private set; }

        protected bool UsesSpecialGameplayRateMode => specialGameplayRateMode != SpecialGameplayRateMode.None;

        public DimmableStoryboard DimmableStoryboard { get; private set; }
        private bool forceStoryboard;
        private bool forceBeatmapSkin;
        private LocalisableString visualOverrideNotice;

        /// <summary>
        /// Whether failing should be allowed.
        /// By default, this checks whether all selected mods allow failing.
        /// </summary>
        protected virtual bool CheckModsAllowFailure() => GameplayState.Mods.OfType<IApplicableFailOverride>().All(m => m.PerformFail());

        public readonly PlayerConfiguration Configuration;

        /// <summary>
        /// The score for the current play session.
        /// Available only after the player is loaded.
        /// </summary>
        public Score Score { get; private set; }

        /// <summary>
        /// Create a new player instance.
        /// </summary>
        protected Player(PlayerConfiguration configuration = null)
        {
            Configuration = configuration ?? new PlayerConfiguration();
        }

        private ScreenSuspensionHandler screenSuspension;

        private DependencyContainer dependencies;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
            => dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!LoadedBeatmapSuccessfully)
                return;

            PrepareReplay();

            ScoreProcessor.NewJudgement += _ => ScoreProcessor.PopulateScore(Score.ScoreInfo);
            ScoreProcessor.NewJudgement += updateGameplayPerformanceSnapshot;
            ScoreProcessor.OnResetFromReplayFrame += () =>
            {
                ScoreProcessor.PopulateScore(Score.ScoreInfo);
                updateGameplayPerformanceSnapshot();
            };

            gameActive.BindValueChanged(_ => updatePauseOnFocusLostState(), true);
        }

        /// <summary>
        /// Run any recording / playback setup for replays.
        /// </summary>
        protected virtual void PrepareReplay()
        {
            DrawableRuleset.SetRecordTarget(Score);
        }

        [BackgroundDependencyLoader(true)]
        private void load(OsuConfigManager config, OsuGameBase game, CancellationToken cancellationToken)
        {
            config.BindWith(OsuSetting.ForkCustomApproachRateEnabled, customApproachRateEnabled);
            config.BindWith(OsuSetting.ForkCustomApproachRate, customApproachRate);
            config.BindWith(OsuSetting.ForkCustomUsername, customUsername);

            var gameplayMods = Mods.Value.Select(m => m.DeepClone()).ToArray();

            if (gameplayMods.Any(m => m is UnknownMod))
            {
                Logger.Log("Gameplay was started with an unknown mod applied.", level: LogLevel.Important);
                return;
            }

            if (Beatmap.Value is DummyWorkingBeatmap)
                return;

            IBeatmap playableBeatmap = loadPlayableBeatmap(gameplayMods, cancellationToken);

            if (playableBeatmap == null)
                return;

            if (!ModUtils.CheckModsBelongToRuleset(ruleset, gameplayMods))
            {
                Logger.Log($@"Gameplay was started with a mod belonging to a ruleset different than '{ruleset.Description}'.", level: LogLevel.Important);
                return;
            }

            if (game != null)
                gameActive.BindTo(game.IsActive);

            DrawableRuleset = ruleset.CreateDrawableRulesetWith(playableBeatmap, gameplayMods);
            dependencies.CacheAs(DrawableRuleset);

            if (DrawableRuleset is IPositionalMissProvider positionalMissProvider)
                dependencies.CacheAs<IPositionalMissProvider>(positionalMissProvider);

            if (DrawableRuleset is IDrawableScrollingRuleset scrollingRuleset)
                dependencies.CacheAs(scrollingRuleset.ScrollingInfo);

            ScoreProcessor = ruleset.CreateScoreProcessor();
            ScoreProcessor.Mods.Value = gameplayMods;
            ScoreProcessor.IsRankedPlay.Value = IsRankedPlaySession;
            ScoreProcessor.ApplyBeatmap(playableBeatmap);

            dependencies.CacheAs(ScoreProcessor);

            HealthProcessor = gameplayMods.OfType<IApplicableHealthProcessor>().FirstOrDefault()?.CreateHealthProcessor(playableBeatmap.HitObjects[0].StartTime);
            HealthProcessor ??= ruleset.CreateHealthProcessor(playableBeatmap.HitObjects[0].StartTime);
            HealthProcessor.ApplyBeatmap(playableBeatmap);

            dependencies.CacheAs(HealthProcessor);

            InternalChildren = new Drawable[]
            {
                GameplayClockContainer = CreateGameplayClockContainer(Beatmap.Value, DrawableRuleset.GameplayStartTime),
            };

            gameplayIntegrityTracker.ClockReportProvider = createClockIntegrityReport;

            AddInternal(screenSuspension = new ScreenSuspensionHandler(GameplayClockContainer));

            Score = CreateScore(playableBeatmap);

            // ensure the score is in a consistent state with the current player.
            Score.ScoreInfo.BeatmapInfo = Beatmap.Value.BeatmapInfo;
            Score.ScoreInfo.BeatmapHash = Beatmap.Value.BeatmapInfo.Hash;
            Score.ScoreInfo.Ruleset = ruleset.RulesetInfo;
            Score.ScoreInfo.Mods = gameplayMods;

            dependencies.CacheAs(GameplayState = new GameplayState(playableBeatmap, ruleset, gameplayMods, Score, ScoreProcessor, HealthProcessor, Beatmap.Value.Storyboard, PlayingState));
            GameplayPerformanceSnapshot.Reset();

            if (ruleset is IBeatmapVisualOverrideProvider visualOverrideProvider)
            {
                forceStoryboard = visualOverrideProvider.ForceStoryboard(playableBeatmap);
                forceBeatmapSkin = visualOverrideProvider.ForceBeatmapSkin(playableBeatmap);
                visualOverrideNotice = visualOverrideProvider.VisualOverrideNotice(playableBeatmap);
            }

            var rulesetSkinProvider = new RulesetSkinProvidingContainer(ruleset, playableBeatmap, Beatmap.Value.Skin);
            GameplaySkinSource = rulesetSkinProvider;

            // A forced skin is only a property of this play session. Binding it to the global
            // configuration would propagate Disabled to that configuration bindable, preventing
            // subsequent players from changing its value.
            if (forceBeatmapSkin)
            {
                rulesetSkinProvider.BeatmapSkins.Value = true;
                rulesetSkinProvider.BeatmapSkins.Disabled = true;
            }
            else
                config.BindWith(OsuSetting.BeatmapSkins, rulesetSkinProvider.BeatmapSkins);

            config.BindWith(OsuSetting.BeatmapColours, rulesetSkinProvider.BeatmapColours);
            config.BindWith(OsuSetting.BeatmapHitsounds, rulesetSkinProvider.BeatmapHitsounds);
            GameplayClockContainer.Add(new GameplayScrollWheelHandling());

            // needs to exist in frame stable content, but is used by underlay layers so make sure assigned early.
            breakTracker = new BreakTracker(DrawableRuleset.GameplayStartTime, ScoreProcessor)
            {
                Breaks = Beatmap.Value.Beatmap.Breaks
            };

            // load the skinning hierarchy first.
            // this is intentionally done in two stages to ensure things are in a loaded state before exposing the ruleset to skin sources.
            GameplayClockContainer.Add(rulesetSkinProvider);

            if (cancellationToken.IsCancellationRequested)
                return;

            rulesetSkinProvider.AddRange(new Drawable[]
            {
                failAnimationContainer = new FailAnimationContainer(DrawableRuleset)
                {
                    OnComplete = onFailComplete,
                    Children = new[]
                    {
                        // underlay and gameplay should have access to the skinning sources.
                        createUnderlayComponents(Beatmap.Value),
                        createGameplayComponents()
                    }
                },
                FailOverlay = new FailOverlay
                {
                    SaveReplay = Configuration.AllowUserInteraction ? async () => await prepareAndImportScoreAsync(true).ConfigureAwait(false) : null,
                    OnRetry = Configuration.AllowUserInteraction ? () => Restart() : null,
                    OnQuit = () => PerformExitWithConfirmation(),
                },
            });

            if (cancellationToken.IsCancellationRequested)
                return;

            GameplayClockContainer.Add(exitOverlay = new HotkeyExitOverlay
            {
                Depth = float.MinValue,
                Action = () =>
                {
                    if (!this.IsCurrentScreen()) return;

                    PerformExit(skipTransition: true);
                },
            });

            if (Configuration.AllowRestart)
            {
                GameplayClockContainer.Add(retryOverlay = new HotkeyRetryOverlay
                {
                    Depth = float.MinValue,
                    Action = () =>
                    {
                        if (!this.IsCurrentScreen()) return;

                        Restart(true);
                    },
                });
            }

            dependencies.CacheAs(DrawableRuleset.FrameStableClock);
            dependencies.CacheAs<IGameplayClock>(DrawableRuleset.FrameStableClock);

            letterboxOverlay.Clock = DrawableRuleset.FrameStableClock;
            letterboxOverlay.ProcessCustomClock = false;

            // add the overlay components as a separate step as they proxy some elements from the above underlay/gameplay components.
            // also give the overlays the ruleset skin provider to allow rulesets to potentially override HUD elements (used to disable combo counters etc.)
            // we may want to limit this in the future to disallow rulesets from outright replacing elements the user expects to be there.
            failAnimationContainer.Add(createOverlayComponents());

            // Used by ReplaySettingsOverlay for button positioning.
            dependencies.CacheAs(HUDOverlay);

            if (!DrawableRuleset.AllowGameplayOverlays)
            {
                HUDOverlay.ShowHud.Value = false;
                HUDOverlay.ShowHud.Disabled = true;
                BreakOverlay.Hide();
            }

            DrawableRuleset.FrameStableClock.WaitingOnFrames.BindValueChanged(waiting =>
            {
                if (waiting.NewValue)
                    GameplayClockContainer.Stop();
                else
                    GameplayClockContainer.Start();
            });

            DrawableRuleset.IsPaused.BindValueChanged(_ =>
            {
                updateGameplayState();
                updateSampleDisabledState();
            });

            DrawableRuleset.FrameStableClock.IsCatchingUp.BindValueChanged(_ => updateSampleDisabledState());

            DrawableRuleset.HasReplayLoaded.BindValueChanged(_ => updateGameplayState());

            // bind clock into components that require it
            ((IBindable<bool>)DrawableRuleset.IsPaused).BindTo(GameplayClockContainer.IsPaused);

            // A pause is a safe window in which an inflated exclusive queue can be reset.
            GameplayClockContainer.IsPaused.BindValueChanged(paused =>
            {
                // Standalone public build note: AudioThread.AllowLatencyRepair is an optional Mosu framework extension.
            });

            DrawableRuleset.NewResult += r =>
            {
                HealthProcessor.ApplyResult(r);
                ScoreProcessor.ApplyResult(r);
                GameplayState.ApplyResult(r);
            };

            DrawableRuleset.RevertResult += r =>
            {
                HealthProcessor.RevertResult(r);
                ScoreProcessor.RevertResult(r);
            };

            DimmableStoryboard.HasStoryboardEnded.ValueChanged += _ => checkScoreCompleted();

            // Bind the judgement processors to ourselves
            ScoreProcessor.HasCompleted.BindValueChanged(_ => checkScoreCompleted());
            HealthProcessor.Failed += onFail;

            // Provide judgement processors to mods after they're loaded so that they're on the gameplay clock,
            // this is required for mods that apply transforms to these processors.
            ScoreProcessor.OnLoadComplete += _ =>
            {
                foreach (var mod in gameplayMods.OfType<IApplicableToScoreProcessor>())
                    mod.ApplyToScoreProcessor(ScoreProcessor);
            };

            HealthProcessor.OnLoadComplete += _ =>
            {
                foreach (var mod in gameplayMods.OfType<IApplicableToHealthProcessor>())
                    mod.ApplyToHealthProcessor(HealthProcessor);
            };

            IsBreakTime.BindTo(breakTracker.IsBreakTime);
            IsBreakTime.BindValueChanged(onBreakTimeChanged, true);
            breakTracker.CurrentBreak.BindValueChanged(onCurrentBreakChanged);
        }

        protected virtual GameplayClockContainer CreateGameplayClockContainer(WorkingBeatmap beatmap, double gameplayStart) => new MasterGameplayClockContainer(beatmap, gameplayStart);

        private Drawable createUnderlayComponents(WorkingBeatmap working)
        {
            var container = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    DimmableStoryboard = new DimmableStoryboard(GameplayState.Storyboard, GameplayState.Mods)
                    {
                        RelativeSizeAxes = Axes.Both,
                        IgnoreUserSettings = { Value = forceStoryboard },
                    },
                    letterboxOverlay = new LetterboxOverlay
                    {
                        BreakTracker = breakTracker,
                        Alpha = working.Beatmap.LetterboxInBreaks ? 1 : 0,
                    },
                    new KiaiGameplayFountains(),
                },
            };

            return container;
        }

        private Drawable createGameplayComponents() => new ScalingContainer(ScalingMode.Gameplay)
        {
            Children = new Drawable[]
            {
                DrawableRuleset.With(r =>
                    r.FrameStableComponents.Children = new Drawable[]
                    {
                        ScoreProcessor,
                        HealthProcessor,
                        new ComboEffects(ScoreProcessor),
                        breakTracker,
                    }),
            }
        };

        private Drawable createOverlayComponents()
        {
            var container = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new[]
                {
                    DimmableStoryboard.OverlayLayerContainer.CreateProxy(),
                    HUDOverlay = new HUDOverlay(DrawableRuleset, GameplayState.Mods, Configuration, ScoreProcessor)
                    {
                        HoldToQuit =
                        {
                            Action = () => PerformExitWithConfirmation(),
                            IsPaused = { BindTarget = GameplayClockContainer.IsPaused },
                            ReplayLoaded = { BindTarget = DrawableRuleset.HasReplayLoaded },
                        },
                        InputCountController =
                        {
                            IsCounting =
                            {
                                Value = false
                            },
                        },
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre
                    },
                    BreakOverlay = new BreakOverlay(ScoreProcessor)
                    {
                        Clock = DrawableRuleset.FrameStableClock,
                        ProcessCustomClock = false,
                        BreakTracker = breakTracker,
                    },
                    breakSkipOverlayContainer = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                    },
                    // display the cursor above some HUD elements.
                    DrawableRuleset.Cursor?.CreateProxy() ?? new Container(),
                    SkipIntroOverlay = CreateSkipOverlay(DrawableRuleset.GameplayStartTime).With(o =>
                    {
                        o.RequestSkip = RequestIntroSkip;
                    }),
                    skipOutroOverlay = new SkipOverlay(GameplayState.Storyboard.LatestEventTime ?? 0)
                    {
                        RequestSkip = () => progressToResults(false),
                        Alpha = 0
                    },
                    DrawableRuleset.ResumeOverlay?.CreateProxy() ?? new Container(),
                    PauseOverlay = new PauseOverlay
                    {
                        OnResume = Resume,
                        Retries = RestartCount,
                        OnRetry = () => Restart(),
                        OnQuit = () => PerformExitWithConfirmation(),
                    },
                },
            };

            if (!Configuration.AllowSkipping || !DrawableRuleset.AllowGameplayOverlays)
            {
                SkipIntroOverlay.Expire();
                skipOutroOverlay.Expire();
            }

            if (forceStoryboard || forceBeatmapSkin)
            {
                var notice = new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 42,
                    Width = 520,
                    Height = 38,
                    CornerRadius = 8,
                    Masking = true,
                    Depth = float.MinValue,
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(18, 20, 28, 235),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = visualOverrideNotice,
                            Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                        },
                    },
                };
                container.Add(notice);
                notice.Delay(5000).FadeOut(500);
            }

            return container;
        }

        protected virtual SkipOverlay CreateSkipOverlay(double startTime) => new SkipOverlay(startTime);

        protected virtual SkipOverlay CreateBreakSkipOverlay(BreakPeriod breakPeriod) => new SkipOverlay(breakPeriod.EndTime);

        private void onCurrentBreakChanged(ValueChangedEvent<BreakPeriod> currentBreak)
        {
            breakSkipOverlay?.Expire();
            breakSkipOverlay = null;
            breakSkipOverlayContainer?.Clear(false);

            BreakPeriod breakPeriod = currentBreak.NewValue;

            if (breakPeriod == null
                || breakPeriod.Duration < MasterGameplayClockContainer.MINIMUM_BREAK_DURATION_FOR_SKIP
                || !MosuServerEnvironment.SupportsBreakSkipping
                || !Configuration.AllowSkipping
                || !Configuration.AllowUserInteraction
                || !DrawableRuleset.AllowGameplayOverlays
                || UsesSpecialGameplayRateMode
                || DrawableRuleset.HasReplayLoaded.Value)
                return;

            SkipOverlay overlay = CreateBreakSkipOverlay(breakPeriod);
            overlay.RequestSkip = () => RequestBreakSkip(breakPeriod);

            LoadComponentAsync(overlay, loaded =>
            {
                if (breakTracker.CurrentBreak.Value?.Equals(breakPeriod) != true)
                {
                    loaded.Expire();
                    return;
                }

                breakSkipOverlay = loaded;
                breakSkipOverlayContainer.Add(loaded);
            });
        }

        private void onBreakTimeChanged(ValueChangedEvent<bool> isBreakTime)
        {
            updateGameplayState();
            updatePauseOnFocusLostState();
            HUDOverlay.InputCountController.IsCounting.Value = !isBreakTime.NewValue;
        }

        private void updateGameplayState()
        {
            bool inGameplay = !DrawableRuleset.HasReplayLoaded.Value && !GameplayState.HasPassed && !GameplayState.HasFailed;
            bool inBreak = breakTracker.IsBreakTime.Value || DrawableRuleset.IsPaused.Value;

            if (inGameplay)
                playingState.Value = inBreak ? LocalUserPlayingState.Break : LocalUserPlayingState.Playing;
            else
                playingState.Value = LocalUserPlayingState.NotPlaying;

            localUserPlaying.Value = playingState.Value == LocalUserPlayingState.Playing;
            OverlayActivationMode.Value = playingState.Value == LocalUserPlayingState.Playing ? OverlayActivation.Disabled : OverlayActivation.UserTriggered;
        }

        private void updateGameplayPerformanceSnapshot(JudgementResult result)
        {
            updateGameplayPerformanceSnapshot();

            GameplayPerformanceSnapshot.LastJudgement = result.Type.ToString();
            GameplayPerformanceSnapshot.LastJudgementTime = GameplayClockContainer?.CurrentTime ?? 0;
        }

        private void updateGameplayPerformanceSnapshot()
        {
            GameplayPerformanceSnapshot.Combo = ScoreProcessor.Combo.Value;
            GameplayPerformanceSnapshot.MaxCombo = ScoreProcessor.HighestCombo.Value;
            GameplayPerformanceSnapshot.MissCount = ScoreProcessor.Statistics.Where(s => s.Key.IsMiss()).Sum(s => s.Value);
            GameplayPerformanceSnapshot.Health = HealthProcessor?.Health.Value ?? 0;
        }

        private void updateSampleDisabledState()
        {
            samplePlaybackDisabled.Value = SuppressSamplePlayback
                                          || specialGameplayRateMode != SpecialGameplayRateMode.None
                                          || (DrawableRuleset.FrameStableClock.IsCatchingUp.Value && !AllowSamplePlaybackDuringCatchUp)
                                          || GameplayClockContainer.IsPaused.Value;
        }

        protected virtual bool SuppressSamplePlayback => false;

        protected virtual bool AllowSamplePlaybackDuringCatchUp => false;

        private void updatePauseOnFocusLostState()
        {
            if (!PauseOnFocusLost || !pausingSupportedByCurrentState || breakTracker.IsBreakTime.Value)
                return;

            if (gameActive.Value == false)
            {
                bool paused = Pause();

                // if the initial pause could not be satisfied, the pause cooldown may be active.
                // reschedule the pause attempt until it can be achieved.
                if (!paused)
                    Scheduler.AddOnce(updatePauseOnFocusLostState);
            }
        }

        private IBeatmap loadPlayableBeatmap(Mod[] gameplayMods, CancellationToken cancellationToken)
        {
            IBeatmap playable;

            try
            {
                if (Beatmap.Value.Beatmap == null)
                    throw new InvalidOperationException("Beatmap was not loaded");

                var rulesetInfo = Ruleset.Value ?? Beatmap.Value.BeatmapInfo.Ruleset;
                ruleset = rulesetInfo.CreateInstance();

                if (ruleset == null)
                    throw new RulesetLoadException("Instantiation failure");

                try
                {
                    IReadOnlyList<Mod> beatmapMods = gameplayMods;

                    if (customApproachRateEnabled.Value)
                    {
                        float targetApproachRate = Math.Clamp(customApproachRate.Value, 0f, 12f);
                        float compensatedApproachRate = getClockRateInvariantApproachRate(targetApproachRate, gameplayMods);

                        beatmapMods = gameplayMods.Append(new ForcedApproachRateMod(compensatedApproachRate)).ToArray();
                    }

                    playable = Beatmap.Value.GetPlayableBeatmap(ruleset.RulesetInfo, beatmapMods, cancellationToken);
                    gameplayIntegrityTracker.SetDifficulty(Beatmap.Value.Beatmap.Difficulty, playable.Difficulty, customApproachRateEnabled.Value);
                }
                catch (BeatmapInvalidForRulesetException)
                {
                    Logger.Log($"The current beatmap is not playable in {ruleset.RulesetInfo.Name}!", level: LogLevel.Important);
                    return null;
                }

                if (playable.HitObjects.Count == 0)
                {
                    Logger.Log("Beatmap contains no hit objects!", level: LogLevel.Important);
                    return null;
                }
            }
            catch (OperationCanceledException)
            {
                // Load has been cancelled. No logging is required.
                return null;
            }
            catch (Exception e)
            {
                Logger.Error(e, "Could not load beatmap successfully!");
                //couldn't load, hard abort!
                return null;
            }

            return playable;
        }

        private GameplayClockIntegrityReport createClockIntegrityReport()
        {
            if (GameplayClockContainer is not MasterGameplayClockContainer masterClock)
                return new GameplayClockIntegrityReport { SpecialRateMode = UsesSpecialGameplayRateMode };

            return new GameplayClockIntegrityReport
            {
                ValidationEnabled = masterClock.PlaybackValidationEnabled,
                PlaybackRateValid = masterClock.PlaybackRateValid.Value,
                DiscrepancyCount = masterClock.PlaybackDiscrepancyCount,
                MaxDriftMilliseconds = masterClock.MaxPlaybackDriftMilliseconds,
                GameplayElapsedMilliseconds = masterClock.ElapsedGameplayClockTimeExcludingSeeks,
                RawGameplayElapsedMilliseconds = masterClock.ElapsedGameplayClockTime,
                SeekCount = masterClock.GameplaySeekCount,
                SeekDeltaMilliseconds = masterClock.GameplaySeekDeltaMilliseconds,
                WallElapsedMilliseconds = masterClock.ElapsedWallClockTime,
                SpecialRateMode = UsesSpecialGameplayRateMode,
                AuthorisedSkips = masterClock.AuthorisedSkips.ToArray(),
            };
        }

        /// <summary>
        /// Attempts to complete a user request to exit gameplay, with confirmation.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>This should only be called in response to a user interaction. Exiting is not guaranteed.</item>
        /// <item>This will interrupt any pending progression to the results screen, even if the transition has begun.</item>
        /// </list>
        ///
        /// This method will show the pause or fail dialog before performing an exit.
        /// If a dialog is not yet displayed, the exit will be blocked and the relevant dialog will display instead.
        /// </remarks>
        /// <returns>Whether this call resulted in a final exit.</returns>
        protected bool PerformExitWithConfirmation()
        {
            bool pauseOrFailDialogVisible =
                PauseOverlay.State.Value == Visibility.Visible || FailOverlay.State.Value == Visibility.Visible;

            if (!pauseOrFailDialogVisible)
            {
                // if the fail animation is currently in progress, accelerate it (it will show the pause dialog on completion).
                if (ValidForResume && GameplayState.HasFailed)
                {
                    failAnimationContainer.FinishTransforms(true);
                    return false;
                }

                // even if this call has requested a dialog, there is a chance the current player mode doesn't support pausing.
                if (pausingSupportedByCurrentState)
                {
                    // in the case a dialog needs to be shown, attempt to pause and show it.
                    // this may fail (see internal checks in Pause()) but the fail cases are temporary, so don't fall through to Exit().
                    Pause();
                    return false;
                }
            }

            return PerformExit();
        }

        /// <summary>
        /// Attempts to complete a user request to exit gameplay.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        /// <item>This should only be called in response to a user interaction. Exiting is not guaranteed.</item>
        /// <item>This will interrupt any pending progression to the results screen, even if the transition has begun.</item>
        /// </list>
        /// </remarks>
        /// <param name="skipTransition">Whether the exit should perform without a transition, because the screen had faded to black already.</param>
        /// <returns>Whether this call resulted in a final exit.</returns>
        protected bool PerformExit(bool skipTransition = false)
        {
            // Matching osu!stable behaviour, if the results screen is pending and the user requests an exit,
            // show the results instead.
            if (GameplayState.HasPassed && !isRestarting)
            {
                progressToResults(false);
                return false;
            }

            // import current score if possible.
            prepareAndImportScoreAsync();

            if (this.IsCurrentScreen())
            {
                skipExitTransition = skipTransition;

                // The actual exit is performed if
                // - the pause / fail dialog was not requested
                // - the pause / fail dialog was requested but is already displayed (user showing intention to exit).
                // - the pause / fail dialog was requested but couldn't be displayed due to the type or state of this Player instance.
                this.Exit();
            }
            else
            {
                // May be restarting from results screen.
                if (this.GetChildScreen() != null)
                    this.MakeCurrent();
            }

            return true;
        }

        protected virtual void RequestIntroSkip()
        {
            PerformIntroSkip();
        }

        protected virtual void RequestBreakSkip(BreakPeriod breakPeriod)
        {
            if (!MosuServerEnvironment.SupportsBreakSkipping)
                return;

            PerformBreakSkip(breakPeriod, false);
        }

        protected int GetBreakIndex(BreakPeriod breakPeriod) => breakTracker.GetBreakIndex(breakPeriod);

        /// <summary>
        /// Performs a locally validated break skip.
        /// </summary>
        protected bool PerformBreakSkip(BreakPeriod breakPeriod, bool multiplayerServerAuthorised)
        {
            if (!MosuServerEnvironment.SupportsBreakSkipping
                || breakTracker.CurrentBreak.Value?.Equals(breakPeriod) != true)
                return false;

            samplePlaybackDisabled.Value = true;

            bool skipped = (GameplayClockContainer as MasterGameplayClockContainer)?.SkipBreak(
                breakPeriod, breakTracker.GetBreakIndex(breakPeriod), multiplayerServerAuthorised) == true;

            // A break skip is a large seek after gameplay has already started. Explicitly allow
            // the frame-stable clock to consume this validated discontinuity in one frame;
            // otherwise release builds reject it as an invalid audio-clock value indefinitely.
            if (skipped)
                DrawableRuleset.AllowOneFrameClockSeek();

            updateSampleDisabledState();
            return skipped;
        }

        /// <summary>
        /// Skip forward to the next valid skip point.
        /// </summary>
        /// <param name="fullLength"><c>true</c> to skip as close to gameplay as possible, or <c>false</c> to skip only to the next valid skip point.</param>
        protected void PerformIntroSkip(bool fullLength = false)
        {
            // user requested skip
            // disable sample playback to stop currently playing samples and perform skip
            samplePlaybackDisabled.Value = true;

            (GameplayClockContainer as MasterGameplayClockContainer)?.Skip(fullLength);

            // return samplePlaybackDisabled.Value to what is defined by the beatmap's current state
            updateSampleDisabledState();
        }

        /// <summary>
        /// Seek to a specific time in gameplay.
        /// </summary>
        /// <param name="time">The destination time to seek to.</param>
        public void Seek(double time) => GameplayClockContainer.Seek(time);

        private ScheduledDelegate frameStablePlaybackResetDelegate;

        /// <summary>
        /// Specify and seek to a custom start time from which gameplay should be observed.
        /// </summary>
        /// <remarks>
        /// This performs a non-frame-stable seek. Intermediate hitobject judgements may not be applied or reverted correctly during this seek.
        /// </remarks>
        /// <param name="time">The destination time to seek to.</param>
        protected void SetGameplayStartTime(double time)
        {
            if (frameStablePlaybackResetDelegate?.Cancelled == false && !frameStablePlaybackResetDelegate.Completed)
                frameStablePlaybackResetDelegate.RunTask();

            bool wasFrameStable = DrawableRuleset.FrameStablePlayback;
            DrawableRuleset.FrameStablePlayback = false;

            GameplayClockContainer.Reset(time);

            // Delay resetting frame-stable playback for one frame to give the FrameStabilityContainer a chance to seek.
            frameStablePlaybackResetDelegate = ScheduleAfterChildren(() => DrawableRuleset.FrameStablePlayback = wasFrameStable);
        }

        /// <summary>
        /// Restart gameplay via a parent <see cref="PlayerLoader"/>.
        /// <remarks>This can be called from a child screen in order to trigger the restart process.</remarks>
        /// </summary>
        /// <param name="quickRestart">Whether a quick restart was requested (skipping intro etc.).</param>
        /// <returns>Whether this call resulted in a restart.</returns>
        public bool Restart(bool quickRestart = false)
        {
            if (!Configuration.AllowRestart)
                return false;

            isRestarting = true;

            // at the point of restarting the track should either already be paused or the volume should be zero.
            // stopping here is to ensure music doesn't become audible after exiting back to PlayerLoader.
            musicController.Stop();

            skipExitTransition = quickRestart;
            PrepareLoaderForRestart?.Invoke(quickRestart);

            return PerformExit(quickRestart);
        }

        /// <summary>
        /// This delegate, when set, means the results screen has been queued to appear.
        /// The display of the results screen may be delayed by any work being done in <see cref="PrepareScoreForResultsAsync"/>.
        /// </summary>
        /// <remarks>
        /// Once set, this can *only* be cancelled by rewinding, ie. if <see cref="JudgementProcessor.HasCompleted">ScoreProcessor.HasCompleted</see> becomes <see langword="false"/>.
        /// Even if the user requests an exit, it will forcefully proceed to the results screen (see special case in <see cref="OnExiting"/>).
        /// </remarks>
        private ScheduledDelegate resultsDisplayDelegate;

        /// <summary>
        /// A task which asynchronously prepares a completed score for display at results.
        /// This may include performing net requests or importing the score into the database, generally to ensure things are in a sane state for the play session.
        /// </summary>
        private Task<ScoreInfo> prepareScoreForDisplayTask;

        /// <summary>
        /// Handles changes in player state which may progress the completion of gameplay / this screen's lifetime.
        /// </summary>
        private void checkScoreCompleted()
        {
            // If this player instance is in the middle of an exit, don't attempt any kind of state update.
            if (!this.IsCurrentScreen())
                return;

            // Handle cases of arriving at this method when not in a completed state.
            // - When a storyboard completion triggered this call earlier than gameplay finishes.
            // - When a replay has been rewound before a queued resultsDisplayDelegate has run.
            //
            // Currently, even if this scenario is hit, prepareAndImportScoreAsync has already been queued (and potentially run).
            // In the scenarios above, this is a non-issue, but it still feels a bit convoluted to have to cancel in this method.
            // Maybe this can be improved with further refactoring.
            if (!ScoreProcessor.HasCompleted.Value)
            {
                resultsDisplayDelegate?.Cancel();
                resultsDisplayDelegate = null;

                GameplayState.HasPassed = false;
                ValidForResume = true;
                skipOutroOverlay.Hide();
                return;
            }

            // Only show the completion screen if the player hasn't failed
            if (GameplayState.HasFailed)
                return;

            GameplayState.HasPassed = true;

            // Setting this early in the process means that even if something were to go wrong in the order of events following, there
            // is no chance that a user could return to the (already completed) Player instance from a child screen.
            ValidForResume = false;

            bool storyboardStillRunning = DimmableStoryboard.ContentDisplayed && !DimmableStoryboard.HasStoryboardEnded.Value;

            // If the current beatmap has a storyboard, this method will be called again on storyboard completion.
            // Alternatively, the user may press the outro skip button, forcing immediate display of the results screen.
            if (storyboardStillRunning)
            {
                skipOutroOverlay.Show();
                return;
            }

            progressToResults(true);
        }

        /// <summary>
        /// Queue the results screen for display.
        /// </summary>
        /// <remarks>
        /// A final display will only occur once all work is completed in <see cref="PrepareScoreForResultsAsync"/>. This means that even after calling this method, the results screen will never be shown until <see cref="JudgementProcessor.HasCompleted">ScoreProcessor.HasCompleted</see> becomes <see langword="true"/>.
        /// </remarks>
        /// <param name="withDelay">Whether a minimum delay (<see cref="RESULTS_DISPLAY_DELAY"/>) should be added before the screen is displayed.</param>
        private void progressToResults(bool withDelay)
        {
            if (!Configuration.ShowResults)
                return;

            // Setting this early in the process means that even if something were to go wrong in the order of events following, there
            // is no chance that a user could return to the (already completed) Player instance from a child screen.
            ValidForResume = false;

            double delay = withDelay ? RESULTS_DISPLAY_DELAY : 0;

            resultsDisplayDelegate?.Cancel();
            resultsDisplayDelegate = new ScheduledDelegate(() =>
            {
                if (prepareScoreForDisplayTask == null)
                {
                    // Try importing score since the task hasn't been invoked yet.
                    prepareAndImportScoreAsync();
                    return;
                }

                if (!prepareScoreForDisplayTask.IsCompleted)
                    // If the asynchronous preparation has not completed, keep repeating this delegate.
                    return;

                resultsDisplayDelegate?.Cancel();

                if (prepareScoreForDisplayTask.GetResultSafely() == null)
                {
                    // If score import did not occur, we do not want to show the results screen.
                    return;
                }

                if (!this.IsCurrentScreen())
                    // This player instance may already be in the process of exiting.
                    return;

                OnShowingResults?.Invoke();
                this.Push(CreateResults(prepareScoreForDisplayTask.GetResultSafely()));
            }, Time.Current + delay, 50);

            Scheduler.Add(resultsDisplayDelegate);
        }

        /// <summary>
        /// Asynchronously run score preparation operations (database import, online submission etc.).
        /// </summary>
        /// <param name="forceImport">Whether the score should be imported even if non-passing (or the current configuration doesn't allow for it).</param>
        /// <returns>The final score.</returns>
        [ItemCanBeNull]
        private Task<ScoreInfo> prepareAndImportScoreAsync(bool forceImport = false)
        {
            // Ensure we are not writing to the replay any more, as we are about to consume and store the score.
            DrawableRuleset.SetRecordTarget(null);

            if (prepareScoreForDisplayTask != null)
                return prepareScoreForDisplayTask;

            // We do not want to import the score in cases where we don't show results
            bool canShowResults = Configuration.ShowResults && ScoreProcessor.HasCompleted.Value && GameplayState.HasPassed;
            if (!canShowResults && !forceImport)
                return Task.FromResult<ScoreInfo>(null);

            // Clone score before beginning any async processing.
            // - Must be run synchronously as the score may potentially be mutated in the background.
            // - Must be cloned for the same reason.
            Score scoreCopy = Score.DeepClone();

            return prepareScoreForDisplayTask = Task.Run(async () =>
            {
                try
                {
                    await PrepareScoreForResultsAsync(scoreCopy).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, @"Score preparation failed!");
                }

                try
                {
                    await ImportScore(scoreCopy).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, @"Score import failed!");
                }

                return scoreCopy.ScoreInfo;
            });
        }

        #region Fail Logic

        protected FailOverlay FailOverlay { get; private set; }

        private FailAnimationContainer failAnimationContainer;

        private bool onFail()
        {
            // Multiple failing judgements can arrive in the same frame before HealthProcessor latches its failed state.
            // In that case we still want the fail to be accepted, but must not re-enter the player's fail sequence.
            if (GameplayState.HasFailed)
                return true;

            // Failing after the quit sequence has started may cause weird side effects with the fail animation / effects.
            if (GameplayState.HasQuit)
                return false;

            if (!CheckModsAllowFailure())
                return false;

            PerformFail();
            return true;
        }

        /// <summary>
        /// Called when the player is determined to have failed.
        /// </summary>
        protected virtual void PerformFail()
        {
            Debug.Assert(!GameplayState.HasFailed);
            Debug.Assert(!GameplayState.HasPassed);
            Debug.Assert(!GameplayState.HasQuit);

            GameplayState.HasFailed = true;

            updateGameplayState();

            // There is a chance that we could be in a paused state as the ruleset's internal clock (see FrameStabilityContainer)
            // could process an extra frame after the GameplayClock is stopped.
            // In such cases we want the fail state to precede a user triggered pause.
            if (PauseOverlay.State.Value == Visibility.Visible)
                PauseOverlay.Hide();

            bool restartOnFail = GameplayState.Mods.OfType<IApplicableFailOverride>().Any(m => m.RestartOnFail);
            if (!restartOnFail)
                failAnimationContainer.Start();

            // Failures can be triggered either by a judgement, or by a mod.
            //
            // For the case of a judgement, due to ordering considerations, ScoreProcessor will not have received
            // the final judgement which triggered the failure yet (see DrawableRuleset.NewResult handling above).
            //
            // A schedule here ensures that any lingering judgements from the current frame are applied before we
            // finalise the score as "failed".
            Schedule(() =>
            {
                ConcludeFailedScore(Score);

                if (restartOnFail)
                    Restart(true);
            });
        }

        /// <summary>
        /// Performs last operations on the supplied <paramref name="score"/> before this <see cref="Player"/> is definitively exited due to failing.
        /// </summary>
        protected virtual void ConcludeFailedScore(Score score)
        {
            ScoreProcessor.FailScore(score.ScoreInfo);
        }

        /// <summary>
        /// Invoked when the fail animation has finished.
        /// </summary>
        private void onFailComplete()
        {
            GameplayClockContainer.Stop();

            FailOverlay.Retries = RestartCount;
            FailOverlay.Show();
        }

        #endregion

        #region Pause Logic

        public bool IsResuming { get; private set; }

        /// <summary>
        /// The amount of gameplay time after which a second pause is allowed.
        /// </summary>
        protected virtual double PauseCooldownDuration => 1000;

        protected PauseOverlay PauseOverlay { get; private set; }

        private double? lastPauseActionTime;

        private HotkeyRetryOverlay retryOverlay;
        private HotkeyExitOverlay exitOverlay;

        protected bool PauseCooldownActive =>
            PlayingState.Value == LocalUserPlayingState.Playing && lastPauseActionTime.HasValue && GameplayClockContainer.CurrentTime < lastPauseActionTime + PauseCooldownDuration;

        /// <summary>
        /// A set of conditionals which defines whether the current game state and configuration allows for
        /// pausing to be attempted via <see cref="Pause"/>. If false, the game should generally exit if a user pause
        /// is attempted.
        /// </summary>
        private bool pausingSupportedByCurrentState =>
            // must pass basic screen conditions (beatmap loaded, instance allows pause)
            LoadedBeatmapSuccessfully && Configuration.AllowPause && ValidForResume
            // replays cannot be paused and exit immediately
            && !DrawableRuleset.HasReplayLoaded.Value
            // cannot pause if we are already in a fail state
            && !GameplayState.HasFailed;

        private bool canResume =>
            // cannot resume from a non-paused state
            GameplayClockContainer.IsPaused.Value
            // cannot resume if we are already in a fail state
            && !GameplayState.HasFailed
            // already resuming
            && !IsResuming;

        public virtual bool Pause()
        {
            if (!pausingSupportedByCurrentState) return false;

            if (!IsResuming && PauseCooldownActive)
                return false;

            if (IsResuming)
            {
                DrawableRuleset.CancelResume();
                IsResuming = false;
            }

            GameplayClockContainer.Stop();
            PauseOverlay.Show();
            lastPauseActionTime = GameplayClockContainer.CurrentTime;
            return true;
        }

        public void Resume()
        {
            if (!canResume) return;

            // Standalone public build note: AudioThread.FlushExclusiveQueueNow is an optional Mosu framework extension.

            IsResuming = true;
            PauseOverlay.Hide();

            // breaks and time-based conditions may allow instant resume.
            if (breakTracker.IsBreakTime.Value)
                completeResume();
            else
                DrawableRuleset.RequestResume(completeResume);

            void completeResume()
            {
                // The audio clock may continue moving while the gameplay clock is paused. Allow the
                // frame-stable clock to resynchronise directly on the first resumed frame rather than
                // rejecting the multi-second discontinuity and freezing the playfield update subtree.
                DrawableRuleset.AllowOneFrameClockSeek();
                GameplayClockContainer.Start();
                IsResuming = false;
            }
        }

        #endregion

        #region Screen Logic

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            // Standalone public build note: FreezeDetector is an optional Mosu framework extension.

            if (!LoadedBeatmapSuccessfully)
                return;

            updateFrameStablePlaybackForCurrentRate();

            useInstantGameplayStart = shouldUseInstantGameplayStart();

            if (useInstantGameplayStart)
            {
                ClearTransforms();
                Alpha = 1;
                this.ScaleTo(1);
            }
            else
            {
                Alpha = 0;
                this
                    .ScaleTo(0.7f)
                    .ScaleTo(1, 750, Easing.OutQuint)
                    .Delay(250)
                    .FadeIn(250);
            }

            ApplyToBackground(b =>
            {
                b.IgnoreUserSettings.Value = false;
                b.BlurAmount.Value = 0;
                b.FadeColour(Color4.White, 250);

                // bind component bindables.
                ((IBindable<bool>)b.IsBreakTime).BindTo(breakTracker.IsBreakTime);

                b.StoryboardReplacesBackground.BindTo(storyboardReplacesBackground);

                failAnimationContainer.Background = b;
            });

            HUDOverlay.IsPlaying.BindTo(localUserPlaying);
            ShowingOverlayComponents.BindTo(HUDOverlay.ShowHud);

            DimmableStoryboard.IsBreakTime.BindTo(breakTracker.IsBreakTime);

            storyboardReplacesBackground.Value = GameplayState.Storyboard.ReplacesBackground && GameplayState.Storyboard.HasDrawable;

            foreach (var mod in GameplayState.Mods.OfType<IApplicableToPlayer>())
                mod.ApplyToPlayer(this);

            foreach (var mod in GameplayState.Mods.OfType<IApplicableToHUD>())
                mod.ApplyToHUD(HUDOverlay);

            specialGameplayRateMode = getSpecialGameplayRateMode();

            IAdjustableAudioComponent gameplayTrackAdjustments = specialGameplayRateMode == SpecialGameplayRateMode.None
                ? new SafeRateAdjustableAudioComponent(GameplayClockContainer.AdjustmentsFromMods)
                : GameplayClockContainer.AdjustmentsFromMods;

            foreach (var mod in GameplayState.Mods.OfType<IApplicableToTrack>())
                mod.ApplyToTrack(gameplayTrackAdjustments);

            updateGameplayState();

            GameplayClockContainer.FadeInFromZero(useInstantGameplayStart ? 0 : 750, Easing.OutQuint);

            StartGameplay();
            OnGameplayStarted?.Invoke();
        }

        protected override void Update()
        {
            base.Update();

            if (!LoadedBeatmapSuccessfully)
                return;

            updateFrameStablePlaybackForCurrentRate();
            updateSpecialGameplayRateProgression();
        }

        /// <summary>
        /// Called to trigger the starting of the gameplay clock and underlying gameplay.
        /// This will be called on entering the player screen once. A derived class may block the first call to this to delay the start of gameplay.
        /// </summary>
        protected virtual void StartGameplay()
        {
            if (GameplayClockContainer.IsRunning)
                Logger.Error(new InvalidOperationException($"{nameof(StartGameplay)} should not be called when the gameplay clock is already running"), "Clock failure");

            double? startTime = useInstantGameplayStart ? GameplayClockContainer.GameplayStartTime : null;

            if (specialGameplayRateMode != SpecialGameplayRateMode.None)
            {
                if (GameplayClockContainer is MasterGameplayClockContainer masterClock)
                {
                    var timeRange = getSpecialGameplayTimeRange();
                    masterClock.UseSilentReferenceClockPlayback(timeRange.MinimumTime, timeRange.MaximumTime);
                }

                GameplayClockContainer.TreatNegativeRateAsRewinding = specialGameplayRateMode != SpecialGameplayRateMode.Reverse;
                musicController.Stop();
                startTime = getSpecialGameplayStartTime();
            }
            else
            {
                GameplayClockContainer.TreatNegativeRateAsRewinding = true;
            }

            GameplayClockContainer.Reset(time: startTime, startClock: true);

            if (specialGameplayRateMode == SpecialGameplayRateMode.None && !useInstantGameplayStart && (Configuration.AutomaticallySkipIntro || !string.IsNullOrEmpty(GameplayPerformanceSnapshot.BenchmarkId)))
                SkipIntroOverlay.SkipWhenReady();
        }

        private SpecialGameplayRateMode getSpecialGameplayRateMode()
        {
            double rate = ModUtils.CalculateRateWithMods(GameplayState.Mods);

            if (rate < 0)
                return SpecialGameplayRateMode.Reverse;

            if (rate == 0)
                return SpecialGameplayRateMode.Freeze;

            return SpecialGameplayRateMode.None;
        }

        private double getSpecialGameplayStartTime()
        {
            return specialGameplayRateMode switch
            {
                SpecialGameplayRateMode.Freeze => GameplayClockContainer.GameplayStartTime,
                SpecialGameplayRateMode.Reverse => Math.Max(GameplayClockContainer.GameplayStartTime, GameplayState.Beatmap.GetLastObjectTime()),
                _ => GameplayClockContainer.GameplayStartTime,
            };
        }

        private (double MinimumTime, double MaximumTime) getSpecialGameplayTimeRange()
        {
            double gameplayStartTime = GameplayClockContainer.GameplayStartTime;

            return specialGameplayRateMode switch
            {
                SpecialGameplayRateMode.Freeze => (gameplayStartTime, gameplayStartTime),
                SpecialGameplayRateMode.Reverse => (gameplayStartTime, Math.Max(gameplayStartTime, GameplayState.Beatmap.GetLastObjectTime())),
                _ => (GameplayClockContainer.StartTime, GameplayClockContainer.GameplayStartTime),
            };
        }

        private bool shouldUseInstantGameplayStart()
        {
            double rate = ModUtils.CalculateRateWithMods(GameplayState.Mods);

            if (rate <= 1)
                return false;

            const double minimum_visible_intro_duration = 500;

            double introDuration = GameplayClockContainer.GameplayStartTime - GameplayClockContainer.StartTime;
            double realTimeUntilGameplay = introDuration / rate;

            return realTimeUntilGameplay < minimum_visible_intro_duration;
        }

        private void updateFrameStablePlaybackForCurrentRate()
        {
            if (frameStablePlaybackDisabledForExtremeRate)
                return;

            double currentRate = Math.Abs(GameplayClockContainer.IsRunning ? GameplayClockContainer.Rate : ModUtils.CalculateRateWithMods(GameplayState.Mods));

            if (currentRate <= max_safe_frame_stable_rate)
                return;

            DrawableRuleset.FrameStablePlayback = false;
            frameStablePlaybackDisabledForExtremeRate = true;
        }

        private void updateSpecialGameplayRateProgression()
        {
            if (specialGameplayRateMode != SpecialGameplayRateMode.Reverse)
                return;

            if (GameplayClockContainer.IsRunning)
            {
                specialGameplayClockStarted = true;
                return;
            }

            if (GameplayState.HasCompleted || resultsDisplayDelegate != null)
                return;

            if (!specialGameplayClockStarted || GameplayClockContainer.IsPaused.Value)
                return;

            GameplayState.HasPassed = true;
            ValidForResume = false;
            skipOutroOverlay.Hide();

            updateGameplayState();
            prepareAndImportScoreAsync(forceImport: true);
            progressToResults(true);
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            Debug.Assert(!ValidForResume);

            screenSuspension?.RemoveAndDisposeImmediately();

            // If these are not disposed, audio volume dimming can get stuck.
            retryOverlay?.RemoveAndDisposeImmediately();
            exitOverlay?.RemoveAndDisposeImmediately();

            fadeOut();
            base.OnSuspending(e);
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            // Standalone public build note: FreezeDetector is an optional Mosu framework extension.

            screenSuspension?.RemoveAndDisposeImmediately();

            // Eagerly clean these up as disposal of child components is asynchronous and may leave sounds playing beyond user expectations.
            failAnimationContainer?.Stop();
            PauseOverlay?.StopAllSamples();
            FailOverlay?.StopAllSamples();

            if (LoadedBeatmapSuccessfully && !GameplayState.HasPassed)
            {
                Debug.Assert(resultsDisplayDelegate == null);

                if (!GameplayState.HasFailed)
                    GameplayState.HasQuit = true;

                if (DrawableRuleset.ReplayScore == null)
                    ScoreProcessor.FailScore(Score.ScoreInfo);

                if (UsesSpecialGameplayRateMode)
                    prepareAndImportScoreAsync(forceImport: true);
            }

            // GameplayClockContainer performs seeks / start / stop operations on the beatmap's track.
            // as we are no longer the current screen, we cannot guarantee the track is still usable.
            (GameplayClockContainer as MasterGameplayClockContainer)?.StopUsingBeatmapClock();

            musicController.ResetTrackAdjustments();

            fadeOut();

            return base.OnExiting(e);
        }

        /// <summary>
        /// Creates the player's <see cref="Scoring.Score"/>.
        /// </summary>
        /// <param name="beatmap"></param>
        /// <returns>The <see cref="Scoring.Score"/>.</returns>
        protected virtual Score CreateScore(IBeatmap beatmap) => new Score
        {
            ScoreInfo = new ScoreInfo
            {
                User = api.LocalUser.Value.WithDisplayUsername(customUsername.Value),
                ClientVersion = game.Version,
            },
        };

        /// <summary>
        /// Imports the player's <see cref="Scoring.Score"/> to the local database.
        /// </summary>
        /// <param name="score">The <see cref="Scoring.Score"/> to import.</param>
        /// <returns>The imported score.</returns>
        protected virtual Task ImportScore(Score score)
        {
            // Replays are already populated and present in the game's database, so should not be re-imported.
            if (DrawableRuleset.ReplayScore != null)
                return Task.CompletedTask;

            ByteArrayArchiveReader replayReader = null;

            if (score.ScoreInfo.Ruleset.IsLegacyRuleset())
            {
                using (var stream = new MemoryStream())
                {
                    new LegacyScoreEncoder(score, GameplayState.Beatmap).Encode(stream);
                    replayReader = new ByteArrayArchiveReader(stream.ToArray(), "replay.osr");
                }
            }

            // the import process will re-attach managed beatmap/rulesets to this score. we don't want this for now, so create a temporary copy to import.
            var importableScore = score.ScoreInfo.DeepClone();

            var imported = scoreManager.Import(importableScore, replayReader);
            Debug.Assert(imported != null);

            imported.PerformRead(s =>
            {
                // because of the clone above, it's required that we copy back the post-import hash/ID to use for availability matching.
                score.ScoreInfo.Hash = s.Hash;
                score.ScoreInfo.ID = s.ID;
                score.ScoreInfo.Files.AddRange(s.Files.Detach());
            });

            return Task.CompletedTask;
        }

        /// <summary>
        /// Prepare the <see cref="Scoring.Score"/> for display at results.
        /// </summary>
        /// <param name="score">The <see cref="Scoring.Score"/> to prepare.</param>
        /// <returns>A task that prepares the provided score. On completion, the score is assumed to be ready for display.</returns>
        protected virtual Task PrepareScoreForResultsAsync(Score score) => Task.CompletedTask;

        /// <summary>
        /// Creates the <see cref="ResultsScreen"/> for a <see cref="ScoreInfo"/>.
        /// </summary>
        /// <param name="score">The <see cref="ScoreInfo"/> to be displayed in the results screen.</param>
        /// <returns>The <see cref="ResultsScreen"/>.</returns>
        protected abstract ResultsScreen CreateResults(ScoreInfo score);

        private void fadeOut()
        {
            if (!skipExitTransition)
                this.FadeOut(250);

            if (this.IsCurrentScreen())
            {
                ApplyToBackground(b =>
                {
                    b.IgnoreUserSettings.Value = true;

                    // May be null if the load never completed.
                    if (breakTracker != null)
                    {
                        b.IsBreakTime.UnbindFrom(breakTracker.IsBreakTime);
                        b.IsBreakTime.Value = false;
                    }
                });

                storyboardReplacesBackground.Value = false;
            }
        }

        #endregion

        IBindable<bool> ISamplePlaybackDisabler.SamplePlaybackDisabled => samplePlaybackDisabled;

        public IBindable<LocalUserPlayingState> PlayingState => playingState;

        private static readonly DifficultyRange approachRatePreemptRange = new(1800, 1200, 450);

        private static float getClockRateInvariantApproachRate(float targetApproachRate, IReadOnlyCollection<Mod> gameplayMods)
        {
            double clockRate = ModUtils.CalculateRateWithMods(gameplayMods);

            if (clockRate <= 0)
                return targetApproachRate;

            // AR is stored as a difficulty value, but DT/HT affect the real-time preempt window via clock rate.
            // Compensate the stored AR so the effective preempt still matches the configured value.
            double targetPreempt = IBeatmapDifficultyInfo.DifficultyRange(targetApproachRate, approachRatePreemptRange);
            double compensatedPreempt = targetPreempt * clockRate;

            return (float)IBeatmapDifficultyInfo.InverseDifficultyRange(compensatedPreempt, approachRatePreemptRange);
        }

        private sealed class ForcedApproachRateMod : Mod, IApplicableToDifficulty
        {
            private readonly float approachRate;

            public ForcedApproachRateMod(float approachRate)
            {
                this.approachRate = approachRate;
            }

            public override string Name => @"Forced AR";
            public override string Acronym => @"FAR";
            public override LocalisableString Description => @"Applies a fork-configured approach rate.";
            public override ModType Type => ModType.System;
            public override bool UserPlayable => false;

            public void ApplyToDifficulty(BeatmapDifficulty difficulty) => difficulty.ApproachRate = approachRate;
        }

        private enum SpecialGameplayRateMode
        {
            None,
            Freeze,
            Reverse,
        }
    }
}
