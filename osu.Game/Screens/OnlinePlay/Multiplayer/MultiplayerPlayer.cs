// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Framework.Screens;
using osu.Framework.Threading;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.Legacy;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.Dodge;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Online.Rooms;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Dodge;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.TagCoop;
using osu.Game.Scoring;
using osu.Game.Screens.OnlinePlay.Playlists;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Ranking;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.Multiplayer
{
    public partial class MultiplayerPlayer : RoomSubmittingPlayer
    {
        // The canonical Tag Co-op participant submits the single combined result even when it
        // contains no successful hit. Other multiplayer modes retain per-player submissions.


        // Do not report FinishedPlay until the API has accepted this participant's score.
        // Otherwise the spectator server may complete the match and expire the playlist item
        // while a normal room score submission is still in flight.

        protected override bool PauseOnFocusLost => false;

        protected override UserActivity InitialActivity => new UserActivity.InMultiplayerGame(Beatmap.Value.BeatmapInfo, Ruleset.Value)
        {
            RankedPlayRoomID = Room.Type == MatchType.RankedPlay ? Room.RoomID : null
        }.WithMods(Mods.Value);

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private IBindable<bool> isConnected = null!;

        private readonly TaskCompletionSource<bool> resultsReady = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> tagCoopReplayReady = new TaskCompletionSource<bool>();
        private int finishedPlayRequested;

        private LoadingLayer loadingDisplay = null!;

        [Cached(typeof(IGameplayLeaderboardProvider))]
        private readonly MultiplayerLeaderboardProvider leaderboardProvider;

        private GameplayMatchScoreDisplay teamScoreDisplay = null!;
        private GameplayChatDisplay chat = null!;

        private readonly MultiplayerRoomUser[] multiplayerUsers;
        private TagCoopGameplayController? tagCoopGameplayController;
        private readonly Dictionary<int, TagCoopRemoteCursor> tagCoopCursors = new Dictionary<int, TagCoopRemoteCursor>();
        private readonly HashSet<int> tagCoopLoggedCursorUsers = new HashSet<int>();
        private IReadOnlyDictionary<int, string> tagCoopUsernames = new Dictionary<int, string>();
        private TagCoopReplayMetadata? tagCoopReplay;
        private TagCoopRemoteCursor? tagCoopLocalBotCursor;
        private long nextTagCoopCursorSample;
        private long nextTagCoopPingSend;
        private uint tagCoopCursorSequence;
        private ushort tagCoopPing;
        private bool tagCoopEnabled;
        private readonly Random tagCoopRandom = new Random();
        private int tagCoopArtificialLatency;
        private int tagCoopArtificialJitter;
        private int tagCoopArtificialLoss;
        private bool tagCoopAutoplayLocalTurns;
        private bool tagCoopLoggedFirstPing;
        private DodgeMultiplayerGameplayController? dodgeMultiplayerController;
        private long nextDodgePingSend;
        private ushort dodgePing;
        private int dodgeArtificialLatency;
        private bool dodgeMultiplayerEnabled;
        private RankedPlayRemoteCursor? rankedPlayRemoteCursor;
        private long nextRankedPlayPingSend;
        private ushort rankedPlayPing;
        private bool rankedPlayCursorEnabled;

        private int tagCoopReplayOwnerId => multiplayerUsers.Where(user => user.Role != MultiplayerRoomUserRole.Referee)
                                                                .OrderBy(user => user.UserID)
                                                                .Select(user => user.UserID)
                                                                .FirstOrDefault();

        // Token retrieval runs during asynchronous drawable loading. MultiplayerClient.LocalUser
        // accesses the update-thread-only room state and asserts when queried from that worker.
        // The score's user is created from the local API user before LoadAsyncComplete(), so it is
        // the safe and authoritative identity to use here.

        /// <summary>
        /// Construct a multiplayer player.
        /// </summary>
        /// <param name="room">The room.</param>
        /// <param name="playlistItem">The playlist item to be played.</param>
        /// <param name="users">The users which are participating in this game.</param>
        /// <param name="showFailingOverlay">Whether to show the red failing overlay.</param>
        public MultiplayerPlayer(Room room, PlaylistItem playlistItem, MultiplayerRoomUser[] users, bool showFailingOverlay = true)
            : base(room, playlistItem, new PlayerConfiguration
            {
                AllowPause = false,
                AllowRestart = false,
                AutomaticallySkipIntro = room.AutoSkip,
                ShowLeaderboard = true,
                ShowFailingOverlay = showFailingOverlay
            })
        {
            multiplayerUsers = users;
            leaderboardProvider = new MultiplayerLeaderboardProvider(users);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (!LoadedBeatmapSuccessfully)
                return;

            // also applied in `MultiSpectatorPlayer.load()`
            ScoreProcessor.ApplyNewJudgementsWhenFailed = true;

            // Tag Co-op has one shared health bar reconstructed from every participant's
            // judgements. The match continues after reaching zero, so later hits must be able
            // to recover the displayed health even though the score retains its failed state.
            if (Room.Type == MatchType.TagCoop)
                HealthProcessor.ApplyNewJudgementsWhenFailed = true;

            LoadComponentAsync(new FillFlowContainer
            {
                Width = 260,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(5),
                Children = new Drawable[]
                {
                    chat = new GameplayChatDisplay(Room),
                    teamScoreDisplay = new GameplayMatchScoreDisplay
                    {
                        Expanded = { BindTarget = HUDOverlay.ShowHud },
                        Alpha = 0,
                    }
                }
            }, HUDOverlay.TopLeftElements.Add);
            LoadComponentAsync(new MultiplayerPositionDisplay
            {
                Anchor = Anchor.BottomRight,
                Origin = Anchor.BottomRight,
            }, d => HUDOverlay.BottomRightElements.Insert(-1, d));

            LoadComponentAsync(leaderboardProvider, loaded =>
            {
                AddInternal(loaded);

                if (loaded.HasTeams)
                {
                    teamScoreDisplay.Alpha = 1;
                    teamScoreDisplay.Team1Score.BindTarget = leaderboardProvider.TeamScores.First().Value;
                    teamScoreDisplay.Team2Score.BindTarget = leaderboardProvider.TeamScores.Last().Value;
                }
            });

            HUDOverlay.Add(loadingDisplay = new LoadingLayer(true) { Depth = float.MaxValue });
        }

        protected override void LoadAsyncComplete()
        {
            base.LoadAsyncComplete();

            if (!LoadedBeatmapSuccessfully)
                return;

            if (!ValidForResume)
                return; // token retrieval may have failed.

            client.GameplayStarted += onGameplayStarted;
            client.ResultsReady += onResultsReady;
            client.VoteToSkipIntroPassed += onVoteToSkipIntroPassed;
            client.VoteToSkipBreakPassed += onVoteToSkipBreakPassed;

            ScoreProcessor.HasCompleted.BindValueChanged(_ =>
            {
                // wait for server to tell us that results are ready (see SubmitScore implementation)
                loadingDisplay.Show();
            });


            isConnected = client.IsConnected.GetBoundCopy();
            isConnected.BindValueChanged(connected => Schedule(() =>
            {
                if (!connected.NewValue)
                {
                    // messaging to the user about this disconnect will be provided by the MultiplayerMatchSubScreen.
                    failAndBail();
                }
            }), true);

            LocalUserPlaying.BindValueChanged(_ => chat.Expanded.Value = !LocalUserPlaying.Value, true);
        }

        private void initialiseTagCoop()
        {
            if (Room.Type != MatchType.TagCoop)
                return;

            MultiplayerRoomUser? localUser = client.LocalUser;
            if (localUser == null)
                return;

            if (GameplayState.Ruleset is not ITagCoopRuleset tagCoopRuleset)
            {
                failAndBail("Tag Co-op is currently supported only by Standard.");
                return;
            }

            int[] actualPlayerIds = multiplayerUsers.Where(u => u.Role != MultiplayerRoomUserRole.Referee)
                                                   .Select(u => u.UserID)
                                                   .Distinct()
                                                   .ToArray();
            var actualPlayerIdSet = actualPlayerIds.ToHashSet();

            // The room state is assembled while the room is open and can still contain users who
            // did not ready up for this particular round. Preserve the server-defined turn order,
            // but only for the users that actually entered this gameplay screen.
            int[] playerOrder = ((client.Room?.MatchState as TagCoopRoomState)?.PlayerOrder ?? Array.Empty<int>())
                                .Where(actualPlayerIdSet.Contains)
                                .Distinct()
                                .ToArray();

            if (playerOrder.Length == 0)
                playerOrder = actualPlayerIds.OrderBy(id => id).ToArray();

            if (!playerOrder.Contains(localUser.UserID))
                return;

            bool autoplayLocalTurns = tagCoopAutoplayLocalTurns = Environment.GetEnvironmentVariable("MOSU_TAG_COOP_BOT") == "1";
            tagCoopArtificialLatency = readTagCoopNetworkOption("MOSU_TAG_COOP_LATENCY", 5000);
            tagCoopArtificialJitter = readTagCoopNetworkOption("MOSU_TAG_COOP_JITTER", 5000);
            tagCoopArtificialLoss = readTagCoopNetworkOption("MOSU_TAG_COOP_LOSS", 100);

            Color4[] palette =
            {
                new Color4(255, 102, 204, 255),
                new Color4(79, 195, 247, 255),
                new Color4(255, 202, 40, 255),
                new Color4(105, 240, 174, 255),
                new Color4(179, 136, 255, 255),
                new Color4(255, 112, 67, 255),
            };

            var playerColours = playerOrder.Select((userId, index) => (userId, colour: palette[index % palette.Length]))
                                           .ToDictionary(pair => pair.userId, pair => pair.colour);
            var usernames = tagCoopUsernames = playerOrder.ToDictionary(userId => userId, userId =>
                multiplayerUsers.FirstOrDefault(u => u.UserID == userId)?.User?.Username
                ?? client.Room?.Users.FirstOrDefault(u => u.UserID == userId)?.User?.Username
                ?? $"Player {userId}");

            TagCoopReplayPlayer[] replayPlayers = playerOrder.Select(userId => new TagCoopReplayPlayer
            {
                UserID = userId,
                Username = usernames[userId],
            }).ToArray();

            // Keep per-player replay metadata for local results.
            Score.ScoreInfo.TagCoopReplay = tagCoopReplay = new TagCoopReplayMetadata
            {
                Players = replayPlayers.ToList(),
            };
            SpectatorClient.ConfigureTagCoopReplay(Score, replayPlayers);

            tagCoopGameplayController = tagCoopRuleset.CreateTagCoopGameplayController(DrawableRuleset, new TagCoopGameplayConfiguration
            {
                PlayerOrder = playerOrder,
                LocalUserId = localUser.UserID,
                AutoplayLocalTurns = autoplayLocalTurns,
                PlayerColours = playerColours,
                SendJudgement = (_, _) => { },
                IsPlayerPresent = userId => client.Room?.Users.Any(u => u.UserID == userId && u.State == MultiplayerUserState.Playing) == true,
            });

            AddInternal(tagCoopGameplayController);
            tagCoopGameplayController.RemoteJudgementFeedback += onTagCoopRemoteJudgementFeedback;

            HUDOverlay.BottomRightElements.Insert(0, new TagCoopTurnDisplay(localUser.UserID, tagCoopGameplayController, usernames, playerColours, userId =>
            {
                if (userId == localUser.UserID)
                    return tagCoopPing;

                return tagCoopCursors.TryGetValue(userId, out TagCoopRemoteCursor? cursor) && cursor.HasPosition
                    ? cursor.PingMilliseconds
                    : null;
            }));

            if (autoplayLocalTurns)
            {
                tagCoopLocalBotCursor = new TagCoopRemoteCursor(localUser.UserID, usernames[localUser.UserID], playerColours[localUser.UserID], DrawableRuleset, IsBreakTime,
                    () => tagCoopGameplayController?.ActiveUserId, true);
                DrawableRuleset.Overlays.Add(tagCoopLocalBotCursor);
            }

            for (int i = 0; i < playerOrder.Length; i++)
            {
                int userId = playerOrder[i];
                if (userId == localUser.UserID)
                    continue;

                var cursor = new TagCoopRemoteCursor(userId, usernames[userId], playerColours[userId], DrawableRuleset, IsBreakTime,
                    () => tagCoopGameplayController?.ActiveUserId);

                tagCoopCursors[userId] = cursor;
                DrawableRuleset.Overlays.Add(cursor);
            }

            client.MatchEvent += onTagCoopMatchEvent;
            long now = Stopwatch.GetTimestamp();
            nextTagCoopCursorSample = now;
            nextTagCoopPingSend = now;
            tagCoopEnabled = true;

            Logger.Log(
                $"Tag Co-op initialised: local={localUser.UserID}, order=[{string.Join(',', playerOrder)}], bot={autoplayLocalTurns}, remoteCursors={tagCoopCursors.Count}",
                LoggingTarget.Runtime,
                LogLevel.Verbose);
        }

        private void initialiseDodgeMultiplayer()
        {
            // Dodge uses ordinary multiplayer rooms. Tag Co-op has its own cursor and
            // judgement transport and must never enable this path.
            if (Room.Type == MatchType.TagCoop || GameplayState.Ruleset is not IDodgeMultiplayerRuleset dodgeRuleset)
                return;

            MultiplayerRoomUser? localUser = client.LocalUser;
            if (localUser == null)
                return;

            int[] playerIds = multiplayerUsers.Where(user => user.Role != MultiplayerRoomUserRole.Referee)
                                               .Select(user => user.UserID)
                                               .Distinct()
                                               .OrderBy(id => id)
                                               .ToArray();

            if (!playerIds.Contains(localUser.UserID))
                return;

            dodgeArtificialLatency = readDodgeNetworkOption("MOSU_DODGE_LATENCY");

            Color4[] palette =
            {
                new Color4(255, 102, 204, 255),
                new Color4(79, 195, 247, 255),
                new Color4(255, 202, 40, 255),
                new Color4(105, 240, 174, 255),
                new Color4(179, 136, 255, 255),
                new Color4(255, 112, 67, 255),
            };

            var playerColours = playerIds.Select((userId, index) => (userId, colour: palette[index % palette.Length]))
                                         .ToDictionary(pair => pair.userId, pair => pair.colour);
            var usernames = playerIds.ToDictionary(userId => userId, userId =>
                multiplayerUsers.FirstOrDefault(user => user.UserID == userId)?.User?.Username
                ?? client.Room?.Users.FirstOrDefault(user => user.UserID == userId)?.User?.Username
                ?? $"Player {userId}");

            dodgeMultiplayerController = dodgeRuleset.CreateDodgeMultiplayerGameplayController(DrawableRuleset, new DodgeMultiplayerGameplayConfiguration
            {
                LocalUserId = localUser.UserID,
                Usernames = usernames,
                PlayerColours = playerColours,
                IsBreakTime = IsBreakTime,
            });

            DrawableRuleset.Overlays.Add(dodgeMultiplayerController);
            client.MatchEvent += onDodgeMatchEvent;

            long now = Stopwatch.GetTimestamp();
            nextDodgePingSend = now;
            dodgeMultiplayerEnabled = true;

            Logger.Log(
                $"Dodge multiplayer initialised: local={localUser.UserID}, remotePlayers={Math.Max(0, playerIds.Length - 1)}, simulatedLatency={dodgeArtificialLatency} ms",
                LoggingTarget.Runtime,
                LogLevel.Verbose);
        }

        private void initialiseRankedPlayCursor()
        {
            if (!SupportsRankedPlayCursor(IsRankedPlaySession, Room.Type, PlaylistItem.RulesetID, PlaylistItem.RequiredMods))
                return;

            MultiplayerRoomUser? localUser = client.LocalUser;
            if (localUser == null)
                return;

            MultiplayerRoomUser? opponent = multiplayerUsers.FirstOrDefault(user => user.Role != MultiplayerRoomUserRole.Referee && user.UserID != localUser.UserID);
            if (opponent == null)
                return;

            string username = opponent.User?.Username
                              ?? client.Room?.Users.FirstOrDefault(user => user.UserID == opponent.UserID)?.User?.Username
                              ?? $"Player {opponent.UserID}";

            rankedPlayRemoteCursor = new RankedPlayRemoteCursor(
                username,
                new Color4(255, 102, 102, 255),
                DrawableRuleset,
                IsBreakTime);

            DrawableRuleset.Overlays.Add(rankedPlayRemoteCursor);
            client.MatchEvent += onRankedPlayMatchEvent;

            long now = Stopwatch.GetTimestamp();
            nextRankedPlayPingSend = now;
            rankedPlayCursorEnabled = true;
        }

        internal static bool SupportsRankedPlayCursor(bool isRankedPlaySession, MatchType matchType, int rulesetId, IEnumerable<APIMod> requiredMods)
            => isRankedPlaySession
               && matchType == MatchType.RankedPlay
               && rulesetId == 0
               && requiredMods.All(mod => mod.Acronym != "AP");

        protected override void Update()
        {
            base.Update();

            updateTagCoop();
            updateDodgeMultiplayer();
            updateRankedPlayCursor();
        }

        private void updateRankedPlayCursor()
        {
            if (!rankedPlayCursorEnabled
                || !IsBreakTime.Value
                || client.LocalUser?.State != MultiplayerUserState.Playing)
                return;

            long now = Stopwatch.GetTimestamp();

            if (now >= nextRankedPlayPingSend)
            {
                nextRankedPlayPingSend = now + Stopwatch.Frequency * 2;
                client.SendMatchRequest(new RankedPlayPingRequest { Nonce = now }).FireAndForget();
            }

        }

        private void updateTagCoop()
        {

            if (!tagCoopEnabled || client.LocalUser?.State != MultiplayerUserState.Playing)
                return;

            long now = Stopwatch.GetTimestamp();

            if (now >= nextTagCoopPingSend)
            {
                nextTagCoopPingSend = now + Stopwatch.Frequency * 2;
                sendTagCoopRequest(new TagCoopPingRequest { Nonce = now });
            }

            if (now < nextTagCoopCursorSample)
                return;

            nextTagCoopCursorSample = now + Stopwatch.Frequency / 30;
            recordLocalTagCoopCursorSample();
        }

        private void recordLocalTagCoopCursorSample()
        {
            if (!tagCoopEnabled || client.LocalUser?.State != MultiplayerUserState.Playing)
                return;

            Vector2 gamefieldPosition = tagCoopGameplayController?.AutomatedCursorPosition
                                        ?? DrawableRuleset.Playfield.ScreenSpaceToGamefield(DrawableRuleset.Cursor.ActiveCursor.ScreenSpaceDrawQuad.Centre);
            Vector2 normalisedPosition = new Vector2(gamefieldPosition.X / 512, gamefieldPosition.Y / 384);

            if (!float.IsFinite(normalisedPosition.X) || !float.IsFinite(normalisedPosition.Y))
                return;

            normalisedPosition = Vector2.ComponentMin(Vector2.One, Vector2.ComponentMax(Vector2.Zero, normalisedPosition));

            uint sequence = ++tagCoopCursorSequence;
            byte buttonState = tagCoopGameplayController?.LocalButtonState ?? 0;
            double gameplayTime = GameplayClockContainer.CurrentTime;

            if (tagCoopAutoplayLocalTurns)
                buttonState |= TagCoopRemoteCursor.AUTOMATED_FLAG;

            tagCoopLocalBotCursor?.Push(sequence, normalisedPosition, 0, buttonState);
            recordTagCoopReplayFallbackFrame(new TagCoopReplayFrame
            {
                UserID = client.LocalUser.UserID,
                Sequence = sequence,
                GameplayTime = gameplayTime,
                X = normalisedPosition.X,
                Y = normalisedPosition.Y,
                ButtonState = (byte)(buttonState & 0b11),
            });

        }

        private void updateDodgeMultiplayer()
        {
            if (!dodgeMultiplayerEnabled || dodgeMultiplayerController == null || client.LocalUser?.State != MultiplayerUserState.Playing)
                return;

            long now = Stopwatch.GetTimestamp();

            if (now >= nextDodgePingSend)
            {
                nextDodgePingSend = now + Stopwatch.Frequency * 2;
                sendDodgeRequest(new DodgePingRequest { Nonce = now });
            }

        }

        private void sendDodgeRequest(MatchUserRequest request)
        {
            if (dodgeArtificialLatency == 0)
                client.SendMatchRequest(request).FireAndForget();
            else
                Scheduler.AddDelayed(() => client.SendMatchRequest(request).FireAndForget(), dodgeArtificialLatency);
        }

        private static int readDodgeNetworkOption(string name)
            => int.TryParse(Environment.GetEnvironmentVariable(name), out int value) ? Math.Clamp(value, 0, 5000) : 0;

        private void sendTagCoopRequest(MatchUserRequest request, bool allowPacketLoss = true)
        {
            if (allowPacketLoss && tagCoopArtificialLoss > 0 && tagCoopRandom.Next(100) < tagCoopArtificialLoss)
                return;

            int jitter = tagCoopArtificialJitter == 0 ? 0 : tagCoopRandom.Next(-tagCoopArtificialJitter, tagCoopArtificialJitter + 1);
            int delay = Math.Max(0, tagCoopArtificialLatency + jitter);

            if (delay == 0)
                client.SendMatchRequest(request).FireAndForget();
            else
                Scheduler.AddDelayed(() =>
                {
                    if (tagCoopEnabled)
                        client.SendMatchRequest(request).FireAndForget();
                }, delay);
        }

        private static int readTagCoopNetworkOption(string name, int maximum)
            => int.TryParse(Environment.GetEnvironmentVariable(name), out int value) ? Math.Clamp(value, 0, maximum) : 0;

        private void onTagCoopMatchEvent(MatchServerEvent matchEvent)
        {
            switch (matchEvent)
            {
                case TagCoopPingEvent ping:
                    long now = Stopwatch.GetTimestamp();
                    if (ping.Nonce > 0 && ping.Nonce <= now)
                    {
                        tagCoopPing = (ushort)Math.Clamp(Stopwatch.GetElapsedTime(ping.Nonce, now).TotalMilliseconds, ushort.MinValue, ushort.MaxValue);

                        if (!tagCoopLoggedFirstPing)
                        {
                            tagCoopLoggedFirstPing = true;
                            Logger.Log($"Tag Co-op ping response received: {tagCoopPing} ms", LoggingTarget.Network, LogLevel.Verbose);
                        }
                    }
                    break;

                case TagCoopCursorPositionEvent cursor when cursor.UserID != client.LocalUser?.UserID:
                    recordTagCoopReplayFallbackFrame(new TagCoopReplayFrame
                    {
                        UserID = cursor.UserID,
                        Sequence = cursor.Sequence,
                        GameplayTime = cursor.GameplayTime,
                        X = cursor.X,
                        Y = cursor.Y,
                        ButtonState = (byte)(cursor.ButtonState & 0b11),
                    });

                    if (tagCoopCursors.TryGetValue(cursor.UserID, out TagCoopRemoteCursor? drawableCursor))
                    {
                        drawableCursor.Push(cursor.Sequence, new Vector2(cursor.X, cursor.Y), cursor.PingMilliseconds, cursor.ButtonState);

                        if (tagCoopLoggedCursorUsers.Add(cursor.UserID))
                            Logger.Log($"Tag Co-op cursor received: remote={cursor.UserID}, ping={cursor.PingMilliseconds} ms", LoggingTarget.Network, LogLevel.Verbose);
                    }

                    break;

                case TagCoopReplayFramesEvent replayFrames:
                    receiveTagCoopReplayFrames(replayFrames);
                    break;

                case TagCoopJudgementEvent judgement when judgement.UserID != client.LocalUser?.UserID:
                    tagCoopGameplayController?.ApplyRemoteJudgement(judgement.UserID, judgement.ObjectIndex, judgement.Result);
                    break;
            }
        }

        private void recordTagCoopReplayFallbackFrame(TagCoopReplayFrame frame)
        {
            // Live cursor samples are retained only as a fallback for a client which disconnects
            // before post-game exchange. A completed native track replaces every one of these.
            tagCoopReplay?.Frames.Add(frame);
            SpectatorClient.HandleTagCoopFrame(frame, tagCoopGameplayController?.CreateCompatibilityReplayFrame(frame));
        }

        private void onDodgeMatchEvent(MatchServerEvent matchEvent)
        {
            switch (matchEvent)
            {
                case DodgePingEvent ping:
                    long now = Stopwatch.GetTimestamp();
                    if (ping.Nonce > 0 && ping.Nonce <= now)
                    {
                        dodgePing = (ushort)Math.Clamp(Stopwatch.GetElapsedTime(ping.Nonce, now).TotalMilliseconds, ushort.MinValue, ushort.MaxValue);
                        dodgeMultiplayerController?.SetLocalPing(dodgePing);
                    }
                    break;

                case DodgePlayerPositionEvent position when position.UserID != client.LocalUser?.UserID:
                    dodgeMultiplayerController?.PushRemotePlayerState(
                        position.UserID,
                        position.Sequence,
                        new Vector2(position.X, position.Y),
                        position.PingMilliseconds);
                    break;
            }
        }

        private void onRankedPlayMatchEvent(MatchServerEvent matchEvent)
        {
            switch (matchEvent)
            {
                case RankedPlayPingEvent ping:
                    long now = Stopwatch.GetTimestamp();
                    if (ping.Nonce > 0 && ping.Nonce <= now)
                        rankedPlayPing = (ushort)Math.Clamp(Stopwatch.GetElapsedTime(ping.Nonce, now).TotalMilliseconds, ushort.MinValue, ushort.MaxValue);
                    break;

                case RankedPlayCursorPositionEvent position when position.UserID != client.LocalUser?.UserID:
                    rankedPlayRemoteCursor?.Push(
                        position.Sequence,
                        new Vector2(position.X, position.Y),
                        position.PingMilliseconds,
                        position.ButtonState);
                    break;
            }
        }

        private void onTagCoopRemoteJudgementFeedback(int userId, Vector2 gamefieldPosition, HitResult result)
        {
            if (result.IsHit() || result == HitResult.IgnoreMiss)
                return;

            string username = tagCoopUsernames.TryGetValue(userId, out string? value) ? value : $"Player {userId}";
            Scheduler.Add(() => DrawableRuleset.Overlays.Add(new TagCoopRemoteJudgementMarker(DrawableRuleset, gamefieldPosition, username)));
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Debug.Assert(client.Room != null);

            SpectatorClient.OnUserScoreProcessed += onUserScoreProcessed;

            // Tag co-op setup reads multiplayer room state, which is update-thread-only.
            initialiseTagCoop();
            initialiseDodgeMultiplayer();
            initialiseRankedPlayCursor();
        }

        protected override SkipOverlay CreateSkipOverlay(double startTime) => new MultiplayerSkipOverlay(startTime);

        protected override SkipOverlay CreateBreakSkipOverlay(BreakPeriod breakPeriod)
        {
            return new MultiplayerSkipOverlay(GetBreakSkipTarget(breakPeriod) + MasterGameplayClockContainer.MINIMUM_SKIP_TIME, new MultiplayerBreakSkipRequest
            {
                BreakIndex = GetBreakIndex(breakPeriod),
                BreakStartTime = breakPeriod.StartTime,
                BreakEndTime = breakPeriod.EndTime,
            });
        }

        protected override void StartGameplay()
        {
            // We can enter this screen one of two ways:
            // 1. Via the automatic natural progression of PlayerLoader into Player.
            //    We'll arrive here in a Loaded state, and we need to let the server know that we're ready to start.
            // 2. Via the server forcefully starting gameplay because players have been hanging out in PlayerLoader for too long.
            //    We'll arrive here in a Playing state, and we should neither show the loading spinner nor tell the server that we're ready to start (gameplay has already started).
            //
            // The base call is blocked here because in both cases gameplay is started only when the server says so via onGameplayStarted().

            if (client.LocalUser?.State == MultiplayerUserState.Loaded)
            {
                loadingDisplay.Show();
                client.ChangeState(MultiplayerUserState.ReadyForGameplay).FireAndForget();
            }

            // This will pause the clock, pending the gameplay started callback from the server.
            GameplayClockContainer.Reset();
        }

        protected override void PerformFail()
        {
            // base logic intentionally suppressed - failing in multiplayer only marks the score with F rank
            // see also: `MultiSpectatorPlayer.OnFail()`
            ScoreProcessor.FailScore(Score.ScoreInfo);
        }

        private void failAndBail(string? message = null)
        {
            if (!string.IsNullOrEmpty(message))
                Logger.Log(message, LoggingTarget.Runtime, LogLevel.Important);

            Schedule(() => PerformExit());
        }

        private void onGameplayStarted() => Scheduler.Add(() =>
        {
            if (!this.IsCurrentScreen())
                return;

            loadingDisplay.Hide();
            base.StartGameplay();
        });

        private void onResultsReady()
        {
            // Schedule is required to ensure that `TaskCompletionSource.SetResult` is not called more than once.
            // A scenario where this can occur is if this instance is not immediately disposed (ie. async disposal queue).
            Schedule(() =>
            {
                if (!this.IsCurrentScreen())
                    return;

                resultsReady.SetResult(true);
            });
        }

        protected override async Task PrepareScoreForResultsAsync(Score score)
        {
            // Tag Co-op combines judgements from multiple users into one unranked score.
            // Such a score must never display or award performance points.
            if (Room.Type == MatchType.TagCoop)
            {
                score.ScoreInfo.PP = 0;
                score.ScoreInfo.Ranked = false;
                score.ScoreInfo.TagCoopReplay = await getTagCoopReplaySnapshotAsync().ConfigureAwait(false);
                populateTagCoopCompatibilityReplay(score);
            }

            await base.PrepareScoreForResultsAsync(score).ConfigureAwait(false);

            // Score preparation can be entered again while the server's Results transition is
            // already in flight. Avoid an invalid Results -> FinishedPlay transition.
            if (Interlocked.Exchange(ref finishedPlayRequested, 1) == 0)
                await changeToFinishedPlayAsync().ConfigureAwait(false);

            if (OnlineRecordSendingDisabled)
                return;

            // Await up to 60 seconds for results to become available (6 api request timeouts).
            // This is arbitrary just to not leave the player in an essentially deadlocked state if any connection issues occur.
            Task completedTask = await Task.WhenAny(resultsReady.Task, Task.Delay(TimeSpan.FromSeconds(60))).ConfigureAwait(false);

            if (Room.Type == MatchType.TagCoop && completedTask == resultsReady.Task)
                await attachCanonicalTagCoopScoreAsync(score.ScoreInfo).ConfigureAwait(false);
        }

        private Task<TagCoopReplayMetadata> getTagCoopReplaySnapshotAsync()
        {
            var completion = new TaskCompletionSource<TagCoopReplayMetadata>(TaskCreationOptions.RunContinuationsAsynchronously);
            Schedule(() => completion.TrySetResult(tagCoopReplay?.DeepClone() ?? new TagCoopReplayMetadata()));
            return completion.Task;
        }

        private void receiveTagCoopReplayFrames(TagCoopReplayFramesEvent replayFrames)
        {
            if (tagCoopReplay == null || tagCoopReplay.Players.All(player => player.UserID != replayFrames.UserID))
                return;

            // A sender's first batch starts a replacement of any low-frequency live samples
            // retained by an older path. Subsequent batches append in sequence order.
                if (replayFrames.BatchSequence == 0)
            {
                tagCoopReplay.Frames.RemoveAll(frame => frame.UserID == replayFrames.UserID);
            }

            tagCoopReplay.Frames.AddRange(replayFrames.Frames.Select((frame, index) => frame with
            {
                UserID = replayFrames.UserID,
                Sequence = replayFrames.BatchSequence * 512u + (uint)index + 1,
                ButtonState = (byte)(frame.ButtonState & 0b11),
            }));

        }

        private void populateTagCoopCompatibilityReplay(Score score)
        {
            TagCoopReplayMetadata? metadata = score.ScoreInfo.TagCoopReplay;

            if (metadata == null || tagCoopGameplayController == null)
                return;

            List<ReplayFrame> combinedFrames = tagCoopGameplayController.CreateCompatibilityReplayFrames(metadata).ToList();

            if (combinedFrames.Count > 0)
                score.Replay.Frames = combinedFrames;
        }

        private async Task attachCanonicalTagCoopScoreAsync(ScoreInfo localScore)
        {
            Debug.Assert(Room.RoomID != null);

            var completion = new TaskCompletionSource<MultiplayerScore?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var request = new ShowPlaylistUserScoreRequest(Room.RoomID.Value, PlaylistItem.ID, tagCoopReplayOwnerId);
            request.Success += score => completion.TrySetResult(score);
            request.Failure += ex =>
            {
                Logger.Error(ex, "Failed to resolve the canonical Tag Co-op score for the local replay.");
                completion.TrySetResult(null);
            };
            api.Queue(request);

            Task completedTask = await Task.WhenAny(completion.Task, Task.Delay(TimeSpan.FromSeconds(10))).ConfigureAwait(false);
            if (completedTask != completion.Task)
            {
                Logger.Log("Timed out resolving the canonical Tag Co-op score for the local replay.", LoggingTarget.Network, LogLevel.Important);
                return;
            }

            MultiplayerScore? canonicalScore = await completion.Task.ConfigureAwait(false);
            if (canonicalScore == null || canonicalScore.ID <= 0)
                return;

            // The replay bytes remain the locally recorded combined play, but all participants'
            // databases associate them with the same canonical online leaderboard entry.
            localScore.OnlineID = canonicalScore.ID;
            localScore.Position = canonicalScore.Position;
            localScore.User = canonicalScore.User;
            localScore.HasOnlineReplay = canonicalScore.HasReplay;
        }

        private Task changeToFinishedPlayAsync()
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // PrepareScoreForResultsAsync() resumes on a worker after score submission. Room and
            // LocalUser are update-thread-only, so both the state check and ChangeState invocation
            // must be marshalled back to the drawable scheduler.
            Schedule(() =>
            {
                if (client.LocalUser?.State != MultiplayerUserState.Playing)
                {
                    completion.SetResult(true);
                    return;
                }

                client.ChangeState(MultiplayerUserState.FinishedPlay).ContinueWith(task =>
                {
                    if (task.IsCanceled)
                        completion.SetCanceled();
                    else if (task.Exception != null)
                        completion.SetException(task.Exception.InnerExceptions);
                    else
                        completion.SetResult(true);
                }, TaskScheduler.Default);
            });

            return completion.Task;
        }

        protected override void RequestIntroSkip()
        {
            // If the room is set up such that the intro is automatically skipped, there's no need to vote on it.
            if (Configuration.AutomaticallySkipIntro)
            {
                base.RequestIntroSkip();
                return;
            }

            // No base call because we aren't skipping yet.
            client.VoteToSkipIntro().FireAndForget();
        }

        protected override void RequestBreakSkip(BreakPeriod breakPeriod)
        {
            if (!MosuServerEnvironment.SupportsBreakSkipping)
                return;

            int breakIndex = GetBreakIndex(breakPeriod);
            if (breakIndex < 0 || CurrentBreak?.Equals(breakPeriod) != true)
                return;

            client.VoteToSkipBreak(new MultiplayerBreakSkipRequest
            {
                BreakIndex = breakIndex,
                BreakStartTime = breakPeriod.StartTime,
                BreakEndTime = breakPeriod.EndTime,
            }).FireAndForget();
        }

        private void onVoteToSkipIntroPassed()
        {
            Schedule(() => PerformIntroSkip(true));
        }

        private void onVoteToSkipBreakPassed(MultiplayerBreakSkipRequest request)
        {
            if (request == null || !MosuServerEnvironment.SupportsBreakSkipping)
                return;

            Schedule(() =>
            {
                BreakPeriod breakPeriod = CurrentBreak;

                if (breakPeriod == null
                    || request.BreakIndex != GetBreakIndex(breakPeriod)
                    || Math.Abs(request.BreakStartTime - breakPeriod.StartTime) >= 0.001
                    || Math.Abs(request.BreakEndTime - breakPeriod.EndTime) >= 0.001)
                    return;

                PerformBreakSkip(breakPeriod, true);
            });
        }

        private void onUserScoreProcessed(int userId, long scoreId)
        {
            if (Room.Type == MatchType.TagCoop && userId == client.LocalUser?.UserID && scoreId > 0)
                tagCoopReplayReady.TrySetResult(true);
        }

        protected override ResultsScreen CreateResults(ScoreInfo score)
        {
            Debug.Assert(Room.RoomID != null);

            if (Room.Type == MatchType.TagCoop)
            {
                // Each participant imports the same combined result with a local combined replay.
                // Keep that local score selected so the replay is immediately playable on every
                // client. Only the canonical owner has a positive online ID and can fetch the
                // surrounding server leaderboard entries.
                return new MultiplayerResultsScreen(score, Room.RoomID.Value, PlaylistItem)
                {
                    IsLocalPlay = true,
                };
            }

            return leaderboardProvider.TeamScores.Count == 2
                ? new MultiplayerTeamResultsScreen(score, Room.RoomID.Value, PlaylistItem, leaderboardProvider.TeamScores)
                {
                    IsLocalPlay = true,
                }
                : new MultiplayerResultsScreen(score, Room.RoomID.Value, PlaylistItem)
                {
                    IsLocalPlay = true,
                };
        }

        protected override void Dispose(bool isDisposing)
        {
            tagCoopEnabled = false;
            dodgeMultiplayerEnabled = false;
            rankedPlayCursorEnabled = false;

            base.Dispose(isDisposing);

            if (client.IsNotNull())
            {
                client.GameplayStarted -= onGameplayStarted;
                client.ResultsReady -= onResultsReady;
                client.VoteToSkipIntroPassed -= onVoteToSkipIntroPassed;
                client.VoteToSkipBreakPassed -= onVoteToSkipBreakPassed;
                client.MatchEvent -= onTagCoopMatchEvent;
                client.MatchEvent -= onDodgeMatchEvent;
                client.MatchEvent -= onRankedPlayMatchEvent;
            }

            if (SpectatorClient.IsNotNull())
                SpectatorClient.OnUserScoreProcessed -= onUserScoreProcessed;

            if (tagCoopGameplayController != null)
                tagCoopGameplayController.RemoteJudgementFeedback -= onTagCoopRemoteJudgementFeedback;

        }
    }
}
