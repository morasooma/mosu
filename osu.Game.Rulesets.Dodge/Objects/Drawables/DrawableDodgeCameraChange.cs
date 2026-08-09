// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Audio;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    public partial class DrawableDodgeCameraChange : DrawableHitObject<DodgeHitObject>
    {
        public DrawableDodgeCameraChange(DodgeCameraChange hitObject)
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
            if (timeOffset >= -((DodgeCameraChange)HitObject).Duration)
                ApplyMaxResult();
        }
    }
}
