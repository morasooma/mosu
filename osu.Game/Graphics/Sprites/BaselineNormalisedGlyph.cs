// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Textures;
using osu.Framework.Text;

namespace osu.Game.Graphics.Sprites
{
    /// <summary>
    /// Aligns a glyph's baseline and vertical offset to a reference glyph from another font.
    /// </summary>
    internal sealed class BaselineNormalisedGlyph : ITexturedCharacterGlyph
    {
        private readonly ITexturedCharacterGlyph inner;
        private readonly float baselineOffset;

        public BaselineNormalisedGlyph(ITexturedCharacterGlyph inner, ITexturedCharacterGlyph reference)
        {
            this.inner = inner;
            baselineOffset = reference.Baseline - inner.Baseline;
        }

        public Texture Texture => inner.Texture;

        public float XOffset => inner.XOffset;

        public float YOffset => inner.YOffset + baselineOffset;

        public float XAdvance => inner.XAdvance;

        public float Baseline => inner.Baseline + baselineOffset;

        public char Character => inner.Character;

        public float Width => inner.Width;

        public float Height => inner.Height;

        public float GetKerning<T>(T lastGlyph)
            where T : ICharacterGlyph
            => inner.GetKerning(lastGlyph);
    }
}