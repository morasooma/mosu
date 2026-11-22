// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Statistics;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Performance.Debug;
using osu.Game.Rulesets.Objects.Pooling;

namespace osu.Game.Rulesets.UI
{
    public partial class HitObjectContainer : PooledDrawableWithLifetimeContainer<HitObjectLifetimeEntry, DrawableHitObject>, IHitObjectContainer
    {
        public IEnumerable<DrawableHitObject> Objects => InternalChildren.Cast<DrawableHitObject>().OrderBy(h => h.HitObject.StartTime);

        public IEnumerable<DrawableHitObject> AliveObjects => AliveEntries.Values.OrderBy(h => h.HitObject.StartTime);

        /// <summary>
        /// Copies alive objects in stable start-time order into reusable caller-owned storage.
        /// Avoids LINQ's per-enumeration arrays on high-frequency gameplay paths.
        /// </summary>
        public void CopyAliveObjectsTo(List<DrawableHitObject> destination)
        {
            destination.Clear();
            foreach (var drawable in AliveEntries.Values)
                destination.Add(drawable);

            // Alive sets are small. Stable insertion sort preserves the ordering of equal
            // start times used by AliveObjects without allocating a sorter or index map.
            for (int i = 1; i < destination.Count; i++)
            {
                var current = destination[i];
                int j = i - 1;
                while (j >= 0 && destination[j].HitObject.StartTime.CompareTo(current.HitObject.StartTime) > 0)
                {
                    destination[j + 1] = destination[j];
                    j--;
                }
                destination[j + 1] = current;
            }
        }

        /// <summary>
        /// Invoked when a <see cref="DrawableHitObject"/> is judged.
        /// </summary>
        public event Action<DrawableHitObject, JudgementResult> NewResult;

        /// <summary>
        /// Invoked when a <see cref="HitObject"/> becomes used by a <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <remarks>
        /// If this <see cref="HitObjectContainer"/> uses pooled objects, this represents the time when the <see cref="HitObject"/>s become alive.
        /// </remarks>
        internal event Action<HitObject> HitObjectUsageBegan;

        /// <summary>
        /// Invoked when a <see cref="HitObject"/> becomes unused by a <see cref="DrawableHitObject"/>.
        /// </summary>
        /// <remarks>
        /// If this <see cref="HitObjectContainer"/> uses pooled objects, this represents the time when the <see cref="HitObject"/>s become dead.
        /// </remarks>
        internal event Action<HitObject> HitObjectUsageFinished;

        private readonly Dictionary<DrawableHitObject, IBindable> startTimeMap = new Dictionary<DrawableHitObject, IBindable>();

        private readonly Dictionary<HitObjectLifetimeEntry, DrawableHitObject> nonPooledDrawableMap = new Dictionary<HitObjectLifetimeEntry, DrawableHitObject>();

        [Resolved(CanBeNull = true)]
        private IPooledHitObjectProvider pooledObjectProvider { get; set; }

        public HitObjectContainer()
        {
            RelativeSizeAxes = Axes.Both;
        }

        protected override void LoadAsyncComplete()
        {
            base.LoadAsyncComplete();

            // Application of hitobjects during load() may have changed their start times, so ensure the correct sorting order.
            SortInternal();
        }

        #region Pooling support

        public override bool Remove(HitObjectLifetimeEntry entry)
        {
            if (!base.Remove(entry)) return false;

            // This logic is not in `Remove(DrawableHitObject)` because a non-pooled drawable may be removed by specifying its entry.
            if (nonPooledDrawableMap.Remove(entry, out var drawable))
                removeDrawable(drawable);

            return true;
        }

        protected sealed override DrawableHitObject GetDrawable(HitObjectLifetimeEntry entry)
        {
            if (nonPooledDrawableMap.TryGetValue(entry, out var drawable))
                return drawable;

            using var operation = StutterTraceEventSource.Log.Measure("HitObject.AcquirePooled");
            long started = PerformancePhaseLogger.Enabled ? Stopwatch.GetTimestamp() : 0;
            try
            {
                return pooledObjectProvider?.GetPooledDrawableRepresentation(entry.HitObject, null) ??
                       throw new InvalidOperationException($"A drawable representation could not be retrieved for hitobject type: {entry.HitObject.GetType().ReadableName()}.");
            }
            finally
            {
                if (started != 0)
                {
                    double elapsed = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                    if (elapsed >= 8)
                        Logger.Log($"MOSU_SLOW_HITOBJECT_ACQUIRE elapsed_ms={elapsed:F2} type={entry.HitObject.GetType().Name} "
                                   + $"start_time={entry.HitObject.StartTime:F0}", LoggingTarget.Performance);
                }
            }
        }

        protected override void AddDrawable(HitObjectLifetimeEntry entry, DrawableHitObject drawable)
        {
            if (nonPooledDrawableMap.ContainsKey(entry)) return;

            addDrawable(drawable);
            HitObjectUsageBegan?.Invoke(entry.HitObject);
        }

        protected override void RemoveDrawable(HitObjectLifetimeEntry entry, DrawableHitObject drawable)
        {
            drawable.OnKilled();
            if (nonPooledDrawableMap.ContainsKey(entry)) return;

            removeDrawable(drawable);
            HitObjectUsageFinished?.Invoke(entry.HitObject);
        }

        private void addDrawable(DrawableHitObject drawable)
        {
            drawable.OnNewResult += onNewResult;

            bindStartTime(drawable);
            AddInternal(drawable);
        }

        private void removeDrawable(DrawableHitObject drawable)
        {
            drawable.OnNewResult -= onNewResult;

            unbindStartTime(drawable);

            RemoveInternal(drawable, false);
        }

        #endregion

        #region Non-pooling support

        public virtual void Add(DrawableHitObject drawable)
        {
            if (drawable.Entry == null)
                throw new InvalidOperationException($"May not add a {nameof(DrawableHitObject)} without {nameof(HitObject)} associated");

            nonPooledDrawableMap.Add(drawable.Entry, drawable);
            addDrawable(drawable);
            Add(drawable.Entry);
        }

        public virtual bool Remove(DrawableHitObject drawable)
        {
            if (drawable.Entry == null)
                return false;

            return Remove(drawable.Entry);
        }

        public int IndexOf(DrawableHitObject hitObject) => IndexOfInternal(hitObject);

        #endregion

        private void onNewResult(DrawableHitObject d, JudgementResult r) => NewResult?.Invoke(d, r);

        #region Comparator + StartTime tracking

        private void bindStartTime(DrawableHitObject hitObject)
        {
            var bindable = hitObject.StartTimeBindable.GetBoundCopy();

            bindable.BindValueChanged(_ =>
            {
                if (LoadState >= LoadState.Ready)
                    SortInternal();
            });

            startTimeMap[hitObject] = bindable;
        }

        private void unbindStartTime(DrawableHitObject hitObject)
        {
            startTimeMap[hitObject].UnbindAll();
            startTimeMap.Remove(hitObject);
        }

        private void unbindAllStartTimes()
        {
            foreach (var kvp in startTimeMap)
                kvp.Value.UnbindAll();
            startTimeMap.Clear();
        }

        protected override int Compare(Drawable x, Drawable y)
        {
            if (!(x is DrawableHitObject xObj) || !(y is DrawableHitObject yObj))
                return base.Compare(x, y);

            // Put earlier hitobjects towards the end of the list, so they handle input first
            int i = yObj.HitObject.StartTime.CompareTo(xObj.HitObject.StartTime);
            return i == 0 ? CompareReverseChildID(x, y) : i;
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            unbindAllStartTimes();
        }
    }
}
