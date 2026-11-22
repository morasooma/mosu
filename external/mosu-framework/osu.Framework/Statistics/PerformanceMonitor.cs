// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Microsoft.Extensions.ObjectPool;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Utils;
using osu.Framework.Threading;
using osu.Framework.Timing;

namespace osu.Framework.Statistics
{
    internal class PerformanceMonitor : IDisposable
    {
        private readonly StopwatchClock ourClock = new StopwatchClock(true);

        private readonly Stack<PerformanceCollectionType> currentCollectionTypeStack = new Stack<PerformanceCollectionType>();

        private readonly InvokeOnDisposal[] endCollectionDelegates = new InvokeOnDisposal[FrameStatistics.NUM_PERFORMANCE_COLLECTION_TYPES];

        private BackgroundStackTraceCollector traceCollector;

        private FrameStatistics currentFrame;
        private double latestSchedulerMs;
        private double latestWorkMs;
        private double latestSleepMs;
        private double latestGcMs;
        private long latestInvalidations;
        private long latestRefreshes;
        private long latestDrawNodeCtor;
        private long latestDrawNodeAppl;
        private long latestScheduleInvk;
        private long latestInputQueue;
        private long latestPositionalIQ;
        private long latestCCL;
        private string latestCclTop1Source;
        private long latestCclTop1Count;
        private string latestCclTop2Source;
        private long latestCclTop2Count;
        private string latestCclTop3Source;
        private long latestCclTop3Count;
        private string latestInvalTop1Source;
        private long latestInvalTop1Count;
        private string latestInvalTop2Source;
        private long latestInvalTop2Count;
        private string latestInvalTop3Source;
        private long latestInvalTop3Count;
        private long latestVBufBinds;
        private long latestVBufOverflow;
        private long latestTextureBinds;
        private long latestFBORedraw;
        private long latestDrawCalls;
        private long latestShaderBinds;
        private long latestVerticesDraw;
        private long latestVerticesUpl;
        private long latestUniformUpl;
        private long latestPixels;
        private long latestBatchFlushes;
        private long latestPipelineBinds;
        private long latestResourceSetBinds;
        private long latestResourceSetCreates;
        private long latestTextureUploadFlushes;
        private long latestTextureUploads;
        private long latestDeferredEventProcessUs;
        private long latestDeferredVertexWriteUs;
        private long latestDeferredVertexCommitUs;
        private long latestDeferredVertexDrawUs;
        private long latestDeferredVertexMapCount;
        private long latestDeferredVertexUnmapCount;
        private long latestDeferredVertexBytes;
        private long latestVeldridSwapBuffersUs;
        private long latestVeldridTextureUploadUs;
        private long latestPipelineCreates;
        private long latestVeldridVertexSetUs;
        private long latestVeldridVertexUpdateUs;
        private long latestVeldridVertexDrawUs;
        private long latestVeldridVertexBytes;

        private const int max_pending_frames = 10;

        private readonly string threadName;

        internal readonly ConcurrentQueue<FrameStatistics> PendingFrames = new ConcurrentQueue<FrameStatistics>();

        internal readonly ObjectPool<FrameStatistics> FramesPool =
            new DefaultObjectPoolProvider { MaximumRetained = max_pending_frames }
                .Create(new DefaultPooledObjectPolicy<FrameStatistics>());

        internal bool[] ActiveCounters { get; } = new bool[FrameStatistics.NUM_STATISTICS_COUNTER_TYPES];

        private bool enablePerformanceProfiling;

        public bool EnablePerformanceProfiling
        {
            set
            {
                enablePerformanceProfiling = value;
                updateEnabledState();
            }
        }

        private double consumptionTime;
        private double consumptionGCTotalPauseDuration;

        private readonly IBindable<bool> isActive;

        internal readonly ThrottledFrameClock Clock;

        private Thread thread;

        private readonly FreezeDetector freezeDetector;

        public double FrameAimTime => 1000.0 / (Clock?.MaximumUpdateHz > 0 ? Clock.MaximumUpdateHz : double.MaxValue);

        internal ThreadFramePerformanceSnapshot LatestFramePerformance =>
            new ThreadFramePerformanceSnapshot(latestSchedulerMs, latestWorkMs, latestSleepMs, latestGcMs, latestInvalidations, latestRefreshes, latestDrawNodeCtor, latestDrawNodeAppl, latestScheduleInvk, latestInputQueue, latestPositionalIQ, latestCCL, latestCclTop1Source, latestCclTop1Count, latestCclTop2Source, latestCclTop2Count, latestCclTop3Source, latestCclTop3Count, latestInvalTop1Source, latestInvalTop1Count, latestInvalTop2Source, latestInvalTop2Count, latestInvalTop3Source, latestInvalTop3Count, latestVBufBinds, latestVBufOverflow, latestTextureBinds, latestFBORedraw, latestDrawCalls, latestShaderBinds, latestVerticesDraw, latestVerticesUpl, latestUniformUpl, latestPixels, latestBatchFlushes, latestPipelineBinds, latestResourceSetBinds, latestResourceSetCreates, latestTextureUploadFlushes, latestTextureUploads, latestDeferredEventProcessUs, latestDeferredVertexWriteUs, latestDeferredVertexCommitUs, latestDeferredVertexDrawUs, latestDeferredVertexMapCount, latestDeferredVertexUnmapCount, latestDeferredVertexBytes, latestVeldridSwapBuffersUs, latestVeldridTextureUploadUs, latestPipelineCreates, latestVeldridVertexSetUs, latestVeldridVertexUpdateUs, latestVeldridVertexDrawUs, latestVeldridVertexBytes);

        internal PerformanceMonitor(GameThread thread, IEnumerable<StatisticsCounterType> counters)
        {
            Clock = thread.Clock;
            threadName = thread.Name;
            freezeDetector = new FreezeDetector(threadName);

            isActive = thread.IsActive.GetBoundCopy();
            isActive.BindValueChanged(_ => updateEnabledState());

            currentFrame = FramesPool.Get();

            foreach (var c in counters)
                ActiveCounters[(int)c] = true;

            for (int i = 0; i < FrameStatistics.NUM_PERFORMANCE_COLLECTION_TYPES; i++)
            {
                var t = (PerformanceCollectionType)i;
                endCollectionDelegates[i] = new InvokeOnDisposal(() => endCollecting(t));
            }
        }

        /// <summary>
        /// Switch target thread to <see cref="Thread.CurrentThread"/>.
        /// </summary>
        public void MakeCurrent()
        {
            var current = Thread.CurrentThread;

            if (current == thread)
                return;

            thread = Thread.CurrentThread;

            traceCollector?.Dispose();
            traceCollector = new BackgroundStackTraceCollector(thread, ourClock, threadName);
            updateEnabledState();
        }

        /// <summary>
        /// Start collecting a type of passing time.
        /// </summary>
        public InvokeOnDisposal BeginCollecting(PerformanceCollectionType type)
        {
            // Consume time, regardless of whether we are using it at this point.
            // If not, an `EndCollecting` call may end up reporting more time than actually passed between
            // the Begin-End pair.
            (double workMs, double pauseMs) = consumeStopwatchElapsedTime();

            if (currentCollectionTypeStack.Count > 0)
            {
                PerformanceCollectionType t = currentCollectionTypeStack.Peek();

                currentFrame.CollectedTimes.TryAdd(t, 0);
                currentFrame.CollectedTimes[t] += workMs;

                currentFrame.CollectedTimes.TryAdd(PerformanceCollectionType.GC, 0);
                currentFrame.CollectedTimes[PerformanceCollectionType.GC] += pauseMs;
            }

            currentCollectionTypeStack.Push(type);

            return endCollectionDelegates[(int)type];
        }

        /// <summary>
        /// End collecting a type of passing time (that was previously started).
        /// </summary>
        /// <param name="type"></param>
        private void endCollecting(PerformanceCollectionType type)
        {
            currentCollectionTypeStack.Pop();

            (double workMs, double pauseMs) = consumeStopwatchElapsedTime();

            currentFrame.CollectedTimes.TryAdd(type, 0);
            currentFrame.CollectedTimes[type] += workMs;

            currentFrame.CollectedTimes.TryAdd(PerformanceCollectionType.GC, 0);
            currentFrame.CollectedTimes[PerformanceCollectionType.GC] += pauseMs;
        }

        private readonly int[] lastAmountGarbageCollects = new int[3];

        public bool HandleGC;

        private readonly Dictionary<StatisticsCounterType, GlobalStatistic<long>> globalStatistics = new Dictionary<StatisticsCounterType, GlobalStatistic<long>>();

        /// <summary>
        /// Resets all frame statistics. Run exactly once per frame.
        /// </summary>
        public void NewFrame()
        {
            // Reset the counters we keep track of
            for (int i = 0; i < ActiveCounters.Length; ++i)
            {
                if (ActiveCounters[i])
                {
                    long count = FrameStatistics.COUNTERS[i];
                    var type = (StatisticsCounterType)i;

                    if (!globalStatistics.TryGetValue(type, out var global))
                        globalStatistics[type] = global = GlobalStatistics.Get<long>(threadName, type.ToString());

                    global.Value = count;
                    currentFrame.Counts[type] = count;
                    updateLatestCounter(type, count);
                    currentFrame.FramesPerSecond = Clock.FramesPerSecond;
                    currentFrame.Jitter = Clock.Jitter;

                    FrameStatistics.COUNTERS[i] = 0;
                }
            }

            FrameStatistics.FlushChildLifeSources(currentFrame.ChildLifeSources);
            updateLatestChildLifeSources(currentFrame.ChildLifeSources);

            FrameStatistics.FlushInvalidationSources(currentFrame.InvalidationSources);
            updateLatestInvalidationSources(currentFrame.InvalidationSources);

            if (PendingFrames.Count < max_pending_frames - 1)
            {
                PendingFrames.Enqueue(currentFrame);
                currentFrame = FramesPool.Get();
            }

            currentFrame.Clear();

            if (HandleGC)
            {
                for (int i = 0; i < lastAmountGarbageCollects.Length; ++i)
                {
                    int amountCollections = GC.CollectionCount(i);

                    if (lastAmountGarbageCollects[i] != amountCollections)
                    {
                        lastAmountGarbageCollects[i] = amountCollections;
                        currentFrame.GarbageCollections.Add(i);
                    }
                }
            }

            double dampRate = Math.Max(Clock.ElapsedFrameTime, 0) / 1000;
            averageFrameTime = Interpolation.Damp(averageFrameTime, Clock.ElapsedFrameTime, 0.01, dampRate);

            //check for dropped (stutter) frames
            traceCollector?.NewFrame(Clock.ElapsedFrameTime, Math.Max(10, Math.Max(1000 / Clock.MaximumUpdateHz, averageFrameTime) * 4));

            freezeDetector?.NewFrame(Clock.ElapsedFrameTime);

            consumeStopwatchElapsedTime();
        }

        public void EndFrame()
        {
            updateLatestFramePerformance();
            traceCollector?.EndFrame();
        }

        private void updateLatestFramePerformance()
        {
            latestSchedulerMs = getCollectedTime(PerformanceCollectionType.Scheduler);
            latestWorkMs = getCollectedTime(PerformanceCollectionType.Work);
            latestSleepMs = getCollectedTime(PerformanceCollectionType.Sleep);
            latestGcMs = getCollectedTime(PerformanceCollectionType.GC);
        }

        private double getCollectedTime(PerformanceCollectionType type)
            => currentFrame.CollectedTimes.TryGetValue(type, out double value) ? value : 0;

        private void updateLatestCounter(StatisticsCounterType type, long count)
        {
            switch (type)
            {
                case StatisticsCounterType.Invalidations:
                    latestInvalidations = count;
                    break;

                case StatisticsCounterType.Refreshes:
                    latestRefreshes = count;
                    break;

                case StatisticsCounterType.DrawNodeCtor:
                    latestDrawNodeCtor = count;
                    break;

                case StatisticsCounterType.DrawNodeAppl:
                    latestDrawNodeAppl = count;
                    break;

                case StatisticsCounterType.ScheduleInvk:
                    latestScheduleInvk = count;
                    break;

                case StatisticsCounterType.InputQueue:
                    latestInputQueue = count;
                    break;

                case StatisticsCounterType.PositionalIQ:
                    latestPositionalIQ = count;
                    break;

                case StatisticsCounterType.CCL:
                    latestCCL = count;
                    break;

                case StatisticsCounterType.VBufBinds:
                    latestVBufBinds = count;
                    break;

                case StatisticsCounterType.VBufOverflow:
                    latestVBufOverflow = count;
                    break;

                case StatisticsCounterType.TextureBinds:
                    latestTextureBinds = count;
                    break;

                case StatisticsCounterType.FBORedraw:
                    latestFBORedraw = count;
                    break;

                case StatisticsCounterType.DrawCalls:
                    latestDrawCalls = count;
                    break;

                case StatisticsCounterType.ShaderBinds:
                    latestShaderBinds = count;
                    break;

                case StatisticsCounterType.VerticesDraw:
                    latestVerticesDraw = count;
                    break;

                case StatisticsCounterType.VerticesUpl:
                    latestVerticesUpl = count;
                    break;

                case StatisticsCounterType.UniformUpl:
                    latestUniformUpl = count;
                    break;

                case StatisticsCounterType.Pixels:
                    latestPixels = count;
                    break;

                case StatisticsCounterType.BatchFlushes:
                    latestBatchFlushes = count;
                    break;

                case StatisticsCounterType.PipelineBinds:
                    latestPipelineBinds = count;
                    break;

                case StatisticsCounterType.ResourceSetBinds:
                    latestResourceSetBinds = count;
                    break;

                case StatisticsCounterType.ResourceSetCreates:
                    latestResourceSetCreates = count;
                    break;

                case StatisticsCounterType.TextureUploadFlushes:
                    latestTextureUploadFlushes = count;
                    break;

                case StatisticsCounterType.TextureUploads:
                    latestTextureUploads = count;
                    break;

                case StatisticsCounterType.DeferredEventProcessUs:
                    latestDeferredEventProcessUs = count;
                    break;

                case StatisticsCounterType.DeferredVertexWriteUs:
                    latestDeferredVertexWriteUs = count;
                    break;

                case StatisticsCounterType.DeferredVertexCommitUs:
                    latestDeferredVertexCommitUs = count;
                    break;

                case StatisticsCounterType.DeferredVertexDrawUs:
                    latestDeferredVertexDrawUs = count;
                    break;

                case StatisticsCounterType.DeferredVertexMapCount:
                    latestDeferredVertexMapCount = count;
                    break;

                case StatisticsCounterType.DeferredVertexUnmapCount:
                    latestDeferredVertexUnmapCount = count;
                    break;

                case StatisticsCounterType.DeferredVertexBytes:
                    latestDeferredVertexBytes = count;
                    break;

                case StatisticsCounterType.VeldridSwapBuffersUs:
                    latestVeldridSwapBuffersUs = count;
                    break;

                case StatisticsCounterType.VeldridTextureUploadUs:
                    latestVeldridTextureUploadUs = count;
                    break;

                case StatisticsCounterType.PipelineCreates:
                    latestPipelineCreates = count;
                    break;

                case StatisticsCounterType.VeldridVertexSetUs:
                    latestVeldridVertexSetUs = count;
                    break;

                case StatisticsCounterType.VeldridVertexUpdateUs:
                    latestVeldridVertexUpdateUs = count;
                    break;

                case StatisticsCounterType.VeldridVertexDrawUs:
                    latestVeldridVertexDrawUs = count;
                    break;

                case StatisticsCounterType.VeldridVertexBytes:
                    latestVeldridVertexBytes = count;
                    break;
            }
        }

        private void updateLatestChildLifeSources(Dictionary<string, long> sources)
            => updateLatestTopSources(sources, ref latestCclTop1Source, ref latestCclTop1Count, ref latestCclTop2Source, ref latestCclTop2Count, ref latestCclTop3Source, ref latestCclTop3Count);

        private void updateLatestInvalidationSources(Dictionary<string, long> sources)
            => updateLatestTopSources(sources, ref latestInvalTop1Source, ref latestInvalTop1Count, ref latestInvalTop2Source, ref latestInvalTop2Count, ref latestInvalTop3Source, ref latestInvalTop3Count);

        private static void updateLatestTopSources(Dictionary<string, long> sources, ref string? top1Source, ref long top1Count, ref string? top2Source, ref long top2Count, ref string? top3Source, ref long top3Count)
        {
            top1Source = top2Source = top3Source = null;
            top1Count = top2Count = top3Count = 0;

            foreach (var pair in sources)
            {
                if (pair.Value > top1Count)
                {
                    top3Source = top2Source;
                    top3Count = top2Count;
                    top2Source = top1Source;
                    top2Count = top1Count;
                    top1Source = pair.Key;
                    top1Count = pair.Value;
                }
                else if (pair.Value > top2Count)
                {
                    top3Source = top2Source;
                    top3Count = top2Count;
                    top2Source = pair.Key;
                    top2Count = pair.Value;
                }
                else if (pair.Value > top3Count)
                {
                    top3Source = pair.Key;
                    top3Count = pair.Value;
                }
            }
        }

        private void updateEnabledState()
        {
            traceCollector?.Enabled = enablePerformanceProfiling && isActive.Value;
        }

        private double averageFrameTime;

        private (double workMs, double pauseMs) consumeStopwatchElapsedTime()
        {
            double lastConsumptionTime = consumptionTime;
            consumptionTime = ourClock.CurrentTime;

            traceCollector?.LastConsumptionTime = consumptionTime;

            double lastGCTotalPauseDuration = consumptionGCTotalPauseDuration;
            consumptionGCTotalPauseDuration = GC.GetTotalPauseDuration().TotalMilliseconds;

            double pauseMs = consumptionGCTotalPauseDuration - lastGCTotalPauseDuration;
            double workMs = consumptionTime - lastConsumptionTime - pauseMs;

            return (workMs, pauseMs);
        }

        internal double FramesPerSecond => Clock.FramesPerSecond;

        #region IDisposable Support

        private bool isDisposed;

        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                isDisposed = true;
                traceCollector?.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
