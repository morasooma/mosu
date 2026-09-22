// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Screens.Select
{
    internal static class BeatmapAdditionalInfoFormatter
    {
        public static string Format(StarDifficulty starDifficulty)
        {
            if (starDifficulty.CachedAdditionalInfo != null)
                return starDifficulty.CachedAdditionalInfo;

            var performanceAttributes = starDifficulty.PerformanceAttributes;

            if (performanceAttributes == null)
                return $"Combo: {starDifficulty.MaxCombo}x | PP: -";

            string aspects = string.Join(", ", performanceAttributes.GetAttributesForDisplay()
                                                                      .Where(attribute => attribute.PropertyName != nameof(PerformanceAttributes.Total))
                                                                      .Select(attribute => $"{attribute.DisplayName}: {Math.Round(attribute.Value):0}pp"));
            string summary = $"Combo: {starDifficulty.MaxCombo}x | PP: {Math.Round(performanceAttributes.Total):0} pp";

            return string.IsNullOrEmpty(aspects) ? summary : $"{summary} ({aspects})";
        }
    }
}
