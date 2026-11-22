using System;
using System.Collections.Generic;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.OpenGL.Textures;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osuTK;

namespace osu.Framework.Graphics.OpenGL
{
    // Uses the original atlas pages, UVs and shadow/glyph order. Custom shaders,
    // wrap modes and incompatible page sizes/filtering keep the ordinary path.
    internal sealed class MultiTextureTextBatch
    {
        internal const int MaxTextures = 8;

        private readonly IVertexBatch<MultiTextureVertex2D> batch;
        private readonly Texture[] textures = new Texture[MaxTextures];
        internal long DrawnTexts;

        public MultiTextureTextBatch(GLRenderer renderer)
            : this(((IRenderer)renderer).CreateQuadBatch<MultiTextureVertex2D>(1024, 8))
        {
        }

        internal MultiTextureTextBatch(IVertexBatch<MultiTextureVertex2D> batch) => this.batch = batch;

        public bool Draw(Renderer renderer, IShader shader, IReadOnlyList<SpriteText.ScreenSpaceCharacterPart> parts,
                         ColourInfo colour, bool shadow, ColourInfo shadowColour, Vector2 shadowOffset)
        {
            try
            {
                int count = collectTextures(parts);
                if (count < 2)
                    return false;

                // All declared samplers require a valid binding. Unused slots
                // refer to page zero; no texture data is copied or allocated.
                for (int i = count; i < textures.Length; i++)
                    textures[i] = textures[0];

                if (!renderer.BindTextureBatch(textures))
                    return false;

                // A cold upload can switch a GL texture to manual mipmaps.
                // Recheck the sampler contract after all uploads have finished.
                var firstPage = (GLTexture)textures[0].NativeTexture;
                for (int i = 1; i < count; i++)
                {
                    if (!firstPage.CanShareTextLod((GLTexture)textures[i].NativeTexture))
                        return false;
                }

                shader.Bind();
                try
                {
                    for (int i = 0; i < parts.Count; i++)
                    {
                        var part = parts[i];
                        int index = find(part.Texture, count);
                        if (shadow)
                        {
                            var quad = part.DrawQuad;
                            add(renderer, part.Texture, new Quad(quad.TopLeft + shadowOffset, quad.TopRight + shadowOffset,
                                quad.BottomLeft + shadowOffset, quad.BottomRight + shadowOffset), shadowColour, part.InflationPercentage, index);
                        }
                        add(renderer, part.Texture, part.DrawQuad, colour, part.InflationPercentage, index);
                    }
                }
                finally
                {
                    shader.Unbind();
                }
                DrawnTexts++;
                return true;
            }
            finally
            {
                // Neither successful draws nor fallback retain glyph/page refs.
                Array.Clear(textures);
            }
        }

        private int collectTextures(IReadOnlyList<SpriteText.ScreenSpaceCharacterPart> parts)
        {
            int count = 0;
            for (int i = 0; i < parts.Count; i++)
            {
                Texture texture = parts[i].Texture;
                if (!texture.Available || !texture.IsAtlasTexture || texture is TextureWhitePixel
                    || texture.WrapModeS != WrapMode.None || texture.WrapModeT != WrapMode.None
                    || texture.NativeTexture is not GLTexture native || native.MipLevel != null)
                    return 0;

                // Querying LOD from page zero is valid only for matching native
                // dimensions and sampler state, including manually chosen LOD.
                if (count > 0 && !native.CanShareTextLod((GLTexture)textures[0].NativeTexture))
                    return 0;

                if (find(texture, count) >= 0)
                    continue;

                if (count == textures.Length)
                    return 0;

                textures[count++] = texture;
            }
            return count;
        }

        private int find(Texture texture, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (texture.HasSameNativeTexture(textures[i]))
                    return i;
            }
            return -1;
        }

        private void add(IRenderer renderer, Texture texture, Quad quad, ColourInfo colour, Vector2 inflation, int index)
        {
            RectangleF rect = texture.GetTextureRect();
            Vector2 blend = new Vector2(inflation.X * rect.Width, inflation.Y * rect.Height);
            RectangleF coord = rect.Inflate(blend);
            var texRect = new Vector4(rect.Left, rect.Top, rect.Right, rect.Bottom);

            batch.Add(new MultiTextureVertex2D { TextureIndex = index, Vertex = new TexturedVertex2D(renderer)
            {
                Position = quad.BottomLeft, TexturePosition = new Vector2(coord.Left, coord.Bottom),
                TextureRect = texRect, BlendRange = blend, Colour = colour.BottomLeft.SRGB
            } });
            batch.Add(new MultiTextureVertex2D { TextureIndex = index, Vertex = new TexturedVertex2D(renderer)
            {
                Position = quad.BottomRight, TexturePosition = new Vector2(coord.Right, coord.Bottom),
                TextureRect = texRect, BlendRange = blend, Colour = colour.BottomRight.SRGB
            } });
            batch.Add(new MultiTextureVertex2D { TextureIndex = index, Vertex = new TexturedVertex2D(renderer)
            {
                Position = quad.TopRight, TexturePosition = new Vector2(coord.Right, coord.Top),
                TextureRect = texRect, BlendRange = blend, Colour = colour.TopRight.SRGB
            } });
            batch.Add(new MultiTextureVertex2D { TextureIndex = index, Vertex = new TexturedVertex2D(renderer)
            {
                Position = quad.TopLeft, TexturePosition = new Vector2(coord.Left, coord.Top),
                TextureRect = texRect, BlendRange = blend, Colour = colour.TopLeft.SRGB
            } });

            RendererExtensions.RecordQuadPixels(renderer, quad);
        }
    }
}
