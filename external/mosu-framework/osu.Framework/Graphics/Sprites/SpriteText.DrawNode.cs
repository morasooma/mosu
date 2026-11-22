// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.OpenGL;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Text;
using osuTK;
using osuTK.Graphics;

namespace osu.Framework.Graphics.Sprites
{
    public partial class SpriteText
    {
        internal class SpriteTextDrawNode : TexturedShaderDrawNode
        {
            protected new SpriteText Source => (SpriteText)base.Source;

            private bool shadow;
            private ColourInfo shadowColour;
            private Vector2 shadowOffset;
            private IShader? multiTextureShader;

            private List<ScreenSpaceCharacterPart>? parts;

            public SpriteTextDrawNode(SpriteText source)
                : base(source)
            {
            }

            public override void ApplyState()
            {
                base.ApplyState();

                updateScreenSpaceCharacters();
                shadow = Source.Shadow;
                multiTextureShader = GetType() == typeof(SpriteTextDrawNode) && ShaderManager.IsStandardTextureShader(Source.TextureShader)
                    ? Source.multiTextureShader : null;

                if (shadow)
                {
                    shadowColour = Source.ShadowColour;
                    shadowOffset = Source.premultipliedShadowOffset;
                }
            }

            protected override void Draw(IRenderer renderer)
            {
                Debug.Assert(parts != null);

                base.Draw(renderer);

                var avgColour = (Color4)DrawColourInfo.Colour.AverageColour;
                float shadowAlpha = MathF.Pow(Math.Max(Math.Max(avgColour.R, avgColour.G), avgColour.B), 2);

                //adjust shadow alpha based on highest component intensity to avoid muddy display of darker text.
                //squared result for quadratic fall-off seems to give the best result.
                var finalShadowColour = DrawColourInfo.Colour;
                finalShadowColour.ApplyChild(shadowColour.MultiplyAlpha(shadowAlpha));

                if (multiTextureShader != null && renderer is GLRenderer gl
                    && gl.DrawMultiTextureText(multiTextureShader, parts, DrawColourInfo.Colour, shadow, finalShadowColour, shadowOffset))
                    return;

                // Exact standard nodes have no uniform callback to preserve when
                // the companion is used. Custom/derived paths still bind normally.
                BindTextureShader(renderer);

                for (int i = 0; i < parts.Count; i++)
                {
                    if (shadow)
                    {
                        var shadowQuad = parts[i].DrawQuad;

                        renderer.DrawQuad(parts[i].Texture,
                            new Quad(
                                shadowQuad.TopLeft + shadowOffset,
                                shadowQuad.TopRight + shadowOffset,
                                shadowQuad.BottomLeft + shadowOffset,
                                shadowQuad.BottomRight + shadowOffset),
                            finalShadowColour, inflationPercentage: parts[i].InflationPercentage);
                    }

                    renderer.DrawQuad(parts[i].Texture, parts[i].DrawQuad, DrawColourInfo.Colour, inflationPercentage: parts[i].InflationPercentage);
                }

                UnbindTextureShader(renderer);
            }

            /// <summary>
            /// The characters in screen space. These are ready to be drawn.
            /// </summary>
            private void updateScreenSpaceCharacters()
            {
                var characters = Source.characters;
                int partCount = characters.Count;

                if (parts == null)
                    parts = new List<ScreenSpaceCharacterPart>(partCount);
                else
                {
                    parts.Clear();
                    parts.EnsureCapacity(partCount);
                }

                Vector2 inflationAmount = DrawInfo.MatrixInverse.ExtractScale().Xy;
                // Resolving characters above may update autosize. Snapshot the same
                // source matrix ToScreenSpace uses after that work, once per text.
                Matrix3 matrix = Source.DrawInfo.Matrix;

                foreach (var character in characters)
                {
                    // Whitespace still participates in layout/kerning, but has no
                    // glyph to rasterise (TextBuilderGlyph.Height is also zero).
                    // Do not submit inflated empty quads or switch atlas pages for it.
                    if (character.IsWhiteSpace())
                        continue;

                    parts.Add(new ScreenSpaceCharacterPart
                    {
                        DrawQuad = Quad.FromRectangle(character.DrawRectangle.Inflate(inflationAmount)) * matrix,
                        InflationPercentage = new Vector2(
                            character.DrawRectangle.Size.X == 0 ? 0 : inflationAmount.X / character.DrawRectangle.Size.X,
                            character.DrawRectangle.Size.Y == 0 ? 0 : inflationAmount.Y / character.DrawRectangle.Size.Y),
                        Texture = character.Texture
                    });
                }
            }
        }

        /// <summary>
        /// A character of a <see cref="SpriteText"/> provided with screen space draw coordinates.
        /// </summary>
        internal struct ScreenSpaceCharacterPart
        {
            /// <summary>
            /// The screen-space quad for the character to be drawn in.
            /// </summary>
            public Quad DrawQuad;

            /// <summary>
            /// Extra padding for the character's texture.
            /// </summary>
            public Vector2 InflationPercentage;

            /// <summary>
            /// The texture to draw the character with.
            /// </summary>
            public Texture Texture;
        }
    }
}
