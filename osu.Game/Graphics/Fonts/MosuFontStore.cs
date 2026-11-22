using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Text;

namespace osu.Game.Graphics.Fonts
{
    /// <summary>
    /// Fits the new bitmap families to the client's existing 100px font coordinate system.
    /// Scale applies equally to ink, advance and kerning. UI letters receive a small
    /// symmetric side bearing, without stretching outlines or disabling kerning.
    /// </summary>
    internal sealed class MosuFontStore : FontStore, ITexturedGlyphLookupStore
    {
        private const float ui_tracking = 0.018f; // Keep the accepted Cyrillic spacing.
        private const float latin_tracking = 0.004f; // 0.096px at Font.Size=24; no wide Latin letterspacing.

        private readonly Dictionary<string, string> headingFamilies = new Dictionary<string, string>(StringComparer.Ordinal);
        internal void SetLatinHeadingFamily(string family, string replacement) => headingFamilies[family] = replacement;

        private readonly Dictionary<string, string> metricFamilies = new Dictionary<string, string>(StringComparer.Ordinal);
        internal void SetMetricsFamily(string family, string metricsFamily) => metricFamilies[family] = metricsFamily;

        private readonly ConcurrentDictionary<(string?, char), ITexturedCharacterGlyph> adjustedGlyphs = new ConcurrentDictionary<(string?, char), ITexturedCharacterGlyph>();

        public MosuFontStore(IRenderer renderer)
            : base(renderer, minFilterMode: TextureFilteringMode.Linear)
        {
        }

        public new ITexturedCharacterGlyph? Get(string? fontName, char character)
        {
            if (adjustedGlyphs.TryGetValue((fontName, character), out var cached))
                return cached;

            // Comfortaa's heaviest real face is still much lighter than the old
            // Alternate headings. Use the already bundled heavier Nunito face for
            // Latin legacy headings only. Direct family selections and Cyrillic
            // keep their own faces; do not synthesize bold or modify the atlases.
            string? actualName = fontName;
            if (isLatinUiCharacter(character))
            {
                actualName = fontName switch
                {
                    "Torus-Alternate-SemiBold" => "Nunito-Bold",
                    "Torus-Alternate-Bold" => "Nunito-Black",
                    _ => fontName,
                };
                foreach (var heading in headingFamilies)
                {
                    if (fontName == heading.Key + "-SemiBold") actualName = heading.Value + "-Bold";
                    if (fontName == heading.Key + "-Bold") actualName = heading.Value + "-Black";
                }
            }

            var glyph = base.Get(actualName, character);
            if (glyph == null)
                return null;

            // Empty-family and weight-only lookups follow the actual source face.
            // FontUsage.FontNameNoFamily is "-Regular" / "-Bold", including
            // the hyphen. It is not a registered face: resolve its actual source
            // before applying metrics, just like the empty-family fallback.
            if (string.IsNullOrEmpty(actualName) || !sourceNames.Contains(actualName))
                actualName = findSourceName(actualName, character);

            (float capHeight, float baseline) = metrics(actualName);
            float actualCapHeight = base.Get(actualName, 'H')?.Height ?? 0;
            return adjustedGlyphs[(fontName, character)] = capHeight == 0 || actualCapHeight <= 0
                ? glyph : new AdjustedGlyph(glyph, capHeight / actualCapHeight, baseline,
                    capHeight == 0.60f && !char.IsWhiteSpace(character) && !char.IsControl(character)
                    && CharUnicodeInfo.GetUnicodeCategory(character) is not
                        (UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark
                        or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.Format)
                        ? isLatinUiCharacter(character) ? latin_tracking : ui_tracking : 0);
        }

        private static bool isLatinUiCharacter(char character) => character is >= '\u0021' and <= '\u007e'
            or >= '\u00a1' and <= '\u024f' or >= '\u1e00' and <= '\u1eff';

        private string? findSourceName(string? requested, char character)
        {
            foreach (var name in sourceNames)
            {
                if (name.EndsWith(requested ?? string.Empty, StringComparison.Ordinal) && base.Get(name, character) != null)
                    return name;
            }
            return requested;
        }

        private readonly List<string> sourceNames = new List<string>();

        public override void AddTextureSource(IResourceStore<TextureUpload> store)
        {
            if (store is IGlyphStore glyphs)
                sourceNames.Add(glyphs.FontName);
            base.AddTextureSource(store);
        }

        public new Task<ITexturedCharacterGlyph?> GetAsync(string fontName, char character) => Task.Run(() => Get(fontName, character));

        private (float capHeight, float baseline) metrics(string? name)
        {
            if (name != null)
                foreach (var mapping in metricFamilies)
                    if (name.StartsWith(mapping.Key + "-", StringComparison.Ordinal))
                    {
                        name = mapping.Value + name[mapping.Key.Length..];
                        break;
                    }
            if (name == null)
                return (0, 0);
            // Balance cap height and x-height rather than matching H alone. The old
            // UI has x=43px; these families need H=60px to keep lowercase near 42px
            // and ordinary label widths close to the previous layout. Baselines stay
            // at 84px. Display fonts retain Venera's H=69px and baseline=75px.
            if (name.StartsWith("Nunito-", StringComparison.Ordinal)
                || (name.StartsWith("Torus-", StringComparison.Ordinal) && !name.StartsWith("Torus-Alternate-", StringComparison.Ordinal)))
                return (0.60f, 0.84f);
            if (name.StartsWith("MosuRoundedAlt-", StringComparison.Ordinal) || name.StartsWith("Torus-Alternate-", StringComparison.Ordinal))
                return (0.60f, 0.84f);
            if (name.StartsWith("Syncopate-", StringComparison.Ordinal) || name == "Venera-Light")
                return (0.69f, 0.75f);
            if (name.StartsWith("Michroma-", StringComparison.Ordinal) || name.StartsWith("Venera-", StringComparison.Ordinal))
                return (0.69f, 0.75f);
            // Retained Inter/Noto already use the original client metrics.
            return (0, 0);
        }

        private sealed class AdjustedGlyph : ITexturedCharacterGlyph
        {
            private readonly ITexturedCharacterGlyph source;
            private readonly float scale;
            private readonly float tracking;

            public AdjustedGlyph(ITexturedCharacterGlyph source, float scale, float baseline, float tracking)
            {
                this.source = source;
                this.scale = scale;
                this.tracking = tracking;
                Baseline = baseline;
            }

            public Texture Texture => source.Texture;
            public char Character => source.Character;
            public float XOffset => source.XOffset * scale + tracking / 2;
            public float YOffset => Baseline + (source.YOffset - source.Baseline) * scale;
            public float XAdvance => source.XAdvance * scale + tracking;
            public float Baseline { get; }
            public float Width => source.Width * scale;
            public float Height => source.Height * scale;
            public float GetKerning<T>(T lastGlyph) where T : ICharacterGlyph => source.GetKerning(lastGlyph) * scale;
        }
    }
}
