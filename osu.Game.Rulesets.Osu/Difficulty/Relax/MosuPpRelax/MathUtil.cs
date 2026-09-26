using System;

namespace MosuPpRxCs;

internal static class RxMath
{
    public static float Clamp(float value, float min, float max) => MathF.Min(max, MathF.Max(min, value));
    public static double Clamp(double value, double min, double max) => Math.Min(max, Math.Max(min, value));

    public static float SmoothStep(float value, float start, float end)
    {
        if (end <= start)
            return value >= end ? 1f : 0f;

        float x = Clamp((value - start) / (end - start), 0f, 1f);
        return x * x * (3f - 2f * x);
    }

    public static double SmoothStep(double value, double start, double end)
    {
        if (value <= start)
            return 0d;
        if (value >= end)
            return 1d;

        double x = (value - start) / (end - start);
        return x * x * (3d - 2d * x);
    }

    public static float SmootherStep(float value, float start, float end)
    {
        float x = Clamp((value - start) / (end - start), 0f, 1f);
        return x * x * x * (x * (6f * x - 15f) + 10f);
    }

    public static float Norm(float p, params float[] values)
    {
        float sum = 0f;
        foreach (float value in values)
            sum += MathF.Pow(value, p);

        return MathF.Pow(sum, 1f / p);
    }
}
public readonly struct RxVec2 : IEquatable<RxVec2>
{
    public readonly float X;
    public readonly float Y;

    public RxVec2(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static RxVec2 Zero => new(0f, 0f);
    public float LengthSquared => X * X + Y * Y;
    public float Length => MathF.Sqrt(LengthSquared);

    public RxVec2 Normalize()
    {
        float len = Length;
        return len <= float.Epsilon ? Zero : this / len;
    }

    public float Dot(RxVec2 other) => X * other.X + Y * other.Y;

    public static RxVec2 operator +(RxVec2 a, RxVec2 b) => new(a.X + b.X, a.Y + b.Y);
    public static RxVec2 operator -(RxVec2 a, RxVec2 b) => new(a.X - b.X, a.Y - b.Y);
    public static RxVec2 operator *(RxVec2 a, float s) => new(a.X * s, a.Y * s);
    public static RxVec2 operator *(float s, RxVec2 a) => a * s;
    public static RxVec2 operator /(RxVec2 a, float s) => new(a.X / s, a.Y / s);

    public bool Equals(RxVec2 other) => X.Equals(other.X) && Y.Equals(other.Y);
    public override bool Equals(object? obj) => obj is RxVec2 other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public static bool operator ==(RxVec2 left, RxVec2 right) => left.Equals(right);
    public static bool operator !=(RxVec2 left, RxVec2 right) => !left.Equals(right);
}
