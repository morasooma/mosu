using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// One mod generation. Only visible/selected maps enter the exact-calculation queue;
    /// the rest of the library uses metadata estimates, without decoding every file.
    /// </summary>
    internal sealed class SongSelectRatingWork : IDisposable
    {
        private readonly object sync = new object();
        private readonly Queue<BeatmapInfo> pending = new Queue<BeatmapInfo>();
        private readonly HashSet<(Guid, string)> claimed = new HashSet<(Guid, string)>();
        private readonly Dictionary<Guid, SongSelectRatingSnapshot.Value> values = new Dictionary<Guid, SongSelectRatingSnapshot.Value>();
        private readonly SemaphoreSlim available = new SemaphoreSlim(0);
        private readonly BeatmapDifficultyCache cache;
        private readonly RulesetInfo ruleset;
        private readonly Mod[] mods;
        private readonly bool performance;
        private readonly CancellationTokenSource stop = new CancellationTokenSource();
        private readonly Action changed;
        private bool disposed;
        private bool started;
        public readonly string Key;
        public Task Completion { get; private set; } = Task.CompletedTask;

        public SongSelectRatingWork(BeatmapDifficultyCache cache, RulesetInfo ruleset, Mod[] mods, bool performance, string key, Action changed)
        {
            this.cache = cache;
            this.ruleset = ruleset;
            this.mods = mods.OrderBy(m => m.Acronym).Select(m => m.DeepClone()).ToArray();
            this.performance = performance;
            this.changed = changed;
            Key = key;
        }

        // A replacement generation waits for cancellation of its predecessor. Rapid mod
        // changes cannot accumulate independent sets of CPU-bound workers.
        public void Start(Task? previous = null)
        {
            lock (sync)
            {
                if (started || disposed) throw new InvalidOperationException("Rating work can only be started once");
                started = true;
                Completion = start(previous ?? Task.CompletedTask);
            }
        }

        private async Task start(Task previous)
        {
            try
            {
                await previous.ConfigureAwait(false);
                if (!stop.IsCancellationRequested)
                    await Task.WhenAll(Enumerable.Range(0, Math.Min(2, Math.Max(1, Environment.ProcessorCount - 2))).Select(_ => run())).ConfigureAwait(false);
            }
            finally
            {
                lock (sync)
                {
                    disposed = true;
                    available.Dispose();
                    stop.Dispose();
                }
            }
        }

        public SongSelectRatingSnapshot Snapshot()
        {
            lock (sync)
                return new SongSelectRatingSnapshot(ruleset, mods, values);
        }

        public void Promote(IEnumerable<BeatmapInfo> maps)
        {
            lock (sync)
            {
                if (disposed) return;
                // Replace waiting work after scrolling; do not calculate cards which have
                // long since left the viewport. Two already-running calculations may finish.
                while (pending.TryDequeue(out var old))
                    claimed.Remove((old.ID, old.MD5Hash));
                foreach (var map in maps)
                {
                    if (map.Hidden || map.IsExternalCatalogue || !map.AllowGameplayWithRuleset(ruleset, true)) continue;
                    if (values.TryGetValue(map.ID, out var value) && value.Checksum == map.MD5Hash && (!performance || value.Pp.HasValue)) continue;
                    if (claimed.Add((map.ID, map.MD5Hash))) pending.Enqueue(map);
                }
                if (pending.Count > 0 && available.CurrentCount == 0) available.Release();
            }
        }

        public void Accept(BeatmapDifficultyCache.DifficultyCacheLookup lookup, StarDifficulty result)
        {
            if (!lookup.Ruleset.Equals(ruleset) || !lookup.OrderedMods.SequenceEqual(mods) || !double.IsFinite(result.Stars)) return;
            double? pp = result.PerformanceAttributes?.Total;
            if (pp.HasValue && !double.IsFinite(pp.Value)) pp = null;
            lock (sync)
            {
                if (disposed) return;
                if (values.TryGetValue(lookup.BeatmapInfo.ID, out var old) && old.Checksum == lookup.BeatmapInfo.MD5Hash)
                {
                    if (old.Pp.HasValue && !pp.HasValue) return;
                    if (old.Stars == result.Stars && old.Pp == pp) return;
                }
                values[lookup.BeatmapInfo.ID] = new SongSelectRatingSnapshot.Value(lookup.BeatmapInfo.MD5Hash, result.Stars, pp);
            }
            changed();
        }

        private async Task run()
        {
            try
            {
                await Task.Delay(350, stop.Token).ConfigureAwait(false);
                while (true)
                {
                    await available.WaitAsync(stop.Token).ConfigureAwait(false);
                    BeatmapInfo? map = null;
                    lock (sync)
                    {
                        if (pending.TryDequeue(out map) && pending.Count > 0) available.Release();
                    }
                    if (map == null) continue;
                    var result = await cache.GetSongSelectDifficultyAsync(map, ruleset, mods, performance, stop.Token).ConfigureAwait(false);
                    if (result != null)
                        Accept(new BeatmapDifficultyCache.DifficultyCacheLookup(map, ruleset, mods, performance), result.Value);
                    // A corrupt/unsupported map is not retried every promotion tick.
                    // A new checksum/mod generation may try again.
                    await Task.Delay(20, stop.Token).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { osu.Framework.Logging.Logger.Error(e, "Song-select rating worker failed"); }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (disposed) return;
                disposed = true;
                pending.Clear(); values.Clear(); claimed.Clear();
                stop.Cancel();
                if (!started)
                {
                    available.Dispose();
                    stop.Dispose();
                }
            }
        }
    }
}
