using System;
using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Select
{
    /// <summary>Stable's two-layer star row, rather than lazer's minimum-opacity stars.</summary>
    public partial class StableStarCounter : StarCounter
    {
        private int nextIndex;

        public override Star CreateStar() => new StableStar(nextIndex++);

        private partial class StarSprite : SkinReloadableDrawable
        {
            [Resolved]
            private SkinManager skins { get; set; } = null!;

            private readonly Sprite sprite;

            public StarSprite()
            {
                InternalChild = sprite = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    FillMode = FillMode.Fit,
                };
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                base.SkinChanged(skin);
                // SkinnableSprite's maxSize crops the centre rather than resizing.
                // Fit the full texture, including its transparent outline, into the slot.
                sprite.Texture = skin.GetTexture("star") ?? skins.DefaultClassicSkin.GetTexture("star");
            }
        }

        private partial class StableStar : Star
        {
            private readonly int index;
            private readonly Drawable foreground;

            public StableStar(int index)
            {
                this.index = index;
                Size = new Vector2(12 * StableCarouselLayout.Scale);
                InternalChildren = new Drawable[]
                {
                    createSprite(new Color4(255, 255, 255, 30), 0.35f),
                    foreground = createSprite(Color4.White, 0),
                };
            }

            private static Drawable createSprite(Color4 colour, float scale) => new StarSprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(20),
                Colour = colour,
                Scale = new Vector2(scale),
            };

            public override void DisplayAt(float fraction)
            {
                float scale = fraction <= 0 ? 0 : 0.6f * Math.Max(0.5f, Math.Clamp(fraction, 0, 1) + index * 0.04f);
                foreground.ScaleTo(scale, 500, Easing.OutElastic);
            }
        }
    }
}
