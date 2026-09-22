// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Online.API;
using osuTK;

namespace osu.Game.Online.DodgeWorld
{
    public partial class OnlineDodgeWorldClient : DodgeWorldClient
    {
        private readonly string endpoint;

        private IHubClientConnector? connector;

        public override IBindable<bool> IsConnected { get; } = new BindableBool();

        private HubConnection? connection => connector?.CurrentConnection;

        /// <summary>
        /// The connection only when it is genuinely usable: <see cref="IsConnected"/> is a bindable and
        /// can be a frame or two behind the connection it describes.
        /// </summary>
        private HubConnection? activeConnection
        {
            get
            {
                if (!IsConnected.Value)
                    return null;

                HubConnection? current = connection;
                return current?.State == HubConnectionState.Connected ? current : null;
            }
        }

        public OnlineDodgeWorldClient(EndpointConfiguration endpoints)
        {
            endpoint = endpoints.DodgeWorldUrl;
        }

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            if (string.IsNullOrEmpty(endpoint))
                return;

            connector = api.GetHubConnector(nameof(DodgeWorldClient), endpoint);

            if (connector == null)
                return;

            connector.ConfigureConnection = newConnection =>
            {
                // Strongly typed clients are not supported, so every method is bound by hand.
                newConnection.On<int, DodgeWorldPlayerState>(nameof(IDodgeWorldClient.UserJoined), ((IDodgeWorldClient)this).UserJoined);
                newConnection.On<int>(nameof(IDodgeWorldClient.UserLeft), ((IDodgeWorldClient)this).UserLeft);
                newConnection.On<int, DodgeWorldPlayerState>(nameof(IDodgeWorldClient.UserStateChanged), ((IDodgeWorldClient)this).UserStateChanged);
                newConnection.On<DodgeWorldRoomSnapshot>(nameof(IDodgeWorldClient.RoomStateChanged), ((IDodgeWorldClient)this).RoomStateChanged);
                newConnection.On<int, int, string>(nameof(IDodgeWorldClient.MobDefeated), ((IDodgeWorldClient)this).MobDefeated);
                newConnection.On<DodgeWorldReward>(nameof(IDodgeWorldClient.RewardGranted), ((IDodgeWorldClient)this).RewardGranted);
                newConnection.On<int, float, float>(nameof(IDodgeWorldClient.UserAttacked), ((IDodgeWorldClient)this).UserAttacked);
                newConnection.On<int>(nameof(IDodgeWorldClient.HealthChanged), ((IDodgeWorldClient)this).HealthChanged);
                newConnection.On(nameof(IDodgeWorldClient.Died), ((IDodgeWorldClient)this).Died);
                newConnection.On(nameof(IStatefulUserHubClient.DisconnectRequested), ((IStatefulUserHubClient)this).DisconnectRequested);
                newConnection.On(nameof(IStatefulUserHubClient.ServerShuttingDown), ((IStatefulUserHubClient)this).ServerShuttingDown);
                newConnection.On<string, string>(nameof(IStatefulUserHubClient.SystemNotification), ((IStatefulUserHubClient)this).SystemNotification);
            };

            IsConnected.BindTo(connector.IsConnected);
        }

        protected override async Task<DodgeWorldRoomJoin> JoinRoomInternal(string roomId, DodgeWorldPlayerState state)
        {
            HubConnection? conn = activeConnection;

            if (conn == null)
                return new DodgeWorldRoomJoin();

            return await conn.InvokeAsync<DodgeWorldRoomJoin>(nameof(IDodgeWorldServer.JoinRoom), roomId, state).ConfigureAwait(false);
        }

        protected override async Task AttackInternal(Vector2 direction)
        {
            HubConnection? conn = activeConnection;

            if (conn == null)
                return;

            try
            {
                await conn.SendAsync(nameof(IDodgeWorldServer.Attack), direction.X, direction.Y).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                Logger.Log($"{nameof(OnlineDodgeWorldClient)} send of '{nameof(IDodgeWorldServer.Attack)}' failed: {exception.Message}",
                    LoggingTarget.Network);
            }
        }

        protected override async Task LeaveRoomInternal()
        {
            HubConnection? conn = activeConnection;

            if (conn == null)
                return;

            try
            {
                await conn.InvokeAsync(nameof(IDodgeWorldServer.LeaveRoom)).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                Logger.Log($"{nameof(OnlineDodgeWorldClient)} invoke of '{nameof(IDodgeWorldServer.LeaveRoom)}' failed: {exception.Message}",
                    LoggingTarget.Network);
            }
        }

        protected override async Task UpdateStateInternal(DodgeWorldPlayerState state)
        {
            HubConnection? conn = activeConnection;

            if (conn == null)
                return;

            try
            {
                // Sent rather than invoked: a position is worthless by the time a round trip confirms it.
                await conn.SendAsync(nameof(IDodgeWorldServer.UpdateState), state).ConfigureAwait(false);
            }
            catch (InvalidOperationException exception)
            {
                Logger.Log($"{nameof(OnlineDodgeWorldClient)} send of '{nameof(IDodgeWorldServer.UpdateState)}' failed: {exception.Message}",
                    LoggingTarget.Network);
            }
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
