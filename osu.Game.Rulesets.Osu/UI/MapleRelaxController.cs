using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Gameplay-only adaptation of the Relax core recovered from the supplied August 2022 Maple
    /// payload: 10085890 (sampling), 10085980 (aim), 10086500 (buttons), 100868f0 (normal RNG).
    /// No loader, hooks, authentication, score submission or cursor correction is used here.
    /// Coordinates are lazer's actual stacked playfield coordinates. stable's four input masks
    /// are mapped to lazer's two logical actions. This is not a claim of whole-client parity.
    /// </summary>
    internal sealed class MapleRelaxController
    {
        private readonly OsuModMosuRelax mod;
        private readonly IReadOnlyList<OsuHitObject> objects;
        private readonly Func<double> normal;
        private readonly Func<OsuHitObject, bool>? alreadyJudged;
        private int index;
        private bool initialised;
        private double lastTime = double.NaN;
        private Vector2? previousCursor;
        private Vector2? departureCursor;
        private bool enteredDepartureArea;
        private bool selectedRight;
        private readonly double[] lastPress = { double.NegativeInfinity, double.NegativeInfinity };
        private readonly double[] releaseAt = { double.PositiveInfinity, double.PositiveInfinity };
        private readonly double[] heldTarget = new double[2];
        private readonly bool[] held = new bool[2];
        private double pressOffset;
        private double holdOffset;
        private double unscaledHoldOffset;
        private double sampledRate;

        public OsuHitObject? Target => index < objects.Count ? objects[index] : null;
        public double? ScheduledPressTime => Target?.StartTime + pressOffset + mod.MapleOffset.Value;
        public double? NextReleaseTime => held[0] || held[1] ? Math.Min(releaseAt[0], releaseAt[1]) : null;
        public double AppliedTimingOffset => pressOffset + mod.MapleOffset.Value;
        public Action<bool, OsuAction, double, double>? InputChanged;

        public MapleRelaxController(OsuModMosuRelax mod, IReadOnlyList<OsuHitObject> objects, Func<double>? normal = null,
                                    Func<OsuHitObject, bool>? alreadyJudged = null)
        {
            this.mod = mod;
            this.objects = objects;
            this.alreadyJudged = alreadyJudged;
            this.normal = normal ?? new MapleRelaxRandom((uint)Guid.NewGuid().GetHashCode()).NextNormal;
        }

        public void Reset()
        {
            index = 0;
            initialised = false;
            lastTime = double.NaN;
            previousCursor = departureCursor = null;
            enteredDepartureArea = false;
            selectedRight = false;
            for (int i = 0; i < 2; i++)
            {
                held[i] = false;
                releaseAt[i] = double.PositiveInfinity;
                lastPress[i] = double.NegativeInfinity;
            }
        }

        public bool IsAwaiting(OsuHitObject obj) => initialised && ReferenceEquals(Target, obj);

        public void Update(double time, double rate, Vector2 cursor)
        {
            // Practice seeks must not retain presses or object indices from the previous timeline.
            if (!double.IsNaN(lastTime) && time < lastTime)
            {
                releaseAll(time);
                Reset();
            }
            lastTime = time;
            rate = Math.Max(0.05, Math.Abs(rate));
            double adjustedTime = time - mod.MapleOffset.Value;
            if (!initialised)
            {
                while (index < objects.Count && ((objects[index] is Spinner ? endTime(objects[index]) : objects[index].StartTime) + hitWindow(objects[index]) < adjustedTime
                                                || alreadyJudged?.Invoke(objects[index]) == true))
                    index++;
                selectedRight = mod.MaplePrimaryButton.Value == MapleRelaxButton.Right;
                initialised = true;
                sample(rate);
            }

            if (!mod.MapleLowestHoldTimes.Value && rate != sampledRate)
            {
                // lazer can apply rate adjustments after the controller's initial update.
                // Re-scale the existing hold sample, without drawing a new random timing.
                holdOffset = Math.Truncate(unscaledHoldOffset * rate);
                sampledRate = rate;
            }

            OsuHitObject? target = Target;
            if (target != null)
            {
                double window = hitWindow(target);
                if (adjustedTime >= target.StartTime - window)
                {
                    bool canAim = canPressFromAim(target, cursor, adjustedTime);
                    int button = selectedRight ? 1 : 0;
                    // Preserve Maple's 90ms same-finger spacing and explicit late/off-target
                    // advancement. An emitted click is not assumed to be a successful judgement.
                    if (canAim && (Math.Abs(adjustedTime - lastPress[button]) >= 90 * rate || mod.MapleLowestHoldTimes.Value)
                        || adjustedTime > target.StartTime + window)
                    {
                        double end = target is IHasDuration duration ? duration.EndTime : target.StartTime;
                        releaseAt[button] = (target is HitCircle ? adjustedTime : end) + holdOffset;
                        heldTarget[button] = target.StartTime;
                        lastPress[button] = adjustedTime;
                        setHeld(button, true, time);
                        index++;
                        previousCursor = null;
                        departureCursor = null;
                        enteredDepartureArea = false;
                        sample(rate);
                        chooseNextButton(target);
                    }
                }
            }

            // The recovered implementation compares these deadlines to the unshifted audio clock.
            // Keep this distinction instead of silently changing offset/hold interactions.
            for (int button = 0; button < 2; button++)
            {
                if (held[button] && time >= releaseAt[button])
                {
                    setHeld(button, false, time);
                    releaseAt[button] = double.PositiveInfinity;
                }
            }
        }

        private void sample(double rate)
        {
            if (Target == null)
                return;

            pressOffset = Math.Truncate(normal() * TimingDeviation(mod.MapleTargetUR.Value));
            if (mod.MapleLowestHoldTimes.Value)
            {
                holdOffset = index + 1 < objects.Count
                    ? Math.Min(25, Math.Truncate((objects[index + 1].StartTime - endTime(Target)) * 0.5))
                    : 25;
            }
            else
            {
                bool circle = Target is HitCircle;
                double mean = circle ? mod.MapleCircleHold.Value : mod.MapleSliderHold.Value;
                double error = circle ? mod.MapleCircleHoldError.Value : mod.MapleSliderHoldError.Value;
                unscaledHoldOffset = mean + normal() * (error / 2.5);
                holdOffset = Math.Truncate(unscaledHoldOffset * rate);
                sampledRate = rate;
            }
        }

        internal static double TimingDeviation(double targetUR) => (targetUR - 0.00533158) / 3.99535 / 2.3;

        private void chooseNextButton(OsuHitObject previous)
        {
            if (Target != null && previous is not Spinner)
            {
                double anchor = previous is Slider && mod.MapleSliderAlternationOverride.Value ? previous.StartTime : endTime(previous);
                double interval = Math.Clamp(Target.StartTime - anchor, 1, 60000);
                int bpm = (int)(60000 / interval) / 4;
                if (bpm >= mod.MapleAlternateBPM.Value)
                {
                    bool primaryRight = mod.MaplePrimaryButton.Value == MapleRelaxButton.Right;
                    selectedRight = selectedRight == primaryRight ? mod.MapleSecondaryButton.Value == MapleRelaxButton.Right : primaryRight;
                    return;
                }
            }
            selectedRight = mod.MaplePrimaryButton.Value == MapleRelaxButton.Right;
        }

        private bool canPressFromAim(OsuHitObject target, Vector2 cursor, double time)
        {
            Vector2? previous = previousCursor;
            previousCursor = cursor;
            double scheduled = target.StartTime + pressOffset;
            if (target is Spinner && time >= scheduled)
                return true;
            if (!mod.MapleWaitLate.Value && time >= target.StartTime + TimingDeviation(mod.MapleTargetUR.Value))
                return true;

            float radius = (float)target.Radius;
            Vector2 centre = target.StackedPosition;
            float distance = Vector2.Distance(cursor, centre);
            if (distance <= radius && time >= scheduled && distance <= radius * 0.9)
                return true;
            if (target is not HitCircle || index + 1 >= objects.Count)
                return false;

            Vector2 next = objects[index + 1].StackedPosition;
            float nextDistance = Vector2.Distance(cursor, next);
            float previousDistance = previous.HasValue ? Vector2.Distance(previous.Value, centre) : 0;
            float previousNextDistance = previous.HasValue ? Vector2.Distance(previous.Value, next) : 0;
            double nextAngle = previous.HasValue ? angle(previous.Value - cursor, previous.Value - next) : 0;
            double targetAngle = previous.HasValue ? angle(previous.Value - cursor, previous.Value - centre) : 0;

            if (distance <= radius && mod.MapleDirectionPrediction.Value && previous.HasValue
                && previousDistance < distance && nextDistance < previousNextDistance
                && distance > radius * mod.MaplePredictionScale.Value && nextAngle <= mod.MaplePredictionAngle.Value)
                return true;

            // Maple's additional outside-circle departure branch (not a swept hitbox and not
            // cursor movement). Kept separately from the configurable in-circle prediction.
            if (distance <= radius || nextDistance <= radius)
                return false;
            float departureRadius = (previousNextDistance - radius * 2) * 0.33333334f + radius;
            enteredDepartureArea |= distance <= departureRadius;
            if (departureCursor.HasValue && targetAngle <= 0)
                departureCursor = null;
            else if (!departureCursor.HasValue && targetAngle > 0)
            {
                if (nextAngle > 0)
                    return false;
                departureCursor = cursor;
            }

            if (nextAngle > 0 || time <= scheduled)
                return false;
            return enteredDepartureArea && distance > departureRadius
                   || departureCursor.HasValue && Vector2.Distance(departureCursor.Value, cursor) > departureRadius;
        }

        private static double angle(Vector2 a, Vector2 b)
        {
            double denominator = Math.Sqrt((double)a.LengthSquared * b.LengthSquared);
            return denominator < 1e-15 ? 0 : Math.Acos(Math.Clamp(Vector2.Dot(a, b) / denominator, -1, 1)) * (180 / Math.PI);
        }

        private static double endTime(OsuHitObject obj) => obj is IHasDuration duration ? duration.EndTime : obj.StartTime;

        private static double hitWindow(OsuHitObject obj)
            => (obj is Slider slider ? slider.HeadCircle : obj).HitWindows?.WindowFor(HitResult.Meh) ?? 0;

        private void setHeld(int button, bool value, double time)
        {
            if (held[button] == value)
                return;
            held[button] = value;
            InputChanged?.Invoke(value, button == 0 ? OsuAction.LeftButton : OsuAction.RightButton, time, heldTarget[button]);
        }

        private void releaseAll(double time)
        {
            for (int button = 0; button < 2; button++)
                setHeld(button, false, time);
        }
    }

    /// <summary>MT19937 and cached Marsaglia polar normal sampling, as in the recovered core.</summary>
    internal sealed class MapleRelaxRandom
    {
        private readonly uint[] state = new uint[624];
        private int index = 624;
        private double? spare;

        public MapleRelaxRandom(uint seed)
        {
            state[0] = seed;
            for (uint i = 1; i < state.Length; i++)
                state[i] = unchecked(1812433253U * (state[i - 1] ^ state[i - 1] >> 30) + i);
        }

        internal uint NextUInt()
        {
            if (index == state.Length)
            {
                for (int i = 0; i < state.Length; i++)
                {
                    uint value = state[i] & 0x80000000U | state[(i + 1) % 624] & 0x7fffffffU;
                    state[i] = state[(i + 397) % 624] ^ value >> 1 ^ ((value & 1) != 0 ? 0x9908b0dfU : 0);
                }
                index = 0;
            }
            uint result = state[index++];
            result ^= result >> 11;
            result ^= result << 7 & 0x9d2c5680U;
            result ^= result << 15 & 0xefc60000U;
            return result ^ result >> 18;
        }

        private double uniform() => (NextUInt() + NextUInt() * 4294967296.0) / 18446744073709551616.0;

        public double NextNormal()
        {
            if (spare.HasValue)
            {
                double result = spare.Value;
                spare = null;
                return result;
            }
            double u;
            double v;
            double length;
            do
            {
                u = uniform() * 2 - 1;
                v = uniform() * 2 - 1;
                length = u * u + v * v;
            } while (length >= 1 || u == 0 || v == 0);
            double factor = Math.Sqrt(-2 * Math.Log(length) / length);
            spare = v * factor;
            return u * factor;
        }
    }
}
