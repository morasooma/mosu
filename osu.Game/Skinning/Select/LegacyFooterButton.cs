// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Audio;
using osuTK;

namespace osu.Game.Skinning.Select
{
    public partial class LegacyFooterButton : ClickableContainer
    {
        private readonly string kind;

        private Sprite hoverSprite = null!;
        private SkinnableSound hoverSound = null!;
        private SkinnableSound clickSound = null!;

        /// <summary>de donde salen las texturas del boton. null = el skin actual.</summary>
        public ISkin? TextureSource { get; init; }

        /// <summary>
        /// cuando el glyph base de este boton es en realidad la decoracion skinnable-top gigante del skin
        /// (tipico de selection-mode), no lo dibujamos como glyph del boton: clampeado al slot saldria una
        /// mini version rara de toda la decoracion. la decoracion la dibuja aparte LegacyFooter.
        /// </summary>
        public bool SuppressBaseGlyph { get; init; }

        // Only used when neither skin nor classic supplies an image. Real button
        // hitboxes follow the hover texture, including intentionally small textures.
        private const float slot_width = 74;
        private const float slot_height = 90;

        public LegacyFooterButton(string kind)
        {
            this.kind = kind;

            Enabled.Value = true;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, SkinManager skins)
        {
            ISkin source = TextureSource ?? skin;
            var modsProvider = TextureSource ?? skin.FindProvider(s => s.GetTexture("selection-mods") != null);
            bool customLayout = modsProvider != null && modsProvider != skins.DefaultClassicSkin;

            // Stable pins custom buttons' TOP at y=426 in 480-space, not their
            // bottom at y=480. Bottom anchoring shifts every non-90px asset.
            Anchor = Anchor.BottomLeft;
            Origin = customLayout ? Anchor.TopLeft : Anchor.BottomLeft;
            Y = customLayout ? -54 * 1.6f : 0;
            Anchor sprite_anchor = customLayout ? Anchor.TopLeft : Anchor.BottomLeft;

            var baseTex = source.GetTexture($"selection-{kind}") ?? skins.DefaultClassicSkin.GetTexture($"selection-{kind}");
            var overTex = source.GetTexture($"selection-{kind}-over") ?? baseTex;

            // The oversized mode base is already drawn as decoration. Its normal
            // -over still owns the button's hover/click area and must not disappear.
            if (SuppressBaseGlyph)
                baseTex = null;

            Size = overTex != null ? sizeFor(overTex) : new Vector2(slot_width, slot_height);

            Children = new Drawable[]
            {
                new Sprite
                {
                    Anchor = sprite_anchor,
                    Origin = sprite_anchor,
                    Texture = baseTex,
                    BypassAutoSizeAxes = Axes.Both,
                    Size = sizeFor(baseTex),
                },
                // stable dibuja el -over EXACTAMENTE en la misma posicion que el base (mismo
                // field/origin/position), solo fadeado por alpha en hover. nada de nudges.
                hoverSprite = new Sprite
                {
                    Anchor = sprite_anchor,
                    Origin = sprite_anchor,
                    Texture = overTex,
                    BypassAutoSizeAxes = Axes.Both,
                    Size = sizeFor(overTex),
                    Alpha = 0,
                    AlwaysPresent = true,
                    Blending = BlendingParameters.Additive,
                },
                hoverSound = new SkinnableSound(new SampleInfo("click-short")),
                clickSound = new SkinnableSound(new SampleInfo("click-short-confirm")),
            };

            // tamaño natural, como stable: un boton grande simplemente sobresale de la barra del footer.
            static Vector2 sizeFor(Texture? tex) => tex == null ? Vector2.Zero : new Vector2(tex.DisplayWidth, tex.DisplayHeight);
        }

        protected override bool OnHover(HoverEvent e)
        {
            hoverSprite.FadeIn(100);
            hoverSound.Play();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            hoverSprite.FadeOut(100);
            base.OnHoverLost(e);
        }

        protected override bool OnClick(ClickEvent e)
        {
            clickSound.Play();
            return base.OnClick(e);
        }
    }
}
