// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.Play;

namespace osu.Game.Skinning.Components
{
    [UsedImplicitly]
    public partial class PlayerName : FontAdjustableSkinComponent
    {
        private readonly SkinSpriteText text;

        [Resolved]
        private GameplayState? gameplayState { get; set; }

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private IBindable<APIUser>? apiUser;
        private IBindable<string>? customUsername;

        public PlayerName()
        {
            AutoSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                text = new SkinSpriteText
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            if (gameplayState != null)
                text.Text = gameplayState.Score.ScoreInfo.User.Username;
            else
            {
                apiUser = api.LocalUser.GetBoundCopy();
                customUsername = config.GetBindable<string>(OsuSetting.ForkCustomUsername);
                apiUser.BindValueChanged(_ => updateDisplayUsername(), true);
                customUsername.BindValueChanged(_ => updateDisplayUsername(), true);
            }
        }

        private void updateDisplayUsername() => text.Text = apiUser?.Value.GetDisplayUsername(customUsername?.Value) ?? string.Empty;

        protected override void SetFont(FontUsage font) => text.Font = font.With(size: 40);

        protected override void SetTextColour(Colour4 textColour) => text.Colour = textColour;
    }
}
