// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;

namespace osu.Framework.Graphics.Rendering.Deferred
{
    /// <summary>
    /// Runtime switches for experimental Deferred renderer optimisations.
    /// </summary>
    public static class DeferredRendererPerformanceSettings
    {
        private static int coalesceVertexUploadEvents;
        private static int directVertexBufferWrites;
        private static int directUniformBufferWrites;
        private static int midFrameFlush;
        private static int midFrameFlushPercent = 70;

        static DeferredRendererPerformanceSettings()
        {
            MidFrameFlush = Environment.GetEnvironmentVariable("MOSU_DEFERRED_MIDFRAME_FLUSH") == "1";

            if (int.TryParse(Environment.GetEnvironmentVariable("MOSU_DEFERRED_MIDFRAME_FLUSH_PERCENT"), out int percent) && percent is >= 10 and <= 90)
                MidFrameFlushPercent = percent;
        }

        /// <summary>
        /// Whether the frame's command list should be submitted to the device once mid-frame, letting
        /// driver processing and GPU execution of the first half overlap CPU replay of the second half.
        /// D3D11-only in v1 (gated in <see cref="DeferredContext.NewFrame"/>). Off by default;
        /// initialised from <c>MOSU_DEFERRED_MIDFRAME_FLUSH</c>.
        /// </summary>
        public static bool MidFrameFlush
        {
            get => Volatile.Read(ref midFrameFlush) != 0;
            set => Volatile.Write(ref midFrameFlush, value ? 1 : 0);
        }

        /// <summary>
        /// The percentage of the frame's render events after which the mid-frame submit is taken
        /// (at the next backbuffer flush boundary). Defaults to 70; initialised from
        /// <c>MOSU_DEFERRED_MIDFRAME_FLUSH_PERCENT</c> (clamped to 10-90).
        /// </summary>
        public static int MidFrameFlushPercent
        {
            get => Volatile.Read(ref midFrameFlushPercent);
            set => Volatile.Write(ref midFrameFlushPercent, value);
        }

        /// <summary>
        /// Whether adjacent vertex allocator regions targeting the same batch should be represented
        /// by a single render event. Disabled by default because driver and workload behaviour must
        /// be benchmarked before enabling it globally.
        /// </summary>
        public static bool CoalesceVertexUploadEvents
        {
            get => Volatile.Read(ref coalesceVertexUploadEvents) != 0;
            set => Volatile.Write(ref coalesceVertexUploadEvents, value ? 1 : 0);
        }

        /// <summary>
        /// Whether vertex primitives should be copied straight into the frame's mapped GPU vertex
        /// buffers while the draw tree is traversed. This removes the intermediate frame-allocator
        /// copy and the corresponding <c>AddPrimitiveToBatch</c> event stream.
        /// </summary>
        /// <remarks>
        /// The value is snapshotted at the beginning of each draw frame, so changing it cannot mix
        /// direct and deferred vertex allocation within one frame. Disabled by default until the
        /// backend-specific map/write behaviour has been benchmarked.
        /// </remarks>
        public static bool DirectVertexBufferWrites
        {
            get => Volatile.Read(ref directVertexBufferWrites) != 0;
            set => Volatile.Write(ref directVertexBufferWrites, value ? 1 : 0);
        }

        /// <summary>
        /// Whether uniform data should be copied straight into the frame's mapped uniform buffers.
        /// This replaces allocator-backed upload events with already-resolved buffer-range events.
        /// </summary>
        public static bool DirectUniformBufferWrites
        {
            get => Volatile.Read(ref directUniformBufferWrites) != 0;
            set => Volatile.Write(ref directUniformBufferWrites, value ? 1 : 0);
        }
    }
}
