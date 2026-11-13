// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
using System;
using System.Linq;
using MosuPpRxCs;

namespace osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

/// <summary>Bounded experimental corrections based on actual movement, clutter and repeated rotation, in played time.</summary>
internal static class RxPhysicalChallenge
{
    internal readonly record struct Features(double SpeedPressure, double VisibleNotes, double RotationShare);

    public static Features Analyze(RxBeatmap map)
    {
        if (map.HitObjects.Count < 3) return default;
        var path = RxDifficultyCalculator.CursorPath(map, map.CircleSize);
        double start = path[0].Time;
        int bins = Math.Max(1, (int)Math.Ceiling((path[^1].Time - start) / 1000) + 1);
        var pressure = new double[bins];
        double flow = 0, total = 0;
        int run = 0, lastSign = 0;
        for (int i = 1; i < path.Count; i++)
        {
            double gap = path[i].Time - path[i - 1].Time;
            if (gap <= 0 || gap > 500 || path[i].IsSpinner || path[i - 1].IsSpinner)
            { run = 0; lastSign = 0; continue; }
            double distance = (path[i].CursorPosition - path[i - 1].EndPosition).Length;
            // A metadata BPM change or fully overlapping notes cannot produce pressure.
            double value = RxMath.SmoothStep(1000 / gap, 4, 10) * RxMath.SmoothStep(distance * 1000 / gap, 350, 1600);
            int bin = Math.Clamp((int)((path[i].Time - start) / 1000), 0, bins - 1);
            pressure[bin] += value * gap / 1000;
            total += value;
            if (i < 2 || map.HitObjects[i].Kind != RxHitObjectKind.Circle || map.HitObjects[i - 1].Kind != RxHitObjectKind.Circle
                || map.HitObjects[i - 2].Kind != RxHitObjectKind.Circle)
            { run = 0; lastSign = 0; continue; }
            var incoming = path[i - 1].Position - path[i - 2].Position;
            var outgoing = path[i].Position - path[i - 1].Position;
            double a = incoming.Length, b = outgoing.Length;
            double previousGap = path[i - 1].Time - path[i - 2].Time;
            if (a < 1 || b < 1 || previousGap <= 0 || distance <= 0)
            { run = 0; lastSign = 0; continue; }
            double angle = Math.Acos(Math.Clamp(-incoming.Dot(outgoing) / (a * b), -1, 1)) * 180 / Math.PI;
            double cross = incoming.X * outgoing.Y - incoming.Y * outgoing.X;
            int sign = Math.Sign(cross);
            double rhythm = 1 - RxMath.SmoothStep(Math.Abs(Math.Log(gap / previousGap)), Math.Log(1.05), Math.Log(1.25));
            double spacing = 1 - RxMath.SmoothStep(Math.Abs(Math.Log(a / b)), Math.Log(1.10), Math.Log(1.5));
            double angleWeight = RxMath.SmoothStep(angle, 5, 15) * (1 - RxMath.SmoothStep(angle, 60, 75));
            if (sign != 0 && angleWeight > 0 && rhythm > 0 && spacing > 0)
                run = sign == lastSign ? run + 1 : 1;
            else run = 0;
            lastSign = sign;
            flow += value * angleWeight * rhythm * spacing * RxMath.SmoothStep(run, 2, 5);
        }
        // Fixed hardest twenty seconds: a few isolated fast notes cannot earn a full speed correction.
        double speed = pressure.Select(x => Math.Min(1, x)).OrderByDescending(x => x).Take(20).Sum() / 20;
        return new Features(speed, RxVisibleNotesReading.VisibleNotes(map, map.ApproachRate), total > 0 ? flow / total : 0);
    }

    public static double Speed(Features f, double strength) => 1 + 0.16 * strength * f.SpeedPressure;
    public static double Reading(Features f, double existing, double strength)
        => Math.Max(1, (1 + 0.12 * strength * RxMath.SmoothStep(f.VisibleNotes, 4, 10)) / Math.Max(1, existing));
    public static double Circle(float nativeCs, float playedCs, double strength)
    {
        double ratio = RxCursorGeometry.Radius(nativeCs) / RxCursorGeometry.Radius(playedCs);
        return 1 + 0.08 * strength * RxMath.SmoothStep(ratio - 1, 0, 0.4);
    }
    public static double Rotation(Features f, double strength) => 1 - 0.06 * strength * f.RotationShare;
}
