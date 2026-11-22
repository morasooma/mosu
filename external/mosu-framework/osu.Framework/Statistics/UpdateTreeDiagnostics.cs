using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using osu.Framework.Graphics;
using osu.Framework.Logging;

namespace osu.Framework.Statistics
{
    /// <summary>
    /// Attribution of slow update trees while the game's performance recording is enabled.
    /// Exclusive time removes descendant UpdateSubTree calls, but includes work done directly
    /// by a node (including child lifetime and loading work). Diagnostic timings are not baselines.
    /// </summary>
    internal static class UpdateTreeDiagnostics
    {
        public static volatile bool Enabled;

        private const double slow_frame_ms = 50;

        [ThreadStatic]
        private static FrameState? current;

        [ThreadStatic]
        private static FrameState? currentMasking;

        private static long nextFrame;

        // The capture request can change while a frame is being traversed. Only BeginFrame
        // decides whether to capture; finish the current frame with the same decision.
        public static bool IsFrameActive => current?.Depth > 0;

        public static bool IsMaskingFrameActive => currentMasking?.Depth > 0;

        public static void BeginMaskingFrame(Drawable root)
        {
            var frame = currentMasking ??= new FrameState();
            frame.Reset(nextFrame);
            frame.Enter(root);
        }

        public static void EnterMaskingNode(Drawable drawable) => currentMasking!.Enter(drawable);

        public static void ExitMaskingNode() => currentMasking!.Exit();

        public static void EndMaskingFrame() => ExitMaskingNode();

        public static void ReportMaskingFrame(long maskingTicks)
        {
            if (maskingTicks * 1000.0 / Stopwatch.Frequency >= 30)
                currentMasking!.Report(maskingTicks, "MASKING");
        }

        public static void BeginFrame(Drawable root)
        {
            var frame = current ??= new FrameState();
            frame.Reset(++nextFrame);
            frame.Enter(root);
        }

        public static void EnterNode(Drawable drawable) => current!.Enter(drawable);

        public static void ExitNode() => current!.Exit();

        public static void EndFrame() => ExitNode(); // root

        // Report outside the measured tree/masking/draw-node spans so diagnostic logging is not mistaken for masking.
        public static void ReportFrame(long treeTicks)
        {
            if (treeTicks * 1000.0 / Stopwatch.Frequency >= slow_frame_ms)
                current!.Report(treeTicks, "UPDATE_TREE");
        }

        private struct Node
        {
            public Drawable? Drawable;
            public long Start;
            public long Children;

            public Node(Drawable drawable, long start)
            {
                Drawable = drawable;
                Start = start;
                Children = 0;
            }
        }

        private readonly record struct Sample(Drawable Drawable, long Total, long Own, int Depth);

        private sealed class TypeTotal
        {
            public int Count;
            public long Own;
        }

        private sealed class FrameState
        {
            public Node[] Stack = new Node[64];
            public int Depth;
            private long frameId;
            private Sample[] samples = new Sample[4096];
            private int sampleCount;
            private readonly Dictionary<Type, TypeTotal> types = new Dictionary<Type, TypeTotal>();
            private readonly Sample[] topOwn = new Sample[10];
            private readonly Sample[] topTotal = new Sample[5];
            private int ownCount;
            private int totalCount;
            private long summedOwn;

            public void Enter(Drawable drawable)
            {
                if (Depth == Stack.Length)
                    Array.Resize(ref Stack, Stack.Length * 2);

                Stack[Depth++] = new Node(drawable, Stopwatch.GetTimestamp());
            }

            public void Exit()
            {
                long end = Stopwatch.GetTimestamp();
                int index = --Depth;
                Node node = Stack[index];
                long total = end - node.Start;
                long own = Math.Max(0, total - node.Children);

                if (index > 0)
                    Stack[index - 1].Children += total;

                Stack[index] = default;
                Add(node.Drawable!, total, own, index);
            }

            public void Reset(long id)
            {
                frameId = id;
                Depth = 0;
                Array.Clear(samples, 0, sampleCount);
                Array.Clear(topOwn);
                Array.Clear(topTotal);
                sampleCount = 0;
                summedOwn = 0;
            }

            public void Add(Drawable drawable, long total, long own, int depth)
            {
                if (sampleCount == samples.Length)
                    Array.Resize(ref samples, samples.Length * 2);

                samples[sampleCount++] = new Sample(drawable, total, own, depth);
                summedOwn += own;
            }

            private static void addTop(Sample[] destination, ref int count, Sample sample, long value, Func<Sample, long> metric)
            {
                if (count < destination.Length)
                {
                    destination[count++] = sample;
                    return;
                }

                int smallest = 0;
                for (int i = 1; i < count; i++)
                {
                    if (metric(destination[i]) < metric(destination[smallest]))
                        smallest = i;
                }

                if (value > metric(destination[smallest]))
                    destination[smallest] = sample;
            }

            public void Report(long ticks, string category)
            {
                static double ms(long value) => value * 1000.0 / Stopwatch.Frequency;

                string measurement = category == "UPDATE_TREE" ? "tree_ms" : "mask_ms";
                Logger.Log($"MOSU_SLOW_{category} frame={frameId} {measurement}={ms(ticks):F2} measured_own_ms={ms(summedOwn):F2} nodes={sampleCount} "
                           + "note=exclusive_excludes_descendant_calls_but_includes_diagnostic_overhead",
                    LoggingTarget.Performance);

                types.Clear();
                Array.Clear(topOwn);
                Array.Clear(topTotal);
                ownCount = totalCount = 0;

                for (int i = 0; i < sampleCount; i++)
                {
                    Sample sample = samples[i];
                    Type type = sample.Drawable.GetType();
                    if (!types.TryGetValue(type, out TypeTotal? group))
                        types.Add(type, group = new TypeTotal());
                    group.Count++;
                    group.Own += sample.Own;
                    addTop(topOwn, ref ownCount, sample, sample.Own, static s => s.Own);
                    addTop(topTotal, ref totalCount, sample, sample.Total, static s => s.Total);
                }

                var groups = new StringBuilder($"MOSU_{category}_TYPES frame={frameId} ");
                foreach (var group in types.OrderByDescending(p => p.Value.Own).Take(10))
                    groups.Append($"{group.Key.Name}:{ms(group.Value.Own):F2}ms/{group.Value.Count} ");
                Logger.Log(groups.ToString(), LoggingTarget.Performance);

                reportSamples($"MOSU_{category}_OWN frame={frameId}", topOwn.Take(ownCount).OrderByDescending(s => s.Own));
                reportSamples($"MOSU_{category}_SUBTREES frame={frameId}", topTotal.Take(totalCount).OrderByDescending(s => s.Total));
            }

            private static void reportSamples(string label, IEnumerable<Sample> samples)
            {
                static double ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

                var line = new StringBuilder(label);
                foreach (Sample sample in samples)
                {
                    Drawable drawable = sample.Drawable;
                    string type = drawable.GetType().Name;
                    string name = drawable.Name ?? string.Empty;
                    if (name.Length > 48)
                        name = name[..48];
                    name = name.Replace(' ', '_').Replace('\n', '_').Replace('\r', '_');
                    string parent = drawable.Parent?.GetType().Name ?? "-";
                    line.Append($" [{type}{(name.Length > 0 ? ":" + name : string.Empty)}<-{parent} depth={sample.Depth} "
                                + $"own={ms(sample.Own):F2}ms total={ms(sample.Total):F2}ms]");
                }

                Logger.Log(line.ToString(), LoggingTarget.Performance);
            }
        }
    }
}
