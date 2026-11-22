// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Logging;

namespace osu.Framework.Statistics
{
    /// <summary>
    /// Low-overhead, opt-in attribution for the three unconditional update-frame scene passes.
    /// Enabled only with MOSU_UPDATE_PHASE_DIAGNOSTICS=1.
    /// </summary>
    internal static class UpdateFramePhaseDiagnostics
    {
        public static readonly bool Enabled = Environment.GetEnvironmentVariable("MOSU_UPDATE_PHASE_DIAGNOSTICS") == "1";

        private static long intervalStart;
        private static long frameCount;
        private static long updateTicks;
        private static long maskingTicks;
        private static long drawTreeTicks;
        private static long updateMaxTicks;
        private static long maskingMaxTicks;
        private static long drawTreeMaxTicks;

        [ThreadStatic]
        private static long frameUpdateNodes;

        [ThreadStatic]
        private static long frameMaskingNodes;

        [ThreadStatic]
        private static long frameMaskingRecomputes;

        [ThreadStatic]
        private static long frameDrawTreeNodes;

        [ThreadStatic]
        private static long frameDrawNodeApplies;

        private static long updateNodes;
        private static long maskingNodes;
        private static long maskingRecomputes;
        private static long drawTreeNodes;
        private static long drawNodeApplies;
        private static long transformTicks;
        private static long schedulerTicks;
        private static long customUpdateTicks;
        private static long childLifeTicks;
        private static long afterChildrenTicks;
        private static long layoutTicks;
        private static long transformCalls;
        private static long schedulerCalls;
        private static long customUpdateCalls;
        private static long childLifeCalls;
        private static long afterChildrenCalls;
        private static long layoutCalls;

        public static void BeginFrame()
        {
            if (!Enabled)
                return;

            frameUpdateNodes = 0;
            frameMaskingNodes = 0;
            frameMaskingRecomputes = 0;
            frameDrawTreeNodes = 0;
            frameDrawNodeApplies = 0;

            if (intervalStart == 0)
                intervalStart = Stopwatch.GetTimestamp();
        }

        public static void UpdateNode() => frameUpdateNodes++;
        public static void MaskingNode() => frameMaskingNodes++;
        public static void MaskingRecompute() => frameMaskingRecomputes++;
        public static void DrawTreeNode() => frameDrawTreeNodes++;
        public static void DrawNodeApply() => frameDrawNodeApplies++;
        public static long Timestamp() => Stopwatch.GetTimestamp();

        public static void Transform(long ticks)
        {
            transformTicks += ticks;
            transformCalls++;
        }

        public static void Scheduler(long ticks)
        {
            schedulerTicks += ticks;
            schedulerCalls++;
        }

        public static void CustomUpdate(long ticks)
        {
            customUpdateTicks += ticks;
            customUpdateCalls++;
        }

        public static void ChildLife(long ticks)
        {
            childLifeTicks += ticks;
            childLifeCalls++;
        }

        public static void AfterChildren(long ticks)
        {
            afterChildrenTicks += ticks;
            afterChildrenCalls++;
        }

        public static void Layout(long ticks)
        {
            layoutTicks += ticks;
            layoutCalls++;
        }

        public static void EndFrame(long updateElapsedTicks, long maskingElapsedTicks, long drawTreeElapsedTicks)
        {
            if (!Enabled)
                return;

            frameCount++;
            updateTicks += updateElapsedTicks;
            maskingTicks += maskingElapsedTicks;
            drawTreeTicks += drawTreeElapsedTicks;
            updateMaxTicks = Math.Max(updateMaxTicks, updateElapsedTicks);
            maskingMaxTicks = Math.Max(maskingMaxTicks, maskingElapsedTicks);
            drawTreeMaxTicks = Math.Max(drawTreeMaxTicks, drawTreeElapsedTicks);
            updateNodes += frameUpdateNodes;
            maskingNodes += frameMaskingNodes;
            maskingRecomputes += frameMaskingRecomputes;
            drawTreeNodes += frameDrawTreeNodes;
            drawNodeApplies += frameDrawNodeApplies;

            long now = Stopwatch.GetTimestamp();
            if (now - intervalStart < Stopwatch.Frequency)
                return;

            double tickToMicroseconds = 1_000_000.0 / Stopwatch.Frequency;
            double frames = Math.Max(1, frameCount);

            Logger.Log(
                $"MOSU_UPDATE_PHASE frames={frameCount} "
                + $"update_avg_us={updateTicks * tickToMicroseconds / frames:F2} update_max_us={updateMaxTicks * tickToMicroseconds:F2} "
                + $"mask_avg_us={maskingTicks * tickToMicroseconds / frames:F2} mask_max_us={maskingMaxTicks * tickToMicroseconds:F2} "
                + $"drawtree_avg_us={drawTreeTicks * tickToMicroseconds / frames:F2} drawtree_max_us={drawTreeMaxTicks * tickToMicroseconds:F2} "
                + $"transform_us={transformTicks * tickToMicroseconds / frames:F2} scheduler_us={schedulerTicks * tickToMicroseconds / frames:F2} "
                + $"custom_update_us={customUpdateTicks * tickToMicroseconds / frames:F2} child_life_us={childLifeTicks * tickToMicroseconds / frames:F2} "
                + $"after_children_us={afterChildrenTicks * tickToMicroseconds / frames:F2} layout_us={layoutTicks * tickToMicroseconds / frames:F2} "
                + $"update_nodes={updateNodes / frames:F2} mask_nodes={maskingNodes / frames:F2} mask_recomputes={maskingRecomputes / frames:F2} "
                + $"drawtree_nodes={drawTreeNodes / frames:F2} drawnode_applies={drawNodeApplies / frames:F2} "
                + $"transform_calls={transformCalls / frames:F2} scheduler_calls={schedulerCalls / frames:F2} custom_update_calls={customUpdateCalls / frames:F2} "
                + $"child_life_calls={childLifeCalls / frames:F2} after_children_calls={afterChildrenCalls / frames:F2} layout_calls={layoutCalls / frames:F2}",
                LoggingTarget.Performance);

            intervalStart = now;
            frameCount = 0;
            updateTicks = maskingTicks = drawTreeTicks = 0;
            updateMaxTicks = maskingMaxTicks = drawTreeMaxTicks = 0;
            updateNodes = maskingNodes = maskingRecomputes = drawTreeNodes = drawNodeApplies = 0;
            transformTicks = schedulerTicks = customUpdateTicks = childLifeTicks = afterChildrenTicks = layoutTicks = 0;
            transformCalls = schedulerCalls = customUpdateCalls = childLifeCalls = afterChildrenCalls = layoutCalls = 0;
        }
    }
}
