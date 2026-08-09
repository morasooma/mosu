// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class BestScorePpRankingStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.BestScorePpRanking";

        /// <summary>
        /// "Top PP Score"
        /// </summary>
        public static LocalisableString TopPpScore => new TranslatableString(getKey(@"top_pp_score"), @"Top PP Score");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
