// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Threading;
using osu.Game.Configuration;
using osu.Game.Performance;
using osu.Game.Screens.Play;
using Win32Exception = System.ComponentModel.Win32Exception;

namespace osu.Desktop.Windows
{
    [SupportedOSPlatform("windows")]
    public partial class WindowsPerformanceMode : Component
    {
        private const uint timer_period_ms = 1;
        private const uint timerr_noerror = 0;
        private const uint ultra_timer_resolution_100ns = 5000;
        private const uint high_priority_class = 0x00000080;
        private const uint realtime_priority_class = 0x00000100;
        private const double priority_reassert_interval_ms = 500;
        private const double topmost_reassert_interval_ms = 500;
        private const int ultra_watchdog_poll_ms = 250;
        private const int ultra_watchdog_stall_ms = 3000;

        private const uint thread_power_throttling_current_version = 1;
        private const uint thread_power_throttling_execution_speed_flag = 0x1;

        // Cached P-core affinity mask — computed once, 0 means homogeneous CPU (no pinning).
        private static nuint? cachedPCoreMask;

        private static readonly IntPtr hwnd_topmost = new IntPtr(-1);
        private static readonly IntPtr hwnd_notopmost = new IntPtr(-2);

        private const uint swp_nosize = 0x0001;
        private const uint swp_nomove = 0x0002;
        private const uint swp_noactivate = 0x0010;
        private const uint swp_noownerzorder = 0x0200;
        private const uint swp_asyncwindowpos = 0x4000;

        private const uint process_power_throttling_current_version = 1;
        private const uint process_power_throttling_execution_speed = 0x1;
        private const uint process_power_throttling_ignore_timer_resolution = 0x4;

        private readonly BindableBool ultraEnabled = new BindableBool();
        private readonly object threadPolicyLock = new object();
        private readonly Dictionary<string, ThreadPolicy> threadPolicies = new Dictionary<string, ThreadPolicy>();
        private readonly AutoResetEvent windowTopmostSignal = new AutoResetEvent(false);
        private CancellationTokenSource? ultraWatchdogCancellation;
        private Thread? windowTopmostWorker;

        private ProcessPriorityClass? originalPriorityClass;
        private bool? originalPriorityBoostDisabled;
        private bool baseActive;
        private volatile bool ultraActive;
        private volatile bool realtimeFallbackActive;
        private long lastUpdateHeartbeat;
        private bool timerResolutionActive;
        private bool ultraTimerResolutionActive;
        private bool powerThrottlingManaged;
        private ScheduledDelegate? priorityReassertDelegate;
        private volatile bool windowTopmostActive;
        private volatile bool requestedWindowTopmost;
        private volatile bool windowTopmostWorkerExit;
        private long nextTopmostReassertAt;
        private IntPtr cachedWindowHandle;

        [Resolved]
        private GameHost host { get; set; } = null!;

        internal static bool UltraRealtimeFallbackActive => Volatile.Read(ref ultraRealtimeFallbackActive) != 0;

        private static int ultraRealtimeFallbackActive;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            startWindowTopmostWorker();
            config.BindWith(OsuSetting.ForkWindowsUltraPerformanceMode, ultraEnabled);

            ultraEnabled.BindValueChanged(_ => apply(), true);
            host.IsActive.ValueChanged += onHostActiveChanged;

            Logger.Log($"Windows performance mode component loaded. ultra={ultraEnabled.Value}; topmost=background_worker_v2", LoggingTarget.Runtime, LogLevel.Verbose);
        }

        private void onHostActiveChanged(ValueChangedEvent<bool> active)
        {
            if (!baseActive && !ultraActive)
                return;

            updateWindowTopmostState();
        }

        private void apply()
        {
            Logger.Log($"WindowsPerformanceMode.apply() called. ultraEnabled.Value={ultraEnabled.Value}", LoggingTarget.Runtime, LogLevel.Verbose);
            if (ultraEnabled.Value)
            {
                if (!baseActive)
                    enableBase();

                if (!ultraActive)
                    enableUltra();

                return;
            }

            if (ultraActive)
                disableUltra();

            if (baseActive)
                disableBase();
        }

        private void enableBase()
        {
            baseActive = true;

            logCheckpoint("base.process_priority_high", () => raiseProcessPriority(ProcessPriorityClass.High));
            logCheckpoint("base.priority_reassertion", startPriorityReassertion);
            logCheckpoint("base.topmost_reassertion", startWindowTopmostReassertion);
            logCheckpoint("base.timer_resolution", beginTimerResolution);
            logCheckpoint("base.power_throttling", setPowerThrottlingForPerformance);
        }

        private void disableBase()
        {
            if (!baseActive)
                return;

            resetPowerThrottling();
            endTimerResolution();
            baseActive = false;
            stopWindowTopmostReassertionIfIdle();
            restoreProcessPriorityIfIdle();
            stopPriorityReassertionIfIdle();
        }

        private void enableUltra()
        {
            ultraActive = true;
            realtimeFallbackActive = false;
            Interlocked.Exchange(ref ultraRealtimeFallbackActive, 0);
            startUltraWatchdog();

            logCheckpoint("ultra.priority_boost", () => setProcessPriorityBoost(disabled: true));
            logCheckpoint("ultra.process_priority_realtime", () => raiseProcessPriority(ProcessPriorityClass.RealTime));
            logCheckpoint("ultra.priority_reassertion", startPriorityReassertion);
            logCheckpoint("ultra.topmost_reassertion", startWindowTopmostReassertion);
            logCheckpoint("ultra.timer_resolution", beginUltraTimerResolution);
            logCheckpoint("ultra.thread_policies", applyThreadPolicies);

            Logger.Log("Windows ultra latency mode enabled.", LoggingTarget.Runtime, LogLevel.Verbose);
            Logger.Flush();
        }

        private static void logCheckpoint(string name, Action action)
        {
            Logger.Log($"Windows performance mode step begin: {name}.", LoggingTarget.Runtime, LogLevel.Verbose);
            Logger.Flush();
            action();
            Logger.Log($"Windows performance mode step end: {name}.", LoggingTarget.Runtime, LogLevel.Verbose);
            Logger.Flush();
        }

        private void disableUltra()
        {
            if (!ultraActive)
                return;

            stopUltraWatchdog();
            restoreThreadPolicies();
            endUltraTimerResolution();
            restoreProcessPriorityIfIdle(force: !baseActive);
            restoreProcessPriorityBoost();

            ultraActive = false;

            if (baseActive)
                raiseProcessPriority(ProcessPriorityClass.High);

            stopWindowTopmostReassertionIfIdle();
            stopPriorityReassertionIfIdle();
            Logger.Log("Windows ultra latency mode disabled.", LoggingTarget.Runtime, LogLevel.Verbose);
        }

        private void raiseProcessPriority(ProcessPriorityClass priorityClass)
        {
            try
            {
                using var process = Process.GetCurrentProcess();

                originalPriorityClass ??= process.PriorityClass;
                uint desiredPriorityClass = toWin32PriorityClass(priorityClass);

                if (!setPriorityClass(process.Handle, desiredPriorityClass))
                {
                    Logger.Log($"Windows performance mode could not set process priority to {priorityClass}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
                    return;
                }

                Logger.Log($"Windows performance mode requested process priority {priorityClass}; effective={formatPriorityClass(getPriorityClass(process.Handle))}", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Windows performance mode could not raise process priority to {priorityClass}.");
            }
        }

        private void restoreProcessPriorityIfIdle(bool force = false)
        {
            if (originalPriorityClass == null || (!force && (baseActive || ultraActive)))
                return;

            try
            {
                using var process = Process.GetCurrentProcess();

                if (!setPriorityClass(process.Handle, toWin32PriorityClass(originalPriorityClass.Value)))
                {
                    Logger.Log($"Windows performance mode could not restore process priority to {originalPriorityClass.Value}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
                    return;
                }

                Logger.Log($"Windows performance mode restored process priority to {formatPriorityClass(getPriorityClass(process.Handle))}", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Windows performance mode could not restore process priority.");
            }
            finally
            {
                originalPriorityClass = null;
            }
        }

        private void startPriorityReassertion()
        {
            if (priorityReassertDelegate != null)
                return;

            Scheduler.Add(priorityReassertDelegate = new ScheduledDelegate(reassertProcessPriority, priority_reassert_interval_ms, priority_reassert_interval_ms));
        }

        private void stopPriorityReassertionIfIdle()
        {
            if (baseActive || ultraActive)
                return;

            priorityReassertDelegate?.Cancel();
            priorityReassertDelegate = null;
        }

        private void reassertProcessPriority()
        {
            if (!baseActive && !ultraActive)
                return;

            ProcessPriorityClass desiredPriority = ultraActive && !realtimeFallbackActive
                ? ProcessPriorityClass.RealTime
                : ProcessPriorityClass.High;

            try
            {
                using var process = Process.GetCurrentProcess();

                originalPriorityClass ??= process.PriorityClass;

                uint currentPriority = getPriorityClass(process.Handle);
                uint desiredWin32Priority = toWin32PriorityClass(desiredPriority);

                // Some non-elevated sessions refuse Realtime and cap us at High. Treat that as the useful target.
                if (currentPriority == desiredWin32Priority || (desiredPriority == ProcessPriorityClass.RealTime && currentPriority == high_priority_class))
                    return;

                if (!setPriorityClass(process.Handle, desiredWin32Priority))
                {
                    Logger.Log($"Windows performance mode could not reassert process priority {desiredPriority}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
                    return;
                }

                Logger.Log($"Windows performance mode reasserted process priority {desiredPriority}; effective={formatPriorityClass(getPriorityClass(process.Handle))}", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Windows performance mode could not reassert process priority {desiredPriority}.");
            }
        }

        protected override void Update()
        {
            long now = Environment.TickCount64;
            Volatile.Write(ref lastUpdateHeartbeat, now);

            if (now >= nextTopmostReassertAt)
            {
                nextTopmostReassertAt = now + (long)topmost_reassert_interval_ms;
                updateWindowTopmostState();
            }

            base.Update();
        }

        private void startUltraWatchdog()
        {
            stopUltraWatchdog();
            Volatile.Write(ref lastUpdateHeartbeat, Environment.TickCount64);

            var cancellation = ultraWatchdogCancellation = new CancellationTokenSource();
            var thread = new Thread(() => runUltraWatchdog(cancellation))
            {
                IsBackground = true,
                Name = "Windows ultra performance watchdog",
                Priority = ThreadPriority.Normal,
            };

            thread.Start();
        }

        private void stopUltraWatchdog()
        {
            var cancellation = Interlocked.Exchange(ref ultraWatchdogCancellation, null);

            if (cancellation == null)
                return;

            try
            {
                cancellation.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // The watchdog may have already completed and disposed its token after detecting
                // that Windows denied realtime priority. Its stale reference must not break config restoration.
            }
        }

        private void runUltraWatchdog(CancellationTokenSource cancellation)
        {
            try
            {
                while (!cancellation.Token.WaitHandle.WaitOne(ultra_watchdog_poll_ms))
                {
                    if (!ultraActive || realtimeFallbackActive)
                        return;

                    long heartbeatAge = Environment.TickCount64 - Volatile.Read(ref lastUpdateHeartbeat);

                    if (heartbeatAge < ultra_watchdog_stall_ms)
                        continue;

                    using var process = Process.GetCurrentProcess();

                    // A denied REALTIME request already leaves us at High and needs no fallback.
                    if (getPriorityClass(process.Handle) != realtime_priority_class)
                        return;

                    realtimeFallbackActive = true;
                    Interlocked.Exchange(ref ultraRealtimeFallbackActive, 1);

                    if (!setPriorityClass(process.Handle, high_priority_class))
                    {
                        Logger.Log(
                            $"Windows ultra watchdog could not fall back from Realtime to High: {new Win32Exception(Marshal.GetLastWin32Error()).Message}",
                            LoggingTarget.Runtime,
                            LogLevel.Important);
                    }
                    else
                    {
                        Logger.Log(
                            $"Windows ultra watchdog detected a stalled update thread ({heartbeatAge} ms) and fell back from Realtime to High for this process.",
                            LoggingTarget.Runtime,
                            LogLevel.Important);
                    }

                    Logger.Flush();
                    return;
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Windows ultra watchdog failed.");
                Logger.Flush();
            }
            finally
            {
                Interlocked.CompareExchange(ref ultraWatchdogCancellation, null, cancellation);
                cancellation.Dispose();
            }
        }

        private void startWindowTopmostReassertion()
        {
            // Let the next regular update queue the request. Configuration callbacks run inside the
            // update scheduler; even signalling the worker from that callback has caused cross-thread
            // logger/scheduler contention on affected systems.
            nextTopmostReassertAt = 0;
        }

        private void stopWindowTopmostReassertionIfIdle()
        {
            if (baseActive || ultraActive || GameplayPerformanceSnapshot.BenchmarkRunnerActive)
                return;

            setWindowTopmost(false);
        }

        private void updateWindowTopmostState()
        {
            // Exclusive fullscreen already owns the display. Changing HWND_TOPMOST while it is active
            // causes a visible black mode-switch flash on some drivers and provides no focus benefit.
            if (host.Window?.WindowMode.Value == WindowMode.Fullscreen)
            {
                if (windowTopmostActive || requestedWindowTopmost)
                    setWindowTopmost(false);

                return;
            }

            bool benchmarkActive = GameplayPerformanceSnapshot.BenchmarkRunnerActive;
            setWindowTopmost((baseActive || ultraActive || benchmarkActive) && (host.IsActive.Value || benchmarkActive));
        }

        private void setWindowTopmost(bool topmost)
        {
            if (!topmost && !windowTopmostActive && !requestedWindowTopmost)
                return;

            requestedWindowTopmost = topmost;

            // Window discovery can enter the Windows/SDL windowing stack and has been observed to
            // block indefinitely on individual systems. Never perform it on osu!'s update thread.
            windowTopmostSignal.Set();
        }

        private void startWindowTopmostWorker()
        {
            if (windowTopmostWorker != null)
                return;

            windowTopmostWorkerExit = false;
            windowTopmostWorker = new Thread(runWindowTopmostWorker)
            {
                IsBackground = true,
                Name = "Windows topmost worker",
                Priority = ThreadPriority.Normal,
            };

            windowTopmostWorker.Start();
        }

        private void stopWindowTopmostWorker()
        {
            requestedWindowTopmost = false;
            windowTopmostWorkerExit = true;
            windowTopmostSignal.Set();
            windowTopmostWorker = null;
        }

        private void runWindowTopmostWorker()
        {
            while (true)
            {
                windowTopmostSignal.WaitOne();

                applyWindowTopmost(requestedWindowTopmost);

                if (windowTopmostWorkerExit)
                    return;
            }
        }

        private void applyWindowTopmost(bool topmost)
        {
            try
            {
                IntPtr windowHandle = getWindowHandle();

                if (windowHandle == IntPtr.Zero)
                    return;

                // The window belongs to SDL's window thread. Keep the operation asynchronous so this
                // worker does not wait on SDL processing the window-position message either.
                bool success = setWindowPos(windowHandle, topmost ? hwnd_topmost : hwnd_notopmost, 0, 0, 0, 0,
                    swp_nomove | swp_nosize | swp_noactivate | swp_noownerzorder | swp_asyncwindowpos);

                if (!success)
                {
                    Logger.Log($"Windows performance mode could not set window topmost={topmost}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
                    return;
                }

                if (windowTopmostActive == topmost)
                    return;

                windowTopmostActive = topmost;
                Logger.Log($"Windows performance mode window topmost={topmost}.", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Windows performance mode worker could not set window topmost={topmost}.");
            }
        }

        private IntPtr getWindowHandle()
        {
            if (cachedWindowHandle != IntPtr.Zero && isWindow(cachedWindowHandle))
                return cachedWindowHandle;

            cachedWindowHandle = IntPtr.Zero;
            uint processId = (uint)Environment.ProcessId;

            // SDL_GetWindowWMInfo is not safe to call synchronously from the update thread on all
            // systems. Find the native top-level window without crossing SDL's thread boundary.
            enumWindows((handle, _) =>
            {
                getWindowThreadProcessId(handle, out uint windowProcessId);

                if (windowProcessId != processId || !isWindowVisible(handle))
                    return true;

                cachedWindowHandle = handle;
                return false;
            }, IntPtr.Zero);

            return cachedWindowHandle;
        }

        private static uint toWin32PriorityClass(ProcessPriorityClass priorityClass)
        {
            switch (priorityClass)
            {
                case ProcessPriorityClass.RealTime:
                    return realtime_priority_class;

                case ProcessPriorityClass.High:
                    return high_priority_class;

                default:
                    return (uint)priorityClass;
            }
        }

        private static string formatPriorityClass(uint priorityClass)
        {
            switch (priorityClass)
            {
                case realtime_priority_class:
                    return ProcessPriorityClass.RealTime.ToString();

                case high_priority_class:
                    return ProcessPriorityClass.High.ToString();

                case 0:
                    return $"unknown ({new Win32Exception(Marshal.GetLastWin32Error()).Message})";

                default:
                    return $"0x{priorityClass:X}";
            }
        }

        private void setProcessPriorityBoost(bool disabled)
        {
            if (originalPriorityBoostDisabled != null)
                return;

            try
            {
                using var process = Process.GetCurrentProcess();

                if (getProcessPriorityBoost(process.Handle, out bool currentDisabled))
                    originalPriorityBoostDisabled = currentDisabled;

                if (!setProcessPriorityBoost(process.Handle, disabled))
                    Logger.Log($"Windows ultra latency mode could not set process priority boost: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Windows ultra latency mode could not set process priority boost.");
            }
        }

        private void restoreProcessPriorityBoost()
        {
            if (originalPriorityBoostDisabled == null)
                return;

            try
            {
                using var process = Process.GetCurrentProcess();

                if (!setProcessPriorityBoost(process.Handle, originalPriorityBoostDisabled.Value))
                    Logger.Log($"Windows ultra latency mode could not restore process priority boost: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Windows ultra latency mode could not restore process priority boost.");
            }
            finally
            {
                originalPriorityBoostDisabled = null;
            }
        }

        private void beginTimerResolution()
        {
            if (timerResolutionActive)
                return;

            uint result = timeBeginPeriod(timer_period_ms);

            if (result == timerr_noerror)
            {
                timerResolutionActive = true;
                return;
            }

            Logger.Log($"Windows performance mode could not request {timer_period_ms} ms timer resolution: {result}", LoggingTarget.Runtime, LogLevel.Verbose);
        }

        private void endTimerResolution()
        {
            if (!timerResolutionActive)
                return;

            uint result = timeEndPeriod(timer_period_ms);
            timerResolutionActive = false;

            if (result != timerr_noerror)
                Logger.Log($"Windows performance mode could not release {timer_period_ms} ms timer resolution: {result}", LoggingTarget.Runtime, LogLevel.Verbose);
        }

        private void beginUltraTimerResolution()
        {
            if (ultraTimerResolutionActive)
                return;

            int status = ntSetTimerResolution(ultra_timer_resolution_100ns, true, out uint currentResolution);

            if (status >= 0)
            {
                ultraTimerResolutionActive = true;
                Logger.Log($"Windows ultra latency mode requested {ultra_timer_resolution_100ns / 10000d:0.###} ms timer resolution; current {currentResolution / 10000d:0.###} ms.", LoggingTarget.Runtime, LogLevel.Verbose);
                return;
            }

            Logger.Log($"Windows ultra latency mode could not request sub-1 ms timer resolution: NTSTATUS 0x{status:X8}", LoggingTarget.Runtime, LogLevel.Verbose);
        }

        private void endUltraTimerResolution()
        {
            if (!ultraTimerResolutionActive)
                return;

            int status = ntSetTimerResolution(ultra_timer_resolution_100ns, false, out uint currentResolution);
            ultraTimerResolutionActive = false;

            if (status >= 0)
                Logger.Log($"Windows ultra latency mode released sub-1 ms timer resolution; current {currentResolution / 10000d:0.###} ms.", LoggingTarget.Runtime, LogLevel.Verbose);
            else
                Logger.Log($"Windows ultra latency mode could not release sub-1 ms timer resolution: NTSTATUS 0x{status:X8}", LoggingTarget.Runtime, LogLevel.Verbose);
        }

        private void applyThreadPolicies()
        {
            host.InputThread.Scheduler.Add(() => applyThreadPolicy("input", AvrtPriority.Critical));
            host.UpdateThread.Scheduler.Add(() => applyThreadPolicy("update", AvrtPriority.High));
            host.DrawThread.Scheduler.Add(() => applyThreadPolicy("draw", AvrtPriority.High));
        }

        private void restoreThreadPolicies()
        {
            host.InputThread.Scheduler.Add(() => restoreThreadPolicy("input"));
            host.UpdateThread.Scheduler.Add(() => restoreThreadPolicy("update"));
            host.DrawThread.Scheduler.Add(() => restoreThreadPolicy("draw"));
        }

        private void applyThreadPolicy(string name, AvrtPriority mmcssPriority)
        {
            lock (threadPolicyLock)
            {
                if (threadPolicies.ContainsKey(name))
                    return;
            }

            var policy = new ThreadPolicy(Thread.CurrentThread.Priority);

            try
            {
                Thread.CurrentThread.Priority = ThreadPriority.Highest;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Windows ultra latency mode could not raise {name} thread priority.");
            }

            // MMCSS "Games" is scheduling category Medium with a CPU quota; saturated uncapped threads can be
            // periodically demoted below normal priority when the quota runs out (see MosuOptimisationToggles.ULTRA_MMCSS).
            if (MosuOptimisationToggles.UltraMmcss)
            {
                try
                {
                    policy.MmcssHandle = avSetMmThreadCharacteristics("Games", out uint taskIndex);

                    if (policy.MmcssHandle != IntPtr.Zero)
                    {
                        policy.MmcssTaskIndex = taskIndex;

                        if (!avSetMmThreadPriority(policy.MmcssHandle, mmcssPriority))
                            Logger.Log($"Windows ultra latency mode could not set {name} MMCSS priority: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
                    }
                    else
                        Logger.Log($"Windows ultra latency mode could not register {name} thread with MMCSS: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Windows ultra latency mode could not register {name} thread with MMCSS.");
                }
            }

            // Disable EcoQoS power throttling at the thread level (process-level is already disabled,
            // but belt-and-suspenders ensures the scheduler never demotes this thread to efficiency mode).
            try
            {
                var throttlingState = new ThreadPowerThrottlingState
                {
                    Version = thread_power_throttling_current_version,
                    ControlMask = thread_power_throttling_execution_speed_flag,
                    StateMask = 0, // 0 = disable throttling for the masked controls
                };

                if (!setThreadInformation(getCurrentThread(), ThreadInformationClass.ThreadPowerThrottling,
                        ref throttlingState, (uint)Marshal.SizeOf<ThreadPowerThrottlingState>()))
                    Logger.Log($"Windows ultra latency mode could not disable thread power throttling for {name}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Windows ultra latency mode could not disable thread power throttling for {name}.");
            }

            // Pin to P-cores on hybrid CPUs (Intel 12th+). On homogeneous CPUs the mask is 0 and we skip.
            try
            {
                nuint pCoreMask = cachedPCoreMask ??= computePCoreMask();

                if (pCoreMask != 0)
                {
                    nuint originalMask = setThreadAffinityMask(getCurrentThread(), pCoreMask);

                    if (originalMask != 0)
                    {
                        policy.OriginalAffinityMask = originalMask;
                        Logger.Log($"Windows ultra latency mode pinned {name} thread to P-cores (mask=0x{pCoreMask:X}, was 0x{originalMask:X}).", LoggingTarget.Runtime, LogLevel.Verbose);
                    }
                    else
                        Logger.Log($"Windows ultra latency mode could not set P-core affinity for {name}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Windows ultra latency mode could not set P-core affinity for {name}.");
            }

            lock (threadPolicyLock)
                threadPolicies[name] = policy;

            Logger.Log($"Windows ultra latency mode applied {name} thread policy. ManagedThreadId={Environment.CurrentManagedThreadId}, MMCSS task={policy.MmcssTaskIndex}", LoggingTarget.Runtime, LogLevel.Verbose);
        }

        private void restoreThreadPolicy(string name)
        {
            ThreadPolicy policy;

            lock (threadPolicyLock)
            {
                if (!threadPolicies.Remove(name, out policy))
                    return;
            }

            try
            {
                if (policy.MmcssHandle != IntPtr.Zero && !avRevertMmThreadCharacteristics(policy.MmcssHandle))
                    Logger.Log($"Windows ultra latency mode could not revert {name} MMCSS: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Windows ultra latency mode could not revert {name} MMCSS.");
            }

            // Restore original thread affinity if we changed it.
            if (policy.OriginalAffinityMask != 0)
            {
                try
                {
                    setThreadAffinityMask(getCurrentThread(), policy.OriginalAffinityMask);
                }
                catch (Exception e)
                {
                    Logger.Error(e, $"Windows ultra latency mode could not restore {name} thread affinity.");
                }
            }

            try
            {
                Thread.CurrentThread.Priority = policy.OriginalPriority;
            }
            catch (Exception e)
            {
                Logger.Error(e, $"Windows ultra latency mode could not restore {name} thread priority.");
            }
        }

        private void setPowerThrottlingForPerformance()
        {
            if (powerThrottlingManaged)
                return;

            uint performanceControlMask = process_power_throttling_execution_speed | process_power_throttling_ignore_timer_resolution;

            var throttlingState = new ProcessPowerThrottlingState
            {
                Version = process_power_throttling_current_version,
                ControlMask = performanceControlMask,
                StateMask = 0,
            };

            if (!setPowerThrottling(throttlingState, "disable process power throttling", false))
            {
                throttlingState.ControlMask = process_power_throttling_execution_speed;

                if (!setPowerThrottling(throttlingState, "disable process power throttling", true))
                    return;
            }

            powerThrottlingManaged = true;
        }

        private void resetPowerThrottling()
        {
            if (!powerThrottlingManaged)
                return;

            var throttlingState = new ProcessPowerThrottlingState
            {
                Version = process_power_throttling_current_version,
                ControlMask = 0,
                StateMask = 0,
            };

            if (setPowerThrottling(throttlingState, "reset process power throttling", true))
                powerThrottlingManaged = false;
        }

        private static bool setPowerThrottling(ProcessPowerThrottlingState throttlingState, string action, bool logFailure)
        {
            try
            {
                using var process = Process.GetCurrentProcess();

                if (setProcessInformation(process.Handle, ProcessInformationClass.ProcessPowerThrottling, ref throttlingState, (uint)Marshal.SizeOf<ProcessPowerThrottlingState>()))
                    return true;

                if (logFailure)
                    Logger.Log($"Windows performance mode could not {action}: {new Win32Exception(Marshal.GetLastWin32Error()).Message}", LoggingTarget.Runtime, LogLevel.Verbose);
            }
            catch (Exception e)
            {
                if (logFailure)
                    Logger.Error(e, $"Windows performance mode could not {action}.");
            }

            return false;
        }

        protected override void Dispose(bool isDisposing)
        {
            stopUltraWatchdog();
            disableUltra();
            disableBase();
            priorityReassertDelegate?.Cancel();
            priorityReassertDelegate = null;
            setWindowTopmost(false);
            stopWindowTopmostWorker();
            host.IsActive.ValueChanged -= onHostActiveChanged;
            ultraEnabled.UnbindAll();

            base.Dispose(isDisposing);
        }

        private struct ThreadPolicy
        {
            public readonly ThreadPriority OriginalPriority;
            public IntPtr MmcssHandle;
            public uint MmcssTaskIndex;
            /// <summary>Original affinity mask before P-core pinning; 0 means no pinning was applied.</summary>
            public nuint OriginalAffinityMask;

            public ThreadPolicy(ThreadPriority originalPriority)
            {
                OriginalPriority = originalPriority;
                MmcssHandle = IntPtr.Zero;
                MmcssTaskIndex = 0;
                OriginalAffinityMask = 0;
            }
        }

        private enum AvrtPriority
        {
            VeryLow = -2,
            Low = -1,
            Normal = 0,
            High = 1,
            Critical = 2,
        }

        private enum ProcessInformationClass
        {
            ProcessPowerThrottling = 4,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessPowerThrottlingState
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        private enum ThreadInformationClass
        {
            ThreadPowerThrottling = 3,
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ThreadPowerThrottlingState
        {
            public uint Version;
            public uint ControlMask;
            public uint StateMask;
        }

        private enum LogicalProcessorRelationship : uint
        {
            RelationProcessorCore = 0,
        }

        /// <summary>
        /// Computes an affinity mask covering only the highest-efficiency-class cores (P-cores on Intel hybrid CPUs).
        /// Returns 0 on homogeneous CPUs where all cores share the same efficiency class.
        /// </summary>
        private static nuint computePCoreMask()
        {
            uint bufferSize = 0;
            getLogicalProcessorInformationEx(LogicalProcessorRelationship.RelationProcessorCore, IntPtr.Zero, ref bufferSize);

            if (bufferSize == 0)
                return 0;

            IntPtr buffer = Marshal.AllocHGlobal((int)bufferSize);

            try
            {
                if (!getLogicalProcessorInformationEx(LogicalProcessorRelationship.RelationProcessorCore, buffer, ref bufferSize))
                    return 0;

                // SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX layout:
                //   [0] DWORD Relationship, [4] DWORD Size, [8] PROCESSOR_RELATIONSHIP
                // PROCESSOR_RELATIONSHIP layout:
                //   [0] BYTE Flags, [1] BYTE EfficiencyClass, [2] BYTE Reserved[20], [22] WORD GroupCount, [24] GROUP_AFFINITY[]
                // GROUP_AFFINITY on x64:
                //   [0] ULONG_PTR Mask (8 bytes), [8] WORD Group, [10] WORD Reserved[3] — total 16 bytes
                var classMasks = new Dictionary<byte, nuint>();
                int offset = 0;

                while (offset < (int)bufferSize)
                {
                    uint entrySize = (uint)Marshal.ReadInt32(buffer, offset + 4);
                    if (entrySize == 0) break;

                    byte efficiencyClass = Marshal.ReadByte(buffer, offset + 8 + 1);
                    ushort groupCount = (ushort)Marshal.ReadInt16(buffer, offset + 8 + 22);

                    nuint combinedMask = 0;

                    for (int g = 0; g < groupCount; g++)
                    {
                        int affinityOffset = offset + 8 + 24 + g * 16;
                        ulong maskValue = (ulong)Marshal.ReadInt64(buffer, affinityOffset);
                        combinedMask |= (nuint)maskValue;
                    }

                    if (classMasks.TryGetValue(efficiencyClass, out nuint existing))
                        classMasks[efficiencyClass] = existing | combinedMask;
                    else
                        classMasks[efficiencyClass] = combinedMask;

                    offset += (int)entrySize;
                }

                // Homogeneous CPU — no pinning needed.
                if (classMasks.Count <= 1)
                    return 0;

                byte maxClass = 0;

                foreach (byte cls in classMasks.Keys)
                {
                    if (cls > maxClass) maxClass = cls;
                }

                Logger.Log($"Windows ultra latency mode detected hybrid CPU: {classMasks.Count} efficiency classes, P-core mask=0x{classMasks[maxClass]:X}", LoggingTarget.Runtime, LogLevel.Verbose);
                return classMasks[maxClass];
            }
            catch (Exception e)
            {
                Logger.Log($"Windows ultra latency mode P-core detection failed: {e.Message}", LoggingTarget.Runtime, LogLevel.Verbose);
                return 0;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        [DllImport(@"winmm.dll", EntryPoint = @"timeBeginPeriod")]
        private static extern uint timeBeginPeriod(uint uMilliseconds);

        [DllImport(@"winmm.dll", EntryPoint = @"timeEndPeriod")]
        private static extern uint timeEndPeriod(uint uMilliseconds);

        [DllImport(@"ntdll.dll", EntryPoint = @"NtSetTimerResolution")]
        private static extern int ntSetTimerResolution(uint desiredResolution, [MarshalAs(UnmanagedType.I1)] bool setResolution, out uint currentResolution);

        [DllImport(@"kernel32.dll", EntryPoint = @"GetProcessPriorityBoost", SetLastError = true)]
        private static extern bool getProcessPriorityBoost(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] out bool pDisablePriorityBoost);

        [DllImport(@"kernel32.dll", EntryPoint = @"SetProcessPriorityBoost", SetLastError = true)]
        private static extern bool setProcessPriorityBoost(IntPtr hProcess, [MarshalAs(UnmanagedType.Bool)] bool disablePriorityBoost);

        [DllImport(@"kernel32.dll", EntryPoint = @"SetPriorityClass", SetLastError = true)]
        private static extern bool setPriorityClass(IntPtr hProcess, uint dwPriorityClass);

        [DllImport(@"kernel32.dll", EntryPoint = @"GetPriorityClass", SetLastError = true)]
        private static extern uint getPriorityClass(IntPtr hProcess);

        [DllImport(@"kernel32.dll", EntryPoint = @"SetProcessInformation", SetLastError = true)]
        private static extern bool setProcessInformation(IntPtr hProcess, ProcessInformationClass processInformationClass, ref ProcessPowerThrottlingState processInformation, uint processInformationSize);

        [DllImport(@"user32.dll", EntryPoint = @"SetWindowPos", SetLastError = true)]
        private static extern bool setWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport(@"user32.dll", EntryPoint = @"EnumWindows", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool enumWindows(EnumWindowsProc enumFunc, IntPtr lParam);

        [DllImport(@"user32.dll", EntryPoint = @"GetWindowThreadProcessId")]
        private static extern uint getWindowThreadProcessId(IntPtr hWnd, out uint processId);

        [DllImport(@"user32.dll", EntryPoint = @"IsWindow")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool isWindow(IntPtr hWnd);

        [DllImport(@"user32.dll", EntryPoint = @"IsWindowVisible")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool isWindowVisible(IntPtr hWnd);

        [DllImport(@"avrt.dll", EntryPoint = @"AvSetMmThreadCharacteristicsW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr avSetMmThreadCharacteristics(string taskName, out uint taskIndex);

        [DllImport(@"avrt.dll", EntryPoint = @"AvSetMmThreadPriority", SetLastError = true)]
        private static extern bool avSetMmThreadPriority(IntPtr avrtHandle, AvrtPriority priority);

        [DllImport(@"avrt.dll", EntryPoint = @"AvRevertMmThreadCharacteristics", SetLastError = true)]
        private static extern bool avRevertMmThreadCharacteristics(IntPtr avrtHandle);

        [DllImport(@"kernel32.dll", EntryPoint = @"SetThreadInformation", SetLastError = true)]
        private static extern bool setThreadInformation(IntPtr hThread, ThreadInformationClass threadInformationClass,
            ref ThreadPowerThrottlingState threadInformation, uint threadInformationSize);

        [DllImport(@"kernel32.dll", EntryPoint = @"GetCurrentThread")]
        private static extern IntPtr getCurrentThread();

        [DllImport(@"kernel32.dll", EntryPoint = @"SetThreadAffinityMask", SetLastError = true)]
        private static extern nuint setThreadAffinityMask(IntPtr hThread, nuint dwThreadAffinityMask);

        [DllImport(@"kernel32.dll", EntryPoint = @"GetLogicalProcessorInformationEx", SetLastError = true)]
        private static extern bool getLogicalProcessorInformationEx(
            LogicalProcessorRelationship relationshipType, IntPtr buffer, ref uint returnedLength);
    }
}
