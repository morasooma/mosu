// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects.Types;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public static class DodgeEditorTrackBounds
    {
        /// <summary>
        /// Ensures that a newly-created object does not start or end after the audio track.
        /// </summary>
        /// <param name="hitObject">The object to constrain.</param>
        /// <param name="trackLength">The audio track length.</param>
        /// <param name="clampStartTime">
        /// Whether a start time after the track should be clamped to its end.
        /// Set this to false for generated patterns, where out-of-range objects should be discarded instead.
        /// </param>
        /// <returns>Whether the object is within the track and can be added.</returns>
        public static bool Constrain(DodgeHitObject hitObject, double trackLength, bool clampStartTime)
        {
            trackLength = Math.Max(0, trackLength);

            if (hitObject.StartTime >= trackLength)
            {
                if (!clampStartTime)
                    return false;

                if (hitObject.StartTime > trackLength)
                    hitObject.StartTime = trackLength;
            }

            if (hitObject is DodgeEmitter emitter)
            {
                double availableDuration = Math.Max(0, trackLength - emitter.StartTime);
                int maximumBursts = Math.Max(
                    DodgeEmitter.MIN_BURST_COUNT,
                    (int)Math.Floor(availableDuration / emitter.EffectiveBurstInterval) + 1);
                emitter.BurstCount = Math.Min(emitter.EffectiveBurstCount, maximumBursts);
                emitter.Duration = Math.Clamp(emitter.Duration, 0, Math.Max(0, availableDuration - emitter.EmissionDuration));
            }
            else if (hitObject is IHasDuration hasDuration)
                hasDuration.Duration = Math.Clamp(hasDuration.Duration, 0, Math.Max(0, trackLength - hitObject.StartTime));

            return true;
        }
    }
}
