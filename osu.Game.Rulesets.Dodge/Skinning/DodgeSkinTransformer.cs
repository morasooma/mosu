// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Dodge.Skinning.Components;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Skinning
{
    /// <summary>
    /// Supplies Dodge-specific default HUD components while preserving any
    /// ruleset layout explicitly created by the user in the skin editor.
    /// </summary>
    public class DodgeSkinTransformer : SkinTransformer
    {
        public DodgeSkinTransformer(ISkin skin)
            : base(skin)
        {
        }

        public override Drawable? GetDrawableComponent(ISkinComponentLookup lookup)
        {
            if (lookup is DodgeSkinComponentLookup dodgeComponent)
            {
                string textureName = GetTextureName(dodgeComponent.Component);

                var texture = GetTexture(textureName);
                if (texture == null)
                    return null;

                return new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Texture = texture,
                    FillMode = FillMode.Fit,
                };
            }

            if (lookup is GlobalSkinnableContainerLookup
                {
                    Lookup: GlobalSkinnableContainers.MainHUDComponents,
                    Ruleset: not null,
                })
            {
                return new DefaultSkinComponentsContainer(_ => { })
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new DodgeGrazeCounter
                    {
                        Anchor = Anchor.TopRight,
                        Origin = Anchor.TopRight,
                        Position = new Vector2(-36, 115),
                        UsesFixedAnchor = true,
                    },
                };
            }

            return base.GetDrawableComponent(lookup);
        }

        public static string GetTextureName(DodgeSkinComponents component) => component switch
        {
            DodgeSkinComponents.Player => "dodge-player",
            DodgeSkinComponents.Arena => "dodge-arena",
            DodgeSkinComponents.ArenaBorder => "dodge-arena-border",
            DodgeSkinComponents.BulletCircle => "dodge-bullet-circle",
            DodgeSkinComponents.BulletSquare => "dodge-bullet-square",
            DodgeSkinComponents.BulletDiamond => "dodge-bullet-diamond",
            DodgeSkinComponents.BulletTriangle => "dodge-bullet-triangle",
            DodgeSkinComponents.GrazeEffect => "dodge-graze",
            DodgeSkinComponents.CollisionEffect => "dodge-collision",
            DodgeSkinComponents.PlayerTrail => "dodge-player-trail",
            _ => string.Empty,
        };
    }
}
