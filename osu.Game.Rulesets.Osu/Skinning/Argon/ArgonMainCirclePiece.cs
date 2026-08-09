// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Performance;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Skinning.Default;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Argon
{
    public partial class ArgonMainCirclePiece : CompositeDrawable
    {
        private bool directChildrenLifeStable;

        public const float BORDER_THICKNESS = (OsuHitObject.OBJECT_RADIUS * 2) * (2f / 58);

        public const float GRADIENT_THICKNESS = BORDER_THICKNESS * 2.5f;

        public const float OUTER_GRADIENT_SIZE = (OsuHitObject.OBJECT_RADIUS * 2) - BORDER_THICKNESS * 4;

        public const float INNER_GRADIENT_SIZE = OUTER_GRADIENT_SIZE - GRADIENT_THICKNESS * 2;
        public const float INNER_FILL_SIZE = INNER_GRADIENT_SIZE - GRADIENT_THICKNESS * 2;

        // Typed as Drawable because the fill layers are either a masking Circle or a non-masking FastCircle
        // depending on MosuOptimisationToggles.ArgonFastCircles. Null in the lite tree.
        private readonly Drawable? outerFill;
        private readonly Drawable? outerGradient;
        private readonly Drawable? innerGradient;
        private readonly Drawable? innerFill;

        private readonly RingPiece? border;
        private readonly OsuSpriteText number;

        private readonly IBindable<Color4> accentColour = new Bindable<Color4>();
        private readonly IBindable<int> indexInCurrentCombo = new Bindable<int>();
        private readonly FlashPiece? flash;
        private readonly CircularContainer? kiaiContainer;

        // Lite tree: the whole coloured body is one accent-tinted greyscale sprite plus one white
        // ring sprite, both from a shared native texture (see ArgonLiteCircleTextures).
        private readonly bool lite = SkinPerformanceMode.Enabled && MosuOptimisationToggles.ArgonLiteBody;
        private readonly Sprite? liteBody;
        private readonly Sprite? liteRing;
        private readonly bool withOuterFill;

        private Bindable<bool> configHitLighting = null!;

        private static readonly Vector2 circle_size = OsuHitObject.OBJECT_DIMENSIONS;

        [Resolved]
        private DrawableHitObject drawableObject { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private IBeatmap? beatmap { get; set; }

        public ArgonMainCirclePiece(bool withOuterFill)
        {
            this.withOuterFill = withOuterFill;

            Size = circle_size;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;

            if (lite)
            {
                // Two quads sharing one native texture + the combo number. Body and ring are adjacent
                // so they batch as a single draw call; the number never overlaps the rim (max glyph
                // extent ~78px vs the 88px inner-zone diameter), so drawing it above the ring is safe.
                InternalChildren = new Drawable[]
                {
                    liteBody = new Sprite { Size = circle_size, Anchor = Anchor.Centre, Origin = Anchor.Centre },
                    liteRing = new Sprite { Size = circle_size, Anchor = Anchor.Centre, Origin = Anchor.Centre },
                    number = new OsuSpriteText
                    {
                        Font = OsuFont.Default.With(size: 52, weight: FontWeight.Bold),
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Y = -2,
                        Text = @"1",
                    },
                };

                return;
            }

            InternalChildren = new Drawable[]
            {
                // Slightly inset to prevent bleeding outside the ring
                outerFill = createFill(circle_size - new Vector2(1)), // renders dark fill
                outerGradient = createFill(new Vector2(OUTER_GRADIENT_SIZE)), // renders the outer bright gradient
                innerGradient = createFill(new Vector2(INNER_GRADIENT_SIZE)), // renders the inner bright gradient
                innerFill = createFill(new Vector2(INNER_FILL_SIZE)), // renders the inner dark fill
                kiaiContainer = createKiaiContainer(),
                number = new OsuSpriteText
                {
                    Font = OsuFont.Default.With(size: 52, weight: FontWeight.Bold),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = -2,
                    Text = @"1",
                },
                flash = new FlashPiece(),
                border = new RingPiece(BORDER_THICKNESS),
            };

            outerFill.Alpha = withOuterFill ? 1 : 0;
        }

        /// <summary>
        /// Creates one of the solid/gradient fill layers.
        /// </summary>
        /// <remarks>
        /// <see cref="FastCircle"/> draws an anti-aliased circle as a single quad through a dedicated SDF shader,
        /// so it adds one drawable with no masking region. <see cref="Circle"/> is a masking
        /// <see cref="CircularContainer"/> around a <see cref="Box"/>, which costs an extra drawable plus a
        /// masking push/pop (and therefore a <c>SetMasking</c> batch flush) per instance, per frame.
        /// Both honour per-corner gradient colours, which the outer/inner gradients rely on.
        /// </remarks>
        private static Drawable createFill(Vector2 size)
        {
            if (MosuOptimisationToggles.ArgonFastCircles)
            {
                return new FastCircle
                {
                    Size = size,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                };
            }

            return new Circle
            {
                Size = size,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
            };
        }

        /// <summary>
        /// Creates the host for the kiai flash layer.
        /// </summary>
        /// <remarks>
        /// Masking starts off and is only switched on in <see cref="load"/> if a flash is actually added and that
        /// flash needs clipping. An empty masking container would still push and pop a masking region -- and so
        /// force a <c>SetMasking</c> batch flush -- for every live hit object while drawing nothing.
        /// </remarks>
        private static CircularContainer createKiaiContainer() => new CircularContainer
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            Size = circle_size,
        };

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IRenderer renderer)
        {
            var drawableOsuObject = (DrawableOsuHitObject)drawableObject;

            accentColour.BindTo(drawableObject.AccentColour);
            indexInCurrentCombo.BindTo(drawableOsuObject.IndexInCurrentComboBindable);

            configHitLighting = config.GetBindable<bool>(OsuSetting.HitLighting);

            if (lite)
            {
                liteBody!.Texture = ArgonLiteCircleTextures.GetBody(renderer, withOuterFill);
                liteRing!.Texture = ArgonLiteCircleTextures.GetRing(renderer);

                // No kiai wiring: the lite tree drops KiaiFlash the same way the default skin's
                // perf branch does (see CirclePiece).
                return;
            }

            // The kiai layer is a BeatSyncedContainer: every live instance runs two control-point binary searches
            // per update frame. Leave the host childless (and unmasked) when the flash could never become visible.
            if (kiaiFlashCanFire())
            {
                bool circularFill = MosuOptimisationToggles.ArgonFastCircles;

                kiaiContainer!.Child = new KiaiFlash(circularFill) { RelativeSizeAxes = Axes.Both };

                // A FastCircle fill is already round; only the Box fill needs the masking region to clip it.
                if (!circularFill)
                    kiaiContainer.Masking = true;
            }
        }

        private bool kiaiFlashCanFire()
        {
            if (!MosuOptimisationToggles.SkipUnusedKiaiFlash)
                return true;

            if (SkinPerformanceMode.ShouldDisableKiaiFlashing)
                return false;

            // Fall back to creating the layer when the beatmap is not resolvable (skin editor, isolated test scenes).
            if (beatmap == null)
                return true;

            foreach (var effectPoint in beatmap.ControlPointInfo.EffectPoints)
            {
                if (effectPoint.KiaiMode)
                    return true;
            }

            return false;
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

            indexInCurrentCombo.BindValueChanged(index =>
            {
                string newText = (index.NewValue + 1).ToString();

                if (!number.Text.Equals(newText))
                    number.Text = newText;
            }, true);

            accentColour.BindValueChanged(colour =>
            {
                if (lite)
                {
                    // Flat tint is exact: every layered value was texel(1.0) x accent*k; the lite body
                    // renders texel(k) x accent — the same product (see ArgonLiteCircleTextures).
                    liteBody!.Colour = colour.NewValue;
                }
                else
                {
                    // A colour transform is applied.
                    // Without removing transforms first, when it is rewound it may apply an old colour.
                    outerGradient!.ClearTransforms(targetMember: nameof(Colour));
                    outerGradient.Colour = ColourInfo.GradientVertical(colour.NewValue, colour.NewValue.Darken(0.1f));

                    kiaiContainer!.Colour = colour.NewValue;
                    outerFill!.Colour = innerFill!.Colour = colour.NewValue.Darken(4);
                    innerGradient!.Colour = ColourInfo.GradientVertical(colour.NewValue.Darken(0.5f), colour.NewValue.Darken(0.6f));
                    flash!.Colour = colour.NewValue;
                }

                // Accent colour may be changed many times during a paused gameplay state.
                // Schedule the change to avoid transforms piling up.
                Scheduler.AddOnce(() =>
                {
                    ApplyTransformsAt(double.MinValue, true);
                    ClearTransformsAfter(double.MinValue, true);

                    updateStateTransforms(drawableObject, drawableObject.State.Value);
                });
            }, true);

            drawableObject.ApplyCustomUpdateState += updateStateTransforms;
        }

        private void updateStateTransforms(DrawableHitObject drawableHitObject, ArmedState state)
        {
            using (BeginAbsoluteSequence(drawableObject.HitStateUpdateTime))
            {
                switch (state)
                {
                    case ArmedState.Hit:
                        if (lite)
                        {
                            // The lite tree has no flash/border/gradient pieces to animate; mirror the
                            // default skin's perf branch (immediate fade at HitStateUpdateTime).
                            this.FadeOut();
                            break;
                        }

                        // Fade out time is at a maximum of 800. Must match `DrawableHitCircle`'s arbitrary lifetime spec.
                        const double fade_out_time = 800;
                        const double flash_in_duration = 150;
                        const double resize_duration = 400;

                        const float shrink_size = 0.8f;

                        // Animating with the number present is distracting.
                        // The number disappearing is hidden by the bright flash.
                        number.FadeOut(flash_in_duration / 2);

                        // The fill layers add too much noise during the explosion animation.
                        // They will be hidden by the additive effects anyway.
                        outerFill!.FadeOut(flash_in_duration, Easing.OutQuint);
                        innerFill!.FadeOut(flash_in_duration, Easing.OutQuint);

                        // The inner-most gradient should actually be resizing, but is only visible for
                        // a few milliseconds before it's hidden by the flash, so it's pointless overhead to bother with it.
                        innerGradient!.FadeOut(flash_in_duration, Easing.OutQuint);

                        // The border is always white, but after hit it gets coloured by the skin/beatmap's colouring.
                        // A gradient is applied to make the border less prominent over the course of the animation.
                        // Without this, the border dominates the visual presence of the explosion animation in a bad way.
                        border!.TransformTo(nameof
                            (BorderColour), ColourInfo.GradientVertical(
                            accentColour.Value.Opacity(0.5f),
                            accentColour.Value.Opacity(0)), fade_out_time);

                        // The outer ring shrinks immediately, but accounts for its thickness so it doesn't overlap the inner
                        // gradient layers.
                        border.ResizeTo(Size * shrink_size + new Vector2(border!.BorderThickness), resize_duration, Easing.OutElasticHalf);

                        // Kiai flash should track the overall size but also be cleaned up quite fast, so we don't get additional
                        // flashes after the hit animation is already in a mostly-completed state.
                        kiaiContainer!.ResizeTo(Size * shrink_size, resize_duration, Easing.OutElasticHalf);
                        kiaiContainer.FadeOut(flash_in_duration, Easing.OutQuint);

                        // The outer gradient is resize with a slight delay from the border.
                        // This is to give it a bomb-like effect, with the border "triggering" its animation when getting close.
                        using (BeginDelayedSequence(flash_in_duration / 12))
                        {
                            outerGradient!.ResizeTo(OUTER_GRADIENT_SIZE * shrink_size, resize_duration, Easing.OutElasticHalf);

                            outerGradient!
                                .FadeColour(Color4.White, 80)
                                .Then()
                                .FadeOut(flash_in_duration);
                        }

                        if (configHitLighting.Value)
                        {
                            flash!.HitLighting = true;
                            flash.FadeTo(1, flash_in_duration, Easing.OutQuint);

                            this.FadeOut(fade_out_time, Easing.OutQuad);
                        }
                        else
                        {
                            flash!.HitLighting = false;
                            flash.FadeTo(1, flash_in_duration, Easing.OutQuint)
                                 .Then()
                                 .FadeOut(flash_in_duration, Easing.OutQuint);

                            this.FadeOut(fade_out_time * 0.8f, Easing.OutQuad);
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

        private partial class FlashPiece : Circle
        {
            public FlashPiece()
            {
                Size = new Vector2(OsuHitObject.OBJECT_RADIUS);

                Anchor = Anchor.Centre;
                Origin = Anchor.Centre;

                Alpha = 0;
                Blending = BlendingParameters.Additive;

                // The edge effect provides the fill due to not being rendered hollow.
                Child.Alpha = 0;
                Child.AlwaysPresent = true;
            }

            public bool HitLighting
            {
                get => hitLighting;
                set
                {
                    if (hitLighting == value)
                        return;

                    hitLighting = value;
                    applyEdgeEffect();
                }
            }

            private bool hitLighting;
            private ColourInfo lastEdgeColour;
            private float lastEdgeRadius = -1;

            protected override void LoadComplete()
            {
                base.LoadComplete();
                applyEdgeEffect();
            }

            protected override void Update()
            {
                if (Alpha <= 0)
                    return;

                base.Update();
                applyEdgeEffect();
            }

            private void applyEdgeEffect()
            {
                float radius = OsuHitObject.OBJECT_RADIUS * (hitLighting ? 1.2f : 0.6f);

                if (Colour.Equals(lastEdgeColour) && radius == lastEdgeRadius)
                    return;

                lastEdgeColour = Colour;
                lastEdgeRadius = radius;

                EdgeEffect = new EdgeEffectParameters
                {
                    Type = EdgeEffectType.Glow,
                    Colour = Colour,
                    Radius = radius,
                };
            }
        }
    }
}
