// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;

namespace osu.Game.Online.Spectator
{
    public partial class OnlineSpectatorClient : SpectatorClient
    {
        private readonly string endpoint;

        private IHubClientConnector? connector;

        public override IBindable<bool> IsConnected { get; } = new BindableBool();

        private HubConnection? connection => connector?.CurrentConnection;

        /// <summary>
        /// Returns the current <see cref="HubConnection"/> if it is actually in the <see cref="HubConnectionState.Connected"/> state,
        /// or <c>null</c> otherwise. This is safer than checking <see cref="IsConnected"/> alone, as the bindable can be stale.
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

        public OnlineSpectatorClient(EndpointConfiguration endpoints)
        {
            endpoint = endpoints.SpectatorUrl;
        }

        [BackgroundDependencyLoader]
        private void load(IAPIProvider api)
        {
            connector = api.GetHubConnector(nameof(SpectatorClient), endpoint);

            if (connector != null)
            {
                connector.ConfigureConnection = connection =>
                {
                    // until strong typed client support is added, each method must be manually bound
                    // (see https://github.com/dotnet/aspnetcore/issues/15198)
                    connection.On<int, SpectatorState>(nameof(ISpectatorClient.UserBeganPlaying), ((ISpectatorClient)this).UserBeganPlaying);
                    connection.On<int, FrameDataBundle>(nameof(ISpectatorClient.UserSentFrames), ((ISpectatorClient)this).UserSentFrames);
                    connection.On<int, SpectatorState>(nameof(ISpectatorClient.UserFinishedPlaying), ((ISpectatorClient)this).UserFinishedPlaying);
                    connection.On<int, long>(nameof(ISpectatorClient.UserScoreProcessed), ((ISpectatorClient)this).UserScoreProcessed);
                    connection.On<SpectatorUser[]>(nameof(ISpectatorClient.UserStartedWatching), ((ISpectatorClient)this).UserStartedWatching);
                    connection.On<int>(nameof(ISpectatorClient.UserEndedWatching), ((ISpectatorClient)this).UserEndedWatching);
                    connection.On(nameof(IStatefulUserHubClient.DisconnectRequested), ((IStatefulUserHubClient)this).DisconnectRequested);
                    connection.On(nameof(IStatefulUserHubClient.ServerShuttingDown), ((IStatefulUserHubClient)this).ServerShuttingDown);
                    connection.On<string, string>(nameof(IStatefulUserHubClient.SystemNotification), ((IStatefulUserHubClient)this).SystemNotification);
                };

                IsConnected.BindTo(connector.IsConnected);
            }
        }

        protected override async Task<bool> BeginPlayingInternal(long? scoreToken, SpectatorState state)
        {
            var conn = activeConnection;
            if (conn == null)
                return false;

            try
            {
                await conn.InvokeAsync(nameof(ISpectatorServer.BeginPlaySession), scoreToken, state).ConfigureAwait(false);
                return true;
            }
            catch (Exception exception)
            {
                if (exception.GetHubExceptionMessage() == HubClientConnector.SERVER_SHUTDOWN_MESSAGE)
                {
                    Debug.Assert(connector != null);

                    await connector.Reconnect().ConfigureAwait(false);
                    return await BeginPlayingInternal(scoreToken, state).ConfigureAwait(false);
                }

                // Exceptions can occur if, for instance, the locally played beatmap doesn't have a server-side counterpart.
                // For now, let's ignore these so they don't cause unobserved exceptions to appear to the user (and sentry),
                // but log to disk for diagnostic purposes.
                Logger.Log($"{nameof(OnlineSpectatorClient)}.{nameof(BeginPlayingInternal)} failed: {exception.Message}", LoggingTarget.Network);
                return false;
            }
        }

        protected override Task SendFramesInternal(FrameDataBundle bundle)
        {
            // Frame streaming to spectator hubs is disabled in the public build.
            return Task.CompletedTask;
        }

        protected override async Task EndPlayingInternal(SpectatorState state)
        {
            var conn = activeConnection;
            if (conn == null)
                return;

            try
            {
                await conn.InvokeAsync(nameof(ISpectatorServer.EndPlaySession), state).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Log($"{nameof(OnlineSpectatorClient)} invoke of '{nameof(ISpectatorServer.EndPlaySession)}' failed: {ex.Message}", LoggingTarget.Network);
            }
        }

        protected override async Task WatchUserInternal(int userId)
        {
            var conn = activeConnection;
            if (conn == null)
                return;

            try
            {
                await conn.InvokeAsync(nameof(ISpectatorServer.StartWatchingUser), userId).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Log($"{nameof(OnlineSpectatorClient)} invoke of '{nameof(ISpectatorServer.StartWatchingUser)}' failed: {ex.Message}", LoggingTarget.Network);
            }
        }

        protected override async Task StopWatchingUserInternal(int userId)
        {
            var conn = activeConnection;
            if (conn == null)
                return;

            try
            {
                await conn.InvokeAsync(nameof(ISpectatorServer.EndWatchingUser), userId).ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                Logger.Log($"{nameof(OnlineSpectatorClient)} invoke of '{nameof(ISpectatorServer.EndWatchingUser)}' failed: {ex.Message}", LoggingTarget.Network);
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
