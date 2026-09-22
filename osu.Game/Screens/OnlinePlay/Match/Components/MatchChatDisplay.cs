// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;
using osu.Framework.Allocation;
using osu.Game.Online.Chat;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Legacy;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;

namespace osu.Game.Screens.OnlinePlay.Match.Components
{
    public partial class MatchChatDisplay : StandAloneChatDisplay
    {
        [Resolved]
        private ChannelManager? channelManager { get; set; }

        [Resolved(CanBeNull = true)]
        private StableBanchoSession? stableBanchoSession { get; set; }

        [Resolved]
        private MultiplayerClient multiplayerClient { get; set; } = null!;

        private readonly Room room;
        private readonly bool leaveChannelOnDispose;

        public MatchChatDisplay(Room room, bool leaveChannelOnDispose = true)
            : base(true)
        {
            this.room = room;
            this.leaveChannelOnDispose = leaveChannelOnDispose;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            room.PropertyChanged += onRoomPropertyChanged;
            if (stableBanchoSession != null)
            {
                stableBanchoSession.ChatMessageReceived += onStableMessageReceived;
                multiplayerClient.RoomUpdated += onStableRoomUpdated;
            }
            updateChannel();
        }

        private void onRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(Room.ChannelId))
                updateChannel();
        }

        private void updateChannel()
        {
            if (stableBanchoSession != null)
            {
                if (multiplayerClient.Room == null)
                    return;

                if (Channel.Value?.Id == multiplayerClient.Room.RoomID)
                    return;

                Channel.Value = new Channel
                {
                    Id = multiplayerClient.Room.RoomID,
                    Type = ChannelType.Multiplayer,
                    Name = stableChannelName,
                    Joined = { Value = true },
                    MessagesLoaded = true,
                };
                return;
            }

            if (room.RoomID == null)
                return;

            if (room.ChannelId == 0)
                return;

            Channel.Value = channelManager?.JoinChannel(new Channel { Id = room.ChannelId, Type = ChannelType.Multiplayer, Name = $"#lazermp_{room.RoomID.Value}" });
        }

        private const string stableChannelName = "#multiplayer";

        private void onStableRoomUpdated() => Scheduler.AddOnce(updateChannel);

        protected override void PostMessage(string text)
        {
            if (stableBanchoSession == null || multiplayerClient.Room == null)
            {
                base.PostMessage(text);
                return;
            }

            APIUser? localUser = multiplayerClient.LocalUser?.User;
            if (Channel.Value != null && localUser != null)
            {
                Channel.Value.AddNewMessages(new Message
                {
                    ChannelId = Channel.Value.Id,
                    Timestamp = DateTimeOffset.Now,
                    Content = text,
                    DisplayContent = text,
                    Sender = localUser,
                });
            }

            _ = stableBanchoSession.SendChannelMessageAsync(stableChannelName, text);
        }

        private void onStableMessageReceived(StableBanchoMessage message)
        {
            bool isMatchChannel = string.Equals(message.Target, stableChannelName, StringComparison.OrdinalIgnoreCase)
                                  || string.Equals(message.Target, $"#multi_{multiplayerClient.Room?.RoomID}", StringComparison.OrdinalIgnoreCase);

            if (!isMatchChannel || Channel.Value == null)
                return;

            Channel.Value.AddNewMessages(new Message
            {
                ChannelId = Channel.Value.Id,
                Timestamp = DateTimeOffset.Now,
                Content = message.Content,
                DisplayContent = message.Content,
                Sender = new APIUser
                {
                    Id = message.SenderId,
                    Username = message.Sender,
                    AvatarUrl = stableBanchoSession!.GetAvatarUrl(message.SenderId),
                },
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            room.PropertyChanged -= onRoomPropertyChanged;

            if (stableBanchoSession != null)
            {
                stableBanchoSession.ChatMessageReceived -= onStableMessageReceived;
                multiplayerClient.RoomUpdated -= onStableRoomUpdated;
            }

            if (leaveChannelOnDispose && stableBanchoSession == null)
                channelManager?.LeaveChannel(Channel.Value);
        }
    }
}
