// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Transforms;
using osu.Game.Performance;
using osuTK;

namespace osu.Game.Graphics.Backgrounds
{
    /// <summary>
    /// A background which offers blurring via a <see cref="BufferedContainer"/> on demand.
    /// </summary>
    public partial class Background : CompositeDrawable, IEquatable<Background>
    {
        public readonly Sprite Sprite;

        private readonly string textureName;

        private BufferedContainer bufferedContainer;

        public Background(string textureName = @"")
        {
            this.textureName = textureName;
            RelativeSizeAxes = Axes.Both;

            AddInternal(Sprite = new Sprite
            {
                RelativeSizeAxes = Axes.Both,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                FillMode = FillMode.Fill,
            });
        }

        [BackgroundDependencyLoader]
        private void load(LargeTextureStore textures)
        {
            if (!string.IsNullOrEmpty(textureName))
                Sprite.Texture = textures.Get(textureName);
        }

        // NOTE (R11.B, 2026-07-26): drawing the background with Blending = None while provably opaque was
        // implemented and A/B'd here, and measured NO effect (1255.7 vs 1261.6 avg fps, same exe). On Vega/GCN
        // the destination read of a DCC fast-cleared render target is metadata-only, so there was nothing to
        // save. Removed per the worklog acceptance rule; see RENDERING_PERFORMANCE_WORKLOG_2026-07.md R11.B
        // before re-attempting on different hardware.

        public Vector2 BlurSigma => Vector2.Divide(bufferedContainer?.BlurSigma ?? Vector2.Zero, blurScale);

        /// <summary>
        /// Smoothly adjusts <see cref="IBufferedContainer.BlurSigma"/> over time.
        /// </summary>
        /// <returns>A <see cref="TransformSequence{T}"/> to which further transforms can be added.</returns>
        public void BlurTo(Vector2 newBlurSigma, double duration = 0, Easing easing = Easing.None)
        {
            if (newBlurSigma != Vector2.Zero)
                ensureBufferedContainer();

            if (bufferedContainer != null)
                transformBlurSigma(newBlurSigma, duration, easing);
        }

        // NOTE (R13.G, 2026-07-26): a high-dim FrameBufferScale=0.5 downscale hook lived here and
        // measured NO effect (+0.4% within variance, same exe) — texture-fetch of the fullscreen blit is
        // cache-amortised; the cost is the backbuffer write. Removed per worklog rule §8.
        private void ensureBufferedContainer()
        {
            if (bufferedContainer != null)
                return;

            RemoveInternal(Sprite, false);

            AddInternal(bufferedContainer = new BufferedContainer(cachedFrameBuffer: true)
            {
                RelativeSizeAxes = Axes.Both,
                RedrawOnScale = false,
                Child = Sprite
            });

            applyFrameBufferState();
        }

        // Mosu (R13, lossless, MOSU_PERF_BG_SIGMA0_BYPASS=0 to disable): with zero blur the
        // BufferedContainer is an identity blit of a cached ~9.6MB framebuffer, and the 800ms blur-out at
        // gameplay start repopulates that framebuffer every animation frame. Once blur settles at zero
        // (and no downscale wants the framebuffer), collapse back to the plain sprite.
        protected override void Update()
        {
            base.Update();

            if (bufferedContainer == null || !MosuOptimisationToggles.PerfBackgroundSigma0Bypass)
                return;

            if (blurSigmaBacking != Vector2.Zero)
                return;

            foreach (var transform in Transforms)
            {
                if (transform.TargetMember == nameof(blurSigma))
                    return;
            }

            bufferedContainer.Remove(Sprite, false);
            RemoveInternal(bufferedContainer, true);
            bufferedContainer = null;
            blurScale = Vector2.One;
            AddInternal(Sprite);
        }

        private void transformBlurSigma(Vector2 newBlurSigma, double duration, Easing easing)
            => this.TransformTo(nameof(blurSigma), newBlurSigma, duration, easing);

        private Vector2 blurSigmaBacking = Vector2.Zero;
        private Vector2 blurScale = Vector2.One;

        private Vector2 blurSigma
        {
            get => blurSigmaBacking;
            set
            {
                Debug.Assert(bufferedContainer != null);

                blurSigmaBacking = value;
                applyFrameBufferState();
            }
        }

        private void applyFrameBufferState()
        {
            if (bufferedContainer == null)
                return;

            blurScale = new Vector2(calculateBlurDownscale(blurSigmaBacking.X), calculateBlurDownscale(blurSigmaBacking.Y));

            bufferedContainer.FrameBufferScale = blurScale;
            bufferedContainer.BlurSigma = blurSigmaBacking * blurScale; // If the image is scaled down, the blur radius also needs to be reduced to cover the same pixel block.
        }

        /// <summary>
        /// Determines a factor to downscale the background based on a given blur sigma, in order to reduce the computational complexity of blurs.
        /// </summary>
        /// <param name="sigma">The blur sigma.</param>
        /// <returns>The scale-down factor.</returns>
        private float calculateBlurDownscale(float sigma)
        {
            // If we're blurring within one pixel, scaling down will always result in an undesirable loss of quality.
            // The algorithm below would also cause this value to go above 1, which is likewise undesirable.
            if (sigma <= 1)
                return 1;

            // A good value is one where the loss in quality as a result of downscaling the image is not easily perceivable.
            // The constants here have been experimentally chosen to yield nice transitions by approximating a log curve through the points {{ 1, 1 }, { 4, 0.75 }, { 16, 0.5 }, { 32, 0.25 }}.
            float scale = -0.18f * MathF.Log(0.004f * sigma);

            // To reduce shimmering, the scaling transitions are limited to happen only in increments of 0.2.
            return MathF.Round(scale / 0.2f, MidpointRounding.AwayFromZero) * 0.2f;
        }

        public virtual bool Equals(Background other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;

            return other.GetType() == GetType()
                   && other.textureName == textureName;
        }
    }
}
