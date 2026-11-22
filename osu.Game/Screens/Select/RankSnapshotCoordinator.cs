using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Scoring;
using Realms;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// One Realm transaction listener and one worker snapshot pass for all active rank panels in a database.
    /// Scoped to the RealmAccess lifetime; no pooled panels are retained after deactivation or disposal.
    /// </summary>
    internal sealed class RankSnapshotCoordinator
    {
        private static readonly ConditionalWeakTable<RealmAccess, RankSnapshotCoordinator> coordinators = new ConditionalWeakTable<RealmAccess, RankSnapshotCoordinator>();

        internal static RankSnapshotCoordinator For(RealmAccess realm) => coordinators.GetValue(realm, r => new RankSnapshotCoordinator(r));

        private readonly RealmAccess realm;
        private readonly HashSet<PanelLocalRankDisplay> panels = new HashSet<PanelLocalRankDisplay>();
        private IDisposable? listener;
        private bool dirty;
        private bool scheduled;
        private bool inFlight;
        private bool waitingForInvalidation;
        private PanelLocalRankDisplay? scheduledOn;
        private long? scheduledAt;
        private int version;
        private int scheduledGeneration;

        internal int WorkerPassCount { get; private set; }
        internal int WorkerQueryCount { get; private set; }
        internal bool IsIdle => (!dirty || waitingForInvalidation) && !inFlight && !scheduled;
        internal PanelLocalRankDisplay? ScheduledOnForTesting => scheduledOn;
        internal long? ScheduledAtForTesting => scheduledAt;

        // Instance-scoped worker gate for deterministic Realm recycle tests; never set at runtime.
        internal Action? BeforeQueryForTesting { get; set; }

        private RankSnapshotCoordinator(RealmAccess realm) => this.realm = realm;

        internal void AddOrUpdate(PanelLocalRankDisplay panel)
        {
            panels.Add(panel);
            if (listener == null)
            {
                listener = realm.RegisterCustomSubscription(r =>
                {
                    r.RealmChanged += onChanged;
                    invalidate(); // after Realm instance rebind, missed commits must be re-read.
                    return new InvokeOnDisposal(() => r.RealmChanged -= onChanged);
                });
            }

            invalidate();
        }

        internal void Remove(PanelLocalRankDisplay panel)
        {
            if (!panels.Remove(panel))
                return;

            version++;
            if (panels.Count != 0)
            {
                if (scheduled && scheduledOn == panel)
                {
                    // Transfer the original deadline: repeated pool turnover must not defer the pass.
                    scheduledGeneration++;
                    scheduled = false;
                    scheduledOn = null;
                    schedule();
                }
                return;
            }

            listener?.Dispose();
            listener = null;
            dirty = false;
            waitingForInvalidation = false;
            scheduled = false;
            scheduledOn = null;
            scheduledAt = null;
            scheduledGeneration++;
            // A scheduled callback may still run; its generation prevents touching a new batch.
        }

        private void onChanged(object? sender, EventArgs args) => invalidate();

        private void invalidate()
        {
            if (panels.Count == 0)
                return;

            version++;
            dirty = true;
            waitingForInvalidation = false;
            schedule();
        }

        private void schedule()
        {
            if (scheduled || inFlight || !dirty || waitingForInvalidation || panels.Count == 0)
                return;

            scheduled = true;
            int generation = ++scheduledGeneration;
            // Neither writes nor anchor turnover restart the original debounce deadline.
            scheduledAt ??= Stopwatch.GetTimestamp();
            double remainingMs = Math.Max(0, 50 - Stopwatch.GetElapsedTime(scheduledAt.Value).TotalMilliseconds);
            (scheduledOn = panels.First()).ScheduleSnapshotDelayed(() =>
            {
                if (generation == scheduledGeneration)
                    run();
            }, remainingMs);
        }

        private void run()
        {
            scheduled = false;
            scheduledOn = null;
            scheduledAt = null;
            if (inFlight || !dirty || panels.Count == 0)
                return;

            var keys = panels.Where(p => p.ActiveHash != null)
                             .Select(p => new Key(p.ActiveHash!, p.RulesetName, p.UserId))
                             .Distinct().ToArray();
            if (keys.Length == 0)
                return;

            int requestedVersion = version;
            dirty = false;
            inFlight = true;
            WorkerPassCount++;
            WorkerQueryCount += keys.Length;
            Task<Dictionary<Key, ScoreRank?>> task;
            try
            {
                task = realm.RunAsync(r =>
                {
                    BeforeQueryForTesting?.Invoke();
                    var ranks = new Dictionary<Key, ScoreRank?>();
                    foreach (var key in keys)
                    {
                        // BeatmapHash is indexed; only the scores of active unique hashes are materialised.
                        var top = r.All<ScoreInfo>().Where(s => s.BeatmapHash == key.Hash && !s.DeletePending)
                                   .ToArray()
                                   .Where(s => s.UserID == key.UserId || s.UserID <= 1)
                                   .Where(s => s.Ruleset.ShortName == key.RulesetName)
                                   .MaxBy(s => (s.TotalScore, -s.Date.UtcDateTime.Ticks));
                        ranks.Add(key, top?.Rank);
                    }
                    return ranks;
                });
            }
            catch (ObjectDisposedException e)
            {
                inFlight = false;
                if (panels.Count > 0)
                {
                    Logger.Error(e, "Could not start local rank snapshot; waiting for the next Realm invalidation");
                    dirty = true;
                    waitingForInvalidation = true;
                }
                return;
            }

            _ = task.ContinueWith(completed => realm.PostToUpdateThread(() =>
            {
                inFlight = false;
                if (completed.IsCompletedSuccessfully && requestedVersion == version)
                {
                    var results = completed.GetResultSafely();
                    foreach (var panel in panels)
                    {
                        if (panel.ActiveHash is string hash)
                            panel.ApplySnapshot(results[new Key(hash, panel.RulesetName, panel.UserId)], panel.Generation);
                    }
                }
                else if (panels.Count > 0)
                {
                    dirty = true;
                    if (requestedVersion == version)
                    {
                        // A persistent Realm fault must not spin every 50 ms. A new commit or rebind
                        // clears this gate and retries; an invalidation during this pass also retries.
                        waitingForInvalidation = true;
                        if (completed.IsFaulted)
                            Logger.Error(completed.Exception!, "Could not refresh local ranks; waiting for the next Realm invalidation");
                    }
                }

                schedule();
            }));
        }

        private readonly record struct Key(string Hash, string RulesetName, int UserId);
    }
}
