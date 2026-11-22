using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using ManagedBass;
using osu.Framework.Logging;
using SDL;

namespace osu.Framework.Threading
{
    /// <summary>
    /// Experimental output: BASS decodes/mixes to a float decode stream; SDL3 owns playback.
    /// SDL requests decoded audio on demand, independently of the game audio update cadence.
    /// </summary>
    internal unsafe sealed class LinuxSdlAudioOutput : IDisposable
    {
        private SDL_AudioStream* stream;
        private int mixer;
        private bool ownsAudioSubsystem;
        private GCHandle callbackHandle;
        private bool priorityRequested;
        private IntPtr nativeContext;
        private IntPtr nativeBridge;
        private IntPtr nativeBass;
        private IntPtr nativeSdl;

        internal bool NativeCallbackActive => nativeContext != IntPtr.Zero;
        private const int bytes_per_frame = 2 * sizeof(float);
        private const int frames_per_chunk = 128;

        public bool Start(int mixerHandle)
        {
            if (!SDL3.SDL_InitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO))
            {
                Logger.Log($"SDL audio init failed: {SDL3.SDL_GetError()}", level: LogLevel.Important);
                return false;
            }

            ownsAudioSubsystem = true;
            SDL3.SDL_SetHint(SDL3.SDL_HINT_AUDIO_DEVICE_SAMPLE_FRAMES, "128"u8);
            var spec = new SDL_AudioSpec { format = SDL3.SDL_AUDIO_F32, channels = 2, freq = 44100 };
            stream = SDL3.SDL_OpenAudioDeviceStream(SDL3.SDL_AUDIO_DEVICE_DEFAULT_PLAYBACK, &spec, null, IntPtr.Zero);
            SDL3.SDL_ResetHint(SDL3.SDL_HINT_AUDIO_DEVICE_SAMPLE_FRAMES);
            if (stream == null)
            {
                Logger.Log($"SDL audio open failed: {SDL3.SDL_GetError()}", level: LogLevel.Important);
                Dispose();
                return false;
            }

            mixer = mixerHandle;
            if (!trySetNativeCallback())
            {
                Logger.Log("Native SDL audio callback unavailable; using managed callback", level: LogLevel.Important);
                callbackHandle = GCHandle.Alloc(this);
            }
            if (nativeContext == IntPtr.Zero && !SDL3.SDL_SetAudioStreamGetCallback(stream, &onAudioRequested, GCHandle.ToIntPtr(callbackHandle)))
            {
                Dispose();
                return false;
            }
            if (!SDL3.SDL_ResumeAudioStreamDevice(stream))
            {
                Logger.Log($"SDL audio start failed: {SDL3.SDL_GetError()}", level: LogLevel.Important);
                Dispose();
                return false;
            }

            Logger.Log($"SDL3 Linux output started: driver={SDL3.SDL_GetCurrentAudioDriver()}, BASS decode mixer, 44100 Hz stereo float", level: LogLevel.Important);
            return true;
        }

        private bool trySetNativeCallback()
        {
            bool installed = false;
            try
            {
                nativeBridge = NativeLibrary.Load(System.IO.Path.Combine(AppContext.BaseDirectory, "libmosu_audio_bridge.so"));
                nativeBass = loadNativeDependency("libbass.so");
                nativeSdl = loadNativeDependency("libSDL3.so");
                var create = (delegate* unmanaged[Cdecl]<uint, IntPtr, IntPtr, IntPtr, IntPtr>)NativeLibrary.GetExport(nativeBridge, "mosu_audio_create");
                nativeContext = create((uint)mixer, NativeLibrary.GetExport(nativeBass, "BASS_SetDevice"),
                    NativeLibrary.GetExport(nativeBass, "BASS_ChannelGetData"), NativeLibrary.GetExport(nativeSdl, "SDL_PutAudioStreamData"));
                if (nativeContext == IntPtr.Zero)
                    return false;
                var callback = (delegate* unmanaged[Cdecl]<IntPtr, SDL_AudioStream*, int, int, void>)NativeLibrary.GetExport(nativeBridge, "mosu_audio_supply");
                if (!SDL3.SDL_SetAudioStreamGetCallback(stream, callback, nativeContext))
                    return false;
                installed = true;
                Logger.Log("SDL audio uses native BASS callback (independent of managed GC suspension)", level: LogLevel.Important);
                return true;
            }
            catch (Exception e) when (e is DllNotFoundException || e is EntryPointNotFoundException || e is BadImageFormatException)
            {
                Logger.Log($"Native SDL audio callback setup failed: {e.Message}", level: LogLevel.Important);
                return false;
            }
            finally
            {
                // Failed setup must not leave a context which suppresses the fallback.
                if (!installed)
                    freeNativeCallback();
            }
        }

        private static IntPtr loadNativeDependency(string fileName)
        {
            // RID-specific publish flattens native assets beside the executable.
            // A portable local build keeps them under runtimes/<rid>/native instead.
            string path = System.IO.Path.Combine(AppContext.BaseDirectory, fileName);
            if (!System.IO.File.Exists(path))
            {
                path = System.IO.Path.Combine(AppContext.BaseDirectory, "runtimes",
                    RuntimeInformation.ProcessArchitecture == Architecture.Arm64 ? "linux-arm64" : "linux-x64", "native", fileName);
            }

            return NativeLibrary.Load(path);
        }

        private void freeNativeCallback()
        {
            if (nativeContext != IntPtr.Zero)
            {
                var destroy = (delegate* unmanaged[Cdecl]<IntPtr, void>)NativeLibrary.GetExport(nativeBridge, "mosu_audio_destroy");
                destroy(nativeContext);
                nativeContext = IntPtr.Zero;
            }
            if (nativeSdl != IntPtr.Zero) NativeLibrary.Free(nativeSdl);
            if (nativeBass != IntPtr.Zero) NativeLibrary.Free(nativeBass);
            if (nativeBridge != IntPtr.Zero) NativeLibrary.Free(nativeBridge);
            nativeSdl = nativeBass = nativeBridge = IntPtr.Zero;
        }

        public void Suspend()
        {
            if (stream != null)
                SDL3.SDL_PauseAudioStreamDevice(stream);
        }

        [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
        private static void onAudioRequested(IntPtr userdata, SDL_AudioStream* audioStream, int additionalAmount, int totalAmount)
        {
            // No game-manager locks, scheduler work or logging on the playback thread.
            // SDL serialises this callback with callback removal during disposal.
            try
            {
                var output = (LinuxSdlAudioOutput)GCHandle.FromIntPtr(userdata).Target!;
                output.supplyAudio(audioStream, additionalAmount);
            }
            catch
            {
                // Managed exceptions must never cross the native callback boundary.
            }
        }

        private void supplyAudio(SDL_AudioStream* audioStream, int additionalAmount)
        {
            if (additionalAmount <= 0 || mixer == 0)
                return;

            if (!priorityRequested)
            {
                priorityRequested = true;
                // SDL uses the platform's supported priority mechanism (including RTKit
                // where available). Failure is harmless and must not stop playback.
                SDL3.SDL_SetCurrentThreadPriority(SDL_ThreadPriority.SDL_THREAD_PRIORITY_TIME_CRITICAL);
            }

            Bass.CurrentDevice = Bass.NoSoundDevice;
            byte* data = stackalloc byte[frames_per_chunk * bytes_per_frame];
            while (additionalAmount > 0)
            {
                int length = Math.Min(additionalAmount, frames_per_chunk * bytes_per_frame);
                // Decode whole stereo frames, without queuing extra periods of latency.
                length = (length + bytes_per_frame - 1) / bytes_per_frame * bytes_per_frame;
                int read = Bass.ChannelGetData(mixer, (IntPtr)data, length);
                if (read < 0)
                    read = 0;
                if (read < length)
                    new Span<byte>(data + read, length - read).Clear();

                if (!SDL3.SDL_PutAudioStreamData(audioStream, (IntPtr)data, length))
                    break;
                additionalAmount -= length;
            }
        }

        public void Dispose()
        {
            if (stream != null)
            {
                // Wait for an in-flight callback before releasing its target or BASS mixer.
                SDL3.SDL_SetAudioStreamGetCallback(stream, null, IntPtr.Zero);
                SDL3.SDL_DestroyAudioStream(stream);
                stream = null;
            }

            freeNativeCallback();
            if (callbackHandle.IsAllocated)
                callbackHandle.Free();
            mixer = 0;
            priorityRequested = false;
            if (ownsAudioSubsystem)
            {
                SDL3.SDL_QuitSubSystem(SDL_InitFlags.SDL_INIT_AUDIO);
                ownsAudioSubsystem = false;
            }
        }
    }
}
