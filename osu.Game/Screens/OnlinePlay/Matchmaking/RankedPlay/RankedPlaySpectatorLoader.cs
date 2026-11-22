using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Metadata;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Screens.Play;
using osu.Game.Users;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay
{
    public partial class RankedPlaySpectatorLoader : OsuScreen
    {
        public static OsuScreen Create(APIUser user, MetadataClient metadata)
        {
            long? roomId = metadata.GetPresence(user.Id)?.Activity switch
            {
                UserActivity.InLobby { IsRankedPlay: true, RoomID: > 0 } lobby => lobby.RoomID,
                UserActivity.InMultiplayerGame game => game.RankedPlayRoomID,
                _ => null
            };
            return roomId.HasValue ? new RankedPlaySpectatorLoader(roomId.Value, user.Id) : new SoloSpectatorScreen(user);
        }

        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        private readonly long roomId;
        private readonly int playerId;
        private readonly OsuSpriteText status;
        private bool joined;
        private bool leaving;

        public RankedPlaySpectatorLoader(long roomId, int playerId)
        {
            this.roomId = roomId;
            this.playerId = playerId;
            InternalChild = status = new OsuSpriteText
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Text = global::osu.Game.Localisation.ClientTranslationStrings.J4922
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            join();
        }

        private async void join()
        {
            if (client.Room != null)
            {
                status.Text = global::osu.Game.Localisation.ClientTranslationStrings.J4923;
                return;
            }

            try
            {
                await client.JoinRoom(new Room { RoomID = roomId });
                joined = true;
                Schedule(() =>
                {
                    if (!leaving && this.IsCurrentScreen() && client.Room != null)
                        this.Push(new RankedPlayScreen(client.Room, spectatedPlayerId: playerId));
                    else if (client.Room?.RoomID == roomId)
                        client.LeaveRoom().FireAndForget();
                });
            }
            catch (Exception)
            {
                Schedule(() => status.Text = global::osu.Game.Localisation.ClientTranslationStrings.J4924);
            }
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            this.Exit();
        }

        public override bool OnExiting(ScreenExitEvent e)
        {
            if (base.OnExiting(e))
                return true;
            leaving = true;
            if (joined && client.Room?.RoomID == roomId)
                client.LeaveRoom().FireAndForget();
            return false;
        }
    }
}
