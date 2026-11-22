// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Framework.Statistics
{
    /// <summary>
    /// Mosu diagnostic: attributes the <see cref="StatisticsCounterType.Pixels"/> counter to the drawable type
    /// whose draw node emitted the geometry, answering "which types consume the fill-rate budget".
    /// </summary>
    /// <remarks>
    /// Active only when the <c>MOSU_PERF_SOURCE_LOGGING=1</c> diagnostic environment variable is set (the same
    /// gate the game-side performance logger uses for CCL/invalidation source attribution), so clean FPS runs pay
    /// a single static bool check per draw node / quad.
    ///
    /// The "current source" label is stamped by <see cref="Graphics.DrawNode.Draw"/> (which every node invokes
    /// before emitting vertices) and consumed by the pixel-counting sites in RendererExtensions. All accesses
    /// happen on the draw thread; the accumulator lock only guards against the game-side logger snapshotting from
    /// its own thread.
    /// </remarks>
    public static class PixelSourceStatistics
    {
        public static readonly bool Active =
            Environment.GetEnvironmentVariable("MOSU_PERF_SOURCE_LOGGING") == "1";

        private static readonly object accumulator_lock = new object();
        private static readonly Dictionary<string, long> accumulator = new Dictionary<string, long>(64);

        private static string currentSource = "(none)";

        /// <summary>
        /// Stamps the drawable type currently emitting geometry. Draw-thread only.
        /// </summary>
        public static void SetCurrentSource(string sourceName) => currentSource = sourceName;

        /// <summary>
        /// Accounts <paramref name="area"/> pixels to the current source. Draw-thread only.
        /// </summary>
        public static void Add(long area)
        {
            lock (accumulator_lock)
            {
                accumulator.TryGetValue(currentSource, out long total);
                accumulator[currentSource] = total + area;
            }
        }

        /// <summary>
        /// Returns the accumulated per-type pixel totals since the previous snapshot, sorted descending,
        /// and resets the accumulator. Safe to call from any thread.
        /// </summary>
        public static List<KeyValuePair<string, long>> TakeSnapshot()
        {
            lock (accumulator_lock)
            {
                var result = new List<KeyValuePair<string, long>>(accumulator);
                accumulator.Clear();
                result.Sort(static (a, b) => b.Value.CompareTo(a.Value));
                return result;
            }
        }
    }
}
