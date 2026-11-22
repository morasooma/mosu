// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osuTK;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
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
        private readonly Sprite blurMask;
        private readonly BufferedContainer maskedBlur;

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        protected override void Update()
        {
            base.Update();

            sprite.Alpha = OverlayTransparency.SongSelectSurfaceAlpha;
            maskedBlur.Alpha = OverlayTransparency.SongSelectActive && sprite.Texture != null ? 1 : 0;
            maskedBlur.Size = sprite.Texture == null ? Vector2.Zero : new Vector2(sprite.Texture.DisplayWidth, sprite.Texture.DisplayHeight);
            maskedBlur.Scale = sprite.Scale;

            if (game == null)
                return;

            // Stable sprites use screen height / 768 for BOTH axes. The carousel
            // lives in UI-scaled coordinates, so convert the viewport into this
            // drawable's space before applying that scale. Never resize X alone.
            var viewport = game.ScreenSpaceDrawQuad;
            float height = (ToLocalSpace(viewport.BottomLeft) - ToLocalSpace(viewport.TopLeft)).Length;
            sprite.Scale = new Vector2(height / 768);
            maskedBlur.Scale = sprite.Scale;
        }


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

            // Stable draws this texture at its natural 768-space size, centred
            // vertically on the row. It neither stretches it to the row rectangle
            // nor paints an opaque rectangle beneath its transparent margins.
            InternalChildren = new Drawable[]
            {
                maskedBlur = new BufferedContainer
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    FrameBufferScale = new Vector2(0.5f),
                    Alpha = 0,
                    Children = new Drawable[]
                    {
                        new SongSelectBackdropSurface(),
                        blurMask = new Sprite
                        {
                            RelativeSizeAxes = Axes.Both,
                            // Multiply the captured backdrop by the actual skin texture alpha.
                            // Transparent corners and cutouts must never contain blur.
                            Blending = new BlendingParameters
                            {
                                Source = BlendingType.Zero,
                                Destination = BlendingType.SrcAlpha,
                                SourceAlpha = BlendingType.Zero,
                                DestinationAlpha = BlendingType.SrcAlpha,
                                RGBEquation = BlendingEquation.Add,
                                AlphaEquation = BlendingEquation.Add,
                            },
                        },
                    },
                },
                sprite = new Sprite
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                },
            };
        }

        protected override void SkinChanged(ISkinSource skin)
        {
            base.SkinChanged(skin);
            sprite.Texture = skin.GetTexture(@"menu-button-background");
            blurMask.Texture = sprite.Texture;
            ActiveTextColour = skin.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(@"SongSelectActiveText"))?.Value ?? Color4.Black;
            InactiveTextColour = skin.GetConfig<SkinCustomColourLookup, Color4>(new SkinCustomColourLookup(@"SongSelectInactiveText"))?.Value ?? Color4.White;
            MenuGlowColour = skin.GetConfig<GlobalSkinColours, Color4>(GlobalSkinColours.MenuGlow)?.Value ?? Color4.White;
        }
    }
}
