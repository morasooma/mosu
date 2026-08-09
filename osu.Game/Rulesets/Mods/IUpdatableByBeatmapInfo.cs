// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Allows a mod to derive internal state from the currently selected beatmap outside of gameplay.
    /// </summary>
    public interface IUpdatableByBeatmapInfo
    {
        /// <summary>
        /// Updates the mod from the provided beatmap metadata.
        /// </summary>
        void UpdateFromBeatmapInfo(IBeatmapInfo beatmapInfo);
    }
}
