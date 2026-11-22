// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Localisation;
using osu.Game.Localisation;

namespace osu.Game.Online.Leaderboards
{
    public enum LeaderboardSortMode
    {
        [LocalisableDescription(typeof(BeatmapLeaderboardWedgeStrings), nameof(BeatmapLeaderboardWedgeStrings.Score))]
        Score,

        [LocalisableDescription(typeof(BeatmapLeaderboardWedgeStrings), nameof(BeatmapLeaderboardWedgeStrings.Accuracy))]
        Accuracy,

        [LocalisableDescription(typeof(BeatmapLeaderboardWedgeStrings), nameof(BeatmapLeaderboardWedgeStrings.MaxCombo))]
        MaxCombo,

        [LocalisableDescription(typeof(BeatmapLeaderboardWedgeStrings), nameof(BeatmapLeaderboardWedgeStrings.Misses))]
        Misses,

        [LocalisableDescription(typeof(BeatmapLeaderboardWedgeStrings), nameof(BeatmapLeaderboardWedgeStrings.Date))]
        Date,

        [Description("PP"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3834))]
        PP,
    }
}
