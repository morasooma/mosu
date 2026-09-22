// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Editor
{
    /// <summary>
    /// Parsing for editor text boxes. Anything unparseable keeps the previous value rather than
    /// throwing away the entity's configuration.
    /// </summary>
    internal static class EditorValue
    {
        public static int Int(string text, int fallback, int minimum, int maximum) =>
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? Math.Clamp(parsed, minimum, maximum)
                : fallback;

        public static float Float(string text, float fallback, float minimum, float maximum) =>
            float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) && float.IsFinite(parsed)
                ? Math.Clamp(parsed, minimum, maximum)
                : fallback;

        /// <summary>
        /// A field where blank means "no limit".
        /// </summary>
        public static int? OptionalInt(string text, int? fallback, int minimum, int maximum)
        {
            if (string.IsNullOrWhiteSpace(text))
                return null;

            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                ? Math.Clamp(parsed, minimum, maximum)
                : fallback;
        }

        /// <summary>
        /// A yes-or-no field. Written back as «да» or «нет», and read forgivingly: anything that starts
        /// with a yes-ish letter or a one counts as yes.
        /// </summary>
        /// <remarks>
        /// The property panel is made of text boxes, so a checkbox would be the odd one out. Unparseable
        /// text keeps the previous answer, like every other field here.
        /// </remarks>
        public static bool Bool(string text, bool fallback)
        {
            string trimmed = text.Trim();

            if (trimmed.Length == 0)
                return fallback;

            switch (char.ToLowerInvariant(trimmed[0]))
            {
                case 'д':
                case 'y':
                case 't':
                case '1':
                case '+':
                    return true;

                case 'н':
                case 'n':
                case 'f':
                case '0':
                case '-':
                    return false;

                default:
                    return fallback;
            }
        }

        public static string Format(bool value) => value ? "да" : "нет";

        public static string Format(int value) => value.ToString(CultureInfo.InvariantCulture);

        public static string Format(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

        /// <summary>
        /// Formats without a trailing <c>.0</c>, so a whole number reads as one.
        /// </summary>
        public static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
