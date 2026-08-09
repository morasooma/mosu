// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;

namespace osu.Game.Rulesets.Dodge.Beatmaps
{
    /// <summary>
    /// Captures the mutable editor state before expensive analysis is moved to a worker thread.
    /// </summary>
    internal static class DodgeBeatmapSnapshot
    {
        public static Beatmap<DodgeHitObject> Create(IBeatmap source)
        {
            var difficulty = new BeatmapDifficulty
            {
                ApproachRate = source.Difficulty.ApproachRate,
                CircleSize = source.Difficulty.CircleSize,
                DrainRate = source.Difficulty.DrainRate,
                OverallDifficulty = source.Difficulty.OverallDifficulty,
                SliderMultiplier = source.Difficulty.SliderMultiplier,
                SliderTickRate = source.Difficulty.SliderTickRate,
            };

            DodgeBeatmapSettings.SetPlayerSize(difficulty, DodgeBeatmapSettings.GetPlayerSize(source.Difficulty));
            DodgeBeatmapSettings.SetForceStoryboard(difficulty, DodgeBeatmapSettings.GetForceStoryboard(source.Difficulty));
            DodgeBeatmapSettings.SetForceBeatmapSkin(difficulty, DodgeBeatmapSettings.GetForceBeatmapSkin(source.Difficulty));

            return new Beatmap<DodgeHitObject>
            {
                Difficulty = difficulty,
                HitObjects = source.HitObjects.OfType<DodgeHitObject>()
                                   .Select(DodgeBeatmapConverter.CloneHitObject)
                                   .ToList(),
            };
        }
    }
}
