// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    public partial class RelaxController
    {
        private double getCursorPassPressTime(HitPlan plan, double scheduledTime)
        {
            if (plan.IsSpinner || plan.Pressed || plan.Released || !plan.CursorPass.HasSegment)
                return scheduledTime;

            // Проход отменяет лишнее ожидание доведения, но не приближает сам ритм.
            // Прогноз выхода из круга не даёт разрешения нажать до запланированного клика.
            double earliest = getCursorPassEarliestTime(plan);
            if (Time.Current < earliest)
                return scheduledTime;

            bool crossing = tryGetSweptPressPosition(plan, out _);
            if (!crossing && getPressTarget(plan)?.HitArea.IsHovered == true)
            {
                double elapsed = plan.CursorPass.Time - plan.CursorPass.FromTime;
                double lookAhead = Math.Clamp(elapsed * 1.5, scaleRealTimeWindow(4), scaleRealTimeWindow(16));
                Vector2 predicted = plan.CursorPass.To + (plan.CursorPass.To - plan.CursorPass.From) * (float)(lookAhead / elapsed);
                crossing = CursorPassState.TryGetInsideInterval(plan.CursorPass.To, predicted, out _, out float exit) && exit < 1;
            }

            return crossing ? Math.Max(earliest, Math.Min(scheduledTime, Time.Current)) : scheduledTime;
        }

        private double getCursorPassEarliestTime(HitPlan plan)
        {
            double window = getPressTarget(plan)?.HitObject.HitWindows?.WindowFor(HitResult.Meh) ?? 0;
            return Math.Max(plan.TargetStartTime - window, plan.GetScheduledPressTime());
        }

        private bool tryGetSweptPressPosition(HitPlan plan, out Vector2 position)
        {
            position = default;
            var target = getPressTarget(plan);
            if (target?.IsLoaded != true || target.AllJudged || target.HitArea.IsHovered
                || !CanUsePassToAuthorisePress(plan.IsJump, plan.IsStream)
                || plan.CursorPass.Time != Time.Current || !plan.CursorPass.HasSegment)
                return false;

            double window = target.HitObject.HitWindows?.WindowFor(HitResult.Meh) ?? 0;
            double earliest = usesReliableProfile ? plan.TargetStartTime - window : getCursorPassEarliestTime(plan);
            bool crossed = plan.CursorPass.TryGetCrossing(earliest, plan.TargetStartTime + window, out Vector2 localPosition);
            // Короткий буфер реального пересечения сохраняет пролёт до наступления плана.
            // Не сдвигает клик в раннюю сторону и не принимает проход снаружи хитбокса.
            if (!crossed && usesReliableProfile)
                crossed = plan.CursorPass.TryGetRecentCrossing(earliest, plan.TargetStartTime + window,
                    Time.Current, scaleRealTimeWindow(12), out localPosition);
            if (!crossed)
                return false;

            position = getTargetScreenSpacePosition(plan) + localPosition * getTargetRadius(plan);
            return target.HitArea.ReceivePositionalInputAt(position);
        }

        internal struct CursorPassState
        {
            public Vector2 From { get; private set; }
            public Vector2 To { get; private set; }
            public double FromTime { get; private set; }
            public double Time { get; private set; }
            public bool HasSegment { get; private set; }
            private bool hasSample;
            private bool hasRecentCrossing;
            private Vector2 recentCrossingPosition;
            private double recentCrossingTime;

            public void Observe(Vector2 position, double time, double maximumGap)
            {
                From = To;
                FromTime = Time;
                To = position;
                Time = time;
                HasSegment = hasSample && time > FromTime && time - FromTime <= maximumGap;
                hasSample = true;

                if (!HasSegment)
                {
                    hasRecentCrossing = false;
                    return;
                }

                if (TryGetInsideInterval(From, To, out float entry, out float exit))
                {
                    Vector2 movement = To - From;
                    float progress = movement.LengthSquared > 0.000001f
                        ? Math.Clamp(-Vector2.Dot(From, movement) / movement.LengthSquared, entry, exit) : 1;
                    recentCrossingPosition = From + movement * progress;
                    recentCrossingTime = FromTime + (Time - FromTime) * progress;
                    hasRecentCrossing = true;
                }
            }

            public readonly bool TryGetRecentCrossing(double earliest, double latest, double time, double maximumAge, out Vector2 position)
            {
                position = recentCrossingPosition;
                return hasRecentCrossing && recentCrossingTime >= earliest && recentCrossingTime <= latest
                       && time >= recentCrossingTime && time - recentCrossingTime <= maximumAge;
            }

            public readonly bool TryGetCrossing(double earliest, double latest, out Vector2 position)
            {
                position = default;
                if (!HasSegment || !TryGetInsideInterval(From, To, out float entry, out float exit))
                    return false;

                double elapsed = Time - FromTime;
                entry = Math.Max(entry, (float)((earliest - FromTime) / elapsed));
                exit = Math.Min(exit, (float)((latest - FromTime) / elapsed));
                if (entry > exit)
                    return false;

                // Точка строго на пройденном отрезке и внутри исходного хитбокса.
                position = From + (To - From) * ((entry + exit) * 0.5f);
                return true;
            }

            internal static bool TryGetInsideInterval(Vector2 from, Vector2 to, out float entry, out float exit)
            {
                entry = 0;
                exit = 1;
                Vector2 movement = to - from;
                float a = movement.LengthSquared;
                float c = from.LengthSquared - 1;
                if (a <= 0.000001f)
                    return c <= 0;

                float b = Vector2.Dot(from, movement);
                float discriminant = b * b - a * c;
                if (discriminant < 0)
                    return false;

                float root = MathF.Sqrt(discriminant);
                entry = Math.Max(0, (-b - root) / a);
                exit = Math.Min(1, (-b + root) / a);
                return entry <= exit;
            }
        }
    }
}
