// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Realms;

namespace osu.Game.Database
{
    [Explicit]
    public class ForkBeatmapData : RealmObject
    {
        [PrimaryKey]
        public Guid BeatmapID { get; set; }

        public double MaxPerformancePoints { get; set; }

        public double RelaxStarRating { get; set; }

        public double RelaxMaxPerformancePoints { get; set; }

        /// <summary>
        /// Relax data for the vanilla lazer PP system. Stored separately from the Mosu
        /// fields so switching PP systems never discards the other system's cache.
        /// </summary>
        public double RelaxVanillaStarRating { get; set; }

        public double RelaxVanillaMaxPerformancePoints { get; set; }

        public bool PerformancePointsCalculated { get; set; }

        public bool RelaxPerformancePointsCalculated { get; set; }

        public bool RelaxVanillaPerformancePointsCalculated { get; set; }

        public int PerformancePointsVersion { get; set; }

        public int RelaxPerformancePointsVersion { get; set; }

        public int RelaxVanillaPerformancePointsVersion { get; set; }

        /// <summary>
        /// Last successfully calculated Dodge star rating. Dodge difficulty is
        /// fork-owned and must not use the upstream BeatmapInfo persistence.
        /// </summary>
        public double DodgeStarRating { get; set; } = -1;

        public int DodgeDifficultyVersion { get; set; }

        /// <summary>
        /// Checksum of the beatmap revision used for the cached Dodge analysis.
        /// </summary>
        public string DodgeBeatmapChecksum { get; set; } = string.Empty;

        /// <summary>
        /// Complete ruleset-specific difficulty attributes required by live PP.
        /// Kept as JSON so fork.realm does not need a compile-time dependency on
        /// the separately built Dodge ruleset assembly.
        /// </summary>
        public string DodgeDifficultyAttributesJson { get; set; } = string.Empty;

        /// <summary>
        /// Perfect-score additional info keyed by ruleset and canonical mod settings.
        /// A separate version/checksum key prevents showing data from an older beatmap or calculator.
        /// </summary>
        public string AdditionalInfoJson { get; set; } = string.Empty;
    }
}
