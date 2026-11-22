using System;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class AimAssistController
    {
        private MosuAimAssistAlgorithm activeAlgorithm;
        private DrawableOsuHitObject? mapleTarget;
        private bool mapleHasPreviousTarget;

        private void resetMapleState()
        {
            mapleTarget = null;
            mapleHasPreviousTarget = false;
        }

        private static double getMapleHitWindow(DrawableOsuHitObject target)
            => (target is DrawableSlider slider ? slider.HeadCircle.HitObject : target.HitObject).HitWindows?.WindowFor(HitResult.Meh) ?? 0;

        private void updateMaple(Vector2 raw, Vector2 movement)
        {
            var mod = mosuAimAssistMod!;
            bool v3 = mod.Algorithm.Value == MosuAimAssistAlgorithm.MapleV3;
            bool followSliders = v3 || mod.MapleV2AssistOnSliders.Value;
            DrawableOsuHitObject? target = null;
            bool selectedActiveSlider = false;
            double time = Time.Current;
            bool spinnerActive = false;

            // Keep Maple's chronological selection separate from Mosu's flow/intent planner.
            foreach (Drawable drawable in playfield!.HitObjectContainer.Objects)
            {
                if (drawable is DrawableSpinner spinner)
                {
                    spinnerActive |= time >= spinner.HitObject.StartTime && time <= spinner.HitObject.EndTime;
                    continue;
                }

                if (drawable is not DrawableHitCircle && drawable is not DrawableSlider)
                    continue;

                var candidate = (DrawableOsuHitObject)drawable;
                if (candidate is DrawableSliderHead || candidate is DrawableSliderTail)
                    continue;

                OsuHitObject hitObject = candidate.HitObject;
                bool slider = candidate is DrawableSlider;
                double end = slider ? ((Slider)hitObject).EndTime : hitObject.StartTime;
                double window = getMapleHitWindow(candidate);

                if (time < hitObject.StartTime - hitObject.TimePreempt || (v3 ? time > end : time > end + window))
                    continue;
                if (slider && followSliders && time > end)
                    continue;
                if (!v3 && !slider && candidate.IsHit)
                    continue;
                if (candidate is DrawableSlider drawableSlider && !followSliders
                    && (drawableSlider.HeadCircle.IsHit || time > hitObject.StartTime + window))
                    continue;

                bool activeSlider = slider && followSliders && time >= hitObject.StartTime && time <= end;
                if (target == null || (activeSlider && !selectedActiveSlider)
                                   || (activeSlider == selectedActiveSlider && hitObject.StartTime < target.HitObject.StartTime))
                {
                    target = candidate;
                    selectedActiveSlider = activeSlider;
                }
            }

            if (target != mapleTarget)
            {
                // The original needs a previous target for V2, including when a target disappears.
                mapleHasPreviousTarget = mapleTarget != null;
                mapleTarget = target;
            }

            clearDisplayState();
            antiJitterRemaining = 0;
            PassedActivationFilters = false;
            CurrentAssistAuthority = 0;
            CurrentIntentScore = 0;
            CurrentModeName = v3 ? @"maple-v3" : @"maple-v2";
            CurrentFlowDebugModeName = @"idle";

            if (spinnerActive)
            {
                resetMapleState();
                assistOffset = Vector2.Zero;
                CurrentAssistStateName = @"spinner-release";
                applyOutput(raw);
                return;
            }

            if (target == null)
            {
                assistOffset = MapleAimAssistMath.Resync(assistOffset, movement, 0.3f);
                CurrentAssistStateName = assistOffset.LengthSquared > 0.01f ? @"release" : @"idle";
                applyOutput(raw);
                return;
            }

            OsuHitObject selected = target.HitObject;
            Vector2 position = getDisplayCentre(target);
            if (target is DrawableSlider sliderTarget && followSliders)
            {
                double progress = Math.Clamp((time - selected.StartTime) / Math.Max(1, sliderTarget.HitObject.Duration), 0, 1);
                position = toScreenSpace(sliderTarget.HitObject.StackedPositionAt(progress));
            }

            float radius = getDrawableTargetRadius(target, selected);
            float scale = getScreenSpaceRadius(selected.StackedPosition, 1);
            float height = Vector2.Distance(toScreenSpace(Vector2.Zero), toScreenSpace(new Vector2(0, 384)));
            float power = v3 ? mod.MapleV3Power.Value : mod.MapleV2Power.Value;
            float fov = v3
                ? MapleAimAssistMath.V3Radius(power, radius)
                : MapleAimAssistMath.V2Radius(time, selected.StartTime, selected.TimePreempt,
                    getMapleHitWindow(target), power, scale);

            assistOffset = v3
                ? MapleAimAssistMath.UpdateV3(assistOffset, raw, movement, position, power, mod.MapleV3SliderPower.Value,
                    scale, radius, target is DrawableSlider, time > selected.StartTime, time > selected.StartTime - selected.TimePreempt)
                : MapleAimAssistMath.UpdateV2(assistOffset, raw, movement, position, power, scale, height, fov, mapleHasPreviousTarget);

            if (!float.IsFinite(assistOffset.X) || !float.IsFinite(assistOffset.Y))
                assistOffset = Vector2.Zero;

            CurrentTargetPosition = position;
            CurrentAssistPointPosition = position;
            CurrentBaseTargetRadius = CurrentTargetRadius = radius;
            CurrentAssistRadius = fov;
            CurrentAdaptiveRadiusScale = 1;
            PassedActivationFilters = power > 0 && Vector2.Distance(raw, position) <= fov;
            CurrentAssistAuthority = PassedActivationFilters ? power : 0;
            CurrentAssistStateName = PassedActivationFilters ? @"tracking" : @"release";
            applyOutput(raw);
        }
    }
}
