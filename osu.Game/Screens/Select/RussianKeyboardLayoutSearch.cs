namespace osu.Game.Screens.Select
{
    internal static class RussianKeyboardLayoutSearch
    {
        // Only replace Cyrillic keys. Existing Latin text and query punctuation are untouched.
        private const string russian = "ё йцукенгшщзхъфывапролджэячсмитьбюЁ ЙЦУКЕНГШЩЗХЪФЫВАПРОЛДЖЭЯЧСМИТЬБЮ";
        private const string english = "` qwertyuiop[]asdfghjkl;'zxcvbnm,.~ QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>";

        /// <returns>The alternate query, or null when no Cyrillic keys were found.</returns>
        public static string? Convert(string text)
        {
            char[]? converted = null;

            for (int i = 0; i < text.Length; i++)
            {
                // Spaces are included in the lookup rows for readability, not conversion.
                if (text[i] == ' ')
                    continue;

                int index = russian.IndexOf(text[i]);
                if (index < 0)
                    continue;

                converted ??= text.ToCharArray();
                converted[i] = english[index];
            }

            return converted == null ? null : new string(converted);
        }
    }
}
