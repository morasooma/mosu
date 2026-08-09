// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Game.Online.API.Requests.Responses;
using osuTK;

namespace osu.Game.Overlays.Profile.Sections.Medals
{
    public partial class DrawableProfileMedal : Container, IHasTooltip
    {
        public const float MEDAL_SIZE = 72f;

        private readonly APIMedal medal;

        public DrawableProfileMedal(APIMedal medal)
        {
            this.medal = medal;
            Size = new Vector2(MEDAL_SIZE);
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures)
        {
            Child = new DelayedLoadWrapper(
                new MedalSprite(medal, textures)
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    FillMode = FillMode.Fit,
                })
            {
                RelativeSizeAxes = Axes.Both,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            this.FadeInFromZero(200);
        }

        public LocalisableString TooltipText
        {
            get
            {
                if (medal.Achieved)
                {
                    string date = medal.AchievedAt?.LocalDateTime.ToString("yyyy-MM-dd") ?? string.Empty;
                    return $"{medal.Name}\n{medal.Description}\n{date}";
                }

                return $"{medal.Name}\n{medal.Description}\n(Locked)";
            }
        }

        private partial class MedalSprite : Sprite
        {
            private readonly APIMedal medal;
            private readonly LargeTextureStore textures;

            public MedalSprite(APIMedal medal, LargeTextureStore textures)
            {
                this.medal = medal;
                this.textures = textures;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Texture = textures.Get(!string.IsNullOrEmpty(medal.IconUrl2x) ? medal.IconUrl2x : medal.IconUrl);

                if (!medal.Achieved)
                {
                    Alpha = 0.35f;
                    Colour = Colour4.Gray;
                }
            }
        }
    }
}
