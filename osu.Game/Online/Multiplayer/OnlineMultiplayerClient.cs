// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.Matchmaking;
using osu.Game.Online.Matchmaking.Requests;
using osu.Game.Online.Matchmaking.Responses;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Online.RankedPlay;
using osu.Game.Online.Rooms;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Online.Multiplayer
{
    /// <summary>
    /// A <see cref="MultiplayerClient"/> with online connectivity.
    /// </summary>
    public partial class OnlineMultiplayerClient : MultiplayerClient
    {
        private readonly string endpoint;

        private IHubClientConnector? connector;

        public override IBindable<bool> IsConnected { get; } = new BindableBool();

        private HubConnection? connection => connector?.CurrentConnection;

        /// <summary>
        /// Returns the current <see cref="HubConnection"/> if it is actually in the <see cref="HubConnectionState.Connected"/> state,
        /// or <c>null</c> otherwise. This is safer than checking <see cref="IsConnected"/> alone, as the bindable can be stale
        /// (it is updated asynchronously through a lock-protected disconnect flow, while the <see cref="HubConnection"/> state
        /// transitions synchronously).
        /// </summary>
        private HubConnection? activeConnection
        {
            get
            {
                if (!IsConnected.Value)
                    return null;

                var conn = connection;
                if (conn == null || conn.State != HubConnectionState.Connected)
                    return null;

                return conn;
            }
        }

        /// <summary>
        /// Invokes a hub method, gracefully handling the case where the connection drops between the state check and the invoke.
        /// </summary>
        private Task invokeAsync(string methodName)
            => invokeWithHandlingAsync(methodName, conn => conn.InvokeAsync(methodName));

        private Task invokeAsync<TArg>(string methodName, TArg arg)
            => invokeWithHandlingAsync(methodName, conn => conn.InvokeAsync(methodName, arg));

        private Task invokeAsync<TArg1, TArg2>(string methodName, TArg1 arg1, TArg2 arg2)
            => invokeWithHandlingAsync(methodName, conn => conn.InvokeAsync(methodName, arg1, arg2));

        private Task invokeAsync<TArg1, TArg2, TArg3>(string methodName, TArg1 arg1, TArg2 arg2, TArg3 arg3)
            => invokeWithHandlingAsync(methodName, conn => conn.InvokeAsync(methodName, arg1, arg2, arg3));

        private async Task invokeWithHandlingAsync(string methodName, Func<HubConnection, Task> invocation)
        {
            var conn = activeConnection;
            if (conn == null)
            {
                if (Room != null)
                    LeaveRoom().FireAndForget();
                return;
            }

            try
            {
                await invocation(conn).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Log($"{nameof(OnlineMultiplayerClient)} invoke of '{methodName}' failed: {ex.Message}", LoggingTarget.Network);
            }
            catch (HubException ex)
            {
                string message = ex.GetHubExceptionMessage();
                Logger.Log($"{nameof(OnlineMultiplayerClient)} invoke of '{methodName}' failed with hub error: {message}", LoggingTarget.Network, LogLevel.Important);

                if (isRoomNotFoundException(message) && Room != null)
                    LeaveRoom().FireAndForget();
            }
        }

        /// <summary>
        /// Invokes a hub method with a return value, gracefully handling the case where the connection drops between the state check and the invoke.
        /// </summary>
        private Task<TResult> invokeResultAsync<TResult>(string methodName, TResult fallback)
            => invokeWithHandlingAsync(methodName, fallback, conn => conn.InvokeAsync<TResult>(methodName));

        private Task<TResult> invokeResultAsync<TResult, TArg>(string methodName, TResult fallback, TArg arg)
            => invokeWithHandlingAsync(methodName, fallback, conn => conn.InvokeAsync<TResult>(methodName, arg));

        private Task<TResult> invokeResultAsync<TResult, TArg1, TArg2>(string methodName, TResult fallback, TArg1 arg1, TArg2 arg2)
            => invokeWithHandlingAsync(methodName, fallback, conn => conn.InvokeAsync<TResult>(methodName, arg1, arg2));

        private async Task<TResult> invokeWithHandlingAsync<TResult>(string methodName, TResult fallback, Func<HubConnection, Task<TResult>> invocation)
        {
            var conn = activeConnection;
            if (conn == null)
            {
                if (Room != null)
                    LeaveRoom().FireAndForget();
                return fallback;
            }

            try
            {
                return await invocation(conn).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Log($"{nameof(OnlineMultiplayerClient)} invoke of '{methodName}' failed: {ex.Message}", LoggingTarget.Network);
                return fallback;
            }
            catch (HubException ex)
            {
                string message = ex.GetHubExceptionMessage();
                Logger.Log($"{nameof(OnlineMultiplayerClient)} invoke of '{methodName}' failed with hub error: {message}", LoggingTarget.Network, LogLevel.Important);

                if (isRoomNotFoundException(message) && Room != null)
                    LeaveRoom().FireAndForget();

                return fallback;
            }
        }

        private static bool isRoomNotFoundException(string message)
        {
            return message.Contains("has not yet joined", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("NotJoinedRoomException", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("not in a room", StringComparison.OrdinalIgnoreCase)
                   || message.Contains("not joined", StringComparison.OrdinalIgnoreCase);
        }

        public OnlineMultiplayerClient(EndpointConfiguration endpoints)
        {
            endpoint = endpoints.MultiplayerUrl;
        }

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            // Importantly, we are intentionally not using MessagePack here to correctly support derived class serialization.
            // More information on the limitations / reasoning can be found in osu-server-spectator's initialisation code.
            connector = api.GetHubConnector(nameof(OnlineMultiplayerClient), endpoint);

            if (connector != null)
            {
                connector.ConfigureConnection = connection =>
                {
                    // this is kind of SILLY
                    // https://github.com/dotnet/aspnetcore/issues/15198
                    connection.On<MultiplayerRoomState>(nameof(IMultiplayerClient.RoomStateChanged), ((IMultiplayerClient)this).RoomStateChanged);
                    connection.On<MultiplayerRoomUser>(nameof(IMultiplayerClient.UserJoined), ((IMultiplayerClient)this).UserJoined);
                    connection.On<MultiplayerRoomUser>(nameof(IMultiplayerClient.UserLeft), ((IMultiplayerClient)this).UserLeft);
                    connection.On<MultiplayerRoomUser>(nameof(IMultiplayerClient.UserKicked), ((IMultiplayerClient)this).UserKicked);
                    connection.On<int, long, string>(nameof(IMultiplayerClient.Invited), ((IMultiplayerClient)this).Invited);
                    connection.On<int>(nameof(IMultiplayerClient.HostChanged), ((IMultiplayerClient)this).HostChanged);
                    connection.On<MultiplayerRoomSettings>(nameof(IMultiplayerClient.SettingsChanged), ((IMultiplayerClient)this).SettingsChanged);
                    connection.On<int, MultiplayerUserState>(nameof(IMultiplayerClient.UserStateChanged), ((IMultiplayerClient)this).UserStateChanged);
                    connection.On(nameof(IMultiplayerClient.LoadRequested), ((IMultiplayerClient)this).LoadRequested);
                    connection.On(nameof(IMultiplayerClient.GameplayStarted), ((IMultiplayerClient)this).GameplayStarted);
                    connection.On<GameplayAbortReason>(nameof(IMultiplayerClient.GameplayAborted), ((IMultiplayerClient)this).GameplayAborted);
                    connection.On(nameof(IMultiplayerClient.ResultsReady), ((IMultiplayerClient)this).ResultsReady);
                    connection.On<int, int?, int?>(nameof(IMultiplayerClient.UserStyleChanged), ((IMultiplayerClient)this).UserStyleChanged);
                    connection.On<int, IEnumerable<APIMod>>(nameof(IMultiplayerClient.UserModsChanged), ((IMultiplayerClient)this).UserModsChanged);
                    connection.On<int, BeatmapAvailability>(nameof(IMultiplayerClient.UserBeatmapAvailabilityChanged), ((IMultiplayerClient)this).UserBeatmapAvailabilityChanged);
                    connection.On<MatchRoomState>(nameof(IMultiplayerClient.MatchRoomStateChanged), ((IMultiplayerClient)this).MatchRoomStateChanged);
                    connection.On<int, MatchUserState>(nameof(IMultiplayerClient.MatchUserStateChanged), ((IMultiplayerClient)this).MatchUserStateChanged);
                    connection.On<MatchServerEvent>(nameof(IMultiplayerClient.MatchEvent), ((IMultiplayerClient)this).MatchEvent);
                    connection.On<MultiplayerPlaylistItem>(nameof(IMultiplayerClient.PlaylistItemAdded), ((IMultiplayerClient)this).PlaylistItemAdded);
                    connection.On<long>(nameof(IMultiplayerClient.PlaylistItemRemoved), ((IMultiplayerClient)this).PlaylistItemRemoved);
                    connection.On<MultiplayerPlaylistItem>(nameof(IMultiplayerClient.PlaylistItemChanged), ((IMultiplayerClient)this).PlaylistItemChanged);
                    connection.On<int, bool>(nameof(IMultiplayerClient.UserVotedToSkipIntro), ((IMultiplayerClient)this).UserVotedToSkipIntro);
                    connection.On(nameof(IMultiplayerClient.VoteToSkipIntroPassed), ((IMultiplayerClient)this).VoteToSkipIntroPassed);

                    connection.On(nameof(IMatchmakingClient.MatchmakingQueueJoined), ((IMatchmakingClient)this).MatchmakingQueueJoined);
                    connection.On(nameof(IMatchmakingClient.MatchmakingQueueLeft), ((IMatchmakingClient)this).MatchmakingQueueLeft);
                    connection.On<MatchmakingRoomInvitationParams>(nameof(IMatchmakingClient.MatchmakingRoomInvitedWithParams), ((IMatchmakingClient)this).MatchmakingRoomInvitedWithParams);
                    connection.On<MatchmakingDuelIssuedParams>(nameof(IMatchmakingClient.MatchmakingDuelIssued), ((IMatchmakingClient)this).MatchmakingDuelIssued);
                    connection.On<long, string>(nameof(IMatchmakingClient.MatchmakingRoomReady), ((IMatchmakingClient)this).MatchmakingRoomReady);
                    connection.On<MatchmakingLobbyStatus>(nameof(IMatchmakingClient.MatchmakingLobbyStatusChanged), ((IMatchmakingClient)this).MatchmakingLobbyStatusChanged);
                    connection.On<MatchmakingQueueStatus>(nameof(IMatchmakingClient.MatchmakingQueueStatusChanged), ((IMatchmakingClient)this).MatchmakingQueueStatusChanged);
                    connection.On<int, long>(nameof(IMatchmakingClient.MatchmakingItemSelected), ((IMatchmakingClient)this).MatchmakingItemSelected);
                    connection.On<int, long>(nameof(IMatchmakingClient.MatchmakingItemDeselected), ((IMatchmakingClient)this).MatchmakingItemDeselected);

                    connection.On<int, RankedPlayCardItem>(nameof(IRankedPlayClient.RankedPlayCardAdded), ((IRankedPlayClient)this).RankedPlayCardAdded);
                    connection.On<int, RankedPlayCardItem>(nameof(IRankedPlayClient.RankedPlayCardRemoved), ((IRankedPlayClient)this).RankedPlayCardRemoved);
                    connection.On<RankedPlayCardItem, MultiplayerPlaylistItem>(nameof(IRankedPlayClient.RankedPlayCardRevealed), ((IRankedPlayClient)this).RankedPlayCardRevealed);
                    connection.On<RankedPlayCardItem>(nameof(IRankedPlayClient.RankedPlayCardPlayed), ((IRankedPlayClient)this).RankedPlayCardPlayed);

                    connection.On(nameof(IStatefulUserHubClient.DisconnectRequested), ((IStatefulUserHubClient)this).DisconnectRequested);
                    connection.On(nameof(IStatefulUserHubClient.ServerShuttingDown), ((IStatefulUserHubClient)this).ServerShuttingDown);
                    connection.On<string, string>(nameof(IStatefulUserHubClient.SystemNotification), ((IStatefulUserHubClient)this).SystemNotification);
                };

                IsConnected.BindTo(connector.IsConnected);
            }
        }

        protected override async Task<MultiplayerRoom> CreateRoomInternal(MultiplayerRoom room)
        {
            var conn = activeConnection;

            if (conn == null)
                throw new OperationCanceledException();

            try
            {
                return await conn.InvokeAsync<MultiplayerRoom>(nameof(IMultiplayerServer.CreateRoom), room).ConfigureAwait(false);
            }
            catch (HubException exception)
            {
                if (exception.GetHubExceptionMessage() == HubClientConnector.SERVER_SHUTDOWN_MESSAGE)
                {
                    Debug.Assert(connector != null);

                    await connector.Reconnect().ConfigureAwait(false);
                    return await CreateRoomInternal(room).ConfigureAwait(false);
                }

                throw;
            }
        }

        protected override async Task<MultiplayerRoom> JoinRoomInternal(long roomId, string? password = null)
        {
            var conn = activeConnection;

            if (conn == null)
                throw new OperationCanceledException();

            try
            {
                return await conn.InvokeAsync<MultiplayerRoom>(nameof(IMultiplayerServer.JoinRoomWithPassword), roomId, password ?? string.Empty).ConfigureAwait(false);
            }
            catch (HubException exception)
            {
                if (exception.GetHubExceptionMessage() == HubClientConnector.SERVER_SHUTDOWN_MESSAGE)
                {
                    Debug.Assert(connector != null);

                    await connector.Reconnect().ConfigureAwait(false);
                    return await JoinRoomInternal(roomId, password).ConfigureAwait(false);
                }

                throw;
            }
        }

        protected override Task LeaveRoomInternal()
        {
            var conn = activeConnection;

            if (conn == null)
                return Task.FromCanceled(new CancellationToken(true));

            return conn.InvokeAsync(nameof(IMultiplayerServer.LeaveRoom));
        }

        public override async Task InvitePlayer(int userId)
        {
            var conn = activeConnection;

            if (conn == null)
                return;

            try
            {
                await conn.InvokeAsync(nameof(IMultiplayerServer.InvitePlayer), userId).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Log($"{nameof(OnlineMultiplayerClient)} invoke of '{nameof(IMultiplayerServer.InvitePlayer)}' failed: {ex.Message}", LoggingTarget.Network);
            }
            catch (HubException exception)
            {
                switch (exception.GetHubExceptionMessage())
                {
                    case UserBlockedException.MESSAGE:
                        PostNotification?.Invoke(new SimpleErrorNotification { Text = OnlinePlayStrings.InviteFailedUserBlocked });
                        break;

                    case UserBlocksPMsException.MESSAGE:
                        PostNotification?.Invoke(new SimpleErrorNotification { Text = OnlinePlayStrings.InviteFailedUserOptOut });
                        break;
                }
            }
        }

        public override Task TransferHost(int userId)
            => invokeAsync(nameof(IMultiplayerServer.TransferHost), userId);

        public override Task KickUser(int userId)
            => invokeAsync(nameof(IMultiplayerServer.KickUser), userId);

        public override Task ChangeSettings(MultiplayerRoomSettings settings)
            => invokeAsync(nameof(IMultiplayerServer.ChangeSettings), settings);

        public override Task ChangeState(MultiplayerUserState newState)
            => invokeAsync(nameof(IMultiplayerServer.ChangeState), newState);

        public override Task ChangeBeatmapAvailability(BeatmapAvailability newBeatmapAvailability)
            => invokeAsync(nameof(IMultiplayerServer.ChangeBeatmapAvailability), newBeatmapAvailability);

        public override Task ChangeUserStyle(int? beatmapId, int? rulesetId)
            => invokeAsync(nameof(IMultiplayerServer.ChangeUserStyle), beatmapId, rulesetId);

        public override Task ChangeUserMods(IEnumerable<APIMod> newMods)
            => invokeAsync(nameof(IMultiplayerServer.ChangeUserMods), newMods);

        public override Task SendMatchRequest(MatchUserRequest request)
            => invokeAsync(nameof(IMultiplayerServer.SendMatchRequest), request);

        public override Task StartMatch()
            => invokeAsync(nameof(IMultiplayerServer.StartMatch));

        public override Task AbortGameplay()
            => invokeAsync(nameof(IMultiplayerServer.AbortGameplay));

        public override Task AbortMatch()
            => invokeAsync(nameof(IMultiplayerServer.AbortMatch));

        public override Task AddPlaylistItem(MultiplayerPlaylistItem item)
            => invokeAsync(nameof(IMultiplayerServer.AddPlaylistItem), item);

        public override Task EditPlaylistItem(MultiplayerPlaylistItem item)
            => invokeAsync(nameof(IMultiplayerServer.EditPlaylistItem), item);

        public override Task RemovePlaylistItem(long playlistItemId)
            => invokeAsync(nameof(IMultiplayerServer.RemovePlaylistItem), playlistItemId);

        public override Task VoteToSkipIntro()
            => invokeAsync(nameof(IMultiplayerServer.VoteToSkipIntro));

        public override Task DiscardCards(RankedPlayCardItem[] cards)
            => invokeAsync(nameof(IRankedPlayServer.DiscardCards), cards);

        public override Task PlayCard(RankedPlayCardItem card)
            => invokeAsync(nameof(IRankedPlayServer.PlayCard), card);

        public override Task Surrender()
            => invokeAsync(nameof(IRankedPlayServer.Surrender));

        public override Task<MatchmakingPool[]> GetMatchmakingPoolsOfType(MatchmakingPoolType type)
            => invokeResultAsync(nameof(IMatchmakingServer.GetMatchmakingPoolsOfType), Array.Empty<MatchmakingPool>(), type);

        public override Task<MatchmakingJoinLobbyResponse> MatchmakingJoinLobbyWithParams(MatchmakingJoinLobbyRequest request)
            => invokeResultAsync(nameof(IMatchmakingServer.MatchmakingJoinLobbyWithParams), new MatchmakingJoinLobbyResponse(), request);

        public override Task MatchmakingLeaveLobby()
            => invokeAsync(nameof(IMatchmakingServer.MatchmakingLeaveLobby));

        public override async Task MatchmakingJoinQueue(int poolId, bool randomMods = false)
        {
            var conn = activeConnection;
            if (conn == null)
                return;

            try
            {
                if (randomMods)
                {
                    await conn.InvokeAsync(nameof(IMatchmakingServer.MatchmakingJoinQueueWithParams), new MatchmakingJoinQueueRequest
                    {
                        PoolId = poolId,
                        RandomMods = true
                    }).ConfigureAwait(false);
                }
                else
                {
                    await conn.InvokeAsync(nameof(IMatchmakingServer.MatchmakingJoinQueue), poolId).ConfigureAwait(false);
                }
            }
            catch (InvalidOperationException ex)
            {
                Logger.Log($"{nameof(OnlineMultiplayerClient)} invoke of '{nameof(IMatchmakingServer.MatchmakingJoinQueue)}' failed: {ex.Message}", LoggingTarget.Network);
            }
            catch (HubException ex)
            {
                Logger.Log($"{nameof(OnlineMultiplayerClient)} invoke of '{nameof(IMatchmakingServer.MatchmakingJoinQueue)}' failed with hub error: {ex.GetHubExceptionMessage()}", LoggingTarget.Network, LogLevel.Important);
            }
        }

        public override Task MatchmakingLeaveQueue()
            => invokeAsync(nameof(IMatchmakingServer.MatchmakingLeaveQueue));

        public override Task MatchmakingAcceptInvitation()
            => invokeAsync(nameof(IMatchmakingServer.MatchmakingAcceptInvitation));

        public override Task<MatchmakingIssueDuelResponse> MatchmakingIssueDuel(MatchmakingIssueDuelRequest request)
            => invokeResultAsync(nameof(IMatchmakingServer.MatchmakingIssueDuel), new MatchmakingIssueDuelResponse(), request);

        public override Task<MatchmakingAcceptDuelResponse> MatchmakingAcceptDuel(MatchmakingAcceptDuelRequest request)
            => invokeResultAsync(nameof(IMatchmakingServer.MatchmakingAcceptDuel), new MatchmakingAcceptDuelResponse(), request);

        public override Task MatchmakingDeclineInvitation()
            => invokeAsync(nameof(IMatchmakingServer.MatchmakingDeclineInvitation));

        public override Task MatchmakingToggleSelection(long playlistItemId)
            => invokeAsync(nameof(IMatchmakingServer.MatchmakingToggleSelection), playlistItemId);

        public override Task MatchmakingSkipToNextStage()
            => invokeAsync(nameof(IMatchmakingServer.MatchmakingSkipToNextStage));

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            connector?.Dispose();
        }

        public override async Task Reconnect()
        {
            if (connector != null)
                await connector.Reconnect().ConfigureAwait(false);
        }

        protected override async Task DisconnectInternal()
        {
            if (connector != null)
                await connector.Disconnect().ConfigureAwait(false);
        }
    }
}
