// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Threading;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Dodge.Beatmaps
{
    public class DodgeBeatmapConverter : BeatmapConverter<DodgeHitObject>
    {
        public DodgeBeatmapConverter(IBeatmap beatmap, Ruleset ruleset)
            : base(beatmap, ruleset)
        {
        }

        public override bool CanConvert() => Beatmap.HitObjects.All(hitObject => hitObject is DodgeHitObject);

        protected override Beatmap<DodgeHitObject> ConvertBeatmap(IBeatmap original, CancellationToken cancellationToken)
        {
            Beatmap<DodgeHitObject> converted = base.ConvertBeatmap(original, cancellationToken);

            // BeatmapConverter performs a shallow beatmap clone and passes through objects which
            // already match the target type. A fresh Dodge object is required for every gameplay
            // load, otherwise Retry applies defaults to an object still observed by the previous
            // player's drawable from a background loading thread.
            converted.HitObjects = converted.HitObjects.Select(CloneHitObject).ToList();
            DodgeBeatmapSettings.SetPlayerSize(converted.Difficulty, DodgeBeatmapSettings.GetPlayerSize(Beatmap.Difficulty));
            DodgeBeatmapSettings.SetForceStoryboard(converted.Difficulty, DodgeBeatmapSettings.GetForceStoryboard(Beatmap.Difficulty));
            DodgeBeatmapSettings.SetForceBeatmapSkin(converted.Difficulty, DodgeBeatmapSettings.GetForceBeatmapSkin(Beatmap.Difficulty));

            var playableBreaks = DodgeBreakPeriodCalculator.ExcludeMovingProjectiles(
                converted.Breaks,
                converted.HitObjects,
                DodgeBeatmapSettings.GetBulletSize(converted.Difficulty));

            converted.Breaks.Clear();
            converted.Breaks.AddRange(playableBreaks);
            return converted;
        }

        internal static DodgeHitObject CloneHitObject(DodgeHitObject hitObject)
            => DodgeHitObjectCloner.Clone(hitObject);
    }
}
