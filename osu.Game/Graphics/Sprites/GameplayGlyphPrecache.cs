using System.Threading;
using osu.Framework.Development;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Text;

namespace osu.Game.Graphics.Sprites
{
    /// <summary>
    /// Uses the normal TextBuilder lookup order during background loading.
    /// No drawable layout, input, or native GL calls run here.
    /// </summary>
    internal static class GameplayGlyphPrecache
    {
        // A fixed UI alphabet, not a sweep through every page of Noto/CJK fonts.
        internal const string UI_CHARACTERS = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~"
                                             + "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯабвгдеёжзийклмнопрстуфхцчшщъыьэюя…•×±–—";

        internal static void Prepare(ITexturedGlyphLookupStore store, FontUsage font, string text,
                                     CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            // Synchronous loads also occur on Update in tests/editor. Never move a
            // full precache into that frame; ordinary lookup remains the fallback.
            if (ThreadSafety.IsUpdateThread)
                return;

            foreach (char character in text)
            {
                cancellationToken.ThrowIfCancellationRequested();
                _ = store.Get(font.FontName, character)
                    ?? store.Get(font.FontNameNoFamily, character)
                    ?? store.Get(string.Empty, character);
            }
        }
    }
}
