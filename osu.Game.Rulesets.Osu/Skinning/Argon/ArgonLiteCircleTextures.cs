// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Rulesets.Osu.Skinning.Argon
{
    /// <summary>
    /// Runtime-generated textures for the skin-performance "lite" Argon circle body
    /// (see <see cref="ArgonMainCirclePiece"/>).
    /// </summary>
    /// <remarks>
    /// The four Argon fill layers are all flat or vertically-graded scalar multiples of the accent
    /// colour, so the whole coloured body collapses into one greyscale radial profile tinted by a
    /// flat quad colour. The white border ring is not an accent multiple, so it lives in a second
    /// region. All regions share one native texture, letting the body and ring quads batch into a
    /// single draw call with the standard sprite shader — deliberately avoiding the extra shader
    /// binds that rejected the FastCircle experiment.
    /// </remarks>
    public static class ArgonLiteCircleTextures
    {
        // 4 texels per logical pixel (512 texels for the 128-unit circle); covers on-screen circle
        // sizes up to ~350px before magnification softening.
        private const int resolution = 512;

        // Guards mip levels of adjacent atlas regions against bleeding into each other.
        private const int padding = 16;

        private const float logical_size = 128;

        private static readonly object generation_lock = new object();

        private static IRenderer? generatedRenderer;
        private static Texture? bodyFilled;
        private static Texture? bodyNoOuterFill;
        private static Texture? ring;

        public static Texture GetBody(IRenderer renderer, bool withOuterFill)
        {
            ensureGenerated(renderer);
            return withOuterFill ? bodyFilled! : bodyNoOuterFill!;
        }

        public static Texture GetRing(IRenderer renderer)
        {
            ensureGenerated(renderer);
            return ring!;
        }

        private static void ensureGenerated(IRenderer renderer)
        {
            lock (generation_lock)
            {
                if (generatedRenderer == renderer)
                    return;

                int width = resolution * 3 + padding * 4;

                var image = new Image<Rgba32>(width, resolution);

                generateRegion(image, regionX(0), (r, v) => bodyTexel(r, v, withOuterFill: true));
                generateRegion(image, regionX(1), (r, v) => bodyTexel(r, v, withOuterFill: false));
                generateRegion(image, regionX(2), (r, _) => ringTexel(r));

                var atlas = renderer.CreateTexture(width, resolution, manualMipmaps: false, filteringMode: TextureFilteringMode.Linear);
                atlas.SetData(new TextureUpload(image));

                bodyFilled = atlas.Crop(new osu.Framework.Graphics.Primitives.RectangleF(regionX(0), 0, resolution, resolution));
                bodyNoOuterFill = atlas.Crop(new osu.Framework.Graphics.Primitives.RectangleF(regionX(1), 0, resolution, resolution));
                ring = atlas.Crop(new osu.Framework.Graphics.Primitives.RectangleF(regionX(2), 0, resolution, resolution));

                generatedRenderer = renderer;
            }
        }

        private static int regionX(int index) => padding + index * (resolution + padding);

        private static void generateRegion(Image<Rgba32> image, int originX, Func<float, float, (float value, float alpha)> texel)
        {
            const float texel_to_logical = logical_size / resolution;

            image.ProcessPixelRows(accessor =>
            {
                for (int ty = 0; ty < resolution; ty++)
                {
                    var row = accessor.GetRowSpan(ty);

                    // v is the logical y offset from the circle centre (osu! y-down, gradients top->bottom).
                    float v = (ty + 0.5f) * texel_to_logical - logical_size / 2;

                    for (int tx = 0; tx < resolution; tx++)
                    {
                        float u = (tx + 0.5f) * texel_to_logical - logical_size / 2;
                        float r = MathF.Sqrt(u * u + v * v);

                        (float value, float alpha) = texel(r, v);

                        byte channel = (byte)Math.Clamp((int)MathF.Round(value * 255), 0, 255);
                        row[originX + tx] = new Rgba32(channel, channel, channel, (byte)Math.Clamp((int)MathF.Round(alpha * 255), 0, 255));
                    }
                }
            });
        }

        // Radii of the layer stack, derived from the ArgonMainCirclePiece constants (diameter / 2).
        private static readonly float inner_fill_radius = ArgonMainCirclePiece.INNER_FILL_SIZE / 2;
        private static readonly float inner_gradient_radius = ArgonMainCirclePiece.INNER_GRADIENT_SIZE / 2;
        private static readonly float outer_gradient_radius = ArgonMainCirclePiece.OUTER_GRADIENT_SIZE / 2;
        private const float outer_fill_radius = (logical_size - 1) / 2;
        private const float ring_outer_radius = logical_size / 2;
        private static readonly float ring_inner_radius = ring_outer_radius - ArgonMainCirclePiece.BORDER_THICKNESS;

        // 1-texel wide transitions: keeps zone boundaries as sharp as the original masked layers
        // while giving bilinear/mip sampling a clean ramp.
        private const float aa_width = logical_size / resolution;

        private static (float value, float alpha) bodyTexel(float r, float v, bool withOuterFill)
        {
            // Layer scalars: Darken(a) divides by (1 + a); gradients are normalised to each
            // layer's own height, exactly like ColourInfo.GradientVertical on the layered path.
            const float dark_fill = 1 / 5f; // Darken(4)

            float innerGradient = lerp(1 / 1.5f, 1 / 1.6f, gradientProgress(v, inner_gradient_radius));
            float outerGradient = lerp(1, 1 / 1.1f, gradientProgress(v, outer_gradient_radius));

            float value = dark_fill;
            value = lerp(value, innerGradient, edge(r, inner_fill_radius));
            value = lerp(value, outerGradient, edge(r, inner_gradient_radius));

            float alpha;

            if (withOuterFill)
            {
                value = lerp(value, dark_fill, edge(r, outer_gradient_radius));
                alpha = 1 - edge(r, outer_fill_radius);
            }
            else
                alpha = 1 - edge(r, outer_gradient_radius);

            return (value, alpha);
        }

        private static (float value, float alpha) ringTexel(float r)
        {
            float alpha = edge(r, ring_inner_radius) * (1 - edge(r, ring_outer_radius));
            return (1, alpha);
        }

        private static float gradientProgress(float v, float radius) => Math.Clamp((v + radius) / (radius * 2), 0, 1);

        /// <summary>0 inside the boundary, 1 outside, with a 1-texel linear ramp centred on it.</summary>
        private static float edge(float r, float boundary) => Math.Clamp((r - boundary) / aa_width + 0.5f, 0, 1);

        private static float lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
