// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Argon
{
    public partial class ArgonSliderBall : CircularContainer
    {
        private bool directChildrenLifeStable;

        private readonly Box fill;
        private readonly SpriteIcon icon;

        private readonly Vector2 defaultIconScale = new Vector2(0.6f, 0.8f);

        private readonly IBindable<Color4> accentColour = new Bindable<Color4>();

        [Resolved(canBeNull: true)]
        private DrawableHitObject? parentObject { get; set; }

        public ArgonSliderBall()
        {
            Size = new Vector2(ArgonMainCirclePiece.OUTER_GRADIENT_SIZE);

            Masking = true;

            BorderThickness = ArgonMainCirclePiece.GRADIENT_THICKNESS;
            BorderColour = Color4.White;

            InternalChildren = new Drawable[]
            {
                fill = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
                icon = new SpriteIcon
                {
                    Size = new Vector2(48),
                    Scale = defaultIconScale,
                    Icon = FontAwesome.Solid.AngleRight,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                }
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            if (parentObject != null)
                accentColour.BindTo(parentObject.AccentColour);
        }

        protected override bool CheckChildrenLife()
        {
            if (directChildrenLifeStable)
                return false;

            bool aliveChanged = base.CheckChildrenLife();
            directChildrenLifeStable = InternalChildren.Count == AliveInternalChildren.Count;
            return aliveChanged;
        }

        protected override void AddInternal(Drawable drawable)
        {
            directChildrenLifeStable = false;
            base.AddInternal(drawable);
        }

        protected override bool RemoveInternal(Drawable drawable, bool disposeImmediately)
        {
            directChildrenLifeStable = false;
            return base.RemoveInternal(drawable, disposeImmediately);
        }

        protected override void ClearInternal(bool disposeChildren = true)
        {
            directChildrenLifeStable = false;
            base.ClearInternal(disposeChildren);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            accentColour.BindValueChanged(colour =>
            {
                fill.Colour = ColourInfo.GradientVertical(colour.NewValue, colour.NewValue.Darken(0.5f));
            }, true);

            if (parentObject != null)
            {
                parentObject.ApplyCustomUpdateState += updateStateTransforms;
                updateStateTransforms(parentObject, parentObject.State.Value);
            }
        }

        private void updateStateTransforms(DrawableHitObject drawableObject, ArmedState _)
        {
            // Gets called by slider ticks, tails, etc., leading to duplicated
            // animations which in this case have no visual impact (due to
            // instant fade) but may negatively affect performance
            if (drawableObject is not DrawableSlider)
                return;

            const float duration = 200;
            const float icon_scale = 0.9f;

            using (BeginAbsoluteSequence(drawableObject.StateUpdateTime))
            {
                if (SkinPerformanceMode.Enabled)
                {
                    this.FadeIn();
                    icon.ScaleTo(defaultIconScale);
                }
                else
                {
                    this.FadeInFromZero(duration, Easing.OutQuint);
                    icon.ScaleTo(0).Then().ScaleTo(defaultIconScale, duration, Easing.OutElasticHalf);
                }
            }

            using (BeginAbsoluteSequence(drawableObject.HitStateUpdateTime))
            {
                if (SkinPerformanceMode.Enabled)
                {
                    this.FadeOut();
                    icon.ScaleTo(defaultIconScale);
                    return;
                }

                // intentionally pile on an extra FadeOut to make it happen much faster
                this.FadeOut(duration / 4, Easing.OutQuint);
                icon.ScaleTo(defaultIconScale * icon_scale, duration, Easing.OutQuint);
            }
        }

        protected override void Update()
        {
            base.Update();

            if (SkinPerformanceMode.Enabled)
                return;

            //undo rotation on layers which should not be rotated.
            float appliedRotation = Parent!.Rotation;

            fill.Rotation = -appliedRotation;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (parentObject != null)
                parentObject.ApplyCustomUpdateState -= updateStateTransforms;
        }
    }
}
