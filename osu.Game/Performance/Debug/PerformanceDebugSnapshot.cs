// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Runtime;

namespace osu.Game.Performance.Debug
{
    public struct PerformanceDebugSnapshot
    {
        public double DrawFrameMs { get; set; }
        public double UpdateFrameMs { get; set; }
        public double InputFrameMs { get; set; }
        public double DrawGcMs { get; set; }
        public double UpdateGcMs { get; set; }
        public double InputGcMs { get; set; }
        public double WorkingSetMb { get; set; }
        public double GcHeapMb { get; set; }
        public double AllocationRateMbPerSecond { get; set; }
        public int Gen0Collections { get; set; }
        public int Gen1Collections { get; set; }
        public int Gen2Collections { get; set; }
        public GCLatencyMode GcMode { get; set; }
        public long UpdateInvalidations { get; set; }
        public long UpdateCcl { get; set; }
        public long UpdateInputQueue { get; set; }
        public long DrawPipelineCreates { get; set; }
        public long DrawTextureUploadFlushes { get; set; }
        public long DrawTextureUploads { get; set; }
        public long DrawSwapBuffersUs { get; set; }

        public readonly double WorstFrameMs => System.Math.Max(DrawFrameMs, System.Math.Max(UpdateFrameMs, InputFrameMs));
    }
}