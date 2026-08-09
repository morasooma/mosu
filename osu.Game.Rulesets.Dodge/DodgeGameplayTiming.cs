// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Dodge
{
    /// <summary>
    /// Timing helpers which distinguish actual Dodge threats from preparatory
    /// objects such as an initial arena change.
    /// </summary>
    public static class DodgeGameplayTiming
    {
        public const double GAMEPLAY_LEAD_IN = 2000;

        public static double GetGameplayStartTime(IEnumerable<DodgeHitObject> hitObjects)
        {
            double? firstThreatTime = hitObjects
                                      .Where(hitObject => hitObject is DodgeBullet or DodgeEmitter or DodgeBeam)
                                      .Select(hitObject => (double?)hitObject.StartTime)
                                      .Min();

            return firstThreatTime - GAMEPLAY_LEAD_IN ?? 0;
        }

        /// <summary>
        /// Returns the nominal end of the last projectile/beam threat object.
        /// Non-threatening editor objects such as arena changes must not extend
        /// gameplay or the grace period for projectiles which continue until exit.
        /// </summary>
        public static double GetGameplayEndTime(IEnumerable<DodgeHitObject> hitObjects)
            => hitObjects.Where(hitObject => hitObject is DodgeBullet or DodgeEmitter or DodgeBeam)
                         .Select(hitObject => hitObject.GetEndTime())
                         .DefaultIfEmpty(0)
                         .Max();
    }
}
