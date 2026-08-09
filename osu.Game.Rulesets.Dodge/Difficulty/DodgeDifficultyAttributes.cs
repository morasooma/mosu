// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using Newtonsoft.Json;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;

namespace osu.Game.Rulesets.Dodge.Difficulty
{
    public class DodgeDifficultyAttributes : DifficultyAttributes
    {
        [JsonProperty("movement_difficulty")]
        public double MovementDifficulty { get; set; }

        [JsonProperty("pressure_difficulty")]
        public double PressureDifficulty { get; set; }

        [JsonProperty("projectile_rate")]
        public double ProjectileRate { get; set; }

        [JsonProperty("peak_active_projectiles")]
        public int PeakActiveProjectiles { get; set; }

        [JsonProperty("autoplay_teleports")]
        public int AutoplayTeleports { get; set; }

        public override IEnumerable<(int attributeId, object value)> ToDatabaseAttributes()
        {
            foreach (var value in base.ToDatabaseAttributes())
                yield return value;

            yield return (ATTRIB_ID_DIFFICULTY, StarRating);
        }

        public override void FromDatabaseAttributes(IReadOnlyDictionary<int, double> values, IBeatmapOnlineInfo onlineInfo)
        {
            base.FromDatabaseAttributes(values, onlineInfo);
            StarRating = values[ATTRIB_ID_DIFFICULTY];
        }
    }
}
