// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class RelaxController
    {
        private double getLinkedPressTime(HitPlan plan)
        {
            if (plan.IsSpinner || getConfiguredSyncRadius() <= 0 || getConfiguredMaxSyncDelay() <= 0)
                return getCursorPassPressTime(plan, plan.GetScheduledPressTime());

            double hitWindow = getPressTarget(plan)?.HitObject.HitWindows?.WindowFor(HitResult.Meh) ?? double.PositiveInfinity;

            double linkedTime = GetAimLinkedPressTime(plan.TargetStartTime, plan.PlannedPressTime, plan.RawCursorTrustedTime,
                plan.CursorReactionOffset, GetAimTimingCoupling(getConfiguredTimingVariance()), scaleRealTimeWindow(GetMaximumAimShift(getConfiguredTimingVariance())),
                scaleRealTimeWindow(80), hitWindow, plan.DeferredPressTime);

            if (usesAimIntent && plan.AimIntent.GetPressTime(Time.Current) is double intentTime)
                linkedTime = Math.Max(plan.DeferredPressTime, Math.Min(linkedTime, intentTime));

            return getCursorPassPressTime(plan, linkedTime);
        }

        internal static double GetAimTimingCoupling(double variance)
            => Math.Clamp(0.18 + variance / 400, 0.18, 0.28);

        internal static double GetMaximumAimShift(double variance)
            => Math.Clamp(1.5 + variance * 0.3, 1.5, 6);

        internal static double GetAimLinkedPressTime(double targetTime, double plannedTime, double? aimTime,
                                                    double reactionDelay, double coupling, double maximumEarlyShift,
                                                    double observationLead, double hitWindow, double deferredUntil = double.NegativeInfinity)
        {
            double linkedTime = plannedTime;

            if (aimTime.HasValue)
            {
                double coordinatedTime = aimTime.Value + reactionDelay;

                if (coordinatedTime < plannedTime)
                {
                    // Раннее доведение немного тянет клик за собой. У давнего
                    // ожидания на ноте этот эффект плавно исчезает.
                    double confidence = Math.Clamp((aimTime.Value - targetTime + observationLead) / (observationLead * 0.5), 0, 1);
                    confidence = confidence * confidence * (3 - 2 * confidence);
                    double shift = maximumEarlyShift > 0
                        ? maximumEarlyShift * Math.Tanh((coordinatedTime - plannedTime) * coupling / maximumEarlyShift)
                        : 0;
                    linkedTime += shift * confidence;
                }
                else
                {
                    // Позднее доведение тоже влияет на клик: он не обязан
                    // происходить в тот же кадр, когда курсор вошёл в ноту.
                    linkedTime = coordinatedTime;
                }
            }

            // Очерёдность и восстановление пальца имеют приоритет над ранним
            // наведением. Окно попадания и notelock проверяются перед кликом.
            return Math.Max(deferredUntil, Math.Clamp(linkedTime, targetTime - hitWindow, targetTime + hitWindow));
        }

        internal static double GetSliderReleaseTime(double endTime, double configuredOffset, double padding, double noise, double gapToNext)
        {
            // Явный offset остаётся точной настройкой игрока, в том числе отрицательный.
            if (configuredOffset != 0)
                return endTime + configuredOffset;

            // В обычном режиме торопимся только после хвоста, не теряя его
            // из-за случайного шума или следующего прыжка.
            padding = Math.Max(0, padding);
            double releasePadding = padding + Math.Clamp(noise, -padding * 0.5, padding * 0.5);
            if (gapToNext >= 0)
                releasePadding = Math.Min(releasePadding, gapToNext * 0.5);

            return endTime + releasePadding;
        }

        internal static double GetMisaltProbability(double configuredProbability, double stamina)
            => Math.Clamp(configuredProbability * (1 + (1 - Math.Clamp(stamina, 0, 1)) * 0.8), 0, 1);
    }
}
