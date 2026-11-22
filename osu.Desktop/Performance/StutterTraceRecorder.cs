using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.IO;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Buffers;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using osu.Framework.Statistics;

namespace osu.Desktop.Performance
{
    /// <summary>
    /// Bounded, opt-in self capture. Never blocks a game thread or attaches to another process.
    /// </summary>
    public static class StutterTraceRecorder
    {
        // Zero means record until the existing logging switch is disabled (or the size cap is reached).
        public const int DEFAULT_SECONDS = 0;
        public const long MAX_TRACE_BYTES = 256L * 1024 * 1024;
        public const int EVENTPIPE_BUFFER_MIB = 128;
        private const int drain_block_bytes = 256 * 1024;
        private const int drain_queue_blocks = 16; // 4 MiB, separate from the bounded runtime buffer.
        private static readonly JsonSerializerOptions metadataOptions = new JsonSerializerOptions { WriteIndented = true };

        // Verified against ClrTraceEventParser.Keywords. No heap dump, forced GC, or object survival stream.
        private const long runtime_keywords = 0x40000000000L // WaitHandle (.NET 9+)
                                             | 0x40000000 // Stack
                                             | 0x1000000 // GCHeapAndTypeNames
                                             | 0x20000 // JittedMethodILToNativeMap
                                             | 0x10000 // Threading
                                             | 0x4000 // Contention
                                             | 0x10 | 0x8 | 0x1; // Jit, Loader, GC (allocation ticks at Verbose)

        public static Task<string> CaptureAsync(string directory, object context, CancellationToken cancellationToken = default,
                                                int seconds = DEFAULT_SECONDS, Action? ready = null)
            => CaptureAsync(directory, context, cancellationToken, seconds, ready,
                path => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, drain_block_bytes, useAsync: false));

        internal static Task<string> CaptureGameplayAsync(string directory, object context, CancellationToken cancellationToken, Action ready, bool sampleStacks)
            => CaptureAsync(directory, context, cancellationToken, 20, ready,
                path => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, drain_block_bytes, useAsync: false), sampleStacks, 64);

        // Internal sink seam permits deterministic disk-error validation without exhausting
        // user storage or adding a production fault-injection switch.
        internal static async Task<string> CaptureAsync(string directory, object context, CancellationToken cancellationToken,
                                                        int seconds, Action? ready, Func<string, Stream> createFile,
                                                        bool sampleStacks = true, int bufferMiB = EVENTPIPE_BUFFER_MIB)
        {
            if (seconds < 0 || seconds > 120)
                throw new ArgumentOutOfRangeException(nameof(seconds));

            Directory.CreateDirectory(directory);
            restrict(directory, true);
            string tracePath = Path.Combine(directory, "capture.nettrace");
            string metadataPath = Path.Combine(directory, "metadata.json");
            if (File.Exists(tracePath) || File.Exists(metadataPath))
                throw new IOException($"Capture directory already contains evidence: {directory}");
            var startedUtc = DateTimeOffset.UtcNow;
            long startedTicks = Stopwatch.GetTimestamp();
            string stopReason = "stopped";
            bool complete = false;
            string? error = null;
            long bytes = 0;
            int? eventsLost = null;
            long? decodedEvents = null;
            string? qualityError = null;
            long runtimeKeywords = sampleStacks ? runtime_keywords : 0x1; // GC only in the low-instrumentation pass.

            // Persist provenance before opening the diagnostic transport. An interrupted process
            // must leave explicitly unknown completion, not a raw trace without its build identity.
            await writeMetadata(final: false).ConfigureAwait(false);

            var providers = new List<EventPipeProvider>
            {
                new EventPipeProvider("Microsoft-Windows-DotNETRuntime", sampleStacks ? EventLevel.Verbose : EventLevel.Informational, runtimeKeywords),
                new EventPipeProvider(StutterTraceEventSource.PROVIDER_NAME, EventLevel.Informational, -1),
            };
            if (sampleStacks)
                providers.Add(new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", EventLevel.Informational));

            try
            {
                // This method is dispatched off Update by the manager. Consume immediately, including during rundown.
                using var startTimeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                startTimeout.CancelAfter(TimeSpan.FromSeconds(15));
                using var session = await new DiagnosticsClient(Environment.ProcessId)
                    .StartEventPipeSessionAsync(providers, sampleStacks, bufferMiB, startTimeout.Token).ConfigureAwait(false);
                using var file = createFile(tracePath);
                restrict(tracePath, false);
                var limitReached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                using var drainCancellation = new CancellationTokenSource();
                int writerFailed = 0;
                // Dedicated transport and disk threads keep pool starvation / a short slow
                // filesystem write from delaying the EventPipe reader. Backpressure remains
                // bounded: 4 MiB here plus the explicitly recorded 128 MiB runtime ring.
                var blocks = new BlockingCollection<(byte[] Buffer, int Count)>(drain_queue_blocks);
                Task writer = Task.Factory.StartNew(writeBlocks, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task reader = Task.Factory.StartNew(readBlocks, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                Task drain = Task.WhenAll(reader, writer);
                _ = drain.ContinueWith(_ =>
                {
                    while (blocks.TryTake(out var block)) ArrayPool<byte>.Shared.Return(block.Buffer);
                    blocks.Dispose();
                }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
                using var duration = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (seconds > 0)
                    duration.CancelAfter(TimeSpan.FromSeconds(seconds));
                try
                {
                    StutterTraceEventSource.Log.Marker("CaptureStarted", $"frequency={Stopwatch.Frequency};utc={DateTimeOffset.UtcNow:O}", Stopwatch.GetTimestamp());
                    ready?.Invoke();
                    await Task.WhenAny(Task.Delay(Timeout.Infinite, duration.Token), limitReached.Task, reader, writer).ConfigureAwait(false);
                    stopReason = cancellationToken.IsCancellationRequested ? "cancelled" : limitReached.Task.IsCompleted ? "size-limit" : reader.IsCompleted || writer.IsCompleted ? "stream-ended" : "duration";
                    StutterTraceEventSource.Log.Marker("CaptureStopping", stopReason, Stopwatch.GetTimestamp());

                    // Rundown resolves method names; it may itself perturb gameplay and is outside the measurement window.
                    using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                    await session.StopAsync(stopTimeout.Token).ConfigureAwait(false);
                    await drain.WaitAsync(stopTimeout.Token).ConfigureAwait(false);
                    complete = bytes < MAX_TRACE_BYTES && stopReason != "stream-ended";
                }
                finally
                {
                    duration.Cancel();
                    drainCancellation.Cancel();
                    session.Dispose();
                    // A broken diagnostic transport must not hang capture shutdown indefinitely.
                    try { await drain.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
                    catch (Exception e)
                    {
                        error = e.Message;
                        complete = false;
                        _ = drain.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
                    }
                }

                void readBlocks()
                {
                    try
                    {
                        while (!drainCancellation.IsCancellationRequested)
                        {
                            byte[] buffer = ArrayPool<byte>.Shared.Rent(drain_block_bytes);
                            try
                            {
                                int count = session.EventStream.Read(buffer, 0, drain_block_bytes);
                                if (count == 0)
                                    break;
                                // A failed disk writer cannot consume more blocks. Keep
                                // draining/discarding transport until Stop finishes rundown;
                                // stopping the reader here deadlocks runtime finalization.
                                while (Volatile.Read(ref writerFailed) == 0)
                                {
                                    if (blocks.TryAdd((buffer, count), 50, drainCancellation.Token))
                                    {
                                        buffer = null!; // ownership moved to writer
                                        break;
                                    }
                                }
                            }
                            finally
                            {
                                if (buffer != null) ArrayPool<byte>.Shared.Return(buffer);
                            }
                        }
                    }
                    finally { blocks.CompleteAdding(); }
                }

                void writeBlocks()
                {
                    try
                    {
                        foreach (var block in blocks.GetConsumingEnumerable(drainCancellation.Token))
                        {
                            try
                            {
                                int writable = (int)Math.Min(block.Count, Math.Max(0, MAX_TRACE_BYTES - bytes));
                                if (writable > 0)
                                {
                                    file.Write(block.Buffer, 0, writable);
                                    bytes += writable;
                                }
                                if (bytes >= MAX_TRACE_BYTES - 16 * 1024 * 1024)
                                    limitReached.TrySetResult();
                            }
                            finally { ArrayPool<byte>.Shared.Return(block.Buffer); }
                        }
                        file.Flush();
                    }
                    catch
                    {
                        // Reader switches to discard mode; retain the original disk fault,
                        // while still consuming runtime rundown until the transport closes.
                        Volatile.Write(ref writerFailed, 1);
                        throw;
                    }
                }
            }
            catch (Exception e)
            {
                error = e.ToString();
                stopReason = "error";
                throw;
            }
            finally
            {
                // Verify the closed raw stream without ETLX indexing or retaining events.
                // This runs only after capture stop, never on Update or the live drain path.
                if (complete)
                {
                    try
                    {
                        using var source = new EventPipeEventSource(tracePath);
                        long count = 0;
                        source.AllEvents += _ => count++;
                        source.Process();
                        eventsLost = source.EventsLost;
                        decodedEvents = count;
                    }
                    catch (Exception e)
                    {
                        qualityError = e.Message;
                        complete = false;
                    }
                }
                await writeMetadata(final: true).ConfigureAwait(false);
            }
            if (!complete)
                throw new IOException($"Trace is incomplete ({stopReason}); partial capture and metadata preserved in {directory}.");
            return directory;

            async Task writeMetadata(bool final)
            {
                var metadata = new
                {
                    schemaVersion = 2,
                    metadataStage = final ? "final" : "recording",
                    processId = Environment.ProcessId,
                    startedUtc,
                    endedUtc = final ? (DateTimeOffset?)DateTimeOffset.UtcNow : null,
                    startedTicks,
                    stopwatchFrequency = Stopwatch.Frequency,
                    seconds,
                    bytes,
                    stopReason = final ? stopReason : "recording",
                    complete = final ? (bool?)complete : null,
                    error,
                    runtime = RuntimeInformation.FrameworkDescription,
                    os = RuntimeInformation.OSDescription,
                    architecture = RuntimeInformation.ProcessArchitecture.ToString(),
                    serverGc = GCSettings.IsServerGC,
                    gcLatencyModeAtEnd = final ? GCSettings.LatencyMode.ToString() : null,
                    runtimeKeywords = $"0x{runtimeKeywords:x}",
                    eventPipeBufferMiB = bufferMiB,
                    sampleStacks,
                    drainQueueBytes = drain_block_bytes * drain_queue_blocks,
                    eventsLost,
                    decodedEvents,
                    quality = eventsLost.HasValue ? (eventsLost.Value == 0 ? "no-runtime-loss-reported" : "events-lost") : "unknown",
                    qualityError,
                    context,
                    limitations = new[]
                    {
                        "complete means transport/rundown/file finalization, not losslessness. eventsLost counts gaps DURING the enabled capture, never time before recording started.",
                        "CPU stacks are sampled, not a record of every method call. Allocations are sampled ticks, not heap retention.",
                        "seconds=0 means record until logging is disabled or the size cap is reached; no one-minute auto-stop.",
                        "EventPipe contention/wait events cover managed waits; native scheduler, driver and GPU waits require an OS trace.",
                        "Sampling, stream writing and rundown add overhead. Compare with an untraced reproduction.",
                        "Frame interval includes scheduler, work and deliberate pacing/sleep; a long total alone is not a freeze.",
                        "No automatic upload. Trace method/type/module names and copied logs may contain private paths or map details.",
                    },
                };
                string temporaryPath = metadataPath + ".tmp";
                await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(metadata, metadataOptions)).ConfigureAwait(false);
                restrict(temporaryPath, false);
                File.Move(temporaryPath, metadataPath, overwrite: true);
            }
        }

        private static void restrict(string path, bool directory)
        {
            if (!OperatingSystem.IsWindows())
                File.SetUnixFileMode(path, directory ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute : UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }
}
