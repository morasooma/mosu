// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK;
using osuTK.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Skinning;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Rulesets.Osu.Objects.Drawables
{
    public partial class DrawableSliderTick : DrawableOsuHitObject
    {
        public const double ANIM_DURATION = 150;

        public const float DEFAULT_TICK_SIZE = 16;

        protected DrawableSlider DrawableSlider => (DrawableSlider)ParentHitObject;

        private Drawable scaleContainer;

        public DrawableSliderTick()
            : base(null)
        {
        }

        public DrawableSliderTick(SliderTick sliderTick)
            : base(sliderTick)
        {
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            Size = OsuHitObject.OBJECT_DIMENSIONS;
            Origin = Anchor.Centre;

            AddInternal(scaleContainer = createTickDrawable(textures));

            ScaleBindable.BindValueChanged(scale => scaleContainer.Scale = new Vector2(scale.NewValue));
        }

        private Drawable createTickDrawable(TextureStore textures)
        {
            return new SkinnableDrawable(new OsuSkinComponentLookup(OsuSkinComponents.SliderScorePoint), _ =>
            {
                if (SkinPerformanceMode.Enabled)
                {
                    // Use one textured quad instead of a masked+bordered buffered container.
                    // Custom skin score-point textures still take precedence over this fallback.
                    return new Sprite
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(DEFAULT_TICK_SIZE),
                        Texture = textures.Get(@"Gameplay/osu/disc"),
                        Colour = AccentColour.Value,
                        Alpha = 0.7f,
                    };
                }

                // Construct this lazily. Previously every pooled tick allocated the complete
                // fallback subtree even when a custom skin texture was selected.
                return new CircularContainer
                {
                    Anchor = Anchor.Centre,
                    Masking = true,
                    Origin = Anchor.Centre,
                    Size = new Vector2(DEFAULT_TICK_SIZE),
                    BorderThickness = DEFAULT_TICK_SIZE / 4,
                    BorderColour = Color4.White,
                    Child = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = AccentColour.Value,
                        Alpha = 0.3f,
                    }
                };
            })
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
        }

        protected override void OnApply()
        {
            base.OnApply();

            Position = HitObject.Position - DrawableSlider.HitObject.Position;
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset) => DrawableSlider.SliderInputManager.TryJudgeNestedObject(this, timeOffset);

        protected override void UpdateInitialTransforms()
        {
            if (SkinPerformanceMode.Enabled)
            {
                this.FadeIn();
                this.ScaleTo(1f);
                return;
            }

            this.FadeOut().FadeIn(ANIM_DURATION);
            this.ScaleTo(0.5f).ScaleTo(1f, ANIM_DURATION * 4, Easing.OutElasticHalf);
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            base.UpdateHitStateTransforms(state);

            switch (state)
            {
                case ArmedState.Idle:
                    this.Delay(HitObject.TimePreempt).FadeOut();
                    break;

                case ArmedState.Miss:
                    if (SkinPerformanceMode.Enabled)
                    {
                        this.FadeOut();
                        break;
                    }

                    this.FadeOut(ANIM_DURATION, Easing.OutQuint);
                    break;

                case ArmedState.Hit:
                    if (SkinPerformanceMode.Enabled)
                    {
                        this.FadeOut();
                        break;
                    }

                    this.FadeOut(ANIM_DURATION, Easing.OutQuint);
                    this.ScaleTo(Scale * 1.5f, ANIM_DURATION, Easing.Out);
                    break;
            }
        }
    }
}
