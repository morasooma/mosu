// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Framework.Text;

namespace osu.Game.Graphics.Sprites
{
    /// <summary>
    /// Redirects Cyrillic glyph lookups to a fallback font that supports Russian characters.
    /// </summary>
    internal class CyrillicFallbackGlyphStore : ITexturedGlyphLookupStore
    {
        public const string DEFAULT_RUSSIAN_FONT = @"Comfortaa Regular.fnt";

        private readonly ITexturedGlyphLookupStore inner;
        private readonly string fallbackFontName;

        public CyrillicFallbackGlyphStore(ITexturedGlyphLookupStore inner, string fallbackFontName = DEFAULT_RUSSIAN_FONT)
        {
            this.inner = inner;
            this.fallbackFontName = fallbackFontName;
        }

        public ITexturedCharacterGlyph? Get(string? fontName, char character)
        {
            if (isCyrillic(character))
            {
                ITexturedCharacterGlyph? glyph = tryGetFallback(character);
                if (glyph != null)
                    return normaliseToPrimaryFont(glyph, fontName);
            }

            return inner.Get(fontName, character);
        }

        public Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character)
        {
            if (isCyrillic(character))
            {
                ITexturedCharacterGlyph? glyph = tryGetFallback(character);
                if (glyph != null)
                    return Task.FromResult(normaliseToPrimaryFont(glyph, fontName));
            }

            return inner.GetAsync(fontName, character);
        }

        private ITexturedCharacterGlyph? normaliseToPrimaryFont(ITexturedCharacterGlyph fallbackGlyph, string? primaryFontName)
        {
            ITexturedCharacterGlyph? referenceGlyph = getReferenceGlyph(primaryFontName);

            if (referenceGlyph == null || referenceGlyph.Baseline == fallbackGlyph.Baseline)
                return fallbackGlyph;

            return new BaselineNormalisedGlyph(fallbackGlyph, referenceGlyph);
        }

        private ITexturedCharacterGlyph? getReferenceGlyph(string? primaryFontName)
        {
            if (string.IsNullOrEmpty(primaryFontName))
                return null;

            return inner.Get(primaryFontName, 'm')
                   ?? inner.Get(primaryFontName, 'a')
                   ?? inner.Get(primaryFontName, 'A');
        }

        private ITexturedCharacterGlyph? tryGetFallback(char character) =>
            inner.Get(fallbackFontName, character)
            ?? inner.Get(trimFontExtension(fallbackFontName), character);

        private static string trimFontExtension(string fontName)
        {
            if (fontName.EndsWith(@".fnt", System.StringComparison.OrdinalIgnoreCase))
                return fontName[..^4];

            return fontName;
        }

        internal static bool isCyrillic(char character) =>
            character is >= '\u0400' and <= '\u04FF'
            or >= '\u0500' and <= '\u052F';
    }
}