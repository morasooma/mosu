// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Catch.Objects;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Catch.Mods
{
    public class CatchModMosuStaticBpm : ModMosuStaticBpm
    {
        protected override void ApplyLockDifficultyToHitObjectSelf(HitObject hitObject, double speedChange)
        {
            base.ApplyLockDifficultyToHitObjectSelf(hitObject, speedChange);

            if (hitObject is CatchHitObject catchHitObject)
            {
                catchHitObject.TimePreempt *= speedChange;
            }
        }
    }
}
