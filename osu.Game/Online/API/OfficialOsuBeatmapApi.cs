// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Online.Notifications.WebSocket;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Online.API
{
    public enum OfficialOsuBeatmapApiState
    {
        Disabled,
        TokenMissing,
        Connecting,
        Connected,
        AuthenticationFailed,
        NetworkUnavailable,
    }

    /// <summary>
    /// A capability-limited official osu! API client.
    /// It can only execute beatmap catalogue/download requests and never creates chat,
    /// notifications, metadata, spectator or multiplayer connections.
    /// </summary>
    public partial class OfficialOsuBeatmapApi : Component, IAPIProvider
    {
        private const double credential_poll_interval = 5000;

        private readonly OsuConfigManager config;
        private readonly OAuth authentication;
        private readonly Bindable<bool> enabled;
        private readonly MinimalLocalUserState localUserState = new MinimalLocalUserState();
        private readonly Bindable<APIState> apiState = new Bindable<APIState>(APIState.Offline);
        private readonly Bindable<string?> outageMessage = new Bindable<string?>();

        private bool refreshInProgress;
        private bool suppressTokenWrite;
        private double lastCredentialPoll;
        private string observedToken = string.Empty;
        private Action<Notification>? postNotification;
        private (string Key, LocalisableString Message)? pendingFailureNotification;

        public readonly Bindable<OfficialOsuBeatmapApiState> ConnectionState = new Bindable<OfficialOsuBeatmapApiState>(OfficialOsuBeatmapApiState.Disabled);
        public readonly Bindable<string> AccountUsername = new Bindable<string>(string.Empty);
        public readonly Bindable<string> LastError = new Bindable<string>(string.Empty);

        public bool IsAvailable => enabled.Value && ConnectionState.Value == OfficialOsuBeatmapApiState.Connected;

        public OfficialOsuBeatmapApi(OsuConfigManager config)
        {
            this.config = config;
            enabled = config.GetBindable<bool>(OsuSetting.ForkUseOfficialBeatmapService);

            Endpoints = new OfficialOsuEndpointConfiguration();
            authentication = new OAuth(Endpoints.APIClientID, Endpoints.APIClientSecret, Endpoints.APIUrl);
            authentication.Token.ValueChanged += tokenChanged;
        }

        public Action<Notification>? PostNotification
        {
            set
            {
                postNotification = value;
                dispatchPendingFailureNotification();
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            enabled.BindValueChanged(change =>
            {
                if (change.NewValue)
                    RefreshCredentials();
                else
                    setDisabled();
            }, true);
        }

        protected override void Update()
        {
            base.Update();

            if (!enabled.Value || refreshInProgress || Time.Current - lastCredentialPoll < credential_poll_interval)
                return;

            lastCredentialPoll = Time.Current;
            var credentials = config.ReadOriginalGameCredentials();

            if (!string.Equals(credentials.Token, observedToken, StringComparison.Ordinal))
                beginValidation(credentials.Username, credentials.Token);
        }

        public void RefreshCredentials()
        {
            if (!enabled.Value)
            {
                setDisabled();
                return;
            }

            var credentials = config.ReadOriginalGameCredentials();
            beginValidation(credentials.Username, credentials.Token);
        }

        private void beginValidation(string configuredUsername, string token)
        {
            if (refreshInProgress)
                return;

            observedToken = token;
            AccountUsername.Value = configuredUsername;
            LastError.Value = string.Empty;

            if (string.IsNullOrWhiteSpace(token))
            {
                authentication.Clear();
                localUserState.SetUser(new GuestUser());
                setFailure(OfficialOsuBeatmapApiState.TokenMissing, ForkSettingsStrings.OfficialOsuTokenMissing);
                return;
            }

            suppressTokenWrite = true;
            authentication.TokenString = token;
            suppressTokenWrite = false;

            if (authentication.Token.Value == null)
            {
                localUserState.SetUser(new GuestUser());
                setFailure(OfficialOsuBeatmapApiState.AuthenticationFailed, ForkSettingsStrings.OfficialOsuTokenInvalid);
                return;
            }

            refreshInProgress = true;
            ConnectionState.Value = OfficialOsuBeatmapApiState.Connecting;
            apiState.Value = APIState.Connecting;

            var request = new GetMeRequest();
            request.Success += me =>
            {
                refreshInProgress = false;
                AccountUsername.Value = me.Username;
                localUserState.SetUser(me);
                LastError.Value = string.Empty;
                ConnectionState.Value = OfficialOsuBeatmapApiState.Connected;
                apiState.Value = APIState.Online;
                pendingFailureNotification = null;
                config.SetValue(OsuSetting.ForkOfficialOsuFailureNotificationKey, string.Empty);
            };
            request.Failure += error =>
            {
                refreshInProgress = false;
                localUserState.SetUser(new GuestUser());

                if (isNetworkFailure(error))
                    setFailure(OfficialOsuBeatmapApiState.NetworkUnavailable, ForkSettingsStrings.OfficialOsuNetworkUnavailable, error);
                else
                    setFailure(OfficialOsuBeatmapApiState.AuthenticationFailed, ForkSettingsStrings.OfficialOsuAuthenticationFailed, error);
            };

            Task.Run(() => performRequest(request));
        }

        private void tokenChanged(ValueChangedEvent<OAuthToken> change)
        {
            if (suppressTokenWrite || change.NewValue == null)
                return;

            string updatedToken = change.NewValue.ToString();

            if (string.IsNullOrWhiteSpace(updatedToken) || string.Equals(updatedToken, observedToken, StringComparison.Ordinal))
                return;

            if (config.TryWriteOriginalGameToken(updatedToken))
                observedToken = updatedToken;
        }

        private void setDisabled()
        {
            refreshInProgress = false;
            ConnectionState.Value = OfficialOsuBeatmapApiState.Disabled;
            apiState.Value = APIState.Offline;
            LastError.Value = string.Empty;
            localUserState.SetUser(new GuestUser());
        }

        private void setFailure(OfficialOsuBeatmapApiState state, LocalisableString message, Exception? error = null)
        {
            ConnectionState.Value = state;
            apiState.Value = APIState.Offline;
            LastError.Value = error?.Message ?? message.ToString();

            string keyMaterial = $"{state}:{observedToken}";
            string key = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(keyMaterial)));
            pendingFailureNotification = (key, message);
            dispatchPendingFailureNotification();
        }

        private void dispatchPendingFailureNotification()
        {
            if (postNotification == null || pendingFailureNotification == null)
                return;

            var pending = pendingFailureNotification.Value;

            if (string.Equals(config.Get<string>(OsuSetting.ForkOfficialOsuFailureNotificationKey), pending.Key, StringComparison.Ordinal))
                return;

            config.SetValue(OsuSetting.ForkOfficialOsuFailureNotificationKey, pending.Key);
            postNotification(new SimpleNotification
            {
                Text = pending.Message,
                IsImportant = true,
            });
        }

        private static bool isNetworkFailure(Exception error)
        {
            for (Exception? current = error; current != null; current = current.InnerException)
            {
                if (current is SocketException or HttpRequestException or TimeoutException)
                    return true;

                if (current is WebException webException
                    && webException.Status is WebExceptionStatus.ConnectFailure
                        or WebExceptionStatus.ConnectionClosed
                        or WebExceptionStatus.NameResolutionFailure
                        or WebExceptionStatus.ProxyNameResolutionFailure
                        or WebExceptionStatus.ReceiveFailure
                        or WebExceptionStatus.SendFailure
                        or WebExceptionStatus.Timeout)
                    return true;
            }

            return false;
        }

        private static bool isAuthenticationFailure(Exception error)
        {
            for (Exception? current = error; current != null; current = current.InnerException)
            {
                if (current is APIException apiException
                    && apiException.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                    return true;

                if (current is WebException webException
                    && (webException.Message == "Unauthorized" || webException.Message == "Forbidden"))
                    return true;
            }

            return false;
        }

        private bool requestAllowed(APIRequest request) =>
            request is GetMeRequest
                or SearchBeatmapSetsRequest
                or GetBeatmapSetRequest
                or GetBeatmapRequest
                or GetBeatmapsRequest
                or DownloadBeatmapSetRequest;

        private void performRequest(APIRequest request)
        {
            request.AttachAPI(this);

            if (!requestAllowed(request))
            {
                request.Fail(new NotSupportedException($"Official osu! beatmap API does not allow {request.GetType().Name}."));
                return;
            }

            if (request is not GetMeRequest)
            {
                request.Failure += error =>
                {
                    if (isNetworkFailure(error))
                    {
                        setFailure(OfficialOsuBeatmapApiState.NetworkUnavailable, ForkSettingsStrings.OfficialOsuNetworkUnavailable, error);
                        return;
                    }

                    if (isAuthenticationFailure(error))
                        RefreshCredentials();
                };
            }

            try
            {
                request.Perform();
            }
            catch (Exception error)
            {
                request.Fail(error);
            }
        }

        public void Queue(APIRequest request)
        {
            if (!IsAvailable)
            {
                request.AttachAPI(this);
                request.Fail(new InvalidOperationException("Official osu! beatmap API is not connected."));
                return;
            }

            Task.Run(() => performRequest(request));
        }

        public void Perform(APIRequest request)
        {
            if (!IsAvailable && request is not GetMeRequest)
            {
                request.AttachAPI(this);
                request.Fail(new InvalidOperationException("Official osu! beatmap API is not connected."));
                return;
            }

            performRequest(request);
        }

        public Task PerformAsync(APIRequest request)
        {
            if (!IsAvailable)
            {
                request.AttachAPI(this);
                request.Fail(new InvalidOperationException("Official osu! beatmap API is not connected."));
                return Task.CompletedTask;
            }

            return Task.Run(() => performRequest(request));
        }

        void IAPIProvider.Schedule(Action action) => Schedule(action);

        public IBindable<APIUser> LocalUser => localUserState.User;
        public ILocalUserState LocalUserState => localUserState;
        public string ScoreProcessingNoticeUrl => string.Empty;
        public Language Language => Language.en;
        public string AccessToken => enabled.Value ? authentication.RequestAccessToken() : string.Empty;
        public Guid SessionIdentifier { get; } = Guid.NewGuid();
        public bool IsLoggedIn => IsAvailable;
        public string ProvidedUsername => AccountUsername.Value;
        public EndpointConfiguration Endpoints { get; }
        public int APIVersion => int.Parse(DateTime.UtcNow.ToString("yyyyMMdd"));
        public Exception? LastLoginError => null;
        public IBindable<APIState> State => apiState;
        public IBindable<string?> UserFacingOutageMessage => outageMessage;
        public SessionVerificationMethod? SessionVerificationMethod => null;
        public INotificationsClient NotificationsClient { get; } = new DummyNotificationsClient();

        public void Login(string username, string password) => throw new NotSupportedException();
        public void AuthenticateSecondFactor(string code) => throw new NotSupportedException();
        public void Logout() => setDisabled();
        public IHubClientConnector? GetHubConnector(string clientName, string endpoint) => null;
        public IChatClient GetChatClient() => new NoOpChatClient();
        public RegistrationRequest.RegistrationRequestErrors? CreateAccount(string email, string username, string password) => throw new NotSupportedException();

        private sealed class MinimalLocalUserState : ILocalUserState
        {
            public Bindable<APIUser> User { get; } = new Bindable<APIUser>(new GuestUser());
            public IBindableList<APIRelation> Friends { get; } = new BindableList<APIRelation>();
            public IBindableList<APIRelation> Blocks { get; } = new BindableList<APIRelation>();
            public IBindableList<int> FavouriteBeatmapSets { get; } = new BindableList<int>();

            IBindable<APIUser> ILocalUserState.User => User;

            public void SetUser(APIUser user) => User.Value = user;
            public void UpdateFriends() { }
            public void UpdateBlocks() { }
            public void UpdateFavouriteBeatmapSets() { }
        }

        private sealed class NoOpChatClient : IChatClient
        {
            public event Action<Channel>? ChannelJoined
            {
                add { }
                remove { }
            }

            public event Action<Channel>? ChannelParted
            {
                add { }
                remove { }
            }

            public event Action<List<Message>>? NewMessages
            {
                add { }
                remove { }
            }

            public event Action? PresenceReceived
            {
                add { }
                remove { }
            }

            public void RequestPresence()
            {
                // Intentionally do nothing. Presence must never be requested from the official service.
            }

            public void Dispose()
            {
            }
        }
    }
}
