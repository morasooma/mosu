// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Framework.Localisation;

namespace osu.Game.Rulesets
{
    /// <summary>
    /// Supplies map-authored visual overrides which must be applied consistently
    /// in normal play, replay and spectator sessions.
    /// </summary>
    public interface IBeatmapVisualOverrideProvider
    {
        bool ForceStoryboard(IBeatmap beatmap);

        bool ForceBeatmapSkin(IBeatmap beatmap);

        LocalisableString VisualOverrideNotice(IBeatmap beatmap);
    }
}
