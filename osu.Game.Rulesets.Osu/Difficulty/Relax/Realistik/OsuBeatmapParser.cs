using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace MosuRxPureCs;

public static class OsuBeatmapParser
{
    public static RxBeatmap ParseFile(string path)
        => Parse(File.ReadAllText(path));

    public static RxBeatmap Parse(string text)
    {
        var map = new RxBeatmap();
        string section = string.Empty;
        int order = 0;
        bool hasApproachRate = false;
        var timing = new List<(RxTimingPoint Point, int Order)>();
        var difficulty = new List<(RxDifficultyPoint Point, int Order)>();

        using var reader = new StringReader(text);
        string? line;

        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
                continue;

            if (line.StartsWith("osu file format v", StringComparison.OrdinalIgnoreCase))
            {
                string versionText = line[(line.LastIndexOf('v') + 1)..];
                if (int.TryParse(versionText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int version))
                    map.Version = version;
                continue;
            }

            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                section = line;
                continue;
            }

            switch (section)
            {
                case "[Difficulty]":
                    ParseDifficultyLine(map, line, ref hasApproachRate);
                    break;

                case "[TimingPoints]":
                    ParseTimingPoint(line, timing, difficulty, order++);
                    break;

                case "[HitObjects]":
                    RxHitObject? hitObject = ParseHitObject(line);
                    if (hitObject is not null)
                        map.HitObjects.Add(hitObject);
                    break;
            }
        }

        foreach (var entry in timing.OrderBy(t => t.Point.Time).ThenBy(t => t.Order))
            map.TimingPoints.Add(entry.Point);

        foreach (var entry in difficulty.OrderBy(t => t.Point.Time).ThenBy(t => t.Order))
            map.DifficultyPoints.Add(entry.Point);

        map.HitObjects.Sort((a, b) => a.StartTime.CompareTo(b.StartTime));
        return map;
    }

    private static void ParseDifficultyLine(RxBeatmap map, string line, ref bool hasApproachRate)
    {
        int colon = line.IndexOf(':');
        if (colon < 0)
            return;

        string key = line[..colon].Trim();
        string valueText = line[(colon + 1)..].Trim();
        if (!double.TryParse(valueText, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
            return;

        switch (key)
        {
            case "HPDrainRate":
                map.HpDrainRate = (float)Math.Clamp(value, 0d, 10d);
                break;
            case "CircleSize":
                map.CircleSize = (float)Math.Clamp(value, 0d, 10d);
                break;
            case "OverallDifficulty":
                map.OverallDifficulty = (float)Math.Clamp(value, 0d, 10d);
                if (!hasApproachRate)
                    map.ApproachRate = map.OverallDifficulty;
                break;
            case "ApproachRate":
                map.ApproachRate = (float)Math.Clamp(value, 0d, 10d);
                hasApproachRate = true;
                break;
            case "SliderMultiplier":
                map.SliderMultiplier = Math.Clamp(value, 0.4d, 3.6d);
                break;
            case "SliderTickRate":
                map.SliderTickRate = Math.Clamp(value, 0.5d, 8d);
                break;
        }
    }

    private static void ParseTimingPoint(
        string line,
        List<(RxTimingPoint Point, int Order)> timing,
        List<(RxDifficultyPoint Point, int Order)> difficulty,
        int order)
    {
        string[] fields = line.Split(',');
        if (fields.Length < 2)
            return;

        if (!double.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double time))
            return;
        if (!double.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double beatLength))
            return;

        bool timingChange = fields.Length <= 6 || fields[6].TrimStart().StartsWith('1');
        double speedMultiplier = beatLength < 0d ? 100d / -beatLength : 1d;
        speedMultiplier = Math.Clamp(speedMultiplier, 0.1d, 10d);

        if (timingChange && !double.IsNaN(beatLength))
        {
            double clampedBeatLength = Math.Clamp(beatLength, 6d, 60000d);
            timing.Add((new RxTimingPoint(time, clampedBeatLength), order));
        }

        difficulty.Add((new RxDifficultyPoint(time, speedMultiplier, !double.IsNaN(beatLength)), order));
    }

    private static RxHitObject? ParseHitObject(string line)
    {
        string[] fields = line.Split(',');
        if (fields.Length < 5)
            return null;

        if (!float.TryParse(fields[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x))
            return null;
        if (!float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
            return null;
        if (!double.TryParse(fields[2], NumberStyles.Float, CultureInfo.InvariantCulture, out double time))
            return null;
        if (!int.TryParse(fields[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int type))
            return null;

        var obj = new RxHitObject
        {
            Position = new RxVec2((int)x, (int)y),
            StartTime = time,
        };

        if ((type & 1) != 0)
        {
            obj.Kind = RxHitObjectKind.Circle;
            return obj;
        }

        if ((type & 2) != 0)
        {
            if (fields.Length < 7)
                return null;

            var slider = new RxSliderData();
            slider.ControlPoints.AddRange(ConvertPathString(fields[5], obj.Position));

            if (int.TryParse(fields[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int repeatCount))
                slider.Repeats = Math.Max(0, repeatCount - 1);

            if (fields.Length > 7
                && double.TryParse(fields[7], NumberStyles.Float, CultureInfo.InvariantCulture, out double length)
                && length != 0d)
            {
                slider.ExpectedDistance = Math.Max(0d, length);
            }

            obj.Kind = RxHitObjectKind.Slider;
            obj.Slider = slider;
            return obj;
        }

        if ((type & 8) != 0 || (type & 128) != 0)
        {
            obj.Kind = RxHitObjectKind.Spinner;
            return obj;
        }

        return null;
    }

    private static List<RxPathControlPoint> ConvertPathString(string pointString, RxVec2 offset)
    {
        string[] split = pointString.Split('|');
        var output = new List<RxPathControlPoint>();

        if (split.Length == 0)
            return output;

        int startIndex = 0;
        int endIndex = 0;
        bool first = true;

        while (true)
        {
            endIndex++;
            if (endIndex >= split.Length)
                break;

            if (!IsPathTypeToken(split[endIndex]))
                continue;

            string? endPoint = endIndex + 1 < split.Length ? split[endIndex + 1] : null;
            ConvertPoints(split[startIndex..endIndex], endPoint, first, offset, output);
            startIndex = endIndex;
            first = false;
        }

        if (endIndex > startIndex)
            ConvertPoints(split[startIndex..endIndex], null, first, offset, output);

        return output;
    }

    private static void ConvertPoints(
        string[] points,
        string? endPoint,
        bool first,
        RxVec2 offset,
        List<RxPathControlPoint> output)
    {
        if (points.Length == 0)
            return;

        RxPathType pathType = ParsePathType(points[0]);
        var vertices = new List<RxPathControlPoint>();

        if (first)
            vertices.Add(new RxPathControlPoint(RxVec2.Zero));

        for (int i = 1; i < points.Length; i++)
            vertices.Add(new RxPathControlPoint(ReadPoint(points[i], offset)));

        if (!string.IsNullOrEmpty(endPoint))
            vertices.Add(new RxPathControlPoint(ReadPoint(endPoint, offset)));

        if (vertices.Count == 0)
            return;

        if (pathType == RxPathType.PerfectCurve)
        {
            if (vertices.Count == 3)
            {
                if (IsLinear(vertices[0].Position, vertices[1].Position, vertices[2].Position))
                    pathType = RxPathType.Linear;
            }
            else
            {
                pathType = RxPathType.Bezier;
            }
        }

        vertices[0].Kind = pathType;

        int endPointLength = string.IsNullOrEmpty(endPoint) ? 0 : 1;
        int startIndex = 0;
        int endIndex = 0;

        while (true)
        {
            endIndex++;
            if (endIndex >= vertices.Count - endPointLength)
                break;

            if (vertices[endIndex].Position != vertices[endIndex - 1].Position)
                continue;
            if (pathType == RxPathType.Catmull && endIndex > 1)
                continue;
            if (endIndex == vertices.Count - endPointLength - 1)
                continue;

            vertices[endIndex - 1].Kind = pathType;
            for (int i = startIndex; i < endIndex; i++)
                output.Add(vertices[i]);

            startIndex = endIndex + 1;
        }

        if (endIndex > startIndex)
        {
            for (int i = startIndex; i < endIndex && i < vertices.Count; i++)
                output.Add(vertices[i]);
        }
    }

    private static RxVec2 ReadPoint(string value, RxVec2 startPosition)
    {
        string[] parts = value.Split(':');
        if (parts.Length < 2)
            return RxVec2.Zero;

        float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
        var absolute = new RxVec2((int)x, (int)y);
        return absolute - startPosition;
    }

    private static bool IsLinear(RxVec2 p0, RxVec2 p1, RxVec2 p2)
        => (p1.Y - p0.Y) * (p2.X - p0.X) == (p1.X - p0.X) * (p2.Y - p0.Y);

    private static bool IsPathTypeToken(string token)
        => token.Length > 0 && char.IsAsciiLetter(token[0]);

    private static RxPathType ParsePathType(string token)
    {
        char c = token.Length == 0 ? 'L' : char.ToUpperInvariant(token[0]);
        return c switch
        {
            'B' => RxPathType.Bezier,
            'C' => RxPathType.Catmull,
            'P' => RxPathType.PerfectCurve,
            _ => RxPathType.Linear,
        };
    }
}
