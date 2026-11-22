using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Threading;
using NUnit.Framework;
using osu.Framework.Statistics;
using osu.Game.Performance.Debug;

namespace osu.Game.Tests.Visual.Performance
{
    public partial class TestSceneStutterTrace : OsuTestScene
    {
        [Test]
        public void TestRealGameThreadFramesAndPhases()
        {
            TraceListener? listener = null;
            AddStep("enable marker listener", () => listener = new TraceListener());
            AddStep("emit phase without CSV logging dependency", () =>
            {
                using var phase = PerformancePhaseLogger.Begin("StutterTraceTest");
                StutterTraceEventSource.Log.Marker("TestMarker", "test", Stopwatch.GetTimestamp());
            });
            AddWaitStep("collect actual host frames", 5);
            AddStep("disable listener", () => listener!.Dispose());
            AddAssert("real Update frames recorded", () => listener!.UpdateFrames > 0);
            AddAssert("real Draw frames recorded", () => listener!.DrawFrames > 0);
            AddAssert("phase boundaries recorded", () => listener!.PhaseEvents == 2);
            AddAssert("marker recorded", () => listener!.MarkerEvents == 1);
            AddAssert("frame schema valid and monotonic", () => listener!.Errors.IsEmpty);
        }

        private sealed class TraceListener : EventListener
        {
            public int UpdateFrames;
            public int DrawFrames;
            public int PhaseEvents;
            public int MarkerEvents;
            public readonly ConcurrentQueue<string> Errors = new ConcurrentQueue<string>();

            protected override void OnEventSourceCreated(EventSource source)
            {
                if (source.Name == StutterTraceEventSource.PROVIDER_NAME)
                    EnableEvents(source, EventLevel.Informational);
            }

            protected override void OnEventWritten(EventWrittenEventArgs data)
            {
                // EventListener may be called during base construction, before field initialisation.
                if (Errors == null)
                    return;
                if (data.EventId == 0)
                    Errors.Enqueue(string.Join(";", data.Payload!));
                if (data.EventId == 1)
                {
                    string name = (string)data.Payload![0]!;
                    long start = (long)data.Payload[2]!;
                    long scheduler = (long)data.Payload[3]!;
                    long work = (long)data.Payload[4]!;
                    long end = (long)data.Payload[5]!;
                    if (start <= 0 || start > scheduler || scheduler > work || work > end)
                        Errors.Enqueue("Invalid Stopwatch frame boundaries");
                    if (name == "Update") Interlocked.Increment(ref UpdateFrames);
                    if (name == "Draw") Interlocked.Increment(ref DrawFrames);
                }
                if (data.EventId == 3 && (string)data.Payload![0]! == "StutterTraceTest")
                    Interlocked.Increment(ref PhaseEvents);
                if (data.EventId == 2 && (string)data.Payload![0]! == "TestMarker")
                    Interlocked.Increment(ref MarkerEvents);
            }
        }
    }
}
