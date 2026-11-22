// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;

namespace osu.Game.Online.Notifications.WebSocket
{
    /// <summary>
    /// A connector for <see cref="WebSocketNotificationsClient"/>s that receive events via a websocket.
    /// </summary>
    public class WebSocketNotificationsClientConnector : PersistentEndpointClientConnector, INotificationsClient
    {
        public event Action<SocketMessage>? MessageReceived;

        private readonly IAPIProvider api;

        public WebSocketNotificationsClientConnector(IAPIProvider api)
            : base(api)
        {
            this.api = api;
            Start();
        }

        protected override async Task<PersistentEndpointClient> BuildConnectionAsync(CancellationToken cancellationToken)
        {
            string endpoint = await GetEndpointAsync(api, cancellationToken).ConfigureAwait(false);

            ClientWebSocket socket = new ClientWebSocket();
            socket.Options.SetRequestHeader(@"Authorization", @$"Bearer {api.AccessToken}");
            socket.Options.Proxy = WebRequest.DefaultWebProxy;
            socket.Options.Proxy?.Credentials = CredentialCache.DefaultCredentials;

            var client = new WebSocketNotificationsClient(socket, endpoint);
            client.MessageReceived += msg => MessageReceived?.Invoke(msg);
            return client;
        }

        internal static async Task<string> GetEndpointAsync(IAPIProvider api, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var req = new GetNotificationsRequest();
            var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            req.Success += response =>
            {
                if (Uri.TryCreate(response.Endpoint, UriKind.Absolute, out var uri) && (uri.Scheme == "ws" || uri.Scheme == "wss"))
                    completion.TrySetResult(response.Endpoint);
                else
                    completion.TrySetException(new InvalidOperationException("Notifications response contains no valid WebSocket endpoint."));
            };
            req.Failure += exception =>
            {
                if (cancellationToken.IsCancellationRequested || exception is OperationCanceledException)
                    completion.TrySetCanceled();
                else
                    completion.TrySetException(exception);
            };

            using var registration = cancellationToken.Register(req.Cancel);
            // PerformAsync reports completion of the worker, not success of the request.
            // Success/failure callbacks are scheduled on the API's update thread.
            // Bypass Queue because this also runs during second-factor authentication.
            await api.PerformAsync(req).WaitAsync(cancellationToken).ConfigureAwait(false);
            return await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        public Task SendAsync(SocketMessage message, CancellationToken? cancellationToken = null)
        {
            if (CurrentConnection is not WebSocketNotificationsClient webSocketClient)
                return Task.CompletedTask;

            return webSocketClient.SendAsync(message, cancellationToken);
        }
    }
}
