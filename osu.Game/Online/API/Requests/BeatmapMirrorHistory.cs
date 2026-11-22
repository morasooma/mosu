using System;
using System.Collections.Generic;
using System.Linq;

namespace osu.Game.Online.API.Requests
{
    /// <summary>Session-local measured throughput/latency. A cancelled race is not a failed mirror.</summary>
    internal sealed class BeatmapMirrorHistory
    {
        private readonly object sync = new object();
        private readonly Dictionary<string, Sample> samples = new Dictionary<string, Sample>();

        internal BeatmapMirrorSource[] Order(IEnumerable<BeatmapMirrorSource> sources,
            IReadOnlyList<BeatmapMirrorMetadata> observations, BeatmapMirrorMetadata? reference, DateTimeOffset now)
        {
            lock (sync)
            {
                return sources.OrderBy(s => s.RequiresAuthentication)
                              .ThenBy(s => samples.TryGetValue(s.Name, out var sample) && sample.RetryAfter > now)
                              .ThenByDescending(s => reference != null && observations.Any(m => m.Source == s.Name && BeatmapMirrorMetadata.SameRevision(m, reference)))
                              .ThenBy(s => samples.TryGetValue(s.Name, out var sample) ? sample.StartSeconds + 4 * 1024 * 1024 / Math.Max(1, sample.BytesPerSecond) : 10)
                              .ToArray();
            }
        }

        internal void Record(string name, long bytes, double seconds, double firstByteSeconds)
        {
            lock (sync)
            {
                if (!samples.TryGetValue(name, out var previous))
                    previous = new Sample(0, 0, DateTimeOffset.MinValue);
                double speed = bytes / Math.Max(0.001, seconds - firstByteSeconds);
                samples[name] = new Sample(previous.BytesPerSecond > 0 ? previous.BytesPerSecond * 0.5 + speed * 0.5 : speed,
                    firstByteSeconds, DateTimeOffset.MinValue);
            }
        }

        internal void BackOff(string name, DateTimeOffset until)
        {
            lock (sync)
            {
                if (!samples.TryGetValue(name, out var sample))
                    sample = new Sample(0, 10, DateTimeOffset.MinValue);
                samples[name] = sample with { RetryAfter = until };
            }
        }

        private sealed record Sample(double BytesPerSecond, double StartSeconds, DateTimeOffset RetryAfter);
    }
}
