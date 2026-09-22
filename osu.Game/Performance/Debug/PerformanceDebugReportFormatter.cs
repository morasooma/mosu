// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace osu.Game.Performance.Debug
{
    public static class PerformanceDebugReportFormatter
    {
        public static string FormatCsv(IEnumerable<PerformanceFreezeEvent> events)
        {
            var output = new StringBuilder("sequence,local_time,cause,worst_ms,draw_ms,update_ms,input_ms,gc_ms,working_set_mb,gc_heap_mb,allocation_rate_mb_s,gc_mode,gen0,gen1,gen2,invalidations,ccl,input_queue,pipeline_creates,texture_upload_flushes,texture_uploads,swap_buffers_us\n");

            foreach (PerformanceFreezeEvent freeze in events)
            {
                PerformanceDebugSnapshot s = freeze.Snapshot;
                append(output, freeze.Sequence);
                append(output, freeze.Time.ToString("O", CultureInfo.InvariantCulture));
                append(output, freeze.Cause);
                append(output, s.WorstFrameMs);
                append(output, s.DrawFrameMs);
                append(output, s.UpdateFrameMs);
                append(output, s.InputFrameMs);
                append(output, System.Math.Max(s.DrawGcMs, System.Math.Max(s.UpdateGcMs, s.InputGcMs)));
                append(output, s.WorkingSetMb);
                append(output, s.GcHeapMb);
                append(output, s.AllocationRateMbPerSecond);
                append(output, s.GcMode);
                append(output, s.Gen0Collections);
                append(output, s.Gen1Collections);
                append(output, s.Gen2Collections);
                append(output, s.UpdateInvalidations);
                append(output, s.UpdateCcl);
                append(output, s.UpdateInputQueue);
                append(output, s.DrawPipelineCreates);
                append(output, s.DrawTextureUploadFlushes);
                append(output, s.DrawTextureUploads);
                output.Append(s.DrawSwapBuffersUs.ToString(CultureInfo.InvariantCulture));
                output.Append('\n');
            }

            return output.ToString();
        }

        private static void append(StringBuilder output, object value)
        {
            output.Append(value is IFormattable formattable ? formattable.ToString(null, CultureInfo.InvariantCulture) : value);
            output.Append(',');
        }
    }
}