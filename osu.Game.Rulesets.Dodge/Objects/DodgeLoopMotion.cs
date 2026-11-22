using System;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects
{
    internal static class DodgeLoopMotion
    {
        public static bool IsLooping(this DodgeMovementType type)
            => type is DodgeMovementType.PingPong or DodgeMovementType.OrbitClockwise or DodgeMovementType.OrbitCounterclockwise;

        public static Vector2 Position(Vector2 start, Vector2 end, float progress, DodgeMovementType type,
                                       int cycles, float phase, DodgeMovementEasing easing)
        {
            double cycle = Math.Max(0, progress) * Math.Max(1, cycles) + phase / 360.0;
            double fraction = cycle - Math.Floor(cycle);
            if (type == DodgeMovementType.PingPong)
            {
                float leg = (float)(fraction < 0.5 ? fraction * 2 : (1 - fraction) * 2);
                return start + (end - start) * easing.Apply(leg);
            }

            float angle = (float)(Math.PI * 2 * easing.Apply((float)fraction));
            if (type == DodgeMovementType.OrbitCounterclockwise)
                angle = -angle;
            Vector2 radius = (start - end) / 2;
            Vector2 centre = (start + end) / 2;
            return centre + new Vector2(radius.X * MathF.Cos(angle) - radius.Y * MathF.Sin(angle),
                radius.X * MathF.Sin(angle) + radius.Y * MathF.Cos(angle));
        }
    }
}
