// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Encodes carousel preview resolution into a texture-store resource name.
    /// This gives every resolution its own cache entry while allowing the underlying
    /// beatmap resource store to keep using the original file name.
    /// </summary>
    internal static class CarouselPreviewTextureRequest
    {
        private const string prefix = "__carousel_preview_resolution__";
        private const char separator = '|';
        private const string classic_prefix = "__carousel_classic_preview__|";

        public static string CreateClassic(string resourceName, int resolutionPercent)
            => Create(classic_prefix + resourceName, resolutionPercent);

        public static (string ResourceName, int ResolutionPercent, bool Classic) ParseLegacy(string requestedName)
        {
            var (name, resolution) = Parse(requestedName);
            bool classic = name.StartsWith(classic_prefix, StringComparison.Ordinal);
            return (classic ? name[classic_prefix.Length..] : name, resolution, classic);
        }

        public static string Create(string resourceName, int resolutionPercent)
            => $"{prefix}{Math.Clamp(resolutionPercent, 25, 100)}{separator}{resourceName}";

        public static (string ResourceName, int ResolutionPercent) Parse(string requestedName)
        {
            if (!requestedName.StartsWith(prefix, StringComparison.Ordinal))
                return (requestedName, 100);

            int separatorIndex = requestedName.IndexOf(separator, prefix.Length);

            if (separatorIndex < 0
                || !int.TryParse(requestedName.AsSpan(prefix.Length, separatorIndex - prefix.Length), out int resolutionPercent))
                return (requestedName, 100);

            return (requestedName[(separatorIndex + 1)..], Math.Clamp(resolutionPercent, 25, 100));
        }
    }
}
