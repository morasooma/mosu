// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Utils;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class AimAssistController
    {
        private readonly HashSet<OsuHitObject> exitedMotionTargets = new HashSet<OsuHitObject>();
        private OsuHitObject? motionTarget;
        private bool motionTargetCaptured;
        private bool motionTargetExited;
        private float closestMotionDistance = float.PositiveInfinity;
        private Vector2 fastRawVelocity;
        private Vector2 slowRawVelocity;
        private Vector2 previousPointAssistStep;
        private Vector2 previousSliderAssistStep;

        private void clearPredictiveMotion()
        {
            motionTarget = null;
            motionTargetCaptured = false;
            motionTargetExited = false;
            closestMotionDistance = float.PositiveInfinity;
            exitedMotionTargets.Clear();
            previousPointAssistStep = Vector2.Zero;
            previousSliderAssistStep = Vector2.Zero;
        }

        private void updateTargetMotion(AimAssistContext context, Vector2 rawPosition, Vector2 rawDelta)
        {
            if (!ReferenceEquals(motionTarget, context.Drawable.HitObject))
            {
                motionTarget = context.Drawable.HitObject;
                motionTargetCaptured = false;
                motionTargetExited = false;
                closestMotionDistance = float.PositiveInfinity;
            }

            if (context.ModeName is not (@"point" or @"stream"))
                return;

            if (toRealTimeWindow(context.FocusTime - Time.Current) > 180)
                return;

            Vector2 centre = getDisplayCentre(context.Drawable);
            float radius = getDrawableTargetRadius(context.Drawable, context.Drawable.HitObject);
            Vector2 output = rawPosition + assistOffset;
            Vector2 from = rawPosition - rawDelta;
            float progress = rawDelta.LengthSquared > 0
                ? Math.Clamp(Vector2.Dot(centre - from, rawDelta) / rawDelta.LengthSquared, 0, 1)
                : 0;
            float passDistance = (from + rawDelta * progress - centre).Length;
            float distance = (rawPosition - centre).Length;
            closestMotionDistance = Math.Min(closestMotionDistance, passDistance);
            motionTargetCaptured |= (output - centre).LengthSquared <= radius * radius;

            // Выход определяет исходное движение игрока. Само притяжение не должно
            // удерживать цель активной только потому, что виртуальный курсор ещё внутри.
            bool leaving = Vector2.Dot(rawDelta, rawPosition - centre) > 0
                           && Vector2.Dot(fastRawVelocity, rawPosition - centre) > radius * stationary_hold_speed_threshold;
            if (leaving && distance > closestMotionDistance + radius * 0.18f
                        && (motionTargetCaptured || closestMotionDistance < getTargetGravityRadius(context)))
            {
                motionTargetExited = true;
                if (context.Drawable is DrawableHitCircle)
                    exitedMotionTargets.Add(context.Drawable.HitObject);
            }
        }

        private Vector2 computeEngagedOffset(AimAssistContext context, ActivationState activation, Vector2 rawPosition, float strengthFactor)
        {
            if (context.ModeName == @"point")
                return getTargetGravityOffset(context, rawPosition, activation.EngageAmount);

            if (context.ModeName == @"stream")
            {
                ProjectionResult projection = getStreamCorridorProjection(rawPosition, context);
                Vector2 correction = getTangentLineCorrectionVector(rawPosition, projection.Point, normaliseOrZero(projection.Tangent));
                float radius = getDrawableTargetRadius(context.Drawable, context.Drawable.HitObject);
                Vector2 pathOffset = outsideDeadzone(correction, radius * 0.3f) * activation.EngageAmount;

                Vector2 toTarget = getDisplayCentre(context.Drawable) - rawPosition;
                bool approachingTarget = fastRawVelocity.Length <= stationary_hold_speed_threshold
                                         || Vector2.Dot(fastRawVelocity, toTarget) >= 0;
                float acquisition = approachingTarget ? getGravityFieldFalloff(toTarget.Length, getTargetGravityRadius(context)) : 0;

                // В поле текущей ноты работает полноценное доведение к кругу.
                // Между нотами путь исправляет только поперечное отклонение.
                return interpolate(pathOffset, getTargetGravityOffset(context, rawPosition, activation.EngageAmount), acquisition);
            }

            Vector2 output = rawPosition + assistOffset;
            float trackingRadius = getDrawableTargetRadius(context.Drawable, context.Drawable.HitObject)
                                   * (context.ModeName == @"slider-repeat" ? 0.18f : 0.24f);
            return assistOffset + outsideDeadzone(context.DesiredPoint - output, trackingRadius);
        }

        private Vector2 getTargetGravityOffset(AimAssistContext context, Vector2 rawPosition, float authority)
        {
            Vector2 toTarget = getDisplayCentre(context.Drawable) - rawPosition;
            float radius = getDrawableTargetRadius(context.Drawable, context.Drawable.HitObject);
            float predictionHorizon = getPointAcquisitionPredictionHorizon(context);
            float predictedMissDistance = GetPredictedPointMissDistance(rawPosition, getDisplayCentre(context.Drawable), averagedVelocity, predictionHorizon);
            bool movingTowardTarget = fastRawVelocity.Length <= stationary_hold_speed_threshold || Vector2.Dot(fastRawVelocity, toTarget) >= 0;
            if (!ShouldAttemptPointCorrection(toTarget.Length, predictedMissDistance, radius, getTargetGravityRadius(context), movingTowardTarget))
                return assistOffset;

            if (motionTargetCaptured && fastRawVelocity.Length <= stationary_hold_speed_threshold
                                     && (toTarget - assistOffset).LengthSquared <= radius * radius)
                return assistOffset;

            float distance = toTarget.Length;
            if (distance <= radius)
                return assistOffset;

            Vector2 missCorrection = GetTrajectoryMissCorrection(rawPosition, getDisplayCentre(context.Drawable), radius, averagedVelocity, getPointAcquisitionPredictionHorizon(context));

            // Do not correct a trajectory which already intersects the real hitbox.
            if (missCorrection.LengthSquared <= 0.0001f)
                return assistOffset;

            // Correct perpendicular to the player's projected path. Pulling radially from the
            // current position adds an artificial forward component and fights their timing.
            // Activation authority already contains field falloff, timing, intent and strength.
            // Applying field falloff again here made the useful outer half of the field effectively dead.
            return missCorrection * Math.Clamp(authority, 0, 1);
        }

        internal static bool ShouldAttemptPointCorrection(float rawDistance, float predictedMissDistance, float hitboxRadius, float assistanceRadius, bool movingTowardTarget)
        {
            if (!movingTowardTarget || predictedMissDistance <= hitboxRadius)
                return false;

            // Raw centre distance is deliberately not a rejection criterion. On a long jump it can be
            // hundreds of pixels while the player's projected trajectory misses the hitbox by very little.
            return predictedMissDistance <= Math.Max(hitboxRadius, assistanceRadius);
        }

        internal static Vector2 GetTrajectoryMissCorrection(Vector2 position, Vector2 target, float radius, Vector2 velocity, float predictionHorizon)
        {
            Vector2 toTarget = target - position;
            float velocitySquared = velocity.LengthSquared;
            Vector2 closestPosition = position;

            if (velocitySquared > 1)
            {
                float secondsToClosest = Math.Clamp(Vector2.Dot(toTarget, velocity) / velocitySquared, 0, Math.Max(0, predictionHorizon));
                closestPosition += velocity * secondsToClosest;
            }

            Vector2 miss = target - closestPosition;
            float missDistance = miss.Length;
            return missDistance > radius && missDistance > 0
                ? miss * ((missDistance - Math.Max(0, radius)) / missDistance)
                : Vector2.Zero;
        }

        private static Vector2 outsideDeadzone(Vector2 error, float radius)
        {
            float distance = error.Length;
            return distance > Math.Max(0, radius) && distance > 0
                ? error * ((distance - Math.Max(0, radius)) / distance)
                : Vector2.Zero;
        }

        private double getSliderHeadHandoffWindow(DrawableSlider slider)
        {
            double baseWindow = scaleRealTimeWindow(Math.Clamp(getGreatWindow(slider) * 1.1, 36, 95));
            return Math.Min(baseWindow, Math.Max(scaleRealTimeWindow(10), slider.HitObject.SpanDuration * 0.22));
        }

        private static Vector2 smoothDamp(Vector2 current, Vector2 target, ref Vector2 currentVelocity, float smoothTime, float maxSpeed, float deltaTime)
        {
            smoothTime = Math.Max(0.0001f, smoothTime);
            float omega = 2f / smoothTime;
            float x = omega * deltaTime;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

            Vector2 change = current - target;
            float maxChange = maxSpeed * smoothTime;
            if (change.LengthSquared > maxChange * maxChange)
            {
                change = normaliseOrZero(change) * maxChange;
            }

            target = current - change;

            Vector2 temp = (currentVelocity + change * omega) * deltaTime;
            currentVelocity = (currentVelocity - omega * temp) * exp;
            Vector2 output = target + (change + temp) * exp;

            if (Vector2.Dot(target - current, output - target) > 0)
            {
                output = target;
                currentVelocity = (output - target) / deltaTime;
            }

            return output;
        }

        private Vector2 updateAssistOffset(Vector2 targetOffset, bool engaged, Vector2 rawDelta, double elapsed, float engageAmount,
                                           bool pointMode, bool shortRepeatSliderMode, bool streamMode, float strengthFactor)
        {
            float dt = (float)(toRealTimeWindow(elapsed) / 1000);
            if (dt <= 0)
                return assistOffset;

            float speed = rawDelta.Length / dt;

            if (!engaged)
            {
                previousPointAssistStep = Vector2.Zero;
                previousSliderAssistStep = Vector2.Zero;
                return GetPlayerLedReleaseOffset(assistOffset, rawDelta);
            }

            float responseTime = (float)Interpolation.Lerp(0.022f, 0.007f, strengthFactor);
            if (shortRepeatSliderMode || streamMode)
                responseTime *= 0.85f;
            if (IsAntiJitterActive)
                responseTime *= 1 + (1 - Math.Clamp(speed / 500f, 0, 1)) * 0.4f;

            // Первый порядок: корректор не хранит собственную скорость и не продолжает
            // разгонять курсор после торможения игрока или смены ноты.
            // В режиме притяжения степень помощи уже задаёт величину целевого смещения.
            float responseAuthority = pointMode || streamMode ? 1 : engageAmount;
            Vector2 correction = (targetOffset - assistOffset) * (1 - MathF.Exp(-dt * responseAuthority / responseTime));
            float maxSpeed = (float)Interpolation.Lerp(450f, 1500f, strengthFactor) + speed * 0.8f;
            correction = clampLength(correction, maxSpeed * dt);

            if (pointMode)
            {
                // targetOffset already contains activation authority. Applying it again here made
                // moderate strengths effectively quadratic and therefore almost invisible.
                float timeUntilHit = CurrentFocusTime.HasValue
                    ? Math.Max(0, (float)toRealTimeWindow(CurrentFocusTime.Value - Time.Current))
                    : float.PositiveInfinity;
                bool homingPhase = ShouldAllowForwardCorrectionForMotion(motionTargetCaptured, fastRawVelocity.Length, slowRawVelocity.Length, timeUntilHit);
                bool allowForwardCorrection = homingPhase
                                              && ShouldAllowForwardCorrection(targetOffset - assistOffset, rawDelta, motionTargetCaptured, motionTargetExited);
                correction = GetPlayerLedAssistStep(correction, rawDelta, strengthFactor, previousPointAssistStep, allowForwardCorrection);
                previousPointAssistStep = correction;
                previousSliderAssistStep = Vector2.Zero;
            }
            else if (streamMode && rawDelta.LengthSquared <= 0.0001f)
            {
                correction = Vector2.Zero;
                previousPointAssistStep = Vector2.Zero;
                previousSliderAssistStep = Vector2.Zero;
            }
            else if (shortRepeatSliderMode || (!pointMode && !streamMode))
            {
                correction = GetPlayerLedTrackingStep(correction, rawDelta, strengthFactor, previousSliderAssistStep);
                previousPointAssistStep = Vector2.Zero;
                previousSliderAssistStep = correction;
            }
            else
            {
                previousPointAssistStep = Vector2.Zero;
                previousSliderAssistStep = Vector2.Zero;
            }

            float maxOffset = Math.Max(24, getConfiguredFovRadius() * (float)Interpolation.Lerp(0.55f, 1.1f, strengthFactor));
            return clampLength(assistOffset + correction, maxOffset);
        }

        internal static Vector2 GetPlayerLedAssistStep(Vector2 requestedCorrection, Vector2 rawDelta, float authority, Vector2 previousStep = default, bool allowForwardCorrection = false)
        {
            float movement = rawDelta.Length;
            if (movement <= 0.0001f || authority <= 0)
                return Vector2.Zero;

            Vector2 direction = rawDelta / movement;
            float parallelCorrection = Vector2.Dot(requestedCorrection, direction);
            Vector2 eligibleCorrection = requestedCorrection - direction * parallelCorrection;

            if (allowForwardCorrection && parallelCorrection > 0)
                eligibleCorrection += direction * parallelCorrection;

            float strength = Math.Clamp(authority, 0, 1);
            Vector2 desiredStep = clampLength(eligibleCorrection, movement * (0.18f + 0.42f * strength));

            if (allowForwardCorrection)
            {
                float forwardStep = Math.Clamp(Vector2.Dot(desiredStep, direction), 0, movement * 0.36f);
                Vector2 lateralStep = desiredStep - direction * Vector2.Dot(desiredStep, direction);
                desiredStep = lateralStep + direction * forwardStep;
            }

            float maximumStepChange = movement * (0.08f + 0.16f * strength);
            Vector2 nextStep = previousStep + clampLength(desiredStep - previousStep, maximumStepChange);

            // A curvature reversal must pass through zero rather than crossing it within one frame.
            if (previousStep.LengthSquared > 0.0001f && Vector2.Dot(previousStep, nextStep) < 0)
                return Vector2.Zero;

            return nextStep;
        }

        internal static bool ShouldAllowForwardCorrection(Vector2 requestedCorrection, Vector2 rawDelta, bool targetCaptured, bool targetExited)
        {
            if (targetCaptured || targetExited || rawDelta.LengthSquared <= 0.0001f)
                return false;

            return Vector2.Dot(requestedCorrection, rawDelta) > 0;
        }

        internal static bool ShouldAllowForwardCorrectionForMotion(bool targetCaptured, float fastSpeed, float slowSpeed, float timeUntilHit)
        {
            if (targetCaptured || timeUntilHit > 95)
                return false;

            // Forward correction belongs to the feedback-driven homing phase only. During the initial
            // ballistic surge the player owns longitudinal speed completely.
            return fastSpeed <= slowSpeed * 0.82f;
        }

        internal static Vector2 GetPlayerLedReleaseOffset(Vector2 currentOffset, Vector2 rawDelta)
        {
            if (rawDelta.LengthSquared <= 0.0001f || currentOffset.LengthSquared <= 0.01f)
                return currentOffset;

            Vector2 direction = normaliseOrZero(rawDelta);
            float convergence = Math.Clamp(Vector2.Dot(currentOffset, direction), 0, rawDelta.Length);
            Vector2 remaining = currentOffset - direction * convergence;
            return remaining.LengthSquared <= 0.01f ? Vector2.Zero : remaining;
        }

        internal static Vector2 GetPlayerLedTrackingStep(Vector2 requestedCorrection, Vector2 rawDelta, float authority, Vector2 previousStep = default)
        {
            float movement = rawDelta.Length;
            if (movement <= 0.0001f || authority <= 0)
                return Vector2.Zero;

            Vector2 direction = rawDelta / movement;
            Vector2 lateralCorrection = requestedCorrection - direction * Vector2.Dot(requestedCorrection, direction);
            float strength = Math.Clamp(authority, 0, 1);
            Vector2 desiredStep = clampLength(lateralCorrection, movement * (0.1f + 0.22f * strength));
            float maximumStepChange = movement * (0.06f + 0.1f * strength);
            Vector2 nextStep = previousStep + clampLength(desiredStep - previousStep, maximumStepChange);

            if (previousStep.LengthSquared > 0.0001f && Vector2.Dot(previousStep, nextStep) < 0)
                return Vector2.Zero;

            return nextStep;
        }

        private void releaseVirtualCursor(bool restoreOriginalPosition = true)
        {
            if (!drivingCursor)
                return;

            drivingCursor = false;
            inputManager!.ResetVirtualCursor(restoreOriginalPosition);
        }

        private void applyOutput(Vector2 rawPosition)
        {
            bool shouldDrive = assistOffset.LengthSquared > 0.01f || PassedActivationFilters || IsAntiJitterActive;

            if (mosuAntiAimAssistMod == null && IsAimAssistEnabled && assistOffset.LengthSquared > 0.01f)
            {
                AdjustedFrameCount++;
                MaxAdjustmentMagnitude = Math.Max(MaxAdjustmentMagnitude, assistOffset.Length);
            }

            if (shouldDrive)
            {
                Vector2 outputOffset = assistOffset;

                inputManager!.MoveVirtualCursorTo(rawPosition + outputOffset);
                drivingCursor = true;
            }
            else if (drivingCursor)
                releaseVirtualCursor();

            CurrentOutputPosition = inputManager!.CurrentState.Mouse.Position;
        }
    }
}
