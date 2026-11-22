using System;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Movement equations transcribed from MapleAimAssist in OsuPatcher.Runtime 0.2.10.0.
    /// Uses screen coordinates, like the extracted implementation; no patching or external runtime is used.
    /// </summary>
    internal static class MapleAimAssistMath
    {
        internal static float V2Radius(double time, double startTime, double preempt, double hitWindow, float power, float scale)
            => Math.Min(2.5f, Math.Max(0, (float)((time - (startTime - hitWindow)) / (Math.Max(1, preempt) * 3) + 1)) * 1.4f)
               * 40 * power * scale;

        internal static Vector2 UpdateV2(Vector2 offset, Vector2 raw, Vector2 movement, Vector2 target, float power,
                                       float scale, float displayHeight, float fov, bool hasPreviousTarget)
        {
            if (power <= 0)
                return Vector2.Zero;

            Vector2 difference = target - raw;
            float distance = difference.Length;

            if (distance <= fov && movement.Length > 1.75f * scale && hasPreviousTarget)
            {
                float amount = Math.Min(1, Math.Abs(distance / Math.Max(1, displayHeight / 2)) * power);
                return clampAxes(offset + (difference - offset) * amount, power * 16 * scale);
            }

            return distance > fov ? Resync(offset, movement, 0.3f * power) : offset;
        }

        internal static float V3Strength(float power) => power <= 1 ? power * 0.7f : (power - 1) * 0.15f + 0.7f;

        internal static float V3Radius(float power, float radius) => Math.Max(1.2f, 4 * V3Strength(power)) * radius;

        internal static Vector2 UpdateV3(Vector2 offset, Vector2 raw, Vector2 movement, Vector2 target, float power,
                                       float sliderPower, float scale, float radius, bool isSlider, bool sliderStarted, bool inTimeWindow)
        {
            if (power <= 0)
                return Vector2.Zero;

            float strength = V3Strength(power);
            Vector2 difference = target - (raw + offset);
            float distance = difference.Length;
            float movementLength = movement.Length;

            if (Vector2.Distance(raw, target) > V3Radius(power, radius) || !inTimeWindow)
                return Resync(offset, movement, 0.6f * strength);

            float authority = strength;
            float innerRadius = radius * 0.35f;

            if (isSlider && distance <= innerRadius)
                authority *= Math.Min(distance / Math.Max(1, innerRadius), 1);

            if (isSlider && sliderStarted)
                authority *= sliderPower;

            if (distance > 0 && movementLength > 0)
            {
                float step = (distance <= radius ? 0.65f : 1) * authority * Math.Min(distance, movementLength);
                offset += difference / distance * step;
                offset = clampAxes(offset, power * 16 * scale);
            }

            return offset;
        }

        internal static Vector2 Resync(Vector2 offset, Vector2 movement, float factor)
        {
            float amount = Math.Clamp(factor / 1000, 0, 1);
            return new Vector2(reduceAxis(offset.X, movement.X * amount), reduceAxis(offset.Y, movement.Y * amount));
        }

        private static float reduceAxis(float offset, float movement)
            => Math.Sign(offset) * Math.Max(0, Math.Abs(offset) - Math.Abs(movement));

        private static Vector2 clampAxes(Vector2 offset, float maximum)
            => new Vector2(Math.Clamp(offset.X, -maximum, maximum), Math.Clamp(offset.Y, -maximum, maximum));
    }
}
