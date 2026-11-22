using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;

namespace osu.Game.Screens.OnlinePlay.Multiplayer.Match
{
    public partial class InviteServerButton : RoundedButton
    {
        [Resolved] private MultiplayerClient client { get; set; } = null!;
        [Resolved] private IAPIProvider api { get; set; } = null!;
        [Resolved] private OsuGame game { get; set; } = null!;

        public InviteServerButton()
        {
            Text = ServerInvitationStrings.InviteServer;
            TooltipText = ServerInvitationStrings.CooldownHint;
            Action = () =>
            {
                if (api.LocalUser.Value.IsSupporter)
                    client.InviteServer().FireAndForget();
                else
                    game.OpenUrlExternally("https://morasooma.net/home/support");
            };
            AlwaysPresent = true;
        }

        protected override void Update()
        {
            base.Update();
            Alpha = client.Room != null
                    && !client.Room.Settings.MatchType.IsMatchmakingType()
                    && string.IsNullOrEmpty(client.Room.Settings.Password) ? 1 : 0;
        }
    }
}
