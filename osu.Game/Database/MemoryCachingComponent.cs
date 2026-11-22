// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Statistics;

namespace osu.Game.Database
{
    /// <summary>
    /// A component which performs lookups (or calculations) and caches the results.
    /// Currently not persisted between game sessions.
    /// </summary>
    public abstract partial class MemoryCachingComponent<TLookup, TValue> : Component
        where TLookup : notnull
    {
        private readonly ConcurrentDictionary<TLookup, TValue?> cache = new ConcurrentDictionary<TLookup, TValue?>();
        private readonly object cacheOrderLock = new object();
        private readonly LinkedList<TLookup> cacheOrder = new LinkedList<TLookup>();
        private readonly Dictionary<TLookup, LinkedListNode<TLookup>> cacheNodes = new Dictionary<TLookup, LinkedListNode<TLookup>>();
        // Only in-flight requests are tracked; completed keys do not retain generation tombstones.
        private readonly HashSet<CacheRequest> activeRequests = new HashSet<CacheRequest>();

        protected sealed class CacheRequest
        {
            internal readonly TLookup Lookup;
            internal volatile bool Invalidated;

            internal CacheRequest(TLookup lookup) => Lookup = lookup;
        }

        protected CacheRequest BeginCacheRequest(TLookup lookup)
        {
            lock (cacheOrderLock)
            {
                var request = new CacheRequest(lookup);
                activeRequests.Add(request);
                return request;
            }
        }

        protected void EndCacheRequest(CacheRequest request)
        {
            lock (cacheOrderLock)
                activeRequests.Remove(request);
        }

        protected bool IsCacheRequestValid(CacheRequest request, CancellationToken cancellationToken)
        {
            lock (cacheOrderLock)
                return !request.Invalidated && !cancellationToken.IsCancellationRequested;
        }

        protected void WithCacheLock(Action action)
        {
            lock (cacheOrderLock)
                action();
        }

        protected bool PublishValue(CacheRequest request, TValue? value, CancellationToken cancellationToken)
        {
            lock (cacheOrderLock)
            {
                if (request.Invalidated || cancellationToken.IsCancellationRequested)
                    return false;

                if (value != null || CacheNullValues)
                {
                    OnValueComputed(request.Lookup, value);
                    StoreValue(request.Lookup, value);
                }

                return true;
            }
        }

        private readonly GlobalStatistic<MemoryCachingStatistics> statistics;

        protected virtual bool CacheNullValues => true;

        /// <summary>
        /// Optional bound for caches holding large computed values. Defaults to unlimited for existing consumers.
        /// </summary>
        protected virtual int MaximumCacheEntries => int.MaxValue;

        protected int CachedEntryCount
        {
            get { lock (cacheOrderLock) return cache.Count; }
        }

        protected MemoryCachingComponent()
        {
            statistics = GlobalStatistics.Get<MemoryCachingStatistics>(nameof(MemoryCachingComponent<,>), GetType().ReadableName());
            statistics.Value = new MemoryCachingStatistics();
        }

        /// <summary>
        /// Retrieve the cached value for the given lookup.
        /// </summary>
        /// <param name="lookup">The lookup to retrieve.</param>
        /// <param name="cancellationToken">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
        /// <param name="computationDelay">In the case a cached lookup was not possible, a value in milliseconds of to wait until performing potentially intensive lookup.</param>
        protected async Task<TValue?> GetAsync(TLookup lookup, CancellationToken cancellationToken = default, int computationDelay = 0)
        {
            if (CheckExists(lookup, out TValue? existing))
            {
                statistics.Value.HitCount++;
                if (MaximumCacheEntries < int.MaxValue)
                {
                    lock (cacheOrderLock)
                    {
                        if (cacheNodes.TryGetValue(lookup, out LinkedListNode<TLookup>? node))
                        {
                            cacheOrder.Remove(node);
                            cacheOrder.AddLast(node);
                        }
                    }
                }

                return existing;
            }

            var request = BeginCacheRequest(lookup);
            try
            {
                if (computationDelay > 0)
                    await Task.Delay(computationDelay, cancellationToken).ConfigureAwait(false);

                var computed = await ComputeValueAsync(lookup, cancellationToken).ConfigureAwait(false);
                statistics.Value.MissCount++;
                // A canceled owner may still deliver its result to an independent waiter; it must not cache it.
                // Invalidation, unlike cancellation, makes the old result unusable.
                return PublishValue(request, computed, cancellationToken) || !request.Invalidated ? computed : default;
            }
            finally
            {
                EndCacheRequest(request);
            }
        }

        /// <summary>
        /// Stores a value produced by an alternate lookup path in this cache.
        /// </summary>
        protected void StoreValue(TLookup lookup, TValue? value)
        {
            lock (cacheOrderLock)
            {
                cache[lookup] = value;
                if (MaximumCacheEntries < int.MaxValue)
                {
                    if (cacheNodes.Remove(lookup, out LinkedListNode<TLookup>? previousNode))
                        cacheOrder.Remove(previousNode);

                    cacheNodes[lookup] = cacheOrder.AddLast(lookup);

                    while (cacheNodes.Count > MaximumCacheEntries)
                    {
                        TLookup oldest = cacheOrder.First!.Value;
                        cacheOrder.RemoveFirst();
                        cacheNodes.Remove(oldest);
                        cache.TryRemove(oldest, out _);
                    }
                }

                statistics.Value.Usage = cache.Count;
            }
        }

        /// <summary>
        /// Invalidate all entries matching a provided predicate.
        /// </summary>
        /// <param name="matchKeyPredicate">The predicate to decide which keys should be invalidated.</param>
        protected void Invalidate(Func<TLookup, bool> matchKeyPredicate)
        {
            lock (cacheOrderLock)
            {
                foreach (var request in activeRequests)
                {
                    if (matchKeyPredicate(request.Lookup))
                        request.Invalidated = true;
                }

                foreach (var kvp in cache)
                {
                    if (!matchKeyPredicate(kvp.Key))
                        continue;

                    cache.TryRemove(kvp.Key, out _);
                    if (cacheNodes.Remove(kvp.Key, out LinkedListNode<TLookup>? node))
                        cacheOrder.Remove(node);
                }

                statistics.Value.Usage = cache.Count;
            }
        }

        /// <summary>
        /// Completely purge the cache.
        /// </summary>
        public virtual void Clear()
        {
            lock (cacheOrderLock)
            {
                foreach (var request in activeRequests)
                    request.Invalidated = true;
                cache.Clear();
                cacheOrder.Clear();
                cacheNodes.Clear();
                statistics.Value.Usage = 0;
            }
        }

        /// <summary>
        /// Called after a successful lookup to allow lightweight result projections without retaining the computed graph.
        /// </summary>
        protected virtual void OnValueComputed(TLookup lookup, TValue? value)
        {
        }

        protected bool CheckExists(TLookup lookup, [MaybeNullWhen(false)] out TValue value) =>
            cache.TryGetValue(lookup, out value);

        /// <summary>
        /// Called on cache miss to compute the value for the specified lookup.
        /// </summary>
        /// <param name="lookup">The lookup to retrieve.</param>
        /// <param name="token">An optional <see cref="CancellationToken"/> to cancel the operation.</param>
        /// <returns>The computed value.</returns>
        protected abstract Task<TValue?> ComputeValueAsync(TLookup lookup, CancellationToken token = default);

        private class MemoryCachingStatistics
        {
            /// <summary>
            /// Total number of cache hits.
            /// </summary>
            public int HitCount;

            /// <summary>
            /// Total number of cache misses.
            /// </summary>
            public int MissCount;

            /// <summary>
            /// Total number of cached entities.
            /// </summary>
            public int Usage;

            public override string ToString()
            {
                int totalAccesses = HitCount + MissCount;
                double hitRate = totalAccesses == 0 ? 0 : (double)HitCount / totalAccesses;

                return $"i:{Usage} h:{HitCount} m:{MissCount} {hitRate:0%}";
            }
        }
    }
}
