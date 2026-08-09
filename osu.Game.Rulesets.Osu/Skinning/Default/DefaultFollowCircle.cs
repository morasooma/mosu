// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    public partial class DefaultFollowCircle : FollowCircle
    {
        public DefaultFollowCircle()
        {
            if (SkinPerformanceMode.Enabled)
            {
                // The normal follow circle is a masked, bordered framebuffer. A textured
                // ring keeps the tracking cue while reducing it to one quad.
                InternalChild = new PerformanceFollowCircleVisual();
                return;
            }

            InternalChild = new CircularContainer
            {
                RelativeSizeAxes = Axes.Both,
                Masking = true,
                BorderThickness = 5,
                BorderColour = Color4.Orange,
                Blending = BlendingParameters.Additive,
                Child = new Box
                {
                    Colour = Color4.Orange,
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0.2f,
                }
            };
        }

        private partial class PerformanceFollowCircleVisual : Sprite
        {
            public PerformanceFollowCircleVisual()
            {
                RelativeSizeAxes = Axes.Both;
                Blending = BlendingParameters.Additive;
                Colour = Color4.Orange;
                Alpha = 0.8f;
            }

            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Texture = textures.Get(@"Gameplay/osu/approachcircle");
                Scale = new Vector2(128 / 118f);
            }
        }

        protected override void OnSliderPress()
        {
            if (SkinPerformanceMode.Enabled)
            {
                this.ScaleTo(DrawableSliderBall.FOLLOW_AREA)
                    .FadeIn();
                return;
            }

            const float duration = 300f;

            if (Precision.AlmostEquals(0, Alpha))
                this.ScaleTo(1);

            this.ScaleTo(DrawableSliderBall.FOLLOW_AREA, duration, Easing.OutQuint)
                .FadeIn(duration, Easing.OutQuint);
        }

        protected override void OnSliderRelease()
        {
            if (SkinPerformanceMode.Enabled)
            {
                this.ScaleTo(DrawableSliderBall.FOLLOW_AREA)
                    .FadeOut();
                return;
            }

            const float duration = 150;

            this.ScaleTo(DrawableSliderBall.FOLLOW_AREA * 1.2f, duration, Easing.OutQuint)
                .FadeTo(0, duration, Easing.OutQuint);
        }

        protected override void OnSliderEnd()
        {
            if (SkinPerformanceMode.Enabled)
            {
                this.ScaleTo(1)
                    .FadeOut();
                return;
            }

            const float duration = 300;

            this.ScaleTo(1, duration, Easing.OutQuint)
                .FadeOut(duration / 2, Easing.OutQuint);
        }

        protected override void OnSliderTick()
        {
            if (SkinPerformanceMode.Enabled)
                return;

            if (Scale.X >= DrawableSliderBall.FOLLOW_AREA * 0.98f)
            {
                this.ScaleTo(DrawableSliderBall.FOLLOW_AREA * 1.08f, 40, Easing.OutQuint)
                    .Then()
                    .ScaleTo(DrawableSliderBall.FOLLOW_AREA, 200f, Easing.OutQuint);
            }
        }

        protected override void OnSliderBreak()
        {
        }
    }
}
