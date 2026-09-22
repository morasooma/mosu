// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.PolygonExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Online.Placeholders;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapLeaderboardWedge : VisibilityContainer
    {
        public const float SPACING_BETWEEN_SCORES = 4;

        public Func<bool> UseScorePanelOnlyInput { get; init; } = () => false;

        public IBindable<BeatmapLeaderboardScope> Scope { get; } = new Bindable<BeatmapLeaderboardScope>();

        public IBindable<LeaderboardSortMode> Sorting { get; } = new Bindable<LeaderboardSortMode>();

        public IBindable<bool> FilterBySelectedMods { get; } = new BindableBool();

        [Resolved]
        private LeaderboardManager leaderboardManager { get; set; } = null!;

        [Resolved]
        private Bindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IBindable<IReadOnlyList<Mod>> mods { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private IBeatmapUpdater beatmapUpdater { get; set; } = null!;

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        private Container<Placeholder> placeholderContainer = null!;
        private Placeholder? placeholder;

        private Container<BeatmapLeaderboardScore> scoresContainer = null!;

        private ScoreOnlyScrollContainer scoresScroll = null!;
        private Container personalBestDisplay = null!;

        private Container<BeatmapLeaderboardScore> personalBestScoreContainer = null!;
        private OsuSpriteText personalBestText = null!;
        private IBindable<Colour4> themeColour = null!;
        private LoadingLayer loading = null!;

        private CancellationTokenSource? cancellationTokenSource;

        private readonly IBindable<LeaderboardScores?> fetchedScores = new Bindable<LeaderboardScores?>();
        private ModSettingChangeTracker? modSettingChangeTracker;
        private bool allowBeatmapUploadAfterReload;
        private bool uploadBlockedByLiveBeatmapSet;
        private bool preserveBeatmapRefreshState;
        private bool reloadingBeatmapMetadata;
        private bool uploadingBeatmapSet;

        private const float personal_best_height = 112;

        // Blocking mouse down is required to avoid song select's background reveal logic happening while hovering scores.
        // Our horizontal alignment doesn't really align with the rest of the sheared components (protrudes a touch to the right) which makes
        // it complicated to handle this at a higher level.
        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
            => UseScorePanelOnlyInput() ? receivesInputAtScore(screenSpacePos) : scoresScroll.ReceivePositionalInputAt(screenSpacePos);

        protected override bool OnMouseDown(MouseDownEvent e) => true;

        private Sample? swishSample;

        private readonly List<ScheduledDelegate> scoreSfxDelegates = new List<ScheduledDelegate>();

        [BackgroundDependencyLoader]
        private void load(AudioManager audio)
        {
            RelativeSizeAxes = Axes.Both;

            Child = new OsuContextMenuContainer
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    scoresScroll = new ScoreOnlyScrollContainer
                    {
                        ReceiveInputAt = screenSpacePos => !UseScorePanelOnlyInput() || receivesInputAtScore(screenSpacePos),
                        RelativeSizeAxes = Axes.Both,
                        ScrollbarVisible = false,
                        Shear = OsuGame.SHEAR,
                        Child = scoresContainer = new Container<BeatmapLeaderboardScore>
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Padding = new MarginPadding
                            {
                                Top = 5,
                                // Left padding offsets the shear to create a visually appealing list display.
                                Left = OsuGame.DisableShear.Value ? 40f : 80f,
                                // Bottom padding ensures the last entry's full width is displayed
                                // (ie it is fully on screen after shear is considered).
                                Bottom = BeatmapLeaderboardScore.HEIGHT * 3
                            },
                        },
                    },
                    personalBestDisplay = new ScoreOnlyInputContainer
                    {
                        ReceiveInputAt = screenSpacePos => !UseScorePanelOnlyInput() || receivesInputAtPersonalBest(screenSpacePos),
                        Anchor = Anchor.BottomLeft,
                        Origin = Anchor.BottomLeft,
                        RelativeSizeAxes = Axes.X,
                        Height = personal_best_height,
                        Shear = OsuGame.SHEAR,
                        Margin = new MarginPadding
                        {
                            Left = OsuGame.DisableShear.Value ? -20f : -40f,
                        },
                        CornerRadius = 10f,
                        Masking = true,
                        // push the personal best 1px down to hide masking issues
                        Y = 1f,
                        X = -100f,
                        Alpha = 0f,
                        Children = new Drawable[]
                        {
                            new WedgeBackground(),
                            // Required because wedge background blocks input from passing through
                            // to the main context menu container above.
                            new OsuContextMenuContainer
                            {
                                Shear = -OsuGame.SHEAR,
                                RelativeSizeAxes = Axes.Both,
                                Child = new Container
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Padding = new MarginPadding { Top = 5f, Bottom = 5f, Left = OsuGame.DisableShear.Value ? 30f : 70f, Right = 10f },
                                    Children = new Drawable[]
                                    {
                                        personalBestText = new OsuSpriteText
                                        {
                                            Colour = colourProvider.Content2,
                                            Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold),
                                        },
                                        personalBestScoreContainer = new Container<BeatmapLeaderboardScore>
                                        {
                                            RelativeSizeAxes = Axes.X,
                                            AutoSizeAxes = Axes.Y,
                                            Margin = new MarginPadding { Top = 20f },
                                        },
                                    }
                                },
                            }
                        },
                    },
                    placeholderContainer = new Container<Placeholder>
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Both,
                    },
                    loading = new LoadingLayer(),
                }
            };

            swishSample = audio.Samples.Get(@"SongSelect/leaderboard-score");
        }

        private bool receivesInputAtScore(Vector2 screenSpacePos)
            => scoresScroll.Contains(screenSpacePos)
               && scoresContainer.Any(score => score.IsPresent && score.ReceivePositionalInputAt(screenSpacePos))
               || receivesInputAtPersonalBest(screenSpacePos);

        private bool receivesInputAtPersonalBest(Vector2 screenSpacePos)
            => personalBestDisplay.Contains(screenSpacePos)
               && personalBestScoreContainer.Any(score => score.IsPresent && score.ReceivePositionalInputAt(screenSpacePos));

        private partial class ScoreOnlyScrollContainer : OsuScrollContainer
        {
            public required Func<Vector2, bool> ReceiveInputAt { private get; init; }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
                => base.ReceivePositionalInputAt(screenSpacePos) && ReceiveInputAt(screenSpacePos);
        }

        private partial class ScoreOnlyInputContainer : Container
        {
            public required Func<Vector2, bool> ReceiveInputAt { private get; init; }

            public override bool ReceivePositionalInputAt(Vector2 screenSpacePos)
                => base.ReceivePositionalInputAt(screenSpacePos) && ReceiveInputAt(screenSpacePos);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content2);
            themeColour.BindValueChanged(_ =>
            {
                personalBestText.Colour = colourProvider.Content2;
                if (placeholder != null)
                    placeholder.Colour = colourProvider.Content2;
            }, true);

            Scope.BindValueChanged(_ => RefetchScores());
            Sorting.BindValueChanged(_ => RefetchScores());
            FilterBySelectedMods.BindValueChanged(_ => RefetchScores());
            beatmap.BindValueChanged(_ =>
            {
                if (preserveBeatmapRefreshState)
                    return;

                allowBeatmapUploadAfterReload = false;
                uploadBlockedByLiveBeatmapSet = false;
                RefetchScores();
            });
            ruleset.BindValueChanged(_ => RefetchScores());
            mods.BindValueChanged(change =>
            {
                modSettingChangeTracker?.Dispose();
                modSettingChangeTracker = new ModSettingChangeTracker(mods.Value);
                modSettingChangeTracker.SettingChanged += _ => Scheduler.AddOnce(refetchScoresFromMods);

                // RX/AP select a different online leaderboard even when the explicit
                // "filter by selected mods" option is disabled. Other mod changes only
                // affect the request when that option is enabled.
                if (FilterBySelectedMods.Value || specialLeaderboardMode(change.OldValue) != specialLeaderboardMode(change.NewValue))
                    RefetchScores();
            });

            RefetchScores();
        }

        protected override void PopIn()
        {
            this.FadeIn(300, Easing.OutQuint);
        }

        protected override void PopOut()
        {
            this.FadeOut(300, Easing.OutQuint);
        }

        private void refetchScoresFromMods()
        {
            if (FilterBySelectedMods.Value)
                RefetchScores();
        }

        private static int specialLeaderboardMode(IReadOnlyList<Mod> selectedMods)
        {
            if (selectedMods.Any(m => m.Acronym is "RX" or "MRX"))
                return RulesetInfo.OSU_RELAX_ONLINE_ID;

            if (selectedMods.Any(m => m.Acronym == "AP"))
                return RulesetInfo.OSU_AUTOPILOT_ONLINE_ID;

            return -1;
        }

        private bool initialFetchComplete;

        private ScheduledDelegate? refetchOperation;

        public void RefetchScores()
        {
            SetScores(Array.Empty<ScoreInfo>());

            if (beatmap.IsDefault)
            {
                SetState(LeaderboardState.NoneSelected);
                return;
            }

            SetState(LeaderboardState.Retrieving);

            var fetchScope = Scope.Value;

            refetchOperation?.Cancel();
            refetchOperation = Scheduler.AddDelayed(() =>
            {
                var fetchBeatmapInfo = beatmap.Value.BeatmapInfo;
                var fetchRuleset = ruleset.Value ?? fetchBeatmapInfo.Ruleset;

                bool isRelax = mods.Value.Any(m => m.Acronym == "RX" || m.Acronym == "MRX");
                bool isAutopilot = mods.Value.Any(m => m.Acronym == "AP");

                if (isRelax)
                {
                    if (fetchRuleset.ShortName == RulesetInfo.OSU_MODE_SHORTNAME)
                        fetchRuleset = fetchRuleset.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID);
                    else if (!Online.MosuServerEnvironment.OnlyOsuRelax)
                    {
                        if (fetchRuleset.ShortName == RulesetInfo.TAIKO_MODE_SHORTNAME)
                            fetchRuleset = fetchRuleset.CreateSpecialRuleset(RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME, RulesetInfo.TAIKO_RELAX_ONLINE_ID);
                        else if (fetchRuleset.ShortName == RulesetInfo.CATCH_MODE_SHORTNAME)
                            fetchRuleset = fetchRuleset.CreateSpecialRuleset(RulesetInfo.CATCH_RELAX_MODE_SHORTNAME, RulesetInfo.CATCH_RELAX_ONLINE_ID);
                    }
                }
                else if (isAutopilot && !Online.MosuServerEnvironment.OnlyOsuRelax)
                {
                    if (fetchRuleset.ShortName == RulesetInfo.OSU_MODE_SHORTNAME)
                        fetchRuleset = fetchRuleset.CreateSpecialRuleset(RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME, RulesetInfo.OSU_AUTOPILOT_ONLINE_ID);
                }

                var fetchSorting = Sorting.Value;

                // For now, we forcefully refresh to keep things simple.
                // In the future, removing this requirement may be deemed useful, but will need ample testing of edge case scenarios
                // (like returning from gameplay after setting a new score, returning to song select after main menu).
                leaderboardManager.FetchWithCriteria(new LeaderboardCriteria(fetchBeatmapInfo, fetchRuleset, fetchScope, FilterBySelectedMods.Value ? mods.Value.ToArray() : null, fetchSorting),
                    forceRefresh: true);

                if (!initialFetchComplete)
                {
                    // only bind this after the first fetch to avoid reading stale scores.
                    fetchedScores.BindTo(leaderboardManager.Scores);

                    // Schedule is important here to avoid handling changes after this drawable is disposed.
                    fetchedScores.BindValueChanged(_ => Schedule(updateScores), true);
                    initialFetchComplete = true;
                }
            }, initialFetchComplete && fetchScope != BeatmapLeaderboardScope.Local ? 300 : 0);
        }

        private void updateScores()
        {
            var scores = fetchedScores.Value;

            if (scores == null) return;

            // because leaderboard refetches are debounced, it is technically possible for the global leaderboard manager
            // to contain scores for a different beatmap than the ones the wedge is currently on.
            // in this case, ignore the incoming scores to avoid briefly flashing the wrong leaderboard.
            if (leaderboardManager.CurrentCriteria?.Beatmap?.Equals(beatmap.Value.BeatmapInfo) != true)
                return;

            if (scores.FailState != null)
                SetState((LeaderboardState)scores.FailState);
            else
                SetScores(scores.TopScores, scores.UserScore, scores.TotalScores);
        }

        protected void SetScores(IEnumerable<ScoreInfo> scores, ScoreInfo? userScore = null, int? totalCount = null)
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();

            clearScores();
            SetState(LeaderboardState.Success);

            if (!scores.Any())
            {
                SetState(LeaderboardState.NoScores);
                return;
            }

            LoadComponentsAsync(scores.Select((s, i) =>
            {
                BeatmapLeaderboardScore.HighlightType? highlightType = null;

                if (isSameOnlineScore(s, userScore))
                    highlightType = BeatmapLeaderboardScore.HighlightType.Own;
                else if (api.LocalUserState.Friends.Any(r => r.TargetID == s.UserID) && Scope.Value != BeatmapLeaderboardScope.Friend)
                    highlightType = BeatmapLeaderboardScore.HighlightType.Friend;

                return new BeatmapLeaderboardScore(s)
                {
                    Rank = i + 1,
                    Highlight = highlightType,
                    SelectedMods = { BindTarget = mods },
                    Action = songSelect?.CanPresentScore == true
                        ? () => songSelect.PresentScore(s)
                        : null,
                    ShowReplay = songSelect?.CanPresentScore == true
                        ? info => songSelect.PresentScore(info, ScorePresentType.Gameplay)
                        : null
                };
            }), loadedScores =>
            {
                int delay = 200;
                int i = 0;

                foreach (var d in loadedScores)
                {
                    d.Y = (BeatmapLeaderboardScore.HEIGHT + SPACING_BETWEEN_SCORES) * i;

                    // This is a bit of a weird one. We're already in a sheared state and don't want top-level
                    // shear applied, but still need the `BeatmapLeaderboardScore` to be in "sheared" mode (see ctor).
                    d.Shear = Vector2.Zero;

                    scoresContainer.Add(d);

                    d.FadeOut()
                     .MoveToX(-20f)
                     .Delay(delay)
                     .FadeIn(300, Easing.OutQuint)
                     .MoveToX(0f, 300, Easing.OutQuint);

                    bool visible = d.ScreenSpaceDrawQuad.TopLeft.Y < d.Parent!.ChildMaskingBounds.BottomLeft.Y;

                    if (visible)
                    {
                        var del = Scheduler.AddDelayed(() =>
                        {
                            var chan = swishSample?.GetChannel();
                            if (chan == null) return;

                            chan.Balance.Value = -OsuGameBase.SFX_STEREO_STRENGTH / 2;
                            chan.Frequency.Value = 0.98f + RNG.NextDouble(0.04f);
                            chan.Play();
                        }, delay);

                        scoreSfxDelegates.Add(del);
                    }

                    delay += 30;
                    i++;
                }
            }, cancellation: cancellationTokenSource.Token);

            if (userScore != null)
            {
                personalBestDisplay.MoveToX(0, 600, Easing.OutQuint);
                personalBestDisplay.FadeIn(600, Easing.OutQuint);
                personalBestScoreContainer.Child = new BeatmapLeaderboardScore(userScore)
                {
                    Highlight = BeatmapLeaderboardScore.HighlightType.Own,
                    Rank = userScore.Position,
                    SelectedMods = { BindTarget = mods },
                    Action = () => onLeaderboardScoreClicked(userScore),
                };

                scoresScroll.TransformTo(nameof(scoresScroll.Padding), new MarginPadding { Bottom = personal_best_height }, 300, Easing.OutQuint);

                if (totalCount != null && userScore.Position != null)
                    personalBestText.Text = BeatmapLeaderboardWedgeStrings.PersonalBestWithPosition(userScore.Position.Value, totalCount.Value);
                else
                    personalBestText.Text = BeatmapLeaderboardWedgeStrings.PersonalBest;
            }
        }

        private static bool isSameOnlineScore(ScoreInfo score, ScoreInfo? other)
        {
            if (other == null)
                return false;

            // Stable score IDs live in a separate namespace and deliberately have OnlineID = -1
            // to prevent lazer API/replay requests from being made against stable servers. Never
            // treat non-positive sentinel IDs as identities, otherwise every stable score is
            // highlighted as the local user's score.
            return score.OnlineID > 0 && score.OnlineID == other.OnlineID
                   || score.LegacyOnlineID > 0 && score.LegacyOnlineID == other.LegacyOnlineID;
        }

        private void clearScores()
        {
            float delay = 0;

            foreach (var d in scoresContainer)
            {
                // Avoid applying animations a second time to drawables which are already fading out.
                if (d.LifetimeEnd != double.MaxValue)
                    continue;

                d.Delay(delay)
                 .MoveToX(-10f, 120, Easing.Out)
                 .FadeOut(120, Easing.Out)
                 .Expire();

                // If the user is scrolled down in the list, start delaying only from the current visible range to
                // avoid the perceived transition from taking longer than expected.
                if (d.ScreenSpaceDrawQuad.Intersects(scoresScroll.ScreenSpaceDrawQuad))
                    delay += 20;
            }

            personalBestDisplay.MoveToX(-100, 300, Easing.OutQuint);
            personalBestDisplay.FadeOut(300, Easing.OutQuint);
            scoresScroll.TransformTo(nameof(scoresScroll.Padding), new MarginPadding(), 300, Easing.OutQuint);

            scoreSfxDelegates.ForEach(d => d.Cancel());
            scoreSfxDelegates.Clear();
        }

        private void onLeaderboardScoreClicked(ScoreInfo score) => songSelect?.PresentScore(score);

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            cancellationTokenSource?.Cancel();
            modSettingChangeTracker?.Dispose();
        }

        private void reloadBeatmapMetadata()
        {
            if (reloadingBeatmapMetadata || beatmap.IsDefault)
                return;

            Guid beatmapId = beatmap.Value.BeatmapInfo.ID;
            Guid beatmapSetId = beatmap.Value.BeatmapSetInfo.ID;
            bool selectedBeatmapWasCustomDifficulty = beatmap.Value.BeatmapInfo.OnlineID <= 0;

            Logger.Log($@"Reloading beatmap metadata: beatmapId={beatmapId} beatmapOnlineId={beatmap.Value.BeatmapInfo.OnlineID} setId={beatmapSetId} setOnlineId={beatmap.Value.BeatmapSetInfo.OnlineID} selectedBeatmapWasCustomDifficulty={selectedBeatmapWasCustomDifficulty}", LoggingTarget.Network);

            reloadingBeatmapMetadata = true;
            SetState(LeaderboardState.Retrieving);

            Task.Run(() =>
            {
                bool reloadSucceeded = false;
                bool blockUploadForLiveBeatmapSet = false;

                try
                {
                    (reloadSucceeded, blockUploadForLiveBeatmapSet) = realm.Run(r =>
                    {
                        var managedSet = r.Find<BeatmapSetInfo>(beatmapSetId);
                        if (managedSet == null)
                            return (false, false);

                        beatmapUpdater.Process(managedSet, MetadataLookupScope.OnlineFirst);

                        var updatedBeatmap = r.FindWithRefresh<BeatmapInfo>(beatmapId);
                        bool uploadShouldBeBlocked =
                            selectedBeatmapWasCustomDifficulty
                            && updatedBeatmap?.OnlineID <= 0
                            && managedSet.Beatmaps.Any(b => b.OnlineID > 0);

                        Logger.Log($@"Reload complete in realm: updatedBeatmapOnlineId={updatedBeatmap?.OnlineID ?? -1} managedSetOnlineId={managedSet.OnlineID} anyOnlineBeatmaps={managedSet.Beatmaps.Any(b => b.OnlineID > 0)}", LoggingTarget.Network);

                        return (updatedBeatmap?.OnlineID > 0, uploadShouldBeBlocked);
                    });
                }
                catch (Exception e)
                {
                    Logger.Error(e, $@"Failed to reload beatmap metadata for {beatmapId}");
                }

                Schedule(() =>
                {
                    reloadingBeatmapMetadata = false;

                    if (beatmap.IsDefault || beatmap.Value.BeatmapInfo.ID != beatmapId)
                        return;

                    var refreshedBeatmap = beatmaps.QueryBeatmap(b => b.ID == beatmapId);

                    if (refreshedBeatmap != null)
                    {
                        Logger.Log($@"Reload refreshed beatmap: onlineId={refreshedBeatmap.OnlineID} setOnlineId={refreshedBeatmap.BeatmapSet?.OnlineID ?? -1}", LoggingTarget.Network);
                        reloadSucceeded |= refreshedBeatmap.OnlineID > 0;

                        uploadBlockedByLiveBeatmapSet = blockUploadForLiveBeatmapSet;
                        allowBeatmapUploadAfterReload = !reloadSucceeded && !uploadBlockedByLiveBeatmapSet && api.IsLoggedIn && api.State.Value == APIState.Online;

                        refreshSelectedBeatmap(refreshedBeatmap);
                    }
                    else
                    {
                        uploadBlockedByLiveBeatmapSet = blockUploadForLiveBeatmapSet;
                        allowBeatmapUploadAfterReload = !reloadSucceeded && !uploadBlockedByLiveBeatmapSet && api.IsLoggedIn && api.State.Value == APIState.Online;
                        RefetchScores();
                    }

                    if (uploadBlockedByLiveBeatmapSet)
                    {
                        notifications?.Post(new SimpleNotification
                        {
                            Text = LeaderboardStrings.CannotUploadCustomDifficultyToServer,
                            Icon = FontAwesome.Solid.Ban,
                        });
                    }
                    else if (!reloadSucceeded)
                    {
                        notifications?.Post(new SimpleNotification
                        {
                            Text = LeaderboardStrings.CouldntReloadBeatmapData,
                            Icon = FontAwesome.Solid.Times,
                        });
                    }
                });
            });
        }

        private async void uploadBeatmapToServer()
        {
            if (uploadingBeatmapSet || beatmap.IsDefault || !api.IsLoggedIn)
                return;

            if (!AllowsGenericServerUpload(beatmap.Value.BeatmapInfo, ruleset.Value))
            {
                allowBeatmapUploadAfterReload = false;
                SetState(LeaderboardState.BeatmapUnavailable);
                return;
            }

            if (uploadBlockedByLiveBeatmapSet)
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = LeaderboardStrings.CannotUploadCustomDifficultyToServer,
                    Icon = FontAwesome.Solid.Ban,
                });
                return;
            }

            var selectedBeatmap = beatmap.Value;
            Guid beatmapSetId = selectedBeatmap.BeatmapSetInfo.ID;

            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = LeaderboardStrings.PreparingBeatmapUpload,
                CompletionText = LeaderboardStrings.BeatmapUploadedToServer,
                CancelRequested = () => true,
            };

            notifications?.Post(notification);
            uploadingBeatmapSet = true;

            try
            {
                using var beatmapPackageStream = new MemoryStream();

                var legacyBeatmapExporter = new LegacyBeatmapExporter(storage);
                await legacyBeatmapExporter
                      .ExportToStreamAsync(selectedBeatmap.BeatmapSetInfo.ToLive(realm), beatmapPackageStream, notification, notification.CancellationToken)
                      .ConfigureAwait(true);

                if (notification.State == ProgressNotificationState.Cancelled)
                    return;

                notification.Text = LeaderboardStrings.UploadingBeatmapToServer;
                notification.Progress = 0;

                string filename = $"{selectedBeatmap.Metadata.Artist} - {selectedBeatmap.Metadata.Title} ({api.LocalUser.Value.Username}).osz".GetValidFilename();

                var uploadRequest = new UploadServerBeatmapSetRequest(beatmapPackageStream.ToArray(), filename);
                var uploadCompletion = new TaskCompletionSource<APIBeatmapSet>(TaskCreationOptions.RunContinuationsAsynchronously);

                uploadRequest.Progressed += (current, total) =>
                {
                    notification.State = ProgressNotificationState.Active;
                    if (total > 0)
                        notification.Progress = (float)current / total;
                };
                uploadRequest.Success += response => uploadCompletion.TrySetResult(response);
                uploadRequest.Failure += ex => uploadCompletion.TrySetException(ex);

                notification.CancelRequested = () =>
                {
                    uploadRequest.Cancel();
                    return true;
                };

                await api.PerformAsync(uploadRequest).ConfigureAwait(true);
                var uploadedBeatmapSet = await uploadCompletion.Task.ConfigureAwait(true);

                linkUploadedBeatmapSet(beatmapSetId, uploadedBeatmapSet);

                allowBeatmapUploadAfterReload = false;
                notification.Progress = 1;
                notification.State = ProgressNotificationState.Completed;
            }
            catch (OperationCanceledException)
            {
                notification.CompleteSilently();
            }
            catch (Exception e)
            {
                Logger.Error(e, $@"Failed to upload beatmap set {beatmapSetId} to the server.");
                notification.CompleteSilently();
                notifications?.Post(new SimpleNotification
                {
                    Text = string.IsNullOrWhiteSpace(e.Message) ? LeaderboardStrings.CouldntUploadBeatmapToServer : e.Message,
                    Icon = FontAwesome.Solid.Times,
                });
            }
            finally
            {
                uploadingBeatmapSet = false;
            }
        }

        private void linkUploadedBeatmapSet(Guid localBeatmapSetId, APIBeatmapSet uploadedBeatmapSet)
        {
            realm.Write(r =>
            {
                var localBeatmapSet = r.Find<BeatmapSetInfo>(localBeatmapSetId);
                if (localBeatmapSet == null)
                    return;

                localBeatmapSet.OnlineID = uploadedBeatmapSet.OnlineID;
                localBeatmapSet.Status = uploadedBeatmapSet.Status;
                localBeatmapSet.DateSubmitted = uploadedBeatmapSet.Submitted;
                localBeatmapSet.DateRanked = uploadedBeatmapSet.Ranked;

                var remainingOnlineBeatmaps = uploadedBeatmapSet.Beatmaps.ToList();

                foreach (var localBeatmap in localBeatmapSet.Beatmaps
                                                         .OrderBy(b => b.Ruleset.OnlineID)
                                                         .ThenBy(b => b.DifficultyName, StringComparer.OrdinalIgnoreCase))
                {
                    var matchingOnlineBeatmap = matchUploadedBeatmap(localBeatmap, remainingOnlineBeatmaps);
                    if (matchingOnlineBeatmap == null)
                        continue;

                    remainingOnlineBeatmaps.Remove(matchingOnlineBeatmap);

                    localBeatmap.OnlineID = matchingOnlineBeatmap.OnlineID;
                    localBeatmap.OnlineMD5Hash = matchingOnlineBeatmap.MD5Hash;
                    localBeatmap.LastOnlineUpdate = matchingOnlineBeatmap.LastUpdated;
                    localBeatmap.Status = matchingOnlineBeatmap.Status;

                    localBeatmap.Metadata.Tags = ensureServerExclusiveTag(uploadedBeatmapSet.Tags);
                    localBeatmap.Metadata.Author.OnlineID = uploadedBeatmapSet.AuthorID;
                    localBeatmap.Metadata.Author.Username = uploadedBeatmapSet.Author.Username;
                }
            });

            if (!beatmap.IsDefault && beatmap.Value.BeatmapSetInfo.ID == localBeatmapSetId)
            {
                var refreshedBeatmap = beatmaps.QueryBeatmap(b => b.ID == beatmap.Value.BeatmapInfo.ID);
                if (refreshedBeatmap != null)
                    refreshSelectedBeatmap(refreshedBeatmap);

                else
                    RefetchScores();
            }
        }

        private void refreshSelectedBeatmap(BeatmapInfo refreshedBeatmap)
        {
            var refreshedWorkingBeatmap = beatmaps.GetWorkingBeatmap(refreshedBeatmap, true);

            preserveBeatmapRefreshState = true;
            try
            {
                if (!ReferenceEquals(beatmap.Value, refreshedWorkingBeatmap))
                    beatmap.Value = refreshedWorkingBeatmap;
                else
                    beatmap.TriggerChange();
            }
            finally
            {
                preserveBeatmapRefreshState = false;
            }

            RefetchScores();
        }

        private static APIBeatmap? matchUploadedBeatmap(BeatmapInfo localBeatmap, List<APIBeatmap> remainingOnlineBeatmaps)
        {
            List<APIBeatmap> candidates = remainingOnlineBeatmaps.Where(b => b.RulesetID == localBeatmap.Ruleset.OnlineID).ToList();

            if (candidates.Count == 0)
                return null;

            candidates = narrowCandidates(candidates, b => string.Equals(b.DifficultyName, localBeatmap.DifficultyName, StringComparison.OrdinalIgnoreCase));
            candidates = narrowCandidates(candidates, b => Math.Abs(b.Length - localBeatmap.Length) < 1);
            candidates = narrowCandidates(candidates, b => Math.Abs(b.BPM - localBeatmap.BPM) < 0.01);
            candidates = narrowCandidates(candidates, b => Math.Abs(b.DrainRate - localBeatmap.Difficulty.DrainRate) < 0.01f);
            candidates = narrowCandidates(candidates, b => Math.Abs(b.CircleSize - localBeatmap.Difficulty.CircleSize) < 0.01f);
            candidates = narrowCandidates(candidates, b => Math.Abs(b.ApproachRate - localBeatmap.Difficulty.ApproachRate) < 0.01f);
            candidates = narrowCandidates(candidates, b => Math.Abs(b.OverallDifficulty - localBeatmap.Difficulty.OverallDifficulty) < 0.01f);

            return candidates.OrderBy(b => b.OnlineID).FirstOrDefault();
        }

        private static List<T> narrowCandidates<T>(List<T> candidates, Func<T, bool> predicate)
        {
            var narrowed = candidates.Where(predicate).ToList();
            return narrowed.Count > 0 ? narrowed : candidates;
        }

        private static string ensureServerExclusiveTag(string tags)
        {
            var parts = tags.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();

            if (!parts.Any(tag => string.Equals(tag, BeatmapSetInfoExtensions.ServerExclusiveTag, StringComparison.OrdinalIgnoreCase)))
                parts.Add(BeatmapSetInfoExtensions.ServerExclusiveTag);

            return string.Join(' ', parts);
        }

        private LeaderboardState displayedState;

        private ScheduledDelegate? loadingShowDelegate;

        protected void SetState(LeaderboardState state)
        {
            if (state == displayedState)
                return;

            loading.Hide();

            if (state == LeaderboardState.Retrieving)
            {
                // Slight delay so this doesn't display for a few silly frames for local score retrievals.
                loadingShowDelegate ??= Scheduler.AddDelayed(() => loading.Show(), 250);
            }
            else
            {
                loadingShowDelegate?.Cancel();
                loadingShowDelegate = null;
            }

            displayedState = state;

            placeholder?.FadeOut(150, Easing.OutQuint).Expire();
            placeholder = getPlaceholderFor(state);

if (placeholder == null)
            {
                placeholderContainer.Clear();
                return;
            }

            clearScores();

            placeholderContainer.Child = placeholder;
            placeholder.Colour = colourProvider.Content2;

            placeholder.ScaleTo(0.8f).Then().ScaleTo(1, 900, Easing.OutQuint);
            placeholder.FadeInFromZero(300, Easing.OutQuint);
        }

        #region Fade handling

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            const int height = BeatmapLeaderboardScore.HEIGHT;

            float fadeBottom = (float)(scoresScroll.Current + scoresScroll.DrawHeight);
            float fadeTop = (float)(scoresScroll.Current);

            fadeTop += (float)Math.Min(height, Math.Log10(Math.Max(fadeTop, 0) + 1) * height);

            foreach (var c in scoresContainer)
            {
                float topY = c.ToSpaceOfOtherDrawable(Vector2.Zero, scoresContainer).Y;
                float bottomY = topY + height;

                bool requireBottomFade = bottomY >= fadeBottom;
                bool requireTopFade = topY < fadeTop;

                if (!requireBottomFade && !requireTopFade)
                {
                    c.Colour = Color4.White;
                    continue;
                }

                if (topY > fadeBottom + height || bottomY < fadeTop - height)
                {
                    c.Colour = Color4.Transparent;
                    continue;
                }

                if (requireBottomFade)
                {
                    c.Colour = ColourInfo.GradientVertical(
                        Color4.White.Opacity(Math.Min(1 - (topY - fadeBottom) / height, 1)),
                        Color4.White.Opacity(Math.Min(1 - (bottomY - fadeBottom) / height, 1)));
                }
                else
                {
                    Debug.Assert(requireTopFade);

                    c.Colour = ColourInfo.GradientVertical(
                        Color4.White.Opacity(Math.Min(1 - (fadeTop - topY) / height, 1)),
                        Color4.White.Opacity(Math.Min(1 - (fadeTop - bottomY) / height, 1)));
                }
            }
        }

        #endregion

        private Placeholder? getPlaceholderFor(LeaderboardState state)
        {
            switch (state)
            {
                case LeaderboardState.NetworkFailure:
                    return new ClickablePlaceholder(LeaderboardStrings.CouldntFetchScores, FontAwesome.Solid.Sync)
                    {
                        Action = RefetchScores
                    };

                case LeaderboardState.NoneSelected:
                    return new MessagePlaceholder(LeaderboardStrings.PleaseSelectABeatmap);

                case LeaderboardState.RulesetUnavailable:
                    return new MessagePlaceholder(LeaderboardStrings.LeaderboardsAreNotAvailableForThisRuleset);

                case LeaderboardState.BeatmapUnavailable:
                    return new BeatmapUnavailablePlaceholder(
                        uploadBlockedByLiveBeatmapSet ? LeaderboardStrings.CannotUploadCustomDifficultyToServer : LeaderboardStrings.LeaderboardsAreNotAvailableForThisBeatmap,
                        allowBeatmapUploadAfterReload
                        && api.IsLoggedIn
                        && !beatmap.IsDefault
                        && AllowsGenericServerUpload(beatmap.Value.BeatmapInfo, ruleset.Value))
                    {
                        ReloadAction = reloadBeatmapMetadata,
                        UploadAction = uploadBeatmapToServer,
                    };

                case LeaderboardState.NoScores:
                    return new MessagePlaceholder(LeaderboardStrings.NoRecordsYet);

                case LeaderboardState.NotLoggedIn:
                    return new LoginPlaceholder(LeaderboardStrings.PleaseSignInToViewOnlineLeaderboards);

                case LeaderboardState.NotSupporter:
                    return new MessagePlaceholder(LeaderboardStrings.PleaseInvestInAnOsuSupporterTagToViewThisLeaderboard);

                case LeaderboardState.NoTeam:
                    return new MessagePlaceholder(LeaderboardStrings.NoTeam);

                case LeaderboardState.Retrieving:
                    return null;

                case LeaderboardState.Success:
                    return null;

                default:
                    throw new ArgumentOutOfRangeException(nameof(state));
            }
        }

        internal static bool AllowsGenericServerUpload(IBeatmapInfo beatmapInfo, RulesetInfo? selectedRuleset) =>
            beatmapInfo.Ruleset.ShortName != RulesetInfo.DODGE_MODE_SHORTNAME
            && selectedRuleset?.ShortName != RulesetInfo.DODGE_MODE_SHORTNAME;

        private partial class BeatmapUnavailablePlaceholder : Placeholder
        {
            private readonly LocalisableString message;
            private readonly bool showUploadAction;

            public Action? ReloadAction;
            public Action? UploadAction;

            public BeatmapUnavailablePlaceholder(LocalisableString message, bool showUploadAction)
            {
                this.message = message;
                this.showUploadAction = showUploadAction;

                AddIcon(FontAwesome.Solid.ExclamationCircle, cp =>
                {
                    cp.Font = cp.Font.With(size: TEXT_SIZE);
                    cp.Padding = new MarginPadding { Right = 10 };
                });

                AddText(message);
                NewLine();

                AddArbitraryDrawable(new FillFlowContainer
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(10),
                    Padding = new MarginPadding { Top = 15 },
                    Children = createButtons(),
                });
            }

            private Drawable[] createButtons()
            {
                var buttons = new List<Drawable>
                {
                    createButton(LeaderboardStrings.ReloadBeatmapData, FontAwesome.Solid.SyncAlt, () => ReloadAction?.Invoke()),
                };

                if (showUploadAction)
                    buttons.Add(createButton(LeaderboardStrings.UploadBeatmapToServer, FontAwesome.Solid.Upload, () => UploadAction?.Invoke()));

                return buttons.ToArray();
            }

            private static OsuAnimatedButton createButton(LocalisableString text, IconUsage icon, Action action)
            {
                var button = new OsuAnimatedButton
                {
                    AutoSizeAxes = Axes.Both,
                    Action = action,
                };

                OsuTextFlowContainer textFlow;

                button.Add(textFlow = new OsuTextFlowContainer(cp => cp.Font = cp.Font.With(size: TEXT_SIZE))
                {
                    AutoSizeAxes = Axes.Both,
                    Margin = new MarginPadding(5),
                });

                textFlow.AddIcon(icon, i =>
                {
                    i.Padding = new MarginPadding { Right = 10 };
                });
                textFlow.AddText(text);
                return button;
            }

            public override bool Equals(Placeholder? other)
                => other is BeatmapUnavailablePlaceholder beatmapUnavailablePlaceholder
                   && beatmapUnavailablePlaceholder.message == message
                   && beatmapUnavailablePlaceholder.showUploadAction == showUploadAction;
        }
    }
}
