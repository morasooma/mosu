// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics.Containers;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    public partial class CirclePiece : DirectChildrenLifeOptimisingCompositeDrawable
    {
        [Resolved]
        private DrawableHitObject drawableObject { get; set; } = null!;

        private TrianglesPiece triangles = null!;

        public CirclePiece()
        {
            Size = OsuHitObject.OBJECT_DIMENSIONS;
            // The bundled disc texture is already circular. Masking it requires a buffered
            // subtree per visible hitcircle and is one of the most expensive parts of the
            // default skin. Keep the exact old path unless the explicitly experimental
            // performance mode is enabled.
            Masking = !SkinPerformanceMode.Enabled;

            CornerRadius = Size.X / 2;
            CornerExponent = 2;

            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures)
        {
            var disc = new Sprite
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Texture = textures.Get(@"Gameplay/osu/disc"),
            };

            if (SkinPerformanceMode.Enabled)
            {
                // A single textured quad replaces the masked disc, kiai flash and animated
                // triangle field. This branch is intentionally restricted to skin performance
                // mode because it simplifies the bundled skin's cosmetic detail.
                InternalChild = disc;
                return;
            }

            InternalChildren = new Drawable[]
            {
                disc,
                new KiaiFlash
                {
                    RelativeSizeAxes = Axes.Both,
                },
                triangles = new TrianglesPiece
                {
                    RelativeSizeAxes = Axes.Both,
                    Blending = BlendingParameters.Additive,
                    Alpha = 0.5f,
                }
            };

            drawableObject.HitObjectApplied += onHitObjectApplied;
            onHitObjectApplied(drawableObject);
        }

        private void onHitObjectApplied(DrawableHitObject obj)
        {
            if (obj.HitObject == null)
                return;

            triangles.Reset((int)obj.HitObject.StartTime);
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (drawableObject.IsNotNull())
                drawableObject.HitObjectApplied -= onHitObjectApplied;
        }
    }
}
