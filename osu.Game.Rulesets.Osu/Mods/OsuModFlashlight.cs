// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    public partial class OsuModFlashlight : ModFlashlight<OsuHitObject>, IApplicableToDrawableHitObject
    {
        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[] { typeof(OsuModBloom), typeof(OsuModBlinds) }).ToArray();

        private const double default_follow_delay = 120;

        [SettingSource("Follow delay", "Milliseconds until the flashlight reaches the cursor")]
        public BindableNumber<double> FollowDelay { get; } = new BindableDouble(default_follow_delay)
        {
            MinValue = default_follow_delay,
            MaxValue = default_follow_delay * 10,
            Precision = default_follow_delay,
        };

        public override BindableFloat SizeMultiplier { get; } = new BindableFloat(1)
        {
            MinValue = 0.5f,
            MaxValue = 2f,
            Precision = 0.1f
        };

        public override BindableBool ComboBasedSize { get; } = new BindableBool(true);

        public override float DefaultFlashlightSize => 125;

        private OsuFlashlight flashlight = null!;

        protected override Flashlight CreateFlashlight() => flashlight = new OsuFlashlight(this);

        public void ApplyToDrawableHitObject(DrawableHitObject drawable)
        {
            if (drawable is DrawableSlider s)
                flashlight.RegisterSlider(s);
        }

        private partial class OsuFlashlight : Flashlight, IRequireHighFrequencyMousePosition
        {
            private readonly double followDelay;
            private readonly List<DrawableSlider> sliderPool = new List<DrawableSlider>();

            public OsuFlashlight(OsuModFlashlight modFlashlight)
                : base(modFlashlight)
            {
                followDelay = modFlashlight.FollowDelay.Value;

                FlashlightSize = new Vector2(0, GetSize());
                FlashlightSmoothness = 1.4f;
            }

            public void RegisterSlider(DrawableSlider slider)
            {
                if (!sliderPool.Contains(slider))
                    sliderPool.Add(slider);
            }

            protected override void Update()
            {
                base.Update();

                double currentTime = Time.Current;

                // Slider drawables are pooled and may disappear while still being the last writer of FlashlightDim.
                // Recompute from the live slider pool every frame so the extra dim only exists during active slider tracking.
                FlashlightDim = sliderPool.Any(s => isActivelyTrackingSlider(s, currentTime)) ? 0.8f : 0.0f;
            }

            private static bool isActivelyTrackingSlider(DrawableSlider slider, double currentTime)
            {
                return slider.IsAlive
                       && currentTime >= slider.HitObject.StartTime
                       && currentTime <= slider.HitObject.EndTime
                       && slider.Tracking.Value;
            }

            protected override bool OnMouseMove(MouseMoveEvent e)
            {
                var position = FlashlightPosition;
                var destination = e.MousePosition;

                FlashlightPosition = Interpolation.ValueAt(
                    Math.Min(Math.Abs(Clock.ElapsedFrameTime), followDelay), position, destination, 0, followDelay, Easing.Out);

                return base.OnMouseMove(e);
            }

            // as per https://github.com/peppy/osu-stable-reference/blob/baa8705f782c0de2b10a7387d78014c61c8b17fb/osu!/GameModes/Play/Rulesets/Ruleset.cs#L532-L535,
            // stable's animation speed is 0.1 "units" per 1 frame at 60 fps
            // converting to local units here, this is:
            // (0.1 / 3.2) * (1 / 60 [s]) = 1.875 [1 / s] = (1.875 / 1000) [1 / ms]
            private const double scale_animation_speed = 1.875 / 1000;

            protected override void UpdateFlashlightSize(float size)
            {
                double relativeDelta = Math.Abs(FlashlightSize.Y - size) / DefaultFlashlightSize;
                double duration = relativeDelta / scale_animation_speed;
                this.TransformTo(nameof(FlashlightSize), new Vector2(0, size), duration);
            }

            protected override string FragmentShader => "CircularFlashlight";
        }
    }
}
