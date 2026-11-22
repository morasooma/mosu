using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Runtime.InteropServices;

namespace osu.Framework.Statistics
{
    /// <summary>
    /// Opt-in EventPipe markers. Frame boundaries use Stopwatch ticks, not game-clock time.
    /// Disabled unless a diagnostic listener explicitly enables this provider.
    /// </summary>
    [EventSource(Name = PROVIDER_NAME)]
    public sealed class StutterTraceEventSource : EventSource
    {
        public const string PROVIDER_NAME = "Mosu-Stutter";
        public static readonly StutterTraceEventSource Log = new StutterTraceEventSource();

        internal static readonly bool FramebufferProbe = OperatingSystem.IsLinux()
            && Environment.GetEnvironmentVariable("MOSU_QUICK_FBO_PROBE") == "1";

        private StutterTraceEventSource()
        {
        }

        [NonEvent]
        internal static long ThreadCpuNanoseconds()
            => clock_gettime(3, out var time) == 0 ? time.Seconds * 1_000_000_000L + time.Nanoseconds : -1;

        [DllImport("libc")]
        private static extern int clock_gettime(int id, out Timespec time);

        [StructLayout(LayoutKind.Sequential)]
        private struct Timespec
        {
            public nint Seconds;
            public nint Nanoseconds;
        }

        // Do not use the params object[] WriteEvent overload on a per-frame path.
        [Event(1, Level = EventLevel.Informational)]
        public unsafe void Frame(string threadName, long frameIndex, long startTicks, long schedulerEndTicks, long workEndTicks, long endTicks)
        {
            if (!IsEnabled())
                return;

            fixed (char* name = threadName)
            {
                EventData* data = stackalloc EventData[6];
                data[0] = new EventData { DataPointer = (System.IntPtr)name, Size = (threadName.Length + 1) * sizeof(char) };
                data[1] = new EventData { DataPointer = (System.IntPtr)(&frameIndex), Size = sizeof(long) };
                data[2] = new EventData { DataPointer = (System.IntPtr)(&startTicks), Size = sizeof(long) };
                data[3] = new EventData { DataPointer = (System.IntPtr)(&schedulerEndTicks), Size = sizeof(long) };
                data[4] = new EventData { DataPointer = (System.IntPtr)(&workEndTicks), Size = sizeof(long) };
                data[5] = new EventData { DataPointer = (System.IntPtr)(&endTicks), Size = sizeof(long) };
                WriteEventCore(1, 6, data);
            }
        }

        [Event(4, Level = EventLevel.Informational)]
        public unsafe void DrawCounters(long frameIndex, long textureUploads, long pipelineCreates, long drawCalls, long swapMicroseconds)
        {
            if (!IsEnabled())
                return;

            EventData* data = stackalloc EventData[5];
            data[0] = new EventData { DataPointer = (System.IntPtr)(&frameIndex), Size = sizeof(long) };
            data[1] = new EventData { DataPointer = (System.IntPtr)(&textureUploads), Size = sizeof(long) };
            data[2] = new EventData { DataPointer = (System.IntPtr)(&pipelineCreates), Size = sizeof(long) };
            data[3] = new EventData { DataPointer = (System.IntPtr)(&drawCalls), Size = sizeof(long) };
            data[4] = new EventData { DataPointer = (System.IntPtr)(&swapMicroseconds), Size = sizeof(long) };
            WriteEventCore(4, 5, data);
        }

        /// <summary>
        /// Measures a synchronous operation without formatting, stack walking or heap allocation.
        /// Only operations lasting at least one millisecond emit an event. Do not span await/yield.
        /// </summary>
        [NonEvent]
        public OperationScope Measure(string name, long value = -1)
            => IsEnabled() ? new OperationScope(this, name, value) : default;

        [Event(5, Level = EventLevel.Informational)]
        public unsafe void Operation(string name, long startTicks, long endTicks, long allocatedBytes, long value)
        {
            if (!IsEnabled())
                return;

            fixed (char* text = name)
            {
                EventData* data = stackalloc EventData[5];
                data[0] = new EventData { DataPointer = (IntPtr)text, Size = (name.Length + 1) * sizeof(char) };
                data[1] = new EventData { DataPointer = (IntPtr)(&startTicks), Size = sizeof(long) };
                data[2] = new EventData { DataPointer = (IntPtr)(&endTicks), Size = sizeof(long) };
                data[3] = new EventData { DataPointer = (IntPtr)(&allocatedBytes), Size = sizeof(long) };
                data[4] = new EventData { DataPointer = (IntPtr)(&value), Size = sizeof(long) };
                WriteEventCore(5, 5, data);
            }
        }

        public readonly struct OperationScope : IDisposable
        {
            private readonly StutterTraceEventSource? source;
            private readonly string? name;
            private readonly long start;
            private readonly long allocated;
            private readonly long value;

            internal OperationScope(StutterTraceEventSource source, string name, long value)
            {
                this.source = source;
                this.name = name;
                this.value = value;
                allocated = GC.GetAllocatedBytesForCurrentThread();
                start = Stopwatch.GetTimestamp();
            }

            public void Dispose()
            {
                if (source == null)
                    return;
                long end = Stopwatch.GetTimestamp();
                if (end - start >= Stopwatch.Frequency / 1000)
                    source.Operation(name!, start, end, GC.GetAllocatedBytesForCurrentThread() - allocated, value);
            }
        }

        [Event(2, Level = EventLevel.Informational)]
        public void Marker(string name, string detail, long stopwatchTicks)
        {
            if (IsEnabled())
                WriteEvent(2, name, detail, stopwatchTicks);
        }

        [Event(3, Level = EventLevel.Informational)]
        public void Phase(string name, long id, bool beginning, long stopwatchTicks)
        {
            if (IsEnabled())
                WriteEvent(3, name, id, beginning, stopwatchTicks);
        }
    }
}
