// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Statistics;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using ManagedBass;
using ManagedBass.Mix;
using ManagedBass.Wasapi;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Bindables;
using osu.Framework.Development;
using osu.Framework.Logging;
using osu.Framework.Platform.Linux.Native;

namespace osu.Framework.Threading
{
    public class AudioThread : GameThread
    {
        public AudioThread()
            : this(Environment.GetCommandLineArgs().Any(arg => arg.StartsWith("--render-replay", StringComparison.Ordinal)))
        {
        }

        internal AudioThread(bool offlineRendering)
            : base(name: "Audio")
        {
            IsOfflineRendering = offlineRendering;
            OnNewFrame += onNewFrame;
            PreloadBass();
        }

        internal bool IsOfflineRendering { get; }

        public override bool IsCurrent => ThreadSafety.IsAudioThread;

        internal sealed override void MakeCurrent()
        {
            base.MakeCurrent();

            ThreadSafety.IsAudioThread = true;
        }

        internal override IEnumerable<StatisticsCounterType> StatisticsCounters => new[]
        {
            StatisticsCounterType.TasksRun,
            StatisticsCounterType.Tracks,
            StatisticsCounterType.Samples,
            StatisticsCounterType.SChannels,
            StatisticsCounterType.Components,
            StatisticsCounterType.MixChannels,
        };

        private readonly List<AudioManager> managers = new List<AudioManager>();

        private static readonly HashSet<int> initialised_devices = new HashSet<int>();

        private static readonly GlobalStatistic<double> cpu_usage = GlobalStatistics.Get<double>("Audio", "Bass CPU%");

        private long frameCount;

        private void onNewFrame()
        {
            if (frameCount++ % 1000 == 0)
                cpu_usage.Value = Bass.CPUUsage;

            lock (managers)
            {
                for (int i = 0; i < managers.Count; i++)
                {
                    var m = managers[i];
                    m.Update();
                }
            }

            updateOutputLatency();
        }

        internal void RegisterManager(AudioManager manager)
        {
            lock (managers)
            {
                if (managers.Contains(manager))
                    throw new InvalidOperationException($"{manager} was already registered");

                managers.Add(manager);
            }

            manager.GlobalMixerHandle.BindTo(globalMixerHandle);
        }

        internal void UnregisterManager(AudioManager manager)
        {
            lock (managers)
                managers.Remove(manager);

            manager.GlobalMixerHandle.UnbindFrom(globalMixerHandle);
        }

        protected override void OnExit()
        {
            base.OnExit();

            lock (managers)
            {
                // AudioManagers are iterated over backwards since disposal will unregister and remove them from the list.
                for (int i = managers.Count - 1; i >= 0; i--)
                {
                    var m = managers[i];

                    m.Dispose();

                    // Audio component disposal (including the AudioManager itself) is scheduled and only runs when the AudioThread updates.
                    // But the AudioThread won't run another update since it's exiting, so an update must be performed manually in order to finish the disposal.
                    m.Update();
                }

                managers.Clear();
            }

            // Safety net to ensure we have freed all devices before exiting.
            // This is mainly required for device-lost scenarios.
            // See https://github.com/ppy/osu-framework/pull/3378 for further discussion.
            foreach (int d in initialised_devices.ToArray())
                FreeDevice(d);
        }

        #region BASS Initialisation

        // TODO: All this bass init stuff should probably not be in this class.

        private WasapiNotifyProcedure? wasapiNotifyProcedure;
        private LinuxSdlAudioOutput? linuxSdlOutput;

        internal bool LinuxSdlOutputActive => linuxSdlOutput != null;

        internal void SuspendLinuxSdlOutput() => linuxSdlOutput?.Suspend();

        /// <summary>
        /// If a global mixer is being used, this will be the BASS handle for it.
        /// If non-null, all game mixers should be added to this mixer.
        /// </summary>
        private readonly Bindable<int?> globalMixerHandle = new Bindable<int?>();

        /// <summary>
        /// Output latency of the initialised device in milliseconds. Zero if unknown.
        /// </summary>
        internal readonly Bindable<double> OutputLatency = new Bindable<double>();

        private bool wasapiExclusiveActive;

        // Native stream cleanup can overwrite BASS's thread-local error code.
        internal Errors? WasapiInitialisationError { get; private set; }

        internal virtual bool InitDevice(int deviceId, bool useExperimentalWasapi, bool exclusiveWasapi = false, bool useLinuxSdlOutput = false)
        {
            Debug.Assert(ThreadSafety.IsAudioThread);
            Trace.Assert(deviceId != -1); // The real device ID should always be used, as the -1 device has special cases which are hard to work with.
            if (IsOfflineRendering && deviceId != Bass.NoSoundDevice)
                return false;

            // Settings may be carried across platforms. Never treat a successful native
            // BASS initialisation as a failed WASAPI setup on macOS or Linux.
            useExperimentalWasapi &= OperatingSystem.IsWindows() && deviceId != Bass.NoSoundDevice;
            WasapiInitialisationError = null;

            // BASS cannot reinitialise an endpoint while our own exclusive WASAPI session
            // still owns it. Shared sessions are intentionally left alone until after Bass.Init.
            if (wasapiExclusiveActive || linuxSdlOutput != null)
                freeWasapi();

            // Try to initialise the device, or request a re-initialise.
            if (!BassTrackPreparation.ChangeDevice(() => Bass.Init(deviceId, Flags: (DeviceInitFlags)128))) // 128 == BASS_DEVICE_REINIT
                return false;

            if (IsOfflineRendering)
            {
                freeWasapi();
                int mixer = BassMix.CreateMixerStream(44100, 2, BassFlags.MixerNonStop | BassFlags.Decode | BassFlags.Float);
                if (mixer == 0)
                    return false;

                globalMixerHandle.Value = mixer;
                Logger.Log("Initialised standalone global decode mixer stream for offline capture");
            }
            else if (useLinuxSdlOutput && RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
            {
                freeWasapi();
                int decodeMixer = BassMix.CreateMixerStream(44100, 2, BassFlags.MixerNonStop | BassFlags.Decode | BassFlags.Float);
                if (decodeMixer != 0)
                {
                    var output = new LinuxSdlAudioOutput();
                    if (output.Start(decodeMixer))
                    {
                        linuxSdlOutput = output;
                        globalMixerHandle.Value = decodeMixer;
                    }
                    else
                    {
                        Bass.StreamFree(decodeMixer);
                    }
                }

                if (linuxSdlOutput == null)
                {
                    // BASS no-sound output is not usable as an audio fallback.
                    // The caller will switch back to the ordinary device.
                    Bass.GetDeviceInfo(0, out _);
                    return false;
                }
            }
            else if (useExperimentalWasapi)
            {
                if (!attemptWasapiInitialisation(exclusiveWasapi))
                    return false;
            }
            else
                freeWasapi();

            initialised_devices.Add(deviceId);

            smoothedLatency = 0;
            lastLatencyUpdate = 0;

            // Legacy BASS output sits behind a shared Windows session which BASS does not
            // include in its latency value. Two device periods approximate that final leg.
            legacySessionEstimateMs = 0;

            if (!IsOfflineRendering && !useExperimentalWasapi && RuntimeInfo.OS == RuntimeInfo.Platform.Windows)
            {
                try
                {
                    int wasapiDevice = findWasapiDevice();

                    if (wasapiDevice >= 0 && BassWasapi.GetDeviceInfo(wasapiDevice, out WasapiDeviceInfo info) && info.DefaultUpdatePeriod > 0)
                        legacySessionEstimateMs = info.DefaultUpdatePeriod * 2 * 1000;
                }
                catch
                {
                }
            }

            // Latency probing and device enumeration may leave a harmless BASS error behind.
            // AudioManager reads LastError immediately after this method, so clear it with a
            // guaranteed-success query.
            Bass.GetDeviceInfo(0, out _);
            return true;
        }

        private double lastLatencyUpdate;
        private double smoothedLatency;
        private double legacySessionEstimateMs;

        /// <summary>
        /// Whether an accumulated exclusive WASAPI queue may be reset right now.
        /// Gameplay disables this because resetting the queue can produce a short audio skip.
        /// </summary>
        public readonly BindableBool AllowLatencyRepair = new BindableBool(true);

        private double latencyFloor;
        private double lastQueueFlush;
        private int creepSamples;
        private int failedFlushes;

        private const double creep_threshold_ms = 8;
        private const int creep_samples_needed = 10;
        private const double flush_cooldown_ms = 15000;
        private const int max_failed_flushes = 3;
        private const double pre_gameplay_flush_margin_ms = 2;

        private void updateOutputLatency()
        {
            if (Clock.CurrentTime - lastLatencyUpdate < 200)
                return;

            lastLatencyUpdate = Clock.CurrentTime;

            // BASSWASAPI latency queries are Windows-only. Calling them elsewhere can leave
            // a stale error code which is later reported as an unrelated audio fault.
            if (IsOfflineRendering || RuntimeInfo.OS != RuntimeInfo.Platform.Windows || initialised_devices.Count == 0)
                return;

            double latency = 0;

            try
            {
                if (globalMixerHandle.Value != null && BassWasapi.GetInfo(out WasapiInfo info) && info.Frequency > 0)
                {
                    int queuedBytes = BassWasapi.GetData(IntPtr.Zero, (int)DataFlags.Available);

                    if (queuedBytes > 0)
                    {
                        int bytesPerSample = info.Format switch
                        {
                            WasapiFormat.Float => 4,
                            WasapiFormat.Bit32 => 4,
                            WasapiFormat.Bit24 => 3,
                            WasapiFormat.Bit16 => 2,
                            WasapiFormat.Bit8 => 1,
                            _ => 2,
                        };

                        latency = queuedBytes / ((double)info.Frequency * Math.Max(1, info.Channels) * bytesPerSample) * 1000;
                    }
                }
                else if (Bass.GetInfo(out BassInfo bassInfo))
                {
                    latency = Math.Max(0, bassInfo.Latency)
                              + Math.Max(0, Bass.DeviceBufferLength)
                              + Math.Max(0, Bass.UpdatePeriod)
                              + legacySessionEstimateMs;
                }
            }
            catch
            {
                latency = 0;
            }

            Bass.GetDeviceInfo(0, out _);

            smoothedLatency = smoothedLatency > 0 && latency > 0
                ? smoothedLatency * 0.7 + latency * 0.3
                : latency;
            OutputLatency.Value = smoothedLatency;

            if (wasapiExclusiveActive)
                keepExclusiveQueueTight();
        }

        /// <summary>
        /// Resets an inflated exclusive output queue at a caller-selected safe point.
        /// </summary>
        public void FlushExclusiveQueueNow()
        {
            Scheduler.Add(() =>
            {
                if (!wasapiExclusiveActive || latencyFloor <= 0 || smoothedLatency <= 0)
                    return;

                if (smoothedLatency < latencyFloor + pre_gameplay_flush_margin_ms)
                    return;

                lastQueueFlush = Clock.CurrentTime;
                creepSamples = 0;

                if (!BassWasapi.Stop(true))
                    return;

                BassWasapi.Start();
                smoothedLatency = 0;
            });
        }

        private void keepExclusiveQueueTight()
        {
            if (smoothedLatency <= 0)
                return;

            // The best observed value is the device-specific floor to which repairs return.
            if (latencyFloor <= 0 || smoothedLatency < latencyFloor)
            {
                latencyFloor = smoothedLatency;
                creepSamples = 0;
                return;
            }

            if (smoothedLatency < latencyFloor + creep_threshold_ms)
            {
                creepSamples = 0;
                failedFlushes = 0;
                return;
            }

            if (++creepSamples < creep_samples_needed)
                return;

            if (!AllowLatencyRepair.Value)
            {
                creepSamples = creep_samples_needed;
                return;
            }

            // If three resets cannot restore the old floor, accept the new stable value
            // instead of repeatedly interrupting audio on a changed device or format.
            if (failedFlushes >= max_failed_flushes)
            {
                latencyFloor = smoothedLatency;
                creepSamples = 0;
                failedFlushes = 0;
                return;
            }

            if (Clock.CurrentTime - lastQueueFlush < flush_cooldown_ms)
                return;

            lastQueueFlush = Clock.CurrentTime;
            creepSamples = 0;
            failedFlushes++;

            if (!BassWasapi.Stop(true))
                return;

            BassWasapi.Start();
            smoothedLatency = 0;
        }

        internal void FreeDevice(int deviceId)
        {
            Debug.Assert(ThreadSafety.IsAudioThread);

            int selectedDevice = Bass.CurrentDevice;

            // Stop SDL decoding before freeing a device which may own its mixer.
            freeWasapi();

            if (canSelectDevice(deviceId))
            {
                Bass.CurrentDevice = deviceId;
                BassTrackPreparation.ChangeDevice(Bass.Free);
            }

            if (selectedDevice != deviceId && canSelectDevice(selectedDevice))
                Bass.CurrentDevice = selectedDevice;

            initialised_devices.Remove(deviceId);

            static bool canSelectDevice(int deviceId) => Bass.GetDeviceInfo(deviceId, out var deviceInfo) && deviceInfo.IsInitialized;
        }

        /// <summary>
        /// Makes BASS available to be consumed.
        /// </summary>
        internal static void PreloadBass()
        {
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Linux)
            {
                // required for the time being to address libbass_fx.so load failures (see https://github.com/ppy/osu/issues/2852)
                Library.Load("libbass.so", Library.LoadFlags.RTLD_LAZY | Library.LoadFlags.RTLD_GLOBAL);
            }
        }

        private bool attemptWasapiInitialisation(bool exclusive)
        {
            if (RuntimeInfo.OS != RuntimeInfo.Platform.Windows)
            {
                freeWasapi();
                return false;
            }

            Logger.Log("Attempting local BassWasapi initialisation");

            int wasapiDevice = findWasapiDevice();

            // Keep at most one live WASAPI endpoint.
            freeWasapi();

            if (wasapiDevice < 0)
            {
                WasapiInitialisationError = Errors.Device;
                return false;
            }

            // Audio drivers are third-party code running on the audio thread. Treat an
            // initialisation exception as an ordinary failure so the caller can fall back.
            try
            {
                return initWasapi(wasapiDevice, exclusive);
            }
            catch (Exception e)
            {
                Logger.Log($"BassWasapi initialisation threw for device {wasapiDevice}: {e.Message}", level: LogLevel.Error);

                try
                {
                    freeWasapi();
                }
                catch
                {
                }

                return false;
            }
        }

        private bool initWasapi(int wasapiDevice, bool exclusive)
        {
            // Only device-change notifications enter managed code, not audio rendering.
            wasapiNotifyProcedure = (notify, device, _) => Scheduler.Add(() =>
            {
                if (notify == WasapiNotificationType.DefaultOutput)
                {
                    freeWasapi();
                    initWasapi(device, exclusive);
                }
            });

            bool initialised = exclusive
                ? initWasapiExclusive(wasapiDevice)
                : initWasapiShared(wasapiDevice);

            if (!initialised)
                return false;

            if (!BassWasapi.Start())
            {
                WasapiInitialisationError = Bass.LastError;
                freeWasapi();
                return false;
            }

            BassWasapi.SetNotify(wasapiNotifyProcedure);
            Logger.Log("WASAPI output uses native BASS mixer (no managed output callback)", level: LogLevel.Important);
            return true;
        }

        /// <summary>
        /// Finds the WASAPI endpoint corresponding to the current BASS device.
        /// </summary>
        private static int findWasapiDevice()
        {
            int bassDevice = Bass.CurrentDevice;
            if (bassDevice <= Bass.NoSoundDevice || !Bass.GetDeviceInfo(bassDevice, out var info))
                return -1;

            // BASS's virtual "Default" device has no driver ID. Resolve the real
            // default output before asking for its mix format: unlike Init, WASAPI
            // GetDeviceInfo does not accept the -1 default-device sentinel.
            if (string.IsNullOrEmpty(info.Driver) && bassDevice != 1)
                return -1;

            return FindWasapiDevice(info.Driver, BassWasapi.GetDeviceInfo);
        }

        internal delegate bool WasapiDeviceInfoLookup(int device, out WasapiDeviceInfo info);

        internal static int FindWasapiDevice(string? driver, WasapiDeviceInfoLookup getDeviceInfo)
        {
            // Enumeration ends on the first missing index. Keep a safety bound for
            // broken drivers, but never return that missing index as a valid device.
            for (int device = 0; device < 16384; device++)
            {
                if (!getDeviceInfo(device, out var info))
                    break;

                if (!info.IsEnabled || info.IsInput || info.IsLoopback)
                    continue;

                if (string.IsNullOrEmpty(driver) ? info.IsDefault : info.ID == driver)
                    return device;
            }

            return -1;
        }

        private bool tryGetWasapiDeviceInfo(ref int device, out WasapiDeviceInfo info)
        {
            if (device == BassWasapi.DefaultDevice)
                device = FindWasapiDevice(null, BassWasapi.GetDeviceInfo);

            info = default;
            if (device < 0)
            {
                WasapiInitialisationError = Errors.Device;
                return false;
            }

            if (!BassWasapi.GetDeviceInfo(device, out info))
            {
                WasapiInitialisationError = Bass.LastError;
                return false;
            }

            if (!info.IsEnabled || info.IsInput || info.IsLoopback)
            {
                WasapiInitialisationError = Errors.Device;
                return false;
            }

            return true;
        }

        private bool initWasapiShared(int wasapiDevice)
        {
            if (!tryGetWasapiDeviceInfo(ref wasapiDevice, out WasapiDeviceInfo info))
                return false;

            bool initialised = initWasapiMixer(wasapiDevice, info.MixFrequency, info.MixChannels,
                WasapiInitFlags.EventDriven | WasapiInitFlags.AutoFormat, float.Epsilon);

            // If low-latency shared initialisation fails, retry the same endpoint
            // at its default period before abandoning WASAPI altogether.
            if (!initialised && ShouldRetrySharedWasapi(WasapiInitialisationError ?? Bass.LastError))
            {
                Logger.Log($"BASS WASAPI device {wasapiDevice} failed with the minimum shared period ({WasapiInitialisationError}); retrying the default period");
                initialised = initWasapiMixer(wasapiDevice, info.MixFrequency, info.MixChannels,
                    WasapiInitFlags.EventDriven | WasapiInitFlags.AutoFormat, 0f);
            }

            Logger.Log($"Initialising BassWasapi for device {wasapiDevice} (shared)...{(initialised ? "success!" : $"FAILED ({WasapiInitialisationError ?? Bass.LastError})")}");
            return initialised;
        }

        internal static bool ShouldRetrySharedWasapi(Errors error) => error is Errors.Unknown or Errors.NotAvailable or Errors.Parameter;

        private bool initWasapiExclusive(int wasapiDevice)
        {
            if (!tryGetWasapiDeviceInfo(ref wasapiDevice, out WasapiDeviceInfo info))
                return false;

            var formats = new List<(int frequency, int channels)>();

            if (info.MixFrequency > 0 && info.MixChannels > 0)
                formats.Add((info.MixFrequency, info.MixChannels));

            foreach (int rate in new[] { 48000, 44100, 96000, 192000 })
            {
                formats.Add((rate, 2));

                if (info.MixChannels > 2)
                    formats.Add((rate, info.MixChannels));
            }

            float[] periods = info.MinimumUpdatePeriod > 0
                ? new[] { (float)info.MinimumUpdatePeriod, (float)info.DefaultUpdatePeriod, 0f }
                : new[] { 0f };

            // Prefer the framework's native floating-point path. Integer-only devices
            // remain supported with dithering to avoid audible quantisation artefacts.
            foreach (bool floatOnly in new[] { true, false })
            {
                foreach ((int frequency, int channels) in formats)
                {
                    WasapiFormat format = BassWasapi.CheckFormat(wasapiDevice, frequency, channels, WasapiInitFlags.Exclusive);

                    if (format == WasapiFormat.Unknown || floatOnly && format != WasapiFormat.Float)
                        continue;

                    // Keep one buffer prepared ahead of the device. If the process stalls long
                    // enough to underrun, BASSWASAPI will submit silence instead of leaving the
                    // driver to repeat the last rendered fragment.
                    var flags = WasapiInitFlags.EventDriven | WasapiInitFlags.Exclusive | WasapiInitFlags.Async;

                    if (format != WasapiFormat.Float)
                        flags |= WasapiInitFlags.Dither;

                    foreach (float period in periods)
                    {
                        // Let the driver choose its native buffer size. Forcing a smaller
                        // value can paradoxically increase the effective queue on some devices.
                        if (initWasapiMixer(wasapiDevice, frequency, channels, flags, period))
                        {
                            wasapiExclusiveActive = true;
                            Logger.Log($"Initialising BassWasapi for device {wasapiDevice} (exclusive {frequency}hz {channels}ch {format}, period {period}s)...success!");
                            return true;
                        }

                        // Occupancy is independent of the sample format. Do not overwrite
                        // Busy with a later format-probing error and disable the backend.
                        if (WasapiInitialisationError == Errors.Busy)
                            return false;
                    }
                }
            }

            string reason = (WasapiInitialisationError ?? Bass.LastError) switch
            {
                Errors.Busy => @"the device is in use by another application (Busy)",
                Errors.NotAvailable => @"Windows is blocking exclusive access (NotAvailable)",
                _ => $@"no supported format ({WasapiInitialisationError ?? Bass.LastError})",
            };

            Logger.Log($"Initialising BassWasapi for device {wasapiDevice} (exclusive)...FAILED, {reason}", level: LogLevel.Important);
            return false;
        }

        private bool initWasapiMixer(int device, int frequency, int channels, WasapiInitFlags flags, float period)
        {
            WasapiInitialisationError = null;
            if (frequency <= 0 || channels <= 0)
            {
                WasapiInitialisationError = Errors.SampleFormat;
                return false;
            }

            // WASAPIPROC_BASS pulls directly from this float decoding channel on the
            // native output thread. A managed delegate here would wait for .NET GC.
            int mixer = BassMix.CreateMixerStream(frequency, channels, BassFlags.MixerNonStop | BassFlags.Decode | BassFlags.Float);
            if (mixer == 0)
            {
                WasapiInitialisationError = Bass.LastError;
                return false;
            }

            bool initialised = false;
            try
            {
                initialised = BassWasapi.InitEx(device, Flags: flags, Buffer: 0f, Period: period,
                    Procedure: BassWasapi.WasapiProc_Bass, User: new IntPtr(mixer));
                WasapiInitialisationError = initialised ? null : Bass.LastError;
                if (!initialised)
                    Logger.Log($"Native WASAPI mixer initialisation failed: {WasapiInitialisationError}");
            }
            finally
            {
                if (!initialised)
                    Bass.StreamFree(mixer);
            }

            if (initialised)
                globalMixerHandle.Value = mixer;

            return initialised;
        }

        private void freeWasapi()
        {
            wasapiExclusiveActive = false;
            latencyFloor = 0;
            smoothedLatency = 0;
            creepSamples = 0;
            failedFlushes = 0;

            linuxSdlOutput?.Dispose();
            linuxSdlOutput = null;

            if (globalMixerHandle.Value == null) return;

            // Stop the native consumer before releasing the mixer it reads from.
            if (RuntimeInfo.OS == RuntimeInfo.Platform.Windows && !IsOfflineRendering)
            {
                BassWasapi.Stop();
                BassWasapi.Free();
            }
            Bass.StreamFree(globalMixerHandle.Value.Value);
            globalMixerHandle.Value = null;
        }

        #endregion
    }
}
