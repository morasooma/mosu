// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Users;

namespace osu.Game.Overlays.Profile.Sections.Recent
{
    [LongRunningLoad]
    public partial class MedalIcon : Container
    {
        private readonly string slug;
        private readonly Sprite sprite;

        private readonly string? coverUrl;

        public MedalIcon(string slug, string? coverUrl = null)
        {
            this.slug = slug;
            this.coverUrl = coverUrl;

            Child = sprite = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures)
        {
            string? resource = MorasoomaMedalAssets.Resolve(slug, coverUrl);
            if (resource != null)
                sprite.Texture = textures.Get(resource);
        }
    }
}
