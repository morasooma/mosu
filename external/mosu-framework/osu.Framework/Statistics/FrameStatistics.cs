// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;

namespace osu.Framework.Statistics
{
    public readonly struct ThreadFramePerformanceSnapshot
    {
        public readonly double SchedulerMs;
        public readonly double WorkMs;
        public readonly double SleepMs;
        public readonly double GcMs;
        public readonly long Invalidations;
        public readonly long Refreshes;
        public readonly long DrawNodeCtor;
        public readonly long DrawNodeAppl;
        public readonly long ScheduleInvk;
        public readonly long InputQueue;
        public readonly long PositionalIQ;
        public readonly long CCL;
        public readonly string? CclTop1Source;
        public readonly long CclTop1Count;
        public readonly string? CclTop2Source;
        public readonly long CclTop2Count;
        public readonly string? CclTop3Source;
        public readonly long CclTop3Count;
        public readonly string? InvalTop1Source;
        public readonly long InvalTop1Count;
        public readonly string? InvalTop2Source;
        public readonly long InvalTop2Count;
        public readonly string? InvalTop3Source;
        public readonly long InvalTop3Count;
        public readonly long VBufBinds;
        public readonly long VBufOverflow;
        public readonly long TextureBinds;
        public readonly long FBORedraw;
        public readonly long DrawCalls;
        public readonly long ShaderBinds;
        public readonly long VerticesDraw;
        public readonly long VerticesUpl;
        public readonly long UniformUpl;
        public readonly long Pixels;
        public readonly long BatchFlushes;
        public readonly long PipelineBinds;
        public readonly long ResourceSetBinds;
        public readonly long ResourceSetCreates;
        public readonly long TextureUploadFlushes;
        public readonly long TextureUploads;
        public readonly long DeferredEventProcessUs;
        public readonly long DeferredVertexWriteUs;
        public readonly long DeferredVertexCommitUs;
        public readonly long DeferredVertexDrawUs;
        public readonly long DeferredVertexMapCount;
        public readonly long DeferredVertexUnmapCount;
        public readonly long DeferredVertexBytes;
        public readonly long VeldridSwapBuffersUs;
        public readonly long VeldridTextureUploadUs;
        public readonly long PipelineCreates;
        public readonly long VeldridVertexSetUs;
        public readonly long VeldridVertexUpdateUs;
        public readonly long VeldridVertexDrawUs;
        public readonly long VeldridVertexBytes;

        public ThreadFramePerformanceSnapshot(double schedulerMs, double workMs, double sleepMs, double gcMs, long invalidations, long refreshes, long drawNodeCtor, long drawNodeAppl, long scheduleInvk, long inputQueue, long positionalIQ, long ccl, string? cclTop1Source, long cclTop1Count, string? cclTop2Source, long cclTop2Count, string? cclTop3Source, long cclTop3Count, string? invalTop1Source, long invalTop1Count, string? invalTop2Source, long invalTop2Count, string? invalTop3Source, long invalTop3Count, long vBufBinds, long vBufOverflow, long textureBinds, long fboRedraw, long drawCalls, long shaderBinds, long verticesDraw, long verticesUpl, long uniformUpl, long pixels, long batchFlushes, long pipelineBinds, long resourceSetBinds, long resourceSetCreates, long textureUploadFlushes, long textureUploads)
        {
            SchedulerMs = schedulerMs;
            WorkMs = workMs;
            SleepMs = sleepMs;
            GcMs = gcMs;
            Invalidations = invalidations;
            Refreshes = refreshes;
            DrawNodeCtor = drawNodeCtor;
            DrawNodeAppl = drawNodeAppl;
            ScheduleInvk = scheduleInvk;
            InputQueue = inputQueue;
            PositionalIQ = positionalIQ;
            CCL = ccl;
            CclTop1Source = cclTop1Source;
            CclTop1Count = cclTop1Count;
            CclTop2Source = cclTop2Source;
            CclTop2Count = cclTop2Count;
            CclTop3Source = cclTop3Source;
            CclTop3Count = cclTop3Count;
            InvalTop1Source = invalTop1Source;
            InvalTop1Count = invalTop1Count;
            InvalTop2Source = invalTop2Source;
            InvalTop2Count = invalTop2Count;
            InvalTop3Source = invalTop3Source;
            InvalTop3Count = invalTop3Count;
            VBufBinds = vBufBinds;
            VBufOverflow = vBufOverflow;
            TextureBinds = textureBinds;
            FBORedraw = fboRedraw;
            DrawCalls = drawCalls;
            ShaderBinds = shaderBinds;
            VerticesDraw = verticesDraw;
            VerticesUpl = verticesUpl;
            UniformUpl = uniformUpl;
            Pixels = pixels;
            BatchFlushes = batchFlushes;
            PipelineBinds = pipelineBinds;
            ResourceSetBinds = resourceSetBinds;
            ResourceSetCreates = resourceSetCreates;
            TextureUploadFlushes = textureUploadFlushes;
            TextureUploads = textureUploads;
            DeferredEventProcessUs = 0;
            DeferredVertexWriteUs = 0;
            DeferredVertexCommitUs = 0;
            DeferredVertexDrawUs = 0;
            DeferredVertexMapCount = 0;
            DeferredVertexUnmapCount = 0;
            DeferredVertexBytes = 0;
            VeldridSwapBuffersUs = 0;
            VeldridTextureUploadUs = 0;
            PipelineCreates = 0;
            VeldridVertexSetUs = 0;
            VeldridVertexUpdateUs = 0;
            VeldridVertexDrawUs = 0;
            VeldridVertexBytes = 0;
        }

        public ThreadFramePerformanceSnapshot(double schedulerMs, double workMs, double sleepMs, double gcMs, long invalidations, long refreshes, long drawNodeCtor, long drawNodeAppl, long scheduleInvk, long inputQueue, long positionalIQ, long ccl, string? cclTop1Source, long cclTop1Count, string? cclTop2Source, long cclTop2Count, string? cclTop3Source, long cclTop3Count, string? invalTop1Source, long invalTop1Count, string? invalTop2Source, long invalTop2Count, string? invalTop3Source, long invalTop3Count, long vBufBinds, long vBufOverflow, long textureBinds, long fboRedraw, long drawCalls, long shaderBinds, long verticesDraw, long verticesUpl, long uniformUpl, long pixels, long batchFlushes, long pipelineBinds, long resourceSetBinds, long resourceSetCreates, long textureUploadFlushes, long textureUploads, long deferredEventProcessUs, long deferredVertexWriteUs, long deferredVertexCommitUs, long deferredVertexDrawUs, long deferredVertexMapCount, long deferredVertexUnmapCount, long deferredVertexBytes, long veldridSwapBuffersUs, long veldridTextureUploadUs, long pipelineCreates, long veldridVertexSetUs, long veldridVertexUpdateUs, long veldridVertexDrawUs, long veldridVertexBytes)
            : this(schedulerMs, workMs, sleepMs, gcMs, invalidations, refreshes, drawNodeCtor, drawNodeAppl, scheduleInvk, inputQueue, positionalIQ, ccl, cclTop1Source, cclTop1Count, cclTop2Source, cclTop2Count, cclTop3Source, cclTop3Count, invalTop1Source, invalTop1Count, invalTop2Source, invalTop2Count, invalTop3Source, invalTop3Count, vBufBinds, vBufOverflow, textureBinds, fboRedraw, drawCalls, shaderBinds, verticesDraw, verticesUpl, uniformUpl, pixels, batchFlushes, pipelineBinds, resourceSetBinds, resourceSetCreates, textureUploadFlushes, textureUploads)
        {
            DeferredEventProcessUs = deferredEventProcessUs;
            DeferredVertexWriteUs = deferredVertexWriteUs;
            DeferredVertexCommitUs = deferredVertexCommitUs;
            DeferredVertexDrawUs = deferredVertexDrawUs;
            DeferredVertexMapCount = deferredVertexMapCount;
            DeferredVertexUnmapCount = deferredVertexUnmapCount;
            DeferredVertexBytes = deferredVertexBytes;
            VeldridSwapBuffersUs = veldridSwapBuffersUs;
            VeldridTextureUploadUs = veldridTextureUploadUs;
            PipelineCreates = pipelineCreates;
            VeldridVertexSetUs = veldridVertexSetUs;
            VeldridVertexUpdateUs = veldridVertexUpdateUs;
            VeldridVertexDrawUs = veldridVertexDrawUs;
            VeldridVertexBytes = veldridVertexBytes;
        }
    }

    internal class FrameStatistics
    {
        internal readonly Dictionary<PerformanceCollectionType, double> CollectedTimes = new Dictionary<PerformanceCollectionType, double>(NUM_STATISTICS_COUNTER_TYPES);
        internal readonly Dictionary<StatisticsCounterType, long> Counts = new Dictionary<StatisticsCounterType, long>(NUM_STATISTICS_COUNTER_TYPES);
        internal readonly Dictionary<string, long> ChildLifeSources = new Dictionary<string, long>();
        internal readonly Dictionary<string, long> InvalidationSources = new Dictionary<string, long>();
        internal readonly List<int> GarbageCollections = new List<int>();

        public double FramesPerSecond { get; set; }

        internal static readonly int NUM_STATISTICS_COUNTER_TYPES = Enum.GetValues<StatisticsCounterType>().Length;
        internal static readonly int NUM_PERFORMANCE_COLLECTION_TYPES = Enum.GetValues<PerformanceCollectionType>().Length;

        internal static readonly long[] COUNTERS = new long[NUM_STATISTICS_COUNTER_TYPES];

        public double Jitter;

        internal void Clear()
        {
            CollectedTimes.Clear();
            ChildLifeSources.Clear();
            InvalidationSources.Clear();
            GarbageCollections.Clear();
            Counts.Clear();
            FramesPerSecond = 0;
            Jitter = 0;
        }

        internal static bool TrackChildLifeSources { get; set; }

        internal static bool TrackInvalidationSources { get; set; }

        [ThreadStatic]
        private static Dictionary<string, long>? childLifeSources;

        [ThreadStatic]
        private static Dictionary<string, long>? invalidationSources;

        internal static void Increment(StatisticsCounterType type) => ++COUNTERS[(int)type];

        internal static void Add(StatisticsCounterType type, long amount)
        {
            if (amount < 0)
                throw new ArgumentException($"Statistics counter {type} was attempted to be decremented via {nameof(Add)} call.", nameof(amount));

            COUNTERS[(int)type] += amount;
        }

        internal static void IncrementChildLifeChecks(string source) => AddChildLifeChecks(source, 1);

        internal static void AddChildLifeChecks(string source, long amount)
        {
            Add(StatisticsCounterType.CCL, amount);

            if (!TrackChildLifeSources || amount <= 0)
                return;

            childLifeSources ??= new Dictionary<string, long>();
            childLifeSources.TryGetValue(source, out long existing);
            childLifeSources[source] = existing + amount;
        }

        internal static void FlushChildLifeSources(Dictionary<string, long> target)
        {
            target.Clear();

            if (childLifeSources == null)
                return;

            foreach (var pair in childLifeSources)
                target[pair.Key] = pair.Value;

            childLifeSources.Clear();
        }

        internal static void IncrementInvalidation(string source)
        {
            Increment(StatisticsCounterType.Invalidations);

            if (!TrackInvalidationSources || string.IsNullOrEmpty(source))
                return;

            invalidationSources ??= new Dictionary<string, long>();
            invalidationSources.TryGetValue(source, out long existing);
            invalidationSources[source] = existing + 1;
        }

        internal static void FlushInvalidationSources(Dictionary<string, long> target)
        {
            target.Clear();

            if (invalidationSources == null)
                return;

            foreach (var pair in invalidationSources)
                target[pair.Key] = pair.Value;

            invalidationSources.Clear();
        }
    }

    internal enum PerformanceCollectionType
    {
        Work = 0,
        GC,
        SwapBuffer,
        WndProc,
        Debug,
        Sleep,
        Scheduler,
        IPC,
        DrawReset,
    }

    internal enum StatisticsCounterType
    {
        Invalidations = 0,
        Refreshes,
        DrawNodeCtor,
        DrawNodeAppl,
        ScheduleInvk,
        InputQueue,
        PositionalIQ,

        /// <summary>
        /// See <see cref="Graphics.Containers.CompositeDrawable.CheckChildrenLife"/>.
        /// </summary>
        CCL,

        VBufBinds,
        VBufOverflow,
        TextureBinds,
        FBORedraw,
        DrawCalls,
        ShaderBinds,
        VerticesDraw,
        VerticesUpl,
        UniformUpl,
        Pixels,
        BatchFlushes,
        PipelineBinds,
        ResourceSetBinds,
        ResourceSetCreates,
        TextureUploadFlushes,
        TextureUploads,
        DeferredEventProcessUs,
        DeferredVertexWriteUs,
        DeferredVertexCommitUs,
        DeferredVertexDrawUs,
        DeferredVertexMapCount,
        DeferredVertexUnmapCount,
        DeferredVertexBytes,
        VeldridSwapBuffersUs,
        VeldridTextureUploadUs,
        PipelineCreates,
        VeldridVertexSetUs,
        VeldridVertexUpdateUs,
        VeldridVertexDrawUs,
        VeldridVertexBytes,

        TasksRun,
        Tracks,
        Samples,
        SChannels,
        Components,
        MixChannels,

        MouseEvents,
        KeyEvents,
        JoystickEvents,
        MidiEvents,
        TabletEvents,
        TouchEvents,
    }
}
