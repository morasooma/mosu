// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Audio;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Input.Bindings;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Matchmaking;
using osu.Game.Online.Matchmaking.Requests;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Volume;
using osu.Game.Rulesets;
using osu.Game.Screens.Footer;
using osu.Game.Screens.OnlinePlay.Matchmaking.Match;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.Queue
{
    /// <summary>
    /// The initial screen that users arrive at when preparing for a quick play session.
    /// </summary>
    public partial class ScreenQueue : OsuScreen
    {
        public override bool ShowFooter => true;

        public override bool? ApplyModTrackAdjustments => false;

        private Container mainContent = null!;
        private CloudVisualisation cloud = null!;
        private RatingDistributionGraph ratingGraph = null!;
        private FillFlowContainer resultPanelContainer = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private QueueController queue { get; set; } = null!;

        [Resolved]
        private INotificationOverlay notifications { get; set; } = null!;

        [Resolved]
        private UserLookupCache userLookupCache { get; set; } = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private MusicController music { get; set; } = null!;

        [Resolved]
        private DashboardOverlay? dashboardOverlay { get; set; }

        private readonly IBindable<MatchmakingScreenState> currentState = new Bindable<MatchmakingScreenState>();

        private readonly Bindable<MatchmakingPool[]?> availablePools = new Bindable<MatchmakingPool[]?>();
        private readonly Bindable<MatchmakingPool?> selectedPool = new Bindable<MatchmakingPool?>();
        private readonly BindableBool randomMods = new BindableBool();
        private readonly MatchmakingPoolType poolType;
        private IAPIProvider api = null!;
        private RelaxRankedPoolResponse? relaxPool;
        private ScreenFooterButton? manageRelaxPoolButton;
        private OsuSpriteText queueCountsText = null!;
        private OsuSpriteText relaxPoolOwnerText = null!;
        private bool lobbyStatusSubscribed;

        private CancellationTokenSource userLookupCancellation = new CancellationTokenSource();
        private CancellationTokenSource poolFetchCancellation = new CancellationTokenSource();

        private Sample? enqueueSample;
        private Sample? matchFoundSample;

        private SampleChannel? waitingLoopChannel;
        private ScheduledDelegate? startLoopPlaybackDelegate;
        private DrawableSample waitingLoop = null!;
        private ScheduledDelegate? pushScreenDelegate;
        private ScheduledDelegate? acceptedWaitingTimeoutDelegate;

        private int? userRating;
        private int recentMatchesLoadVersion;
        private string? lastRecentMatchesRenderKey;

        private GridContainer mainGrid = null!;

        private IBindable<bool> isConnected = null!;

        public ScreenQueue(MatchmakingPoolType poolType)
        {
            this.poolType = poolType;
        }

        public override IReadOnlyList<ScreenFooterButton> CreateFooterButtons()
        {
            var buttons = base.CreateFooterButtons().ToList();
            if (poolType == MatchmakingPoolType.RankedPlay)
            {
                manageRelaxPoolButton = new ScreenFooterButton
                {
                    Text = "Manage Relax pool",
                    Icon = FontAwesome.Solid.List,
                    Action = () =>
                    {
                        if (relaxPool?.CanManage == true)
                        {
                            this.Push(new RelaxPoolEditorScreen(relaxPool));
                            return;
                        }

                        if (relaxPool == null)
                        {
                            notifications.Post(new SimpleNotification { Text = "Could not load Relax pool access. Retrying…" });
                            fetchRelaxPool();
                            return;
                        }

                        notifications.Post(new SimpleNotification { Text = "You need the Relax pool creator role to manage a pool." });
                    }
                };
                buttons.Add(manageRelaxPoolButton);
            }
            return buttons;
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, IAPIProvider api)
        {
            this.api = api;
            enqueueSample = audio.Samples.Get(@"Multiplayer/Matchmaking/enqueue");
            matchFoundSample = audio.Samples.Get(@"Multiplayer/Matchmaking/match-found");

            LinkFlowContainer? experimentalText = null;

            InternalChild = new InverseScalingDrawSizePreservingFillContainer
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    waitingLoop = new DrawableSample(audio.Samples.Get(@"Multiplayer/Matchmaking/waiting-loop")),
                    new GlobalScrollAdjustsVolume(),
                    mainGrid = new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding
                        {
                            Horizontal = 20,
                            Top = 20,
                            Bottom = ScreenFooter.HEIGHT + 20
                        },
                        RowDimensions =
                        [
                            new Dimension(),
                            new Dimension(GridSizeMode.Relative, RuntimeInfo.IsMobile ? 0.55f : 0.35f)
                        ],
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding(5),
                                    Child = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        CornerRadius = 10f,
                                        Masking = true,
                                        Children = new Drawable[]
                                        {
                                            new PanelBackground(),
                                            new GridContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Padding = new MarginPadding(10),
                                                RowDimensions =
                                                [
                                                    new Dimension(GridSizeMode.AutoSize)
                                                ],
                                                Content = new[]
                                                {
                                                    new Drawable[]
                                                    {
                                                        new FillFlowContainer
                                                        {
                                                            RelativeSizeAxes = Axes.X,
                                                            AutoSizeAxes = Axes.Y,
                                                            Children = new Drawable[]
                                                            {
                                                                poolType == MatchmakingPoolType.QuickPlay
                                                                    ? new Container
                                                                    {
                                                                        RelativeSizeAxes = Axes.X,
                                                                        AutoSizeAxes = Axes.Y,
                                                                        Masking = true,
                                                                        CornerRadius = 5,
                                                                        Children = new Drawable[]
                                                                        {
                                                                            new Box
                                                                            {
                                                                                RelativeSizeAxes = Axes.Both,
                                                                                Colour = colours.Yellow
                                                                            },
                                                                            experimentalText = new ExperimentalLinkFlowContainer
                                                                            {
                                                                                RelativeSizeAxes = Axes.X,
                                                                                AutoSizeAxes = Axes.Y,
                                                                                Padding = new MarginPadding(10),
                                                                            }
                                                                        }
                                                                    }
                                                                    : Empty(),
                                                                new QueueSectionHeader("Queued players"),
                                                                queueCountsText = new OsuSpriteText
                                                                {
                                                                    Font = OsuFont.Style.Caption1,
                                                                    Text = "Standard: 0  •  Random mods: 0"
                                                                },
                                                                relaxPoolOwnerText = new OsuSpriteText
                                                                {
                                                                    Font = OsuFont.Style.Caption2,
                                                                    Text = "Relax pool selected: automatic fallback"
                                                                }
                                                            }
                                                        }
                                                    },
                                                    new Drawable[]
                                                    {
                                                        new Container
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            Children = new Drawable[]
                                                            {
                                                                cloud = new CloudVisualisation
                                                                {
                                                                    Anchor = Anchor.Centre,
                                                                    Origin = Anchor.Centre,
                                                                    RelativeSizeAxes = Axes.Both,
                                                                    Size = new Vector2(0.6f)
                                                                },
                                                                new MatchmakingAvatar(api.LocalUser.Value, true)
                                                                {
                                                                    Anchor = Anchor.Centre,
                                                                    Origin = Anchor.Centre,
                                                                    Scale = new Vector2(3),
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding(5),
                                    Child = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        CornerRadius = 10f,
                                        Masking = true,
                                        Children = new Drawable[]
                                        {
                                            new PanelBackground(),
                                            new GridContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Padding = new MarginPadding(10) { Bottom = 0 },
                                                RowDimensions =
                                                [
                                                    new Dimension(GridSizeMode.AutoSize)
                                                ],
                                                Content = new[]
                                                {
                                                    new Drawable[] { new QueueSectionHeader("Recent Matches") },
                                                    new Drawable[]
                                                    {
                                                        new OsuScrollContainer(Direction.Vertical)
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            ScrollbarOverlapsContent = false,
                                                            Child = resultPanelContainer = new FillFlowContainer
                                                            {
                                                                RelativeSizeAxes = Axes.X,
                                                                AutoSizeAxes = Axes.Y,
                                                                Spacing = new Vector2(10),
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding(5),
                                    Child = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        CornerRadius = 10f,
                                        Masking = true,
                                        Children = new Drawable[]
                                        {
                                            new PanelBackground(),
                                            new GridContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Padding = new MarginPadding(10),
                                                RowDimensions =
                                                [
                                                    new Dimension(GridSizeMode.AutoSize)
                                                ],
                                                Content = new[]
                                                {
                                                    new Drawable[] { new QueueSectionHeader("Queues") },
                                                    new Drawable[]
                                                    {
                                                        mainContent = new Container
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            Padding = new MarginPadding(20),
                                                            Alpha = 0,
                                                        },
                                                    }
                                                }
                                            }
                                        }
                                    }
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding(5),
                                    Child = new Container
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        CornerRadius = 10f,
                                        Masking = true,
                                        Children = new Drawable[]
                                        {
                                            new PanelBackground(),
                                            new GridContainer
                                            {
                                                RelativeSizeAxes = Axes.Both,
                                                Padding = new MarginPadding(10),
                                                RowDimensions =
                                                [
                                                    new Dimension(GridSizeMode.AutoSize)
                                                ],
                                                Content = new[]
                                                {
                                                    new Drawable[] { new QueueSectionHeader("Ratings") },
                                                    new Drawable[]
                                                    {
                                                        new Container
                                                        {
                                                            RelativeSizeAxes = Axes.Both,
                                                            Padding = new MarginPadding { Top = -10 },
                                                            Child = ratingGraph = new RatingDistributionGraph
                                                            {
                                                                RelativeSizeAxes = Axes.Both,
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            };

            if (experimentalText != null)
            {
                experimentalText.AddIcon(FontAwesome.Solid.Lightbulb);
                experimentalText.AddText(@" ");
                experimentalText.AddText("This system is under continuous and rapid development.\n", sp => sp.Font = sp.Font.With(weight: FontWeight.SemiBold));
                experimentalText.AddText("Follow the ");
                experimentalText.AddLink("changelog", @"https://osu.ppy.sh/community/forums/topics/2202736", sp => sp.Font = sp.Font.With(weight: FontWeight.SemiBold));
                experimentalText.AddText(" and provide any ");
                experimentalText.AddLink("feedback", @"https://osu.ppy.sh/community/forums/topics/2198397", sp => sp.Font = sp.Font.With(weight: FontWeight.SemiBold));
                experimentalText.AddText(" on the osu! forums!");
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            int delay = 0;

            foreach (var a in mainGrid.Content)
            {
                foreach (var d in a)
                {
                    d.FadeOut()
                     .Delay(delay)
                     .FadeInFromZero(500, Easing.OutQuint);

                    delay += 100;
                }
            }

            subscribeToLobbyStatus();

            currentState.BindTo(queue.CurrentState);
            currentState.BindValueChanged(s => SetState(s.NewValue));

            selectedPool.BindTo(queue.SelectedPool);
            selectedPool.BindValueChanged(e =>
            {
                refreshLobbyData();
                updateRelaxPoolOwnerText();
            }, true);

            isConnected = client.IsConnected.GetBoundCopy();
            isConnected.BindValueChanged(connected => Schedule(() =>
            {
                if (connected.NewValue)
                {
                    cancelAndReplace(ref poolFetchCancellation);
                    populateAvailablePools(poolFetchCancellation.Token).FireAndForget();
                    fetchRelaxPool();
                    refreshLobbyData();
                }
                else
                {
                    poolFetchCancellation.Cancel();
                    availablePools.Value = null;
                    clearLobbyData();
                }
            }), true);
        }

        private void fetchRelaxPool()
        {
            if (poolType != MatchmakingPoolType.RankedPlay)
                return;

            var request = new GetRelaxRankedPoolRequest();
            request.Success += response =>
            {
                relaxPool = response;
                updateRelaxPoolOwnerText();
            };
            request.Failure += _ =>
            {
                relaxPool = null;
                updateRelaxPoolOwnerText();
            };
            api.Queue(request);
        }

        private void updateRelaxPoolOwnerText()
        {
            relaxPoolOwnerText.Alpha = selectedPool.Value?.RulesetId == 4 ? 1 : 0;
            relaxPoolOwnerText.Text = relaxPool?.SelectedOwner is { } owner
                ? $"Relax pool selected: {owner.Username}"
                : "Relax pool selected: automatic fallback";
        }

        private async Task populateAvailablePools(CancellationToken cancellationToken)
        {
            const int max_attempts = 5;
            MatchmakingPool[] pools = Array.Empty<MatchmakingPool>();

            for (int attempt = 1; attempt <= max_attempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                pools = await client.GetMatchmakingPoolsOfType(poolType).ConfigureAwait(false);

                if (pools.Length > 0 || !client.IsConnected.Value)
                    break;

                if (attempt < max_attempts)
                    await Task.Delay(500, cancellationToken).ConfigureAwait(false);
            }

            Schedule(() =>
            {
                if (cancellationToken.IsCancellationRequested)
                    return;

                availablePools.Value = pools;

                // Default to the currently queueing pool, or fallback to the user's ruleset for the initial pool selection.
                selectedPool.Value ??= pools.FirstOrDefault(p => p.RulesetId == ruleset.Value.OnlineID) ?? pools.FirstOrDefault();

                if (selectedPool.Value == null)
                    Logger.Log($"{nameof(ScreenQueue)}: no matchmaking pools available for {poolType}.", LoggingTarget.Runtime, LogLevel.Important);
            });
        }

        private void onMatchmakingLobbyStatusChanged(MatchmakingLobbyStatus status) => Scheduler.Add(() =>
        {
            cancelAndReplace(ref userLookupCancellation);
            var cancellation = userLookupCancellation;

            userLookupCache.GetUsersAsync(status.UsersInQueue, cancellation.Token)
                           .ContinueWith(result => Schedule(() =>
                           {
                               if (cancellation.IsCancellationRequested || !result.IsCompletedSuccessfully)
                                   return;

                               APIUser?[] users = result.GetResultSafely();
                               cloud.Users = users.OfType<APIUser>().ToArray();
                           }), cancellation.Token);

            // Global (incremental) updates will not contain the user rating, so keep the one we already received from initial status data.
            if (status.UserRating != null)
                userRating = status.UserRating;

            ratingGraph.SetData(status.RatingDistribution, userRating);
            queueCountsText.Text = $"Standard: {status.StandardQueueCount}  •  Random mods: {status.RandomModsQueueCount}";

            loadRecentMatches(status.RecentMatches, ++recentMatchesLoadVersion).FireAndForget();
        });

        private async Task loadRecentMatches(RankedPlayRecentMatch[] matches, int loadVersion)
        {
            const int max_panels = 50;
            RankedPlayRecentMatch[] distinctMatches = matches
                                                    .Where(match => match.State.Users.Count >= 2)
                                                    .GroupBy(match => match.RoomId)
                                                    .Select(group => group.First())
                                                    .OrderByDescending(match => match.CompletedAtUnixMilliseconds)
                                                    .Take(max_panels)
                                                    .ToArray();
            string renderKey = string.Join("||", distinctMatches.Select(match => $"{match.RoomId}:{buildRecentMatchKey(match)}"));

            await userLookupCache.GetUsersAsync(distinctMatches.SelectMany(m => m.State.Users.Keys).ToArray()).ConfigureAwait(false);

            Scheduler.Add(() =>
            {
                // Lobby status is a complete snapshot. Ignore stale async loads and do not append
                // the same matches again on every periodic status update.
                if (loadVersion != recentMatchesLoadVersion || renderKey == lastRecentMatchesRenderKey)
                    return;

                lastRecentMatchesRenderKey = renderKey;
                resultPanelContainer.Clear();

                foreach (var match in distinctMatches)
                {
                    resultPanelContainer.Add(new RankedPlayMatchPanel(match)
                    {
                        RelativeSizeAxes = Axes.X,
                        Width = 0.48f
                    });
                }

                if (resultPanelContainer.Any(c => c.Position != Vector2.Zero))
                {
                    resultPanelContainer.LayoutDuration = 400;
                    resultPanelContainer.LayoutEasing = Easing.OutQuint;
                }
            });
        }

        private static string buildRecentMatchKey(RankedPlayRecentMatch recentMatch)
        {
            RankedPlayRoomState match = recentMatch.State;
            return $"{recentMatch.HasFinalState}:{match.WinningUserId}:{string.Join("|", match.Users.OrderBy(u => u.Key).Select(u => $"{u.Key}:{u.Value.Rating}:{u.Value.RatingAfter}:{u.Value.Life}:{u.Value.RoundsWon}"))}";
        }

        private void refreshLobbyData()
        {
            clearLobbyData();

            if (selectedPool.Value == null)
            {
                client.MatchmakingLeaveLobby().FireAndForget();
                return;
            }

            client.MatchmakingJoinLobbyWithParams(new MatchmakingJoinLobbyRequest
            {
                PoolId = selectedPool.Value.Id
            }).FireAndForget();
        }

        private void clearLobbyData()
        {
            recentMatchesLoadVersion++;
            lastRecentMatchesRenderKey = null;
            resultPanelContainer.Clear();
            resultPanelContainer.LayoutDuration = 0;
            userRating = null;
            ratingGraph.SetData([], null);
            queueCountsText.Text = "Standard: 0  •  Random mods: 0";

            cloud.Users = Array.Empty<APIUser>();
        }

        public override void OnEntering(ScreenTransitionEvent e)
        {
            base.OnEntering(e);

            subscribeToLobbyStatus();
            queue.SearchInForeground();

            using (BeginDelayedSequence(800))
                Schedule(() => SetState(currentState.Value));
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);

            subscribeToLobbyStatus();
            queue.SearchInForeground();
            fetchRelaxPool();
            // Rejoin the lobby.
            selectedPool.TriggerChange();
        }

        public override void OnSuspending(ScreenTransitionEvent e)
        {
            base.OnSuspending(e);

            unsubscribeFromLobbyStatus();
            cancelPendingScreenPush();
            stopWaitingLoopPlayback();
            client.MatchmakingLeaveLobby().FireAndForget();
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (base.OnExiting(e))
                return true;

            stopWaitingLoopPlayback();
            cancelPendingScreenPush();
            userLookupCancellation.Cancel();

            switch (currentState.Value)
            {
                default:
                    client.MatchmakingLeaveLobby().FireAndForget();
                    queue.SearchInBackground();
                    return false;

                case MatchmakingScreenState.PendingAccept:
                case MatchmakingScreenState.AcceptedWaitingForRoom:
                    queue.LeaveQueue();
                    return true;

                case MatchmakingScreenState.InRoom:
                    // Block exit until it's initiated from inside the matchmaking screen, but don't
                    // trap the user if joining the room failed before the delayed screen push.
                    if (client.Room == null)
                    {
                        queue.CurrentState.Value = MatchmakingScreenState.Idle;
                        return false;
                    }

                    return true;
            }
        }

        public void SetState(MatchmakingScreenState newState)
        {
            mainContent.FadeInFromZero(500, Easing.OutQuint);
            mainContent.Clear();

            startLoopPlaybackDelegate?.Cancel();
            stopWaitingLoopPlayback();

            pushScreenDelegate?.Cancel();
            pushScreenDelegate = null;
            acceptedWaitingTimeoutDelegate?.Cancel();
            acceptedWaitingTimeoutDelegate = null;

            switch (newState)
            {
                case MatchmakingScreenState.Idle:
                    LinkFlowContainer duelHint;

                    mainContent.Child = new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(10),
                        Children = new Drawable[]
                        {
                            new PoolSelector
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                AvailablePools = { BindTarget = availablePools },
                                SelectedPool = { BindTarget = selectedPool }
                            },
                            new FillFlowContainer
                            {
                                Anchor = Anchor.TopCentre,
                                Origin = Anchor.TopCentre,
                                Width = 400,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Horizontal,
                                Spacing = new Vector2(20, 0),
                                Children = new Drawable[]
                                {
                                    new Container
                                    {
                                        Width = 180,
                                        AutoSizeAxes = Axes.Y,
                                        Child = new FormCheckBox
                                        {
                                            Caption = "Random mods",
                                            Current = { BindTarget = randomMods }
                                        }
                                    },
                                    new BeginQueueingButton
                                    {
                                        DarkerColour = colours.Blue2,
                                        LighterColour = colours.Blue1,
                                        Width = 200,
                                        Enabled = { BindTarget = isConnected },
                                        SelectedPool = { BindTarget = selectedPool },
                                        Action = () =>
                                        {
                                            Debug.Assert(selectedPool.Value != null);
                                            queue.JoinQueue(selectedPool.Value, randomMods.Value);
                                        },
                                        Text = "Begin queueing",
                                    }
                                }
                            },
                            duelHint = new LinkFlowContainer
                            {
                                TextAnchor = Anchor.TopCentre,
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                            }
                        }
                    };

                    duelHint.AddText("Open the ");
                    duelHint.AddLink("dashboard", () => dashboardOverlay?.Show());
                    duelHint.AddText(" to duel another player!");

                    break;

                case MatchmakingScreenState.Queueing:
                    mainContent.Child = new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(15),
                        Children = new Drawable[]
                        {
                            new FillFlowContainer
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                AutoSizeAxes = Axes.Both,
                                Direction = FillDirection.Vertical,
                                Spacing = new Vector2(0, 4),
                                Children = new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        Text = "Searching for a match...",
                                        Font = OsuFont.Style.Title,
                                    },
                                    new QueueTimerText
                                    {
                                        Anchor = Anchor.TopCentre,
                                        Origin = Anchor.TopCentre,
                                        Font = OsuFont.Style.Body,
                                    }
                                }
                            },
                            new LoadingSpinner
                            {
                                State = { Value = Visibility.Visible },
                            },
                            new ShearedButton
                            {
                                DarkerColour = colours.Red3,
                                LighterColour = colours.Red4,
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Width = 200,
                                Text = "Stop queueing",
                                Action = () => queue.LeaveQueue()
                            }
                        }
                    };

                    enqueueSample?.Play();
                    startLoopPlaybackDelegate = Scheduler.AddDelayed(startWaitingLoopPlayback, 2000);
                    break;

                case MatchmakingScreenState.PendingAccept:
                    client.MatchmakingAcceptInvitation().FireAndForget();
                    queue.CurrentState.Value = MatchmakingScreenState.AcceptedWaitingForRoom;

                    matchFoundSample?.Play();
                    music.DuckMomentarily(1250);
                    break;

                case MatchmakingScreenState.AcceptedWaitingForRoom:
                    mainContent.Child = new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(20),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = "Waiting for opponents...",
                                Font = OsuFont.GetFont(size: 32, weight: FontWeight.Light, typeface: Typeface.TorusAlternate),
                            },
                            new LoadingSpinner
                            {
                                State = { Value = Visibility.Visible },
                            },
                        }
                    };

                    startWaitingLoopPlayback();
                    acceptedWaitingTimeoutDelegate = Scheduler.AddDelayed(() =>
                    {
                        if (currentState.Value == MatchmakingScreenState.AcceptedWaitingForRoom)
                        {
                            Logger.Log($"{nameof(ScreenQueue)}: Timed out waiting for matchmaking room. Returning to idle.", LoggingTarget.Runtime, LogLevel.Important);
                            queue.LeaveQueue();
                            queue.CurrentState.Value = MatchmakingScreenState.Idle;
                        }
                    }, 30000);
                    break;

                case MatchmakingScreenState.InRoom:
                    // room received, show users and transition to next screen.
                    mainContent.Child = new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(20),
                        Children = new Drawable[]
                        {
                            new OsuSpriteText
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Text = "Good luck!",
                                Font = OsuFont.GetFont(size: 32, weight: FontWeight.Light, typeface: Typeface.TorusAlternate),
                            },
                        }
                    };

                    using (BeginDelayedSequence(2000))
                    {
                        pushScreenDelegate = Schedule(() =>
                        {
                            if (client.Room == null)
                            {
                                Logger.Log("Room became null, returning to idle");
                                queue.CurrentState.Value = MatchmakingScreenState.Idle;
                                return;
                            }

                            switch (poolType)
                            {
                                case MatchmakingPoolType.QuickPlay:
                                    this.Push(new ScreenMatchmaking(client.Room));
                                    break;

                                case MatchmakingPoolType.RankedPlay:
                                    this.Push(new RankedPlayScreen(client.Room));
                                    break;
                            }
                        });
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            unsubscribeFromLobbyStatus();
            cancelAndDispose(ref userLookupCancellation);
            cancelAndDispose(ref poolFetchCancellation);

            stopWaitingLoopPlayback();
            cancelPendingScreenPush();
            acceptedWaitingTimeoutDelegate?.Cancel();

            base.Dispose(isDisposing);
        }

        private void cancelPendingScreenPush()
        {
            pushScreenDelegate?.Cancel();
            pushScreenDelegate = null;
        }

        private static void cancelAndReplace(ref CancellationTokenSource cancellationSource)
        {
            cancellationSource.Cancel();
            cancellationSource.Dispose();
            cancellationSource = new CancellationTokenSource();
        }

        private static void cancelAndDispose(ref CancellationTokenSource cancellationSource)
        {
            cancellationSource.Cancel();
            cancellationSource.Dispose();
        }

        private void subscribeToLobbyStatus()
        {
            if (lobbyStatusSubscribed || client.IsNull())
                return;

            client.MatchmakingLobbyStatusChanged += onMatchmakingLobbyStatusChanged;
            lobbyStatusSubscribed = true;
        }

        private void unsubscribeFromLobbyStatus()
        {
            if (!lobbyStatusSubscribed || client.IsNull())
                return;

            client.MatchmakingLobbyStatusChanged -= onMatchmakingLobbyStatusChanged;
            lobbyStatusSubscribed = false;
        }

        public enum MatchmakingScreenState
        {
            Idle,
            Queueing,
            PendingAccept,
            AcceptedWaitingForRoom,
            InRoom
        }

        private void startWaitingLoopPlayback()
        {
            stopWaitingLoopPlayback();

            waitingLoopChannel = waitingLoop.GetChannel();
            if (waitingLoopChannel == null)
                return;

            waitingLoopChannel.Looping = true;
            waitingLoopChannel?.Play();

            waitingLoop.VolumeTo(1)
                       .Delay(2000)
                       .VolumeTo(0, 12000);
        }

        private void stopWaitingLoopPlayback()
        {
            waitingLoopChannel?.Stop();
            waitingLoopChannel?.Dispose();
        }

        public partial class PanelBackground : CompositeDrawable
        {
            [Resolved]
            private OverlayColourProvider colourProvider { get; set; } = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                RelativeSizeAxes = Axes.Both;

                InternalChild = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background3,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0.3f,
                };
            }
        }

        public partial class QueueSectionHeader : SectionHeader
        {
            public QueueSectionHeader(string header)
                : base(header)
            {
                // Reduce base class padding.
                Margin = new MarginPadding { Top = 5, Bottom = 10, Horizontal = 5 };
            }
        }

        private partial class BeginQueueingButton : SelectionButton
        {
            public readonly IBindable<MatchmakingPool?> SelectedPool = new Bindable<MatchmakingPool?>();

            protected override void LoadComplete()
            {
                base.LoadComplete();

                SelectedPool.BindValueChanged(p => Enabled.Value = p.NewValue != null, true);
            }
        }

        private partial class SelectionButton : ShearedButton, IKeyBindingHandler<GlobalAction>
        {
            public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
            {
                if (e.Action == GlobalAction.Select && !e.Repeat)
                {
                    TriggerClickWithSound();
                    return true;
                }

                return false;
            }

            public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
            {
            }
        }

        private partial class ExperimentalLinkFlowContainer : LinkFlowContainer
        {
            public ExperimentalLinkFlowContainer()
                : base(sp => sp.Colour = Color4.Black)
            {
            }

            protected override DrawableLinkCompiler CreateLinkCompiler(ITextPart textPart)
                => new LinkCompiler(textPart);

            private partial class LinkCompiler : DrawableLinkCompiler
            {
                public LinkCompiler(ITextPart part)
                    : base(part)
                {
                }

                public LinkCompiler(IEnumerable<Drawable> parts)
                    : base(parts)
                {
                }

                [BackgroundDependencyLoader]
                private void load(OsuColour colours)
                {
                    IdleColour = colours.YellowDarker;
                    HoverColour = Color4.Black;
                }
            }
        }

        private partial class QueueTimerText : OsuSpriteText
        {
            [Resolved]
            private QueueController queue { get; set; } = null!;

            public QueueTimerText()
            {
                AlwaysPresent = true;
            }

            protected override void Update()
            {
                base.Update();

                Text = queue.QueueTimer.Elapsed.ToFormattedDuration();
            }
        }
    }
}
