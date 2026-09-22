// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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

        /// <summary>
        /// Allows a final object ending exactly at the audio boundary to be
        /// judged before track clocks stop a fraction below their nominal end.
        /// </summary>
        public const double FINAL_JUDGEMENT_LENIENCE = 50;

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
        /// <remarks>
        /// Kept allocation-free: this runs whenever the hit object set changes,
        /// including the initial load burst of very dense maps.
        /// </remarks>
        public static double GetGameplayEndTime(IEnumerable<DodgeHitObject> hitObjects)
        {
            double endTime = 0;

            foreach (DodgeHitObject hitObject in hitObjects)
            {
                if (hitObject is DodgeBullet or DodgeEmitter or DodgeBeam)
                    endTime = Math.Max(endTime, hitObject.GetEndTime());
            }

            return endTime;
        }

        /// <summary>
        /// Returns the latest time at which a projectile can still be active.
        /// The grace period remains a lower bound, while authored projectiles
        /// which take longer to leave the playfield are allowed to finish.
        /// </summary>
        /// <remarks>
        /// Kept allocation-free (single pass, no LINQ/ToArray): this runs whenever
        /// the hit object set changes, and copying dense maps per event allocates
        /// hundreds of MiB during the load burst.
        /// </remarks>
        public static double GetContinuedProjectileEndTime(IEnumerable<DodgeHitObject> hitObjects, double minimumGracePeriod)
        {
            double threatEndTime = 0;
            double continuedEndTime = 0;

            foreach (DodgeHitObject hitObject in hitObjects)
            {
                switch (hitObject)
                {
                    case DodgeBullet or DodgeEmitter or DodgeBeam:
                        threatEndTime = Math.Max(threatEndTime, hitObject.GetEndTime());
                        break;
                }

                switch (hitObject)
                {
                    case DodgeBullet { ContinueUntilExit: true } bullet:
                        continuedEndTime = Math.Max(continuedEndTime, bullet.MovementEndTime);
                        break;

                    case DodgeEmitter { ContinueUntilExit: true } emitter:
                        continuedEndTime = Math.Max(continuedEndTime, emitter.MovementEndTime);
                        break;
                }
            }

            return Math.Max(threatEndTime + Math.Max(0, minimumGracePeriod), continuedEndTime);
        }

        /// <summary>
        /// Track clocks can stop one rendered frame short of an object ending
        /// exactly at the audio boundary. Only the final threat receives this
        /// lenience; intermediate threats remain active for their full duration.
        /// </summary>
        public static bool HasReachedJudgementTime(double currentTime, double objectEndTime, double gameplayEndTime)
        {
            if (currentTime >= objectEndTime)
                return true;

            return objectEndTime == gameplayEndTime
                   && currentTime >= objectEndTime - FINAL_JUDGEMENT_LENIENCE;
        }
    }
}
