// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    /// <summary>
    /// Invisible nested drawable whose result is controlled by its parent emitter.
    /// </summary>
    public partial class DrawableDodgeEmitterBurst : DrawableHitObject<DodgeEmitterBurst>
    {
        public DrawableDodgeEmitterBurst(DodgeEmitterBurst hitObject)
            : base(hitObject)
        {
            AlwaysPresent = true;
        }

        public void Judge(bool successful)
        {
            if (Result.HasResult)
                return;

            if (successful)
                ApplyMaxResult();
            else
                ApplyMinResult();
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            // The parent emitter judges this after every projectile in the burst
            // has either collided with the player or left the playfield.
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            Expire();
        }
    }
}
