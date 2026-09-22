using System;
using System.Collections.Generic;

namespace MosuRxPureCs;

internal sealed class SliderCurve
{
    private const float BEZIER_TOLERANCE = 0.25f;
    private const int CATMULL_DETAIL = 50;
    private const float CIRCULAR_ARC_TOLERANCE = 0.1f;

    private readonly List<RxVec2> path = new();
    private readonly List<double> lengths = new();

    public SliderCurve(IReadOnlyList<RxPathControlPoint> points, double? expectedLength)
    {
        CalculatePath(points);
        CalculateLength(points, expectedLength);
    }

    public double Distance => lengths.Count == 0 ? 0d : lengths[^1];

    public RxVec2 PositionAt(double progress)
    {
        if (path.Count == 0)
            return RxVec2.Zero;

        double d = Math.Clamp(progress, 0d, 1d) * Distance;
        int i = IndexOfDistance(d);

        if (i <= 0)
            return path[0];
        if (i >= path.Count)
            return path[^1];

        RxVec2 p0 = path[i - 1];
        RxVec2 p1 = path[i];
        double d0 = lengths[i - 1];
        double d1 = lengths[i];

        if (Math.Abs(d0 - d1) <= double.Epsilon)
            return p0;

        float w = (float)((d - d0) / (d1 - d0));
        return p0 + (p1 - p0) * w;
    }

    private int IndexOfDistance(double d)
    {
        int lo = 0;
        int hi = lengths.Count - 1;

        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            double value = lengths[mid];

            if (value < d)
                lo = mid + 1;
            else if (value > d)
                hi = mid - 1;
            else
                return mid;
        }

        return lo;
    }

    private void CalculatePath(IReadOnlyList<RxPathControlPoint> points)
    {
        path.Clear();

        if (points.Count == 0)
            return;

        var vertices = new List<RxVec2>(points.Count);
        foreach (RxPathControlPoint point in points)
            vertices.Add(point.Position);

        int start = 0;

        for (int i = 0; i < points.Count; i++)
        {
            if (points[i].Kind is null && i < points.Count - 1)
                continue;

            int count = i - start + 1;
            RxVec2[] segment = vertices.GetRange(start, count).ToArray();
            RxPathType kind = points[start].Kind ?? RxPathType.Linear;

            CalculateSubpath(segment, kind);
            start = i;
        }

        DeduplicateConsecutive(path);
    }

    private void CalculateLength(IReadOnlyList<RxPathControlPoint> points, double? expectedLength)
    {
        lengths.Clear();

        if (path.Count == 0)
            return;

        double calculated = 0d;
        lengths.Add(0d);

        for (int i = 1; i < path.Count; i++)
        {
            calculated += (path[i] - path[i - 1]).Length;
            lengths.Add(calculated);
        }

        if (!expectedLength.HasValue || calculated == expectedLength.Value)
            return;

        double expected = expectedLength.Value;

        // osu!stable does not extend if the last two control points are equal.
        if (points.Count >= 2
            && points[^2].Position == points[^1].Position
            && expected > calculated)
        {
            lengths.Add(calculated);
            return;
        }

        if (lengths.Count == 1)
            return;

        // The last length is always replaced/adjusted.
        lengths.RemoveAt(lengths.Count - 1);

        int lastValid = 0;
        for (int i = 0; i < lengths.Count; i++)
        {
            if (lengths[i] < expected)
                lastValid = i + 1;
        }

        if (lastValid < lengths.Count)
        {
            lengths.RemoveRange(lastValid, lengths.Count - lastValid);
            int targetPathCount = lastValid + 1;
            if (path.Count > targetPathCount)
                path.RemoveRange(targetPathCount, path.Count - targetPathCount);

            if (lengths.Count == 0)
            {
                lengths.Add(0d);
                return;
            }
        }

        int endIndex = lengths.Count;
        int prevIndex = endIndex - 1;

        if (endIndex >= path.Count)
        {
            lengths.Add(expected);
            return;
        }

        RxVec2 direction = (path[endIndex] - path[prevIndex]).Normalize();
        path[endIndex] = path[prevIndex] + direction * (float)(expected - lengths[prevIndex]);
        lengths.Add(expected);
    }

    private void CalculateSubpath(RxVec2[] points, RxPathType kind)
    {
        switch (kind)
        {
            case RxPathType.Bezier:
                ApproximateBezier(points);
                break;

            case RxPathType.Catmull:
                ApproximateCatmull(points);
                break;

            case RxPathType.Linear:
                path.AddRange(points);
                break;

            case RxPathType.PerfectCurve:
                if (points.Length == 3 && ApproximateCircularArc(points[0], points[1], points[2]))
                    break;

                ApproximateBezier(points);
                break;
        }
    }

    private void ApproximateBezier(RxVec2[] points)
    {
        if (points.Length == 0)
            return;
        if (points.Length == 1)
        {
            path.Add(points[0]);
            return;
        }

        var stack = new Stack<RxVec2[]>();
        stack.Push((RxVec2[])points.Clone());

        while (stack.Count > 0)
        {
            RxVec2[] parent = stack.Pop();

            if (BezierIsFlatEnough(parent))
            {
                BezierApproximate(parent);
                continue;
            }

            BezierSubdivide(parent, out RxVec2[] left, out RxVec2[] right);
            // Depth-first, left first.
            stack.Push(right);
            stack.Push(left);
        }

        path.Add(points[^1]);
    }

    private static bool BezierIsFlatEnough(RxVec2[] points)
    {
        float limit = BEZIER_TOLERANCE * BEZIER_TOLERANCE * 4f;

        for (int i = 0; i + 2 < points.Length; i++)
        {
            RxVec2 secondDifference = points[i] - points[i + 1] * 2f + points[i + 2];
            if (secondDifference.LengthSquared > limit)
                return false;
        }

        return true;
    }

    private static void BezierSubdivide(RxVec2[] points, out RxVec2[] left, out RxVec2[] right)
    {
        int count = points.Length;
        left = new RxVec2[count];
        right = new RxVec2[count];
        var mid = (RxVec2[])points.Clone();

        for (int i = count - 1; i >= 1; i--)
        {
            left[count - i - 1] = mid[0];
            right[i] = mid[i];

            for (int j = 0; j < i; j++)
                mid[j] = (mid[j] + mid[j + 1]) / 2f;
        }

        left[count - 1] = mid[0];
        right[0] = mid[0];
    }

    private void BezierApproximate(RxVec2[] points)
    {
        int count = points.Length;
        BezierSubdivide(points, out RxVec2[] left, out RxVec2[] right);
        path.Add(points[0]);

        // Rust source chains left + right[1..], then consumes overlapping triples
        // with step_by(2). Build the same sequence explicitly.
        var sequence = new List<RxVec2>(count * 2 - 1);
        sequence.AddRange(left);
        for (int i = 1; i < right.Length; i++)
            sequence.Add(right[i]);

        for (int i = 1; i + 2 < sequence.Count; i += 2)
        {
            RxVec2 prev = sequence[i];
            RxVec2 curr = sequence[i + 1];
            RxVec2 next = sequence[i + 2];
            path.Add((prev + curr * 2f + next) * 0.25f);
        }
    }

    private void ApproximateCatmull(RxVec2[] points)
    {
        if (points.Length == 1)
            return;

        RxVec2 v1 = points[0];
        RxVec2 v2 = points[0];
        RxVec2 v3 = points.Length > 1 ? points[1] : v2;
        RxVec2 v4 = points.Length > 2 ? points[2] : v3 * 2f - v2;
        CatmullSubpath(v1, v2, v3, v4);

        for (int i = 2; i < points.Length; i++)
        {
            v1 = points[i - 2];
            v2 = points[i - 1];
            v3 = i < points.Length ? points[i] : v2 * 2f - v1;
            v4 = i + 1 < points.Length ? points[i + 1] : v3 * 2f - v2;
            CatmullSubpath(v1, v2, v3, v4);
        }
    }

    private void CatmullSubpath(RxVec2 v1, RxVec2 v2, RxVec2 v3, RxVec2 v4)
    {
        float x1 = 2f * v2.X;
        float x2 = -v1.X + v3.X;
        float x3 = 2f * v1.X - 5f * v2.X + 4f * v3.X - v4.X;
        float x4 = -v1.X + 3f * (v2.X - v3.X) + v4.X;

        float y1 = 2f * v2.Y;
        float y2 = -v1.Y + v3.Y;
        float y3 = 2f * v1.Y - 5f * v2.Y + 4f * v3.Y - v4.Y;
        float y4 = -v1.Y + 3f * (v2.Y - v3.Y) + v4.Y;

        for (int c = 0; c < CATMULL_DETAIL; c++)
        {
            float t1 = (float)c / CATMULL_DETAIL;
            float t2 = t1 * t1;
            float t3 = t2 * t1;

            var p1 = new RxVec2(
                0.5f * (x1 + x2 * t1 + x3 * t2 + x4 * t3),
                0.5f * (y1 + y2 * t1 + y3 * t2 + y4 * t3));

            t1 = (float)(c + 1) / CATMULL_DETAIL;
            t2 = t1 * t1;
            t3 = t2 * t1;

            var p2 = new RxVec2(
                0.5f * (x1 + x2 * t1 + x3 * t2 + x4 * t3),
                0.5f * (y1 + y2 * t1 + y3 * t2 + y4 * t3));

            path.Add(p1);
            path.Add(p2);
        }
    }

    private bool ApproximateCircularArc(RxVec2 a, RxVec2 b, RxVec2 c)
    {
        if (!TryCircularArcProperties(a, b, c, out CircularArcProperties props))
            return false;

        int amountPoints;
        if (2f * props.Radius <= CIRCULAR_ARC_TOLERANCE)
        {
            amountPoints = 2;
        }
        else
        {
            double divisor = 2d * Math.Acos(1d - CIRCULAR_ARC_TOLERANCE / props.Radius);
            amountPoints = Math.Max(2, (int)Math.Ceiling(props.ThetaRange / divisor));
        }

        double denominator = amountPoints - 1;
        double directedRange = props.Direction * props.ThetaRange;

        for (int i = 0; i < amountPoints; i++)
        {
            double fraction = i / denominator;
            double theta = props.ThetaStart + fraction * directedRange;
            var origin = new RxVec2((float)Math.Cos(theta), (float)Math.Sin(theta));
            path.Add(props.Centre + origin * props.Radius);
        }

        return true;
    }

    private static bool TryCircularArcProperties(RxVec2 a, RxVec2 b, RxVec2 c, out CircularArcProperties result)
    {
        float determinant = (b.Y - a.Y) * (c.X - a.X) - (b.X - a.X) * (c.Y - a.Y);
        if (MathF.Abs(determinant) <= float.Epsilon)
        {
            result = default;
            return false;
        }

        float d = 2f * (a.X * (b - c).Y + b.X * (c - a).Y + c.X * (a - b).Y);
        float aSq = a.LengthSquared;
        float bSq = b.LengthSquared;
        float cSq = c.LengthSquared;

        var centre = new RxVec2(
            (aSq * (b - c).Y + bSq * (c - a).Y + cSq * (a - b).Y) / d,
            (aSq * (c - b).X + bSq * (a - c).X + cSq * (b - a).X) / d);

        RxVec2 dA = a - centre;
        RxVec2 dC = c - centre;
        float radius = dA.Length;

        double thetaStart = Math.Atan2(dA.Y, dA.X);
        double thetaEnd = Math.Atan2(dC.Y, dC.X);

        while (thetaEnd < thetaStart)
            thetaEnd += 2d * Math.PI;

        double direction = 1d;
        double thetaRange = thetaEnd - thetaStart;

        RxVec2 orthoAToC = c - a;
        orthoAToC = new RxVec2(orthoAToC.Y, -orthoAToC.X);

        if (orthoAToC.Dot(b - a) < 0f)
        {
            direction = -direction;
            thetaRange = 2d * Math.PI - thetaRange;
        }

        result = new CircularArcProperties(thetaStart, thetaRange, direction, radius, centre);
        return true;
    }

    private static void DeduplicateConsecutive(List<RxVec2> values)
    {
        if (values.Count <= 1)
            return;

        int write = 1;
        RxVec2 last = values[0];

        for (int read = 1; read < values.Count; read++)
        {
            RxVec2 current = values[read];
            if (current == last)
                continue;

            values[write++] = current;
            last = current;
        }

        if (write < values.Count)
            values.RemoveRange(write, values.Count - write);
    }

    private readonly record struct CircularArcProperties(
        double ThetaStart,
        double ThetaRange,
        double Direction,
        float Radius,
        RxVec2 Centre);
}
