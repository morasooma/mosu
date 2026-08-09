// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Layout;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Rulesets.Dodge.Skinning;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    /// <summary>
    /// A projectile visual whose artwork can change without affecting collision geometry.
    /// </summary>
    public partial class DodgeBulletVisual : CompositeDrawable
    {
        private DodgeBulletShape shape;
        private Vector2 direction;
        private Drawable fill = null!;
        private Drawable? outline;
        private Colour4 fillColour = Colour4.White;
        private Colour4 outlineColour = Colour4.White;
        private float outlineThickness;
        private float shapeScale = 1;
        private readonly LayoutValue outlineLayout = new LayoutValue(Invalidation.DrawSize);

        public DodgeBulletShape Shape
        {
            get => shape;
            set
            {
                if (shape == value && fill != null)
                    return;

                shape = value;
                recreateBody();
                updateRotation();
            }
        }

        public Vector2 Direction
        {
            get => direction;
            set
            {
                if (direction == value)
                    return;

                direction = value;
                updateRotation();
            }
        }

        public Colour4 FillColour
        {
            get => fillColour;
            set
            {
                if (fillColour == value)
                    return;

                fillColour = value;

                if (fill != null)
                    fill.Colour = value;
            }
        }

        public Colour4 OutlineColour
        {
            get => outlineColour;
            set
            {
                if (outlineColour == value)
                    return;

                outlineColour = value;

                if (outline != null)
                    outline.Colour = value;
            }
        }

        public float OutlineThickness
        {
            get => outlineThickness;
            set
            {
                float clamped = Math.Clamp(value, 0, 8);

                if (outlineThickness == clamped)
                    return;

                outlineThickness = clamped;
                syncOutline();
                outlineLayout.Invalidate();
            }
        }

        internal Drawable? OutlineDrawable => outline;

        internal bool HasOutlineDrawable => OutlineDrawable != null;

        internal bool UsesDefaultFastCircle
            => fill is SkinnableDrawable { Drawable: FastCircle };

        public override bool RemoveWhenNotAlive => false;

        public DodgeBulletVisual()
        {
            AddLayout(outlineLayout);
            Origin = Anchor.Centre;
            Size = new Vector2(DodgeBullet.SIZE);
            Shape = DodgeBulletShape.Circle;
        }

        private void recreateBody()
        {
            ClearInternal(true);
            outline = null;

            fill = new SkinnableDrawable(
                new DodgeSkinComponentLookup(getSkinComponent(shape)),
                _ => createShape(),
                ConfineMode.ScaleToFit)
            {
                RelativeSizeAxes = Axes.Both,
            };
            fill.Colour = fillColour;
            AddInternal(fill);
            syncOutline();
            outlineLayout.Invalidate();
        }

        private void syncOutline()
        {
            bool required = outlineThickness > 0;

            if (required == (outline != null))
                return;

            if (required)
            {
                outline = createShape();
                outline.Colour = outlineColour;
                outline.Depth = 1;
                AddInternal(outline);
            }
            else
            {
                RemoveInternal(outline!, true);
                outline = null;
            }
        }

        private Drawable createShape()
        {
            Drawable body = shape switch
            {
                DodgeBulletShape.Square => new Box(),
                DodgeBulletShape.Diamond => new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    RelativeSizeAxes = Axes.Both,
                    Scale = new Vector2(0.72f),
                    Rotation = 45,
                },
                DodgeBulletShape.Triangle => new EquilateralTriangle(),
                // Emitters can keep thousands of circle projectiles alive at once. A framework Circle is a
                // masking CompositeDrawable containing a Box, which creates an extra draw-node subtree and
                // masking work for every projectile. Dodge projectiles are already ordered contiguously, so
                // FastCircle's SDF shader batches efficiently while retaining normal drawable culling.
                _ => new FastCircle(),
            };

            if (shape != DodgeBulletShape.Diamond)
            {
                body.Anchor = Anchor.Centre;
                body.Origin = Anchor.Centre;
                body.RelativeSizeAxes = Axes.Both;
            }

            shapeScale = shape == DodgeBulletShape.Diamond ? 0.72f : 1;
            return body;
        }

        private static DodgeSkinComponents getSkinComponent(DodgeBulletShape shape) => shape switch
        {
            DodgeBulletShape.Square => DodgeSkinComponents.BulletSquare,
            DodgeBulletShape.Diamond => DodgeSkinComponents.BulletDiamond,
            DodgeBulletShape.Triangle => DodgeSkinComponents.BulletTriangle,
            _ => DodgeSkinComponents.BulletCircle,
        };

        private void updateOutline()
        {
            if (outline == null)
                return;

            float expansion = 1 + outlineThickness * 2 / Math.Max(1, Math.Min(DrawWidth, DrawHeight));
            outline.Scale = new Vector2(shapeScale * expansion);
        }

        private void updateRotation() => Rotation = CalculateRotation(shape, direction);

        protected override void Update()
        {
            base.Update();

            if (!outlineLayout.IsValid)
            {
                updateOutline();
                outlineLayout.Validate();
            }
        }

        public static float CalculateRotation(DodgeBulletShape shape, Vector2 direction)
        {
            if (direction.LengthSquared == 0 || shape == DodgeBulletShape.Circle)
                return 0;

            float rotation = MathHelper.RadiansToDegrees(MathF.Atan2(direction.Y, direction.X));

            // Framework triangles point upwards by default, while zero degrees
            // for a trajectory points to the right.
            if (shape == DodgeBulletShape.Triangle)
                rotation += 90;

            return (rotation % 360 + 360) % 360;
        }
    }
}
