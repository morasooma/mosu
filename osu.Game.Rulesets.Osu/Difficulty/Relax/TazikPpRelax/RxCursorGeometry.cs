// Adapted from taziksfear’s MosuPP 2.0.0 (PR #7, 3d3a196ed6540a3099d246dd5c6f662366c28db8).
using System;
namespace TazikPpRxCs;

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
