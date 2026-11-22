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
using osu.Game.Scoring.Render;

namespace osu.Game.Users.Drawables
{
    [LongRunningLoad]
    public partial class DrawableAvatar : Sprite
    {
        private readonly IUser user;

        [Resolved(CanBeNull = true)]
        private ReplayRenderSnapshot renderSnapshot { get; set; }

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
                string avatarLookup = renderSnapshot != null
                    ? renderSnapshot.GetAvatarUrl(user.OnlineID)
                    : GetAvatarLookup(user, api.Endpoints.APIUrl, Online.MosuServerEnvironment.IsThirdPartyServer);

                Texture = onlineTextures.Get(avatarLookup);
            }

            Texture ??= textures.Get(@"Online/avatar-guest");
        }

        internal static string GetAvatarLookup(IUser user, string apiUrl, bool thirdPartyServer)
        {
            if (user is not APIUser apiUser)
                return $@"https://a.ppy.sh/{user.OnlineID}";

            string connectedAvatar = $"{apiUrl.TrimEnd('/')}/users/{apiUser.OnlineID}/avatar";
            if (thirdPartyServer)
                return !string.IsNullOrEmpty(apiUser.AvatarUrl) ? apiUser.AvatarUrl : connectedAvatar;

            return Uri.TryCreate(apiUser.AvatarUrl, UriKind.Absolute, out var avatarUri)
                   && Uri.TryCreate(apiUrl, UriKind.Absolute, out var apiUri)
                   && string.Equals(avatarUri.Host, apiUri.Host, StringComparison.OrdinalIgnoreCase)
                ? apiUser.AvatarUrl
                : connectedAvatar;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(300, Easing.OutQuint);
        }
    }
}
