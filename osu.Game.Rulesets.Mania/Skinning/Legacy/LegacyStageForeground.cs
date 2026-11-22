// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.UI.Scrolling;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Mania.Skinning.Legacy
{
    public partial class LegacyStageForeground : CompositeDrawable
    {
        private readonly IBindable<ScrollingDirection> direction = new Bindable<ScrollingDirection>();

        private readonly bool allowMorasoomaFallback;
        private Drawable? sprite;

        public LegacyStageForeground(bool allowMorasoomaFallback)
        {
            this.allowMorasoomaFallback = allowMorasoomaFallback;
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(ISkinSource skin, IScrollingInfo scrollingInfo)
        {
            string bottomImage = skin.GetManiaSkinConfig<string>(LegacyManiaSkinConfigurationLookups.BottomStageImage)?.Value
                                 ?? "mania-stage-bottom";

            // Check the first matching source, including animations, before accepting the optional panel.
            var provider = skin.FindProvider(s => s.GetTexture($"{bottomImage}-0") != null || s.GetTexture(bottomImage) != null);
            if (!allowMorasoomaFallback && provider is ISkinTransformer { Skin: MorasoomaSkin })
                return;

            sprite = provider.GetAnimation(bottomImage, true, true)?.With(d => d.Scale = new Vector2(LegacySkin.STABLE_MAGIC_SCALE_FACTOR));

            if (sprite != null)
                InternalChild = sprite;

            direction.BindTo(scrollingInfo.Direction);
            direction.BindValueChanged(onDirectionChanged, true);
        }

        private void onDirectionChanged(ValueChangedEvent<ScrollingDirection> direction)
        {
            if (sprite == null)
                return;

            if (direction.NewValue == ScrollingDirection.Up)
                sprite.Anchor = sprite.Origin = Anchor.TopCentre;
            else
                sprite.Anchor = sprite.Origin = Anchor.BottomCentre;
        }
    }
}
