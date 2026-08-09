// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Beatmaps;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Taiko.Mods
{
    public class TaikoModHalfTime : ModHalfTime, IApplicableToHitObject
    {
        protected override void AdjustLockedDifficulty(BeatmapDifficulty difficulty, float originalAR, float originalOD, double speedFactor)
        {
            difficulty.OverallDifficulty = originalOD;
        }

        public void ApplyToHitObject(HitObject hitObject)
        {
            if (!LockDifficultyAdjust.Value)
                return;

            applyLockDifficultyToHitObject(hitObject, SpeedChange.Value);
        }

        private void applyLockDifficultyToHitObject(HitObject hitObject, double speedChange)
        {
            if (hitObject.HitWindows != null)
                hitObject.HitWindows.CustomSpeedMultiplier = speedChange;

            foreach (var nested in hitObject.NestedHitObjects)
                applyLockDifficultyToHitObject(nested, speedChange);
        }
    }
}
