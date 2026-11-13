// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
using System;
namespace MosuPpRxCs;

/// <summary>A feasible carried cursor position, projected onto the actual circle boundary only when outside.</summary>
internal static class RxCursorGeometry
{
    public static float Radius(float cs) => MathF.Max(1, 54.4f - 4.48f * cs);

    public static RxVec2 EnterCircle(RxVec2 cursor, RxVec2 centre, float radius)
    {
        RxVec2 delta = cursor - centre;
        float distance = delta.Length;
        return distance <= radius ? cursor : centre + delta * (radius / distance);
    }

    // The first position is free before the first note. Choose its edge towards the next note.
    public static RxVec2 Start(RxVec2 first, RxVec2 next, float radius)
    {
        RxVec2 delta = next - first;
        float distance = delta.Length;
        return distance <= float.Epsilon ? first : first + delta * (MathF.Min(radius, distance) / distance);
    }
}
