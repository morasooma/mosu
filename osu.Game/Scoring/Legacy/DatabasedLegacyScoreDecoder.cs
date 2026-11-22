// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Rulesets;

namespace osu.Game.Scoring.Legacy
{
    /// <summary>
    /// A <see cref="LegacyScoreDecoder"/> which retrieves the applicable <see cref="Beatmap"/> and <see cref="Ruleset"/>
    /// for the score from the database.
    /// </summary>
    public class DatabasedLegacyScoreDecoder : LegacyScoreDecoder
    {
        private readonly IRulesetStore rulesets;
        private readonly BeatmapManager beatmaps;

        private readonly BeatmapInfo knownBeatmap;

        public DatabasedLegacyScoreDecoder(IRulesetStore rulesets, BeatmapManager beatmaps, BeatmapInfo knownBeatmap = null)
        {
            this.rulesets = rulesets;
            this.beatmaps = beatmaps;
            this.knownBeatmap = knownBeatmap;
        }

        protected override Ruleset GetRuleset(int rulesetId) => rulesets.GetRuleset(rulesetId)?.CreateInstance();

        protected override WorkingBeatmap GetBeatmap(string md5Hash)
        {
            var beatmap = beatmaps.QueryBeatmap(b => b.MD5Hash == md5Hash);

            // Scores played directly from stable retain their beatmap link, but the map
            // has no BeatmapSet in Realm and is deliberately excluded from QueryBeatmap.
            // Only reuse that link when it identifies the exact replay version.
            if (beatmap == null && knownBeatmap != null && StablePathManager.IsStableBeatmap(knownBeatmap.ID)
                && !string.IsNullOrEmpty(md5Hash) && string.Equals(knownBeatmap.MD5Hash, md5Hash, StringComparison.OrdinalIgnoreCase))
                beatmap = knownBeatmap;

            return beatmaps.GetWorkingBeatmap(beatmap);
        }
    }
}
