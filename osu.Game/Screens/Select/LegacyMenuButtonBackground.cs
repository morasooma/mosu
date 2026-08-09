// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Skinning;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// The osu!stable song-select button background supplied by the current skin.
    /// </summary>
    public partial class LegacyMenuButtonBackground : SkinReloadableDrawable
    {
        private readonly Sprite sprite;

        internal bool HasTexture => sprite.Texture != null;

        internal Color4 ActiveTextColour { get; private set; } = Color4.Black;

        internal Color4 InactiveTextColour { get; private set; } = Color4.White;

        internal Color4 MenuGlowColour { get; private set; } = Color4.White;

        internal float SelectedOverlayAlpha
        {
            get
            {
                float luminance = ActiveTextColour.R * 0.2126f + ActiveTextColour.G * 0.7152f + ActiveTextColour.B * 0.0722f;
                return luminance > 0.5f ? 0.45f : 0.8f;
            }
        }

        public LegacyMenuButtonBackground()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.Black,
                },
                sprite = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Stretch,
                },
            };
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);
            sprite.Texture = skin.GetTexture(@"menu-button-background");
            ActiveTextColour = skin.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(@"SongSelectActiveText"))?.Value ?? Color4.Black;
            InactiveTextColour = skin.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(@"SongSelectInactiveText"))?.Value ?? Color4.White;
            MenuGlowColour = skin.GetConfig<GlobalSkinColours, Color4>(GlobalSkinColours.MenuGlow)?.Value ?? Color4.White;
        }
    }
}
