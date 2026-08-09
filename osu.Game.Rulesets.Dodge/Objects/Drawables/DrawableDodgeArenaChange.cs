// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Audio;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    public partial class DrawableDodgeArenaChange : DrawableHitObject<DodgeHitObject>
    {
        public DrawableDodgeArenaChange(DodgeArenaChange hitObject)
            : base(hitObject)
        {
            Alpha = 0;
        }

        public override IEnumerable<HitSampleInfo> GetSamples() => Array.Empty<HitSampleInfo>();

        public override void PlaySamples()
        {
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            // DrawableHitObject supplies an offset from EndTime. Arena changes are
            // non-threatening state transitions, so judge them at StartTime rather
            // than keeping gameplay alive until the visual interpolation finishes.
            if (timeOffset >= -((DodgeArenaChange)HitObject).Duration)
                ApplyMaxResult();
        }
    }
}
