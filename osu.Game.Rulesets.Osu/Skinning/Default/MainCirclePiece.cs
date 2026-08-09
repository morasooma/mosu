// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    public partial class MainCirclePiece : CompositeDrawable
    {
        private bool directChildrenLifeStable;
        private readonly CirclePiece circle;
        private readonly Drawable ring;
        private readonly NumberPiece number;
        private readonly FlashPiece? flash;
        private readonly ExplodePiece? explode;
        private readonly GlowPiece? glow;

        public MainCirclePiece()
        {
            Size = OsuHitObject.OBJECT_DIMENSIONS;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            circle = new CirclePiece();
            number = new NumberPiece();

            if (SkinPerformanceMode.Enabled)
            {
                // Preserve the actual note, ring and combo number while completely omitting
                // cosmetic glow/flash/explosion subtrees. Apart from fewer draw calls this
                // also avoids creating their buffered masking and triangle resources.
                ring = new PerformanceRingPiece();
                InternalChildren = new Drawable[] { circle, number, ring };
            }
            else
            {
                ring = new RingPiece();
                InternalChildren = new Drawable[]
                {
                    glow = new GlowPiece(),
                    circle,
                    number,
                    ring,
                    flash = new FlashPiece(),
                    explode = new ExplodePiece(),
                };
            }
        }

        private partial class PerformanceRingPiece : Sprite
        {
            public PerformanceRingPiece()
            {
                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;
            }

            [BackgroundDependencyLoader]
            private void load(TextureStore textures)
            {
                Texture = textures.Get(@"Gameplay/osu/approachcircle");
                Scale = new Vector2(128 / 118f);
            }
        }

        private readonly IBindable<Color4> accentColour = new Bindable<Color4>();
        private readonly IBindable<int> indexInCurrentCombo = new Bindable<int>();

        [Resolved]
        private DrawableHitObject drawableObject { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            var drawableOsuObject = (DrawableOsuHitObject)drawableObject;

            accentColour.BindTo(drawableObject.AccentColour);
            indexInCurrentCombo.BindTo(drawableOsuObject.IndexInCurrentComboBindable);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            accentColour.BindValueChanged(colour =>
            {
                if (explode != null)
                    explode.Colour = colour.NewValue;

                if (glow != null)
                    glow.Colour = colour.NewValue;

                circle.Colour = colour.NewValue;
            }, true);

            indexInCurrentCombo.BindValueChanged(index => number.Text = (index.NewValue + 1).ToString(), true);

            drawableObject.ApplyCustomUpdateState += updateStateTransforms;
            updateStateTransforms(drawableObject, drawableObject.State.Value);
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

        private void updateStateTransforms(DrawableHitObject drawableHitObject, ArmedState state)
        {
            if (glow != null)
            {
                using (BeginAbsoluteSequence(drawableObject.StateUpdateTime))
                    glow.FadeOut(400);
            }

            using (BeginAbsoluteSequence(drawableObject.HitStateUpdateTime))
            {
                switch (state)
                {
                    case ArmedState.Hit:
                        if (SkinPerformanceMode.Enabled)
                        {
                            // The performance subtree contains no hit-effect pieces, so avoid
                            // scheduling transforms which would keep the object invalidating.
                            this.FadeOut();
                            break;
                        }

                        const double flash_in = 40;
                        const double flash_out = 100;

                        flash!.FadeTo(0.8f, flash_in)
                             .Then()
                             .FadeOut(flash_out);

                        explode!.FadeIn(flash_in);
                        this.ScaleTo(1.5f, 400, Easing.OutQuad);

                        using (BeginDelayedSequence(flash_in))
                        {
                            // after the flash, we can hide some elements that were behind it
                            ring.FadeOut();
                            circle.FadeOut();
                            number.FadeOut();

                            this.FadeOut(800);
                        }

                        break;
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (drawableObject.IsNotNull())
                drawableObject.ApplyCustomUpdateState -= updateStateTransforms;
        }
    }
}
