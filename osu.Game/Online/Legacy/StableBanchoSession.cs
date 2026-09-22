// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Game.Online.API;

namespace osu.Game.Online.Legacy
{
    public partial class StableBanchoSession : Component
    {
        private static readonly TimeSpan poll_interval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan retry_interval = TimeSpan.FromSeconds(10);

        private readonly StableScoreSubmissionClient client;
        private readonly IAPIProvider api;
        private readonly ServerProfile profile;
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

        public Bindable<string> Status { get; } = new Bindable<string>("Not connected");
        public BindableBool IsConnected { get; } = new BindableBool();
        public BindableList<StableBanchoMatch> Matches { get; } = new BindableList<StableBanchoMatch>();
        public BindableDictionary<int, StableBanchoUserPresence> Users { get; } = new BindableDictionary<int, StableBanchoUserPresence>();
        public BindableList<StableBanchoChannel> Channels { get; } = new BindableList<StableBanchoChannel>();
        public BindableList<StableBanchoMessage> ChatMessages { get; } = new BindableList<StableBanchoMessage>();
        public Bindable<StableBanchoMatch?> CurrentMatch { get; } = new Bindable<StableBanchoMatch?>();
        public BindableBool IsInLobby { get; } = new BindableBool();

        public event Action? MatchStarted;
        public event Action<StableBanchoMatch>? MatchJoined;
        public event Action<StableBanchoMatch>? MatchUpdated;
        public event Action? AllPlayersLoaded;
        public event Action? MatchCompleted;
        public event Action? MatchAborted;
        public event Action? MatchSkipPassed;
        public event Action? MatchJoinFailed;
        public event Action<int>? MatchPlayerFailed;
        public event Action<StableBanchoScoreFrame>? MatchScoreUpdated;
        public event Action<StableBanchoMessage>? ChatMessageReceived;
        public event Action<StableBanchoChannel>? ChannelInfoReceived;
        public event Action? ChannelListReceived;
        public event Action<string>? ChannelJoined;
        public event Action<string>? ChannelParted;
        public event Action<string>? NotificationReceived;
        public event Action<StableBanchoUserStatistics>? UserStatisticsUpdated;

        private readonly object matchScoresLock = new object();
        private readonly Dictionary<int, StableBanchoMatchScore> latestMatchScores = new Dictionary<int, StableBanchoMatchScore>();
        private readonly Dictionary<(int UserId, int RulesetId), StableBanchoUserStatistics> userStatistics = new Dictionary<(int, int), StableBanchoUserStatistics>();

        public StableBanchoSession(StableScoreSubmissionClient client, IAPIProvider api, ServerProfile profile)
        {
            this.client = client;
            this.api = api;
            this.profile = profile;
            client.BanchoPacketsReceived += packetsReceived;
        }

        public string GetAvatarUrl(int userId) => client.GetAvatarUrl(userId);

        public IReadOnlyList<StableBanchoMatchScore> GetLatestMatchScores()
        {
            lock (matchScoresLock)
                return latestMatchScores.Values.ToArray();
        }

        public StableBanchoUserStatistics? GetUserStatistics(int userId, int rulesetId)
            => userStatistics.GetValueOrDefault((userId, rulesetId));

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (profile.UseStableProtocol)
                _ = Task.Run(() => run(cancellation.Token));
        }

        public async Task<StableBanchoLoginResult> LoginNowAsync(CancellationToken cancellationToken = default)
        {
            Schedule(() => Status.Value = "Connecting...");
            client.InvalidateBanchoSession();
            StableBanchoLoginResult login = await client.LoginAsync(cancellationToken).ConfigureAwait(false);
            setConnected(login);
            return login;
        }

        private async Task run(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    Schedule(() => Status.Value = "Connecting...");
                    StableBanchoLoginResult login = await client.LoginAsync(cancellationToken).ConfigureAwait(false);
                    setConnected(login);

                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(poll_interval, cancellationToken).ConfigureAwait(false);
                        await client.PollAsync(cancellationToken).ConfigureAwait(false);
                        setConnected(login);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    client.InvalidateBanchoSession();
                    Logger.Error(exception, "Stable Bancho session lost; reconnecting.");
                    Schedule(() =>
                    {
                        IsConnected.Value = false;
                        IsInLobby.Value = false;
                        CurrentMatch.Value = null;
                        Matches.Clear();
                        Channels.Clear();
                        Status.Value = $"Disconnected: {exception.Message}";
                    });

                    try
                    {
                        await Task.Delay(retry_interval, cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }
            }
        }

        private void setConnected(StableBanchoLoginResult login) => Schedule(() =>
        {
            IsConnected.Value = true;
            Status.Value = $"Online as {login.Username} (#{login.UserId})";

            if (api.LocalUserState is LocalUserState localUserState)
                localUserState.SetStableLocalUser(login.UserId, login.Username, profile.BanchoUrl);
        });

        public async Task JoinLobbyAsync(CancellationToken cancellationToken = default)
        {
            await client.SendBanchoPacketsAsync([new StableBanchoPacket(StableBanchoClientPacket.JoinLobby)], cancellationToken).ConfigureAwait(false);
            Schedule(() => IsInLobby.Value = true);
        }

        public async Task LeaveLobbyAsync(CancellationToken cancellationToken = default)
        {
            await client.SendBanchoPacketsAsync([new StableBanchoPacket(StableBanchoClientPacket.PartLobby)], cancellationToken).ConfigureAwait(false);
            Schedule(() =>
            {
                IsInLobby.Value = false;
                Matches.Clear();
            });
        }

        public async Task JoinMatchAsync(int matchId, string password = "", CancellationToken cancellationToken = default)
        {
            await client.SendBanchoPacketsAsync([
                new StableBanchoPacket(StableBanchoClientPacket.PartLobby),
                StableBanchoPacketCodec.CreateJoinMatchPacket(matchId, password),
            ], cancellationToken).ConfigureAwait(false);
            Schedule(() => IsInLobby.Value = false);
        }

        public async Task CreateMatchAsync(StableBanchoMatch match, CancellationToken cancellationToken = default)
        {
            await client.SendBanchoPacketsAsync([
                new StableBanchoPacket(StableBanchoClientPacket.PartLobby),
                StableBanchoPacketCodec.CreateMatchPacket(StableBanchoClientPacket.CreateMatch, match),
            ], cancellationToken).ConfigureAwait(false);
            Schedule(() => IsInLobby.Value = false);
        }

        public async Task LeaveMatchAsync(CancellationToken cancellationToken = default)
        {
            await sendAsync(new StableBanchoPacket(StableBanchoClientPacket.PartMatch), cancellationToken).ConfigureAwait(false);
            Schedule(() => CurrentMatch.Value = null);
        }

        public Task SetReadyAsync(bool ready, CancellationToken cancellationToken = default)
            => sendAsync(new StableBanchoPacket(ready ? StableBanchoClientPacket.MatchReady : StableBanchoClientPacket.MatchNotReady), cancellationToken);

        public Task SetBeatmapAvailableAsync(bool available, CancellationToken cancellationToken = default)
            => sendAsync(new StableBanchoPacket(available ? StableBanchoClientPacket.MatchHasBeatmap : StableBanchoClientPacket.MatchNoBeatmap), cancellationToken);

        public Task StartMatchAsync(CancellationToken cancellationToken = default)
            => sendAsync(new StableBanchoPacket(StableBanchoClientPacket.MatchStart), cancellationToken);

        public Task CompleteLoadingAsync(CancellationToken cancellationToken = default)
            => sendAsync(new StableBanchoPacket(StableBanchoClientPacket.MatchLoadComplete), cancellationToken);

        public Task CompleteMatchAsync(CancellationToken cancellationToken = default)
            => sendAsync(new StableBanchoPacket(StableBanchoClientPacket.MatchComplete), cancellationToken);

        public Task FailMatchAsync(CancellationToken cancellationToken = default)
            => sendAsync(new StableBanchoPacket(StableBanchoClientPacket.MatchFailed), cancellationToken);

        public Task RequestSkipAsync(CancellationToken cancellationToken = default)
            => sendAsync(new StableBanchoPacket(StableBanchoClientPacket.MatchSkipRequest), cancellationToken);

        public Task SendScoreFrameAsync(StableBanchoScoreFrame frame, CancellationToken cancellationToken = default)
            => sendAsync(StableBanchoPacketCodec.CreateScoreFramePacket(frame), cancellationToken);

        public Task SendChannelMessageAsync(string channel, string message, CancellationToken cancellationToken = default)
        {
            Logger.Log($"Stable Bancho chat send: target={channel}, length={message.Length}.", LoggingTarget.Network);
            return sendAsync(StableBanchoPacketCodec.CreatePublicMessagePacket(message, channel), cancellationToken);
        }

        public Task SendPrivateMessageAsync(string username, string message, CancellationToken cancellationToken = default)
        {
            Logger.Log($"Stable Bancho private chat send: target={username}, length={message.Length}.", LoggingTarget.Network);
            return sendAsync(StableBanchoPacketCodec.CreatePrivateMessagePacket(message, username), cancellationToken);
        }

        public Task JoinChannelAsync(string channel, CancellationToken cancellationToken = default)
            => sendAsync(StableBanchoPacketCodec.CreateStringPacket(StableBanchoClientPacket.ChannelJoin, channel), cancellationToken);

        public Task LeaveChannelAsync(string channel, CancellationToken cancellationToken = default)
            => sendAsync(StableBanchoPacketCodec.CreateStringPacket(StableBanchoClientPacket.ChannelPart, channel), cancellationToken);

        public Task ChangeSlotAsync(int slot, CancellationToken cancellationToken = default)
            => sendAsync(StableBanchoPacketCodec.CreateInt32Packet(StableBanchoClientPacket.MatchChangeSlot, slot), cancellationToken);

        public Task TransferHostAsync(int slot, CancellationToken cancellationToken = default)
            => sendAsync(StableBanchoPacketCodec.CreateInt32Packet(StableBanchoClientPacket.MatchTransferHost, slot), cancellationToken);

        public Task ChangeModsAsync(int legacyMods, CancellationToken cancellationToken = default)
            => sendAsync(StableBanchoPacketCodec.CreateInt32Packet(StableBanchoClientPacket.MatchChangeMods, legacyMods), cancellationToken);

        public Task ChangeTeamAsync(CancellationToken cancellationToken = default)
            => sendAsync(new StableBanchoPacket(StableBanchoClientPacket.MatchChangeTeam), cancellationToken);

        public Task ChangeMatchSettingsAsync(StableBanchoMatch match, CancellationToken cancellationToken = default)
            => sendAsync(StableBanchoPacketCodec.CreateMatchPacket(StableBanchoClientPacket.MatchChangeSettings, match), cancellationToken);

        public Task ChangeMatchPasswordAsync(StableBanchoMatch match, CancellationToken cancellationToken = default)
            => sendAsync(StableBanchoPacketCodec.CreateMatchPacket(StableBanchoClientPacket.MatchChangePassword, match), cancellationToken);

        public Task LockSlotAsync(int slot, CancellationToken cancellationToken = default)
            => sendAsync(StableBanchoPacketCodec.CreateInt32Packet(StableBanchoClientPacket.MatchLock, slot), cancellationToken);

        public Task InvitePlayerAsync(int userId, CancellationToken cancellationToken = default)
            => sendAsync(StableBanchoPacketCodec.CreateInt32Packet(StableBanchoClientPacket.MatchInvite, userId), cancellationToken);

        private async Task sendAsync(StableBanchoPacket packet, CancellationToken cancellationToken)
            => await client.SendBanchoPacketsAsync([packet], cancellationToken).ConfigureAwait(false);

        private void packetsReceived(IReadOnlyList<StableBanchoPacket> packets) => Schedule(() => handlePackets(packets));

        private void handlePackets(IReadOnlyList<StableBanchoPacket> packets)
        {
            foreach (var packet in packets)
            {
                try
                {
                    switch ((StableBanchoServerPacket)packet.Id)
                    {
                        case StableBanchoServerPacket.NewMatch:
                        case StableBanchoServerPacket.UpdateMatch:
                        {
                            StableBanchoMatch match = StableBanchoPacketCodec.DecodeMatch(packet.Payload);
                            upsertMatch(match);
                            MatchUpdated?.Invoke(match.Clone());
                            break;
                        }

                        case StableBanchoServerPacket.DisposeMatch:
                            removeMatch(StableBanchoPacketCodec.DecodeInt32(packet.Payload));
                            break;

                        case StableBanchoServerPacket.MatchJoinSuccess:
                        {
                            StableBanchoMatch match = StableBanchoPacketCodec.DecodeMatch(packet.Payload);
                            upsertMatch(match);
                            CurrentMatch.Value = match.Clone();
                            IsInLobby.Value = false;
                            MatchJoined?.Invoke(match.Clone());
                            break;
                        }

                        case StableBanchoServerPacket.MatchJoinFail:
                            MatchJoinFailed?.Invoke();
                            break;

                        case StableBanchoServerPacket.MatchStart:
                            CurrentMatch.Value = StableBanchoPacketCodec.DecodeMatch(packet.Payload);
                            lock (matchScoresLock)
                                latestMatchScores.Clear();
                            MatchStarted?.Invoke();
                            break;

                        case StableBanchoServerPacket.MatchAllPlayersLoaded:
                            AllPlayersLoaded?.Invoke();
                            break;

                        case StableBanchoServerPacket.MatchPlayerFailed:
                            MatchPlayerFailed?.Invoke(StableBanchoPacketCodec.DecodeInt32(packet.Payload));
                            break;

                        case StableBanchoServerPacket.MatchScoreUpdate:
                        {
                            StableBanchoScoreFrame frame = StableBanchoPacketCodec.DecodeScoreFrame(packet.Payload);
                            StableBanchoMatch? match = CurrentMatch.Value;

                            if (match != null && frame.PlayerSlot < match.SlotUserIds.Length && match.SlotUserIds[frame.PlayerSlot] is int userId)
                            {
                                Users.TryGetValue(userId, out StableBanchoUserPresence? presence);
                                lock (matchScoresLock)
                                {
                                    latestMatchScores[userId] = new StableBanchoMatchScore
                                    {
                                        UserId = userId,
                                        Username = presence?.Username ?? $"User {userId}",
                                        Frame = frame,
                                    };
                                }
                            }

                            MatchScoreUpdated?.Invoke(frame);
                            break;
                        }

                        case StableBanchoServerPacket.MatchSkip:
                            MatchSkipPassed?.Invoke();
                            break;

                        case StableBanchoServerPacket.MatchComplete:
                            MatchCompleted?.Invoke();
                            break;

                        case StableBanchoServerPacket.MatchAbort:
                            MatchAborted?.Invoke();
                            break;

                        case StableBanchoServerPacket.Notification:
                        {
                            string message = StableBanchoPacketCodec.DecodeString(packet.Payload);
                            NotificationReceived?.Invoke(message);
                            Logger.Log($"Stable Bancho notification: {message}", LoggingTarget.Network);
                            break;
                        }

                        case StableBanchoServerPacket.SendMessage:
                        {
                            StableBanchoMessage message = StableBanchoPacketCodec.DecodeMessage(packet.Payload);
                            Logger.Log($"Stable Bancho chat receive: sender={message.Sender} ({message.SenderId}), target={message.Target}, length={message.Content.Length}.", LoggingTarget.Network);
                            ChatMessages.Add(message);
                            if (ChatMessages.Count > 300)
                                ChatMessages.RemoveAt(0);
                            ChatMessageReceived?.Invoke(message);
                            break;
                        }

                        case StableBanchoServerPacket.ChannelInfo:
                        {
                            StableBanchoChannel channel = StableBanchoPacketCodec.DecodeChannel(packet.Payload);
                            StableBanchoChannel? existing = Channels.FirstOrDefault(candidate => string.Equals(candidate.Name, channel.Name, StringComparison.OrdinalIgnoreCase));
                            if (existing?.IsJoined == true)
                                channel = channel.WithJoined(true);
                            upsertChannel(channel);
                            ChannelInfoReceived?.Invoke(channel);
                            break;
                        }

                        case StableBanchoServerPacket.ChannelAutoJoin:
                        {
                            StableBanchoChannel channel = StableBanchoPacketCodec.DecodeChannel(packet.Payload).WithJoined(true);
                            upsertChannel(channel);
                            ChannelInfoReceived?.Invoke(channel);
                            ChannelJoined?.Invoke(channel.Name);
                            break;
                        }

                        case StableBanchoServerPacket.ChannelJoinSuccess:
                        {
                            string channelName = StableBanchoPacketCodec.DecodeString(packet.Payload);
                            StableBanchoChannel channel = Channels.FirstOrDefault(candidate => string.Equals(candidate.Name, channelName, StringComparison.OrdinalIgnoreCase))
                                                          ?? new StableBanchoChannel { Name = channelName };
                            upsertChannel(channel.WithJoined(true));
                            ChannelJoined?.Invoke(channelName);
                            break;
                        }

                        case StableBanchoServerPacket.ChannelKick:
                        {
                            string channelName = StableBanchoPacketCodec.DecodeString(packet.Payload);
                            StableBanchoChannel? channel = Channels.FirstOrDefault(candidate => string.Equals(candidate.Name, channelName, StringComparison.OrdinalIgnoreCase));
                            if (channel != null)
                                upsertChannel(channel.WithJoined(false));
                            ChannelParted?.Invoke(channelName);
                            break;
                        }

                        case StableBanchoServerPacket.ChannelInfoEnd:
                            ChannelListReceived?.Invoke();
                            break;

                        case StableBanchoServerPacket.UserPresence:
                        {
                            StableBanchoUserPresence presence = StableBanchoPacketCodec.DecodeUserPresence(packet.Payload);
                            Users[presence.UserId] = presence;
                            break;
                        }

                        case StableBanchoServerPacket.UserStatistics:
                        {
                            StableBanchoUserStatistics statistics = StableBanchoPacketCodec.DecodeUserStatistics(packet.Payload);
                            userStatistics[(statistics.UserId, statistics.RulesetId)] = statistics;
                            UserStatisticsUpdated?.Invoke(statistics);
                            break;
                        }
                    }
                }
                catch (Exception exception)
                {
                    Logger.Error(exception, $"Failed to process stable Bancho packet {packet.Id}.");
                }
            }
        }

        private void upsertMatch(StableBanchoMatch match)
        {
            StableBanchoMatch? existing = Matches.FirstOrDefault(candidate => candidate.Id == match.Id);
            if (existing != null)
                Matches.Remove(existing);
            Matches.Add(match.Clone());

            if (CurrentMatch.Value?.Id == match.Id)
                CurrentMatch.Value = match.Clone();
        }

        private void removeMatch(int matchId)
        {
            StableBanchoMatch? existing = Matches.FirstOrDefault(candidate => candidate.Id == matchId);
            if (existing != null)
                Matches.Remove(existing);

            if (CurrentMatch.Value?.Id == matchId)
                CurrentMatch.Value = null;
        }

        private void upsertChannel(StableBanchoChannel channel)
        {
            StableBanchoChannel? existing = Channels.FirstOrDefault(candidate => string.Equals(candidate.Name, channel.Name, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                Channels.Remove(existing);

            Channels.Add(channel);
        }

        protected override void Dispose(bool isDisposing)
        {
            client.BanchoPacketsReceived -= packetsReceived;
            cancellation.Cancel();
            _ = client.LogoutAsync().ContinueWith(task =>
            {
                if (task.Exception != null)
                    Logger.Error(task.Exception, "Failed to close Stable Bancho session cleanly.");
            }, TaskScheduler.Default);
            cancellation.Dispose();
            base.Dispose(isDisposing);
        }
    }
}
