// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    internal static class ImpactPointHelper
    {
        public static Vector2 GetImpactPoint(Vector2 targetPosition, float radius, Vector2 rawPosition, Vector2? previousPosition, Vector2? nextPosition,
                                             double centerBias, float strengthMultiplier = 1f)
        {
            if (radius <= 0.01f)
                return targetPosition;

            Vector2 undershootDirection = getUndershootDirection(targetPosition, rawPosition, previousPosition, nextPosition);

            if (undershootDirection.LengthSquared <= 0.0001f)
                return targetPosition;

            float clampedCenterBias = Math.Clamp((float)centerBias, 0, 1);
            float rawDistance = (rawPosition - targetPosition).Length;
            float engagement = Math.Clamp(rawDistance / Math.Max(1f, radius * 2.15f), 0f, 1f);
            engagement = MathF.Pow(engagement, 0.9f);
            float baseOffsetRatio = Math.Clamp(0.30f - clampedCenterBias * 0.18f, 0.06f, 0.30f);
            float offset = radius * baseOffsetRatio * Math.Max(0.18f, strengthMultiplier) * engagement;

            return targetPosition + undershootDirection * offset;
        }

        private static Vector2 getUndershootDirection(Vector2 targetPosition, Vector2 rawPosition, Vector2? previousPosition, Vector2? nextPosition)
        {
            Vector2 pathDirection = getPathDirection(targetPosition, previousPosition, nextPosition);
            Vector2 userSideDirection = normaliseOrZero(rawPosition - targetPosition);

            if (pathDirection.LengthSquared <= 0.0001f)
                return userSideDirection;

            Vector2 mapUndershootDirection = -pathDirection;

            if (userSideDirection.LengthSquared <= 0.0001f)
                return mapUndershootDirection;

            float alignment = Vector2.Dot(mapUndershootDirection, userSideDirection);
            float userInfluence = alignment <= 0
                ? 0.16f
                : Math.Clamp(0.28f + alignment * 0.28f, 0.28f, 0.56f);

            return normaliseOrZero(mapUndershootDirection * (1 - userInfluence) + userSideDirection * userInfluence);
        }

        private static Vector2 getPathDirection(Vector2 targetPosition, Vector2? previousPosition, Vector2? nextPosition)
        {
            Vector2 incoming = previousPosition.HasValue ? normaliseOrZero(targetPosition - previousPosition.Value) : Vector2.Zero;
            Vector2 outgoing = nextPosition.HasValue ? normaliseOrZero(nextPosition.Value - targetPosition) : Vector2.Zero;

            if (incoming.LengthSquared > 0.0001f && outgoing.LengthSquared > 0.0001f)
            {
                float alignment = Vector2.Dot(incoming, outgoing);

                if (alignment > -0.25f)
                    return normaliseOrZero(incoming + outgoing * Math.Clamp(0.8f + alignment * 0.2f, 0.6f, 1f));

                return incoming;
            }

            if (incoming.LengthSquared > 0.0001f)
                return incoming;

            return outgoing;
        }

        private static Vector2 normaliseOrZero(Vector2 vector)
        {
            float lengthSquared = vector.LengthSquared;

            if (lengthSquared <= 0.0001f)
                return Vector2.Zero;

            return vector / MathF.Sqrt(lengthSquared);
        }
    }
}
