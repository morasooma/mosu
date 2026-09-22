// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Users.Drawables
{
    [LongRunningLoad]
    public partial class DrawableAvatar : Sprite
    {
        private readonly IUser user;

        /// <summary>
        /// A simple, non-interactable avatar sprite for the specified user.
        /// </summary>
        /// <param name="user">The user. A null value will get a placeholder avatar.</param>
        public DrawableAvatar(IUser user = null)
        {
            this.user = user;

            RelativeSizeAxes = Axes.Both;
            FillMode = FillMode.Fit;
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures, OnlineAssetCachingStore onlineTextures, IAPIProvider api)
        {
            if (user != null && user.OnlineID > 1)
            {
                string avatarLookup = (user as APIUser)?.AvatarUrl ?? $@"https://a.ppy.sh/{user.OnlineID}";

                if (user is APIUser apiUser)
                {
                    string connectedAvatar = $"{api.Endpoints.APIUrl.TrimEnd('/')}/users/{apiUser.OnlineID}/avatar";

                    if (Online.MosuServerEnvironment.IsThirdPartyServer)
                    {
                        avatarLookup = !string.IsNullOrEmpty(apiUser.AvatarUrl) ? apiUser.AvatarUrl : connectedAvatar;
                    }
                    else
                    {
                        avatarLookup = connectedAvatar;

                        if (Uri.TryCreate(apiUser.AvatarUrl, UriKind.Absolute, out var avatarUri)
                            && Uri.TryCreate(api.Endpoints.APIUrl, UriKind.Absolute, out var apiUri)
                            && string.Equals(avatarUri.Host, apiUri.Host, StringComparison.OrdinalIgnoreCase))
                            avatarLookup = apiUser.AvatarUrl;
                    }
                }

                Texture = onlineTextures.Get(avatarLookup);
            }

            Texture ??= textures.Get(@"Online/avatar-guest");
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(300, Easing.OutQuint);
        }
    }
}
