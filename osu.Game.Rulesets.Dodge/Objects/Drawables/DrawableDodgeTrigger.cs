// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Audio;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    /// <summary>
    /// Invisible runtime representation of a <see cref="DodgeTrigger"/>.
    /// The trigger is applied by the playfield's trigger evaluator; this drawable
    /// only exists so the standard hit object lifetime machinery keeps it alive
    /// and judges it at its time.
    /// </summary>
    public partial class DrawableDodgeTrigger : DrawableHitObject<DodgeHitObject>
    {
        public DrawableDodgeTrigger(DodgeTrigger hitObject)
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
            // Triggers are non-threatening state transitions: judge them at StartTime
            // so gameplay does not stay alive until a timed effect finishes.
            if (timeOffset >= 0)
                ApplyMaxResult();
        }
    }
}
