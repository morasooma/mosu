// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public static class DodgeBulletPlacementUtils
    {
        public static Vector2 GetEndPositionForSpeed(Vector2 start, Vector2 directionPoint, float speed, double duration)
        {
            Vector2 direction = directionPoint - start;

            if (direction.LengthSquared == 0 || speed <= 0 || duration <= 0)
                return start;

            direction.Normalize();
            return start + direction * (speed * (float)duration);
        }

        public static float? GetTravelSpeed(Vector2 start, Vector2 end, double duration)
        {
            if (duration <= 0)
                return null;

            float distance = (end - start).Length;
            return distance > 0 ? distance / (float)duration : null;
        }
    }
}
