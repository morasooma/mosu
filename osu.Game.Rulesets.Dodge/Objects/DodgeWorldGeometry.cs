using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Primitives;
using osu.Game.Rulesets.Dodge.UI;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects
{
    internal static class DodgeWorldGeometry
    {
        public static RectangleF? Bounds(IEnumerable<DodgeHitObject> objects)
        {
            DodgeHitObject[] items = objects.ToArray();
            var rectangles = items.OfType<DodgeArenaChange>().Where(a => a.WorldSpace)
                .Select(a => (a.TargetPosition, a.TargetSize, a.TargetRotation))
                .Concat(items.OfType<DodgeTrigger>().Where(t => t.Action == DodgeTriggerAction.ResizeArena)
                    .Select(t => (t.TargetPosition, t.TargetSize, 0f))).ToArray();
            if (rectangles.Length == 0)
                return null;

            Vector2 minimum = Vector2.Zero;
            Vector2 maximum = DodgePlayfield.BASE_SIZE;
            foreach (var (targetPosition, targetSize, rotation) in rectangles)
            {
                Vector2 position = Vector2.Clamp(targetPosition, new Vector2(-DodgeArenaChange.MAX_WORLD_SIZE), new Vector2(DodgeArenaChange.MAX_WORLD_SIZE));
                Vector2 size = Vector2.Clamp(targetSize, new Vector2(DodgeArenaChange.MIN_SIZE), new Vector2(DodgeArenaChange.MAX_WORLD_SIZE));
                float radians = MathHelper.DegreesToRadians(rotation);
                Vector2 halfExtent = new Vector2(Math.Abs(MathF.Cos(radians)) * size.X + Math.Abs(MathF.Sin(radians)) * size.Y,
                    Math.Abs(MathF.Sin(radians)) * size.X + Math.Abs(MathF.Cos(radians)) * size.Y) / 2;
                Vector2 centre = position + size / 2;
                minimum = Vector2.ComponentMin(minimum, centre - halfExtent);
                maximum = Vector2.ComponentMax(maximum, centre + halfExtent);
            }
            return new RectangleF(minimum.X, minimum.Y, maximum.X - minimum.X, maximum.Y - minimum.Y);
        }

        /// <summary>
        /// Supplies one shared arena timeline to all projectile consumers.
        /// World bounds are only a search envelope, never the projectile's exit boundary.
        /// </summary>
        public static DodgeArenaStateEvaluator ApplyArenaTimeline(IEnumerable<DodgeHitObject> objects)
        {
            DodgeHitObject[] items = objects.ToArray();
            var evaluator = new DodgeArenaStateEvaluator(items.OfType<DodgeArenaChange>(), items.OfType<DodgeTrigger>());
            RectangleF? bounds = Bounds(items);
            foreach (DodgeHitObject item in items)
            {
                item.WorldBounds = bounds;
                item.ArenaTimeline = evaluator;
            }
            return evaluator;
        }

        public static double ExitTime(RectangleF bounds, DodgeArenaStateEvaluator arena, double startTime, double duration,
                                      Vector2 start, Vector2 end, float bulletSize, DodgeMovementType movement,
                                      float amplitude, int cycles, float phase, DodgeMovementEasing easing,
                                      Func<double, Vector2>? camera = null, double hardEndTime = 0)
        {
            if (duration <= 0)
                return startTime;

            if (movement.IsLooping())
                return startTime + duration;

            // Preserve the established base-field trajectory solver on maps
            // without arena events (including its offscreen-entry behaviour).
            if (!arena.HasChanges)
                return camera == null
                    ? DodgeTrajectory.CalculateExitTime(startTime, duration, start, end, bulletSize, movement, amplitude, cycles, phase, easing)
                    : DodgeTrajectory.CalculateExitTimeWithCamera(startTime, duration, start, end, bulletSize, movement,
                        amplitude, cycles, phase, camera, hardEndTime, easing);

            float radius = Math.Max(0, bulletSize) / 2;
            Vector2 anchor = camera?.Invoke(startTime) ?? Vector2.Zero;
            Vector2 position(double time) => DodgeTrajectory.PositionAtProgress(start, end, (float)((time - startTime) / duration),
                movement, amplitude, cycles, phase, easing) + (camera?.Invoke(time) ?? Vector2.Zero) - anchor;
            Vector2 local(Vector2 point, DodgeArenaState state)
            {
                Vector2 offset = point - state.Position - state.Size / 2;
                float radians = MathHelper.DegreesToRadians(-state.Rotation);
                return new Vector2(offset.X * MathF.Cos(radians) - offset.Y * MathF.Sin(radians),
                    offset.X * MathF.Sin(radians) + offset.Y * MathF.Cos(radians)) + state.Size / 2;
            }
            bool inside(double time)
            {
                DodgeArenaState state = arena.Evaluate(time);
                Vector2 point = local(position(time), state);
                return point.X >= -radius && point.X <= state.Size.X + radius
                       && point.Y >= -radius && point.Y <= state.Size.Y + radius;
            }

            DodgeArenaState initial = arena.Evaluate(startTime);
            if (movement == DodgeMovementType.Linear && easing == DodgeMovementEasing.Linear && camera == null)
            {
                double candidate = ExitTime(new RectangleF(0, 0, initial.Size.X, initial.Size.Y), startTime, duration,
                    local(start, initial), local(end, initial), bulletSize, movement, amplitude, cycles, phase, easing);
                if (candidate > startTime && arena.IsGeometryConstantBetween(startTime, candidate))
                    return candidate;
            }

            double speed = (end - start).Length / duration;
            double limit = Math.Max(hardEndTime, startTime + Math.Max(duration,
                speed > 0 ? (bounds.Width + bounds.Height + (start - end).Length) / speed * 2 : duration));
            int samples = (int)Math.Clamp(Math.Ceiling((limit - startTime) / 10), 64, 16384);
            double step = (limit - startTime) / samples;
            // Probe both sides of instantaneous events. Otherwise a shrink followed
            // by expansion within one sample can wrongly resurrect a removed bullet.
            double[] boundaries = arena.GeometryBoundaryTimes.Where(t => t > startTime && t <= limit)
                .SelectMany(t => new[] { Math.BitDecrement(t), t }).Distinct().OrderBy(t => t).ToArray();
            bool linear = movement == DodgeMovementType.Linear && easing == DodgeMovementEasing.Linear && camera == null;
            int boundaryIndex = 0;
            int sampleIndex = 1;
            double previous = startTime;
            bool entered = inside(startTime);
            while (sampleIndex <= samples || boundaryIndex < boundaries.Length)
            {
                double nextBoundary = boundaryIndex < boundaries.Length ? boundaries[boundaryIndex] : limit;
                // Solve a straight ray once per constant arena segment, rather than
                // evaluating thousands of timestamps for every ray of every burst.
                // Leave transitions and camera/nonlinear motion to the sampled solver.
                if (linear && nextBoundary > previous && arena.IsGeometryConstantBetween(previous, Math.BitDecrement(nextBoundary)))
                {
                    DodgeArenaState state = arena.Evaluate(previous);
                    Vector2 origin = local(start, state);
                    Vector2 direction = local(end, state) - origin;
                    double entry = double.NegativeInfinity;
                    double exit = double.PositiveInfinity;
                    bool clip(double coordinate, double delta, double minimum, double maximum)
                    {
                        if (delta == 0)
                            return coordinate >= minimum && coordinate <= maximum;
                        double a = (minimum - coordinate) / delta;
                        double b = (maximum - coordinate) / delta;
                        entry = Math.Max(entry, Math.Min(a, b));
                        exit = Math.Min(exit, Math.Max(a, b));
                        return exit >= entry;
                    }

                    bool intersects = clip(origin.X, direction.X, -radius, state.Size.X + radius)
                                      && clip(origin.Y, direction.Y, -radius, state.Size.Y + radius);
                    double entryTime = startTime + entry * duration;
                    double exitTime = startTime + exit * duration;
                    if (intersects && exitTime >= previous && entryTime < nextBoundary)
                    {
                        entered = true;
                        if (exitTime < nextBoundary)
                            return exitTime;
                    }
                    else if (entered)
                        return previous;

                    if (!inside(nextBoundary) && entered)
                        return nextBoundary;
                    entered |= inside(nextBoundary);
                    previous = nextBoundary;
                    while (boundaryIndex < boundaries.Length && boundaries[boundaryIndex] <= previous)
                        boundaryIndex++;
                    sampleIndex = Math.Max(sampleIndex, (int)Math.Floor((previous - startTime) / step) + 1);
                    if (previous >= limit)
                        break;
                    continue;
                }

                double nextSample = sampleIndex <= samples ? startTime + step * sampleIndex : double.PositiveInfinity;
                nextBoundary = boundaryIndex < boundaries.Length ? boundaries[boundaryIndex] : double.PositiveInfinity;
                double time = Math.Min(nextSample, nextBoundary);
                if (nextSample <= time) sampleIndex++;
                if (nextBoundary <= time) boundaryIndex++;
                if (inside(time))
                    entered = true;
                else if (entered)
                {
                    double low = previous;
                    double high = time;
                    for (int iteration = 0; iteration < 20; iteration++)
                    {
                        double middle = (low + high) / 2;
                        if (inside(middle)) low = middle;
                        else high = middle;
                    }
                    return high;
                }
                previous = time;
            }
            return entered ? limit : startTime;
        }

        // Static envelope calculation retained for geometry/search estimates.
        public static double ExitTime(RectangleF bounds, double startTime, double duration, Vector2 start, Vector2 end,
                                      float bulletSize, DodgeMovementType movement, float amplitude, int cycles, float phase,
                                      DodgeMovementEasing easing, Func<double, Vector2>? camera = null, double hardEndTime = 0)
        {
            if (duration <= 0)
                return startTime;
            float radius = Math.Max(0, bulletSize) / 2;
            if (movement == DodgeMovementType.Linear && easing == DodgeMovementEasing.Linear && camera == null)
            {
                Vector2 delta = end - start;
                float entry = float.NegativeInfinity;
                float exit = float.PositiveInfinity;
                bool clip(float origin, float direction, float minimum, float maximum)
                {
                    if (direction == 0) return origin >= minimum && origin <= maximum;
                    float a = (minimum - origin) / direction;
                    float b = (maximum - origin) / direction;
                    entry = Math.Max(entry, Math.Min(a, b));
                    exit = Math.Min(exit, Math.Max(a, b));
                    return exit >= entry;
                }
                if (!clip(start.X, delta.X, bounds.Left - radius, bounds.Right + radius)
                    || !clip(start.Y, delta.Y, bounds.Top - radius, bounds.Bottom + radius) || exit < 0)
                    return startTime;
                return startTime + duration * (float.IsFinite(exit) ? exit : 1);
            }
            bool inside(Vector2 p) => p.X >= bounds.Left - radius && p.X <= bounds.Right + radius
                                     && p.Y >= bounds.Top - radius && p.Y <= bounds.Bottom + radius;
            Vector2 anchor = camera?.Invoke(startTime) ?? Vector2.Zero;
            Vector2 position(double time) => DodgeTrajectory.PositionAtProgress(start, end, (float)((time - startTime) / duration),
                movement, amplitude, cycles, phase, easing) + (camera?.Invoke(time) ?? Vector2.Zero) - anchor;
            double speed = (end - start).Length / duration;
            double limit = Math.Max(hardEndTime, startTime + Math.Max(duration, speed > 0 ? (bounds.Width + bounds.Height + (start - end).Length) / speed * 2 : duration));
            int samples = Math.Clamp((int)Math.Ceiling((limit - startTime) / 10), 64, 16384);
            bool entered = inside(start);
            double previous = startTime;
            for (int i = 1; i <= samples; i++)
            {
                double time = startTime + (limit - startTime) * i / samples;
                if (inside(position(time)))
                {
                    entered = true;
                    previous = time;
                }
                else if (entered)
                {
                    double low = previous;
                    double high = time;
                    for (int iteration = 0; iteration < 16; iteration++)
                    {
                        double middle = (low + high) / 2;
                        if (inside(position(middle))) low = middle;
                        else high = middle;
                    }
                    return high;
                }
            }
            return entered ? limit : startTime;
        }
    }
}
