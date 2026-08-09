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

        public bool PerformancePointsCalculated { get; set; }

        public bool RelaxPerformancePointsCalculated { get; set; }
    }
}
