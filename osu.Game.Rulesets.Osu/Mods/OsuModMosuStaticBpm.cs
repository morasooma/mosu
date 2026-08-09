// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModMosuStaticBpm : ModMosuStaticBpm
    {
        protected override void ApplyLockDifficultyToHitObjectSelf(HitObject hitObject, double speedChange)
        {
            base.ApplyLockDifficultyToHitObjectSelf(hitObject, speedChange);

            if (hitObject is OsuHitObject osuHitObject)
            {
                osuHitObject.TimePreempt *= speedChange;
                osuHitObject.TimeFadeIn *= speedChange;
            }
        }
    }
}
