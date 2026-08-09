// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Scoring;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModHalfTime : ModHalfTime, IApplicableToHitObject
    {
        protected override void AdjustLockedDifficulty(BeatmapDifficulty difficulty, float originalAR, float originalOD, double speedFactor)
        {
            difficulty.ApproachRate = originalAR;
            difficulty.OverallDifficulty = originalOD;
        }

        public void ApplyToHitObject(osu.Game.Rulesets.Objects.HitObject hitObject)
        {
            if (!LockDifficultyAdjust.Value)
                return;

            applyLockDifficultyToHitObject(hitObject, SpeedChange.Value);
        }

        private void applyLockDifficultyToHitObject(osu.Game.Rulesets.Objects.HitObject hitObject, double speedChange)
        {
            if (hitObject is OsuHitObject osuHitObject)
            {
                osuHitObject.TimePreempt *= speedChange;
                osuHitObject.TimeFadeIn *= speedChange;
            }

            if (hitObject.HitWindows != null)
            {
                hitObject.HitWindows.CustomSpeedMultiplier = speedChange;
            }

            foreach (var nested in hitObject.NestedHitObjects)
                applyLockDifficultyToHitObject(nested, speedChange);
        }
    }
}
