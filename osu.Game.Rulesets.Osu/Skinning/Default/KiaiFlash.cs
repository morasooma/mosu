// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Containers;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    public partial class KiaiFlash : BeatSyncedContainer
    {
        private const double fade_length = 80;

        private const float flash_opacity = 0.25f;

        public KiaiFlash()
            : this(false)
        {
        }

        /// <param name="circularFill">
        /// When <c>true</c>, the flash is filled with a <see cref="FastCircle"/> rather than a <see cref="Box"/>.
        /// This lets callers that only wanted a round flash drop their masking <c>CircularContainer</c> wrapper,
        /// removing a masking region (and its <c>SetMasking</c> batch flush) per instance.
        /// </param>
        public KiaiFlash(bool circularFill)
        {
            EarlyActivationMilliseconds = 80;
            Blending = BlendingParameters.Additive;

            Child = circularFill
                ? (Drawable)new FastCircle
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Alpha = 0f,
                }
                : new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = Color4.White,
                    Alpha = 0f,
                };
        }

        protected override void OnNewBeat(int beatIndex, TimingControlPoint timingPoint, EffectControlPoint effectPoint, ChannelAmplitudes amplitudes)
        {
            if (!effectPoint.KiaiMode)
                return;

            Child
                .FadeTo(flash_opacity, EarlyActivationMilliseconds, Easing.OutQuint)
                .Then()
                .FadeOut(Math.Max(fade_length, timingPoint.BeatLength - fade_length), Easing.OutSine);
        }
    }
}
