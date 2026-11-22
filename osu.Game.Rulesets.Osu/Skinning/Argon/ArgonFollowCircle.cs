// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Argon
{
    public partial class ArgonFollowCircle : FollowCircle
    {
        private bool directChildrenLifeStable;

        // Ring-only tree (opt-in visual change): the masked border + ~155k px additive interior fill
        // collapse into a single accent-tinted ring sprite drawn as a segment mesh, so the transparent
        // interior is never rasterised. Opt-in via the ForkArgonFollowRing checkbox, force-disabled by
        // MOSU_ARGON_FOLLOW_RING=0. See MosuOptimisationToggles.ARGON_FOLLOW_RING.
        private readonly bool ringOnly = SkinPerformanceMode.ShouldUseArgonFollowRing;

        private readonly CircularContainer? circleContainer;
        private readonly Box? circleFill;
        private readonly RingSprite? ring;

        private readonly IBindable<Color4> accentColour = new Bindable<Color4>();

        [Resolved(canBeNull: true)]
        private DrawableHitObject? parentObject { get; set; }

        public ArgonFollowCircle()
        {
            if (ringOnly)
            {
                // The shared lite ring band is BORDER_THICKNESS (~4.41 of 128) thick versus the layered
                // path's border of 4 — ~10% thicker, accepted for texture reuse in an opt-in variant.
                InternalChild = ring = new RingSprite
                {
                    RelativeSizeAxes = Axes.Both,
                    Blending = BlendingParameters.Additive,
                };
            }
            else
            {
                InternalChild = circleContainer = new CircularContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    BorderThickness = 4,
                    Blending = BlendingParameters.Additive,
                    Child = circleFill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Alpha = 0.3f,
                    }
                };
            }
        }

        [BackgroundDependencyLoader]
        private void load(IRenderer renderer)
        {
            if (parentObject != null)
                accentColour.BindTo(parentObject.AccentColour);

            if (ring != null)
                ring.Texture = ArgonLiteCircleTextures.GetRing(renderer);
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
                if (ringOnly)
                {
                    // The ring texture is white-on-alpha, so the flat gradient tint reproduces the
                    // layered path's BorderColour exactly. The interior fill is deliberately dropped.
                    ring!.Colour = ColourInfo.GradientVertical(colour.NewValue, colour.NewValue.Darken(0.5f));
                }
                else
                {
                    circleContainer!.BorderColour = ColourInfo.GradientVertical(colour.NewValue, colour.NewValue.Darken(0.5f));
                    circleFill!.Colour = ColourInfo.GradientVertical(colour.NewValue, colour.NewValue.Darken(0.5f));
                }
            }, true);
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

        /// <summary>
        /// Draws the lite ring texture as a 16-segment ring band instead of a full quad.
        /// </summary>
        /// <remarks>
        /// The ring band of <see cref="ArgonLiteCircleTextures"/> occupies r = 0.931–1.0 of the quad
        /// half-width; a full quad would still rasterise and additively blend the transparent interior
        /// (the follow circle was measured at ~155k px/frame while tracking). The mesh removes
        /// the inner 16-gon (inscribed at r = 0.9, whose chords sag to r = 0.883 — safely inside the
        /// invisible centre) — about 62% of the quad area — while emitting the same texture/shader/batch as
        /// the original quad. Geometry rule copied from <c>DefaultApproachCircle</c>'s ring mesh, with the
        /// inner polygon re-derived for this texture's band (margin ~3 logical px, same as the original's).
        /// </remarks>
        private partial class RingSprite : Sprite
        {
            protected override DrawNode CreateDrawNode() => new RingMeshSpriteDrawNode(this);

            private class RingMeshSpriteDrawNode : SpriteDrawNode
            {
                private const int segments = 16;
                private const float inner_radius = 0.9f;

                public RingMeshSpriteDrawNode(RingSprite source)
                    : base(source)
                {
                }

                protected override void Blit(IRenderer renderer)
                {
                    if (DrawRectangle.Width == 0 || DrawRectangle.Height == 0)
                        return;

                    if (!renderer.BindTexture(Texture))
                        return;

                    RectangleF texRect = Texture.GetTextureRect();
                    var textureBounds = new Vector4(texRect.Left, texRect.Top, texRect.Right, texRect.Bottom);

                    // TextureCoords is in display-pixel space of the texture; vertices need normalised atlas
                    // UVs, so it must pass through GetTextureRect the same way DrawQuad's textureCoords does.
                    RectangleF coordRect = Texture.GetTextureRect(TextureCoords);

                    Quad quad = ScreenSpaceDrawQuad;
                    Vector2 topLeft = quad.TopLeft;
                    Vector2 xSpan = quad.TopRight - quad.TopLeft;
                    Vector2 ySpan = quad.BottomLeft - quad.TopLeft;

                    long meshArea = 0;

                    for (int i = 0; i < segments; i++)
                    {
                        // Unit-space (0..1) ring segment: inner vertices on the inscribed 16-gon, outer vertices
                        // radially projected onto the quad boundary (which naturally chamfers the corners).
                        float angle0 = MathF.Tau * i / segments;
                        float angle1 = MathF.Tau * (i + 1) / segments;

                        Vector2 inner0 = ringPoint(angle0, inner_radius);
                        Vector2 inner1 = ringPoint(angle1, inner_radius);
                        Vector2 outer0 = boundaryPoint(angle0);
                        Vector2 outer1 = boundaryPoint(angle1);

                        renderer.AddDefaultBatchQuad(vertex(inner0), vertex(inner1), vertex(outer1), vertex(outer0));

                        // Trapezoid area in unit space, scaled to screen pixels for the statistics counters.
                        float unitArea = MathF.Abs(cross(inner1 - inner0, outer0 - inner0) + cross(outer0 - outer1, inner1 - outer1)) / 2;
                        meshArea += (long)(unitArea * MathF.Abs(cross(xSpan, ySpan)));

                        TexturedVertex2D vertex(Vector2 unit) => new TexturedVertex2D(renderer)
                        {
                            Position = topLeft + unit.X * xSpan + unit.Y * ySpan,
                            TexturePosition = new Vector2(
                                coordRect.Left + unit.X * coordRect.Width,
                                coordRect.Top + unit.Y * coordRect.Height),
                            TextureRect = textureBounds,
                            BlendRange = Vector2.Zero,
                            Colour = DrawColourInfo.Colour.Interpolate(unit).SRGB,
                        };
                    }

                    MosuRendererExtensions.RecordCustomMeshPixels(meshArea);
                }

                private static float cross(Vector2 a, Vector2 b) => a.X * b.Y - a.Y * b.X;

                private static Vector2 ringPoint(float angle, float radius) =>
                    new Vector2(0.5f + 0.5f * radius * MathF.Cos(angle), 0.5f + 0.5f * radius * MathF.Sin(angle));

                /// <summary>
                /// Radial projection of <paramref name="angle"/> onto the unit-square boundary.
                /// </summary>
                private static Vector2 boundaryPoint(float angle)
                {
                    float cos = MathF.Cos(angle);
                    float sin = MathF.Sin(angle);
                    float scale = 0.5f / MathF.Max(MathF.Abs(cos), MathF.Abs(sin));
                    return new Vector2(0.5f + scale * cos, 0.5f + scale * sin);
                }
            }
        }
    }
}
