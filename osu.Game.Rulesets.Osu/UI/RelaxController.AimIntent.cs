// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class RelaxController
    {
        private bool usesAimIntent => mosuRelaxMod?.AimIntentEnabled.Value == true;

        private void updateAimIntent(HitPlan plan, Vector2 rawPosition, Vector2 targetPosition, float radius)
        {
            if (!usesAimIntent || radius <= 0)
            {
                plan.AimIntent = default;
                return;
            }

            // Только движение игрока, в единицах радиуса ноты: размер окна и CS
            // не меняют смысл «рядом». Виртуальный курсор не создаёт намерение.
            plan.AimIntent.Observe((rawPosition - targetPosition) / radius, Time.Current,
                plan.PlannedPressTime, plan.CursorReactionOffset, scaleRealTimeWindow(1));
        }

        private bool shouldAttemptNearTap(HitPlan plan)
            => usesAimIntent
               && CanUsePassToAuthorisePress(plan.IsJump, plan.IsStream)
               && plan.AimIntent.IsReady(Time.Current);

        internal static bool CanUsePassToAuthorisePress(bool isJump, bool isStream)
            => !isJump || isStream;

        internal static bool HasJumpPressCommitment(float normalisedDistance, float inwardProgress)
            => normalisedDistance <= 0.82f || normalisedDistance <= 0.94f && inwardProgress >= 0.08f;

        internal struct AimIntentState
        {
            private const float near_radius = 1.35f;
            private const double observation_lead = 35;
            private const double observation_lag = 55;
            private const double maximum_sample_gap = 50;
            private const double commitment_lifetime = 40;

            private Vector2 previousPosition;
            private double previousTime;
            private bool hasPreviousSample;
            private bool hasPassedTarget;
            private double expiresAt;

            public double? PressTime { get; private set; }

            public readonly bool IsReady(double time)
                => hasPassedTarget && PressTime.HasValue && time >= PressTime.Value && time <= expiresAt;

            public readonly double? GetPressTime(double time)
                => time <= expiresAt ? PressTime : null;

            public void Observe(Vector2 position, double time, double plannedTime, double reactionDelay, double rate)
            {
                Vector2 from = previousPosition;
                double fromTime = previousTime;
                bool hadPreviousSample = hasPreviousSample;
                if (time < fromTime || time > expiresAt)
                    PressTime = null;

                double elapsed = rate > 0 ? (time - fromTime) / rate : 0;
                if (!hadPreviousSample || elapsed <= 0 || elapsed > maximum_sample_gap)
                {
                    hasPassedTarget = false;
                    previousPosition = position;
                    previousTime = time;
                    hasPreviousSample = true;
                    return;
                }

                Vector2 movement = position - from;
                float lengthSquared = movement.LengthSquared;

                // Малые шаги на высоком FPS накапливаются, а не отбрасываются
                // каждый кадр. Медленный проход остаётся тем же жестом.
                if (elapsed < 8 && lengthSquared < 0.025f * 0.025f)
                    return;

                previousPosition = position;
                previousTime = time;

                // Вход в окрестность ещё не законченное наведение. Пустой клик на подлёте
                // съедал план до того, как курсор успевал пересечь настоящий круг.
                hasPassedTarget = lengthSquared > 0 && Vector2.Dot(position, movement) >= 0;

                // Отсекаем дрожание на месте, телепорты и большой разрыв между
                // кадрами. Само присутствие рядом с кругом не означает клик.
                if (PressTime.HasValue || lengthSquared <= 0 || lengthSquared > 6 * 6
                    || Math.Sqrt(lengthSquared) / elapsed < 0.0025)
                    return;

                float closestProgress = Math.Clamp(-Vector2.Dot(from, movement) / lengthSquared, 0, 1);
                Vector2 closest = from + movement * closestProgress;
                double passTime = fromTime + (time - fromTime) * closestProgress;

                if (closest.LengthSquared > near_radius * near_radius
                    || passTime < plannedTime - observation_lead * rate
                    || passTime > plannedTime + observation_lag * rate)
                    return;

                // Запоминаем короткое намерение нажать, а не ждём успешного
                // наведения. После прохода клик может честно оказаться пустым.
                // Нажатие рядом само по себе не тянет ритм в раннюю сторону.
                PressTime = Math.Max(plannedTime, passTime + reactionDelay);
                expiresAt = PressTime.Value + commitment_lifetime * rate;
            }
        }
    }
}
