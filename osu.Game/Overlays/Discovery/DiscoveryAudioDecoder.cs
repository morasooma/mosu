using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using ManagedBass;

namespace osu.Game.Overlays.Discovery
{
    /// <summary>Private decoding-only BASS streams. Never touches a playing channel, device or volume.</summary>
    internal static class DiscoveryAudioDecoder
    {
        internal static float[][] Decode(byte[] audio, CancellationToken token)
        {
            if (audio.Length is < 16 or > 2 * 1024 * 1024) throw new InvalidDataException("Audio preview exceeds 2 MiB.");
            var pinned = GCHandle.Alloc(audio, GCHandleType.Pinned);
            int handle = 0;
            try
            {
                handle = Bass.CreateStream(pinned.AddrOfPinnedObject(), 0, audio.Length, BassFlags.Decode | BassFlags.Float | BassFlags.Prescan);
                if (handle == 0) throw new InvalidDataException("Cannot decode audio preview: " + Bass.LastError);
                var info = Bass.ChannelGetInfo(handle);
                if (info.Frequency is < 8000 or > 192000 || info.Channels is < 1 or > 8) throw new InvalidDataException("Unsupported preview format.");
                // Only official-preview view: exactly the same first 30 seconds used for seed and candidate.
                int framesLimit = checked(info.Frequency * 30);
                var mono = new List<float>(framesLimit);
                var buffer = new float[4096 * info.Channels];
                while (mono.Count < framesLimit)
                {
                    token.ThrowIfCancellationRequested();
                    int read = Bass.ChannelGetData(handle, buffer, buffer.Length * sizeof(float));
                    if (read <= 0)
                    {
                        if (read < 0 && Bass.LastError != Errors.Ended) throw new InvalidDataException("Preview decoding failed.");
                        break;
                    }
                    int samples = read / sizeof(float);
                    for (int i = 0; i + info.Channels <= samples && mono.Count < framesLimit; i += info.Channels)
                    {
                        double value = 0;
                        for (int c = 0; c < info.Channels; c++) value += buffer[i + c];
                        mono.Add((float)(value / info.Channels));
                    }
                }
                return Windows(Resample(mono.ToArray(), info.Frequency), token);
            }
            finally
            {
                if (handle != 0) Bass.StreamFree(handle);
                pinned.Free();
            }
        }

        internal static float[] Resample(float[] mono, int rate)
        {
            if (rate == DiscoveryAudioModel.SampleRate) return mono;
            int count = (int)((long)mono.Length * DiscoveryAudioModel.SampleRate / rate);
            var result = new float[count];
            // Windowed sinc with anti-alias cutoff; no external ffmpeg or Python process.
            double ratio = rate / (double)DiscoveryAudioModel.SampleRate;
            double cutoff = Math.Min(1, 1 / ratio) * .94;
            const int radius = 24;
            for (int i = 0; i < count; i++)
            {
                double at = i * ratio;
                int centre = (int)at;
                double sum = 0, weights = 0;
                for (int j = centre - radius + 1; j <= centre + radius; j++)
                {
                    if (j < 0 || j >= mono.Length) continue;
                    double x = at - j;
                    double sinc = Math.Abs(x) < 1e-12 ? cutoff : Math.Sin(Math.PI * cutoff * x) / (Math.PI * x);
                    double w = sinc * (.5 + .5 * Math.Cos(Math.PI * x / radius));
                    sum += mono[j] * w; weights += w;
                }
                result[i] = (float)(sum / weights);
            }
            return result;
        }

        internal static float[][] Windows(float[] mono, CancellationToken token)
        {
            int size = DiscoveryAudioModel.Window;
            if (mono.Length < DiscoveryAudioModel.SampleRate || mono.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Preview shorter than one second or malformed.");
            if (mono.Length < size)
            {
                // Match CLAP repeatpad: whole repeats followed by zero padding.
                var padded = new float[size];
                for (int repeat = 0; repeat < size / mono.Length; repeat++) Array.Copy(mono, 0, padded, repeat * mono.Length, mono.Length);
                mono = padded;
            }
            var result = new float[3][];
            for (int window = 0; window < result.Length; window++)
            {
                token.ThrowIfCancellationRequested();
                int start = (mono.Length - size) * window / 2;
                var values = result[window] = new float[size];
                Array.Copy(mono, start, values, 0, size);
                if (values.Sum(v => (double)v * v) / size < 1e-12) throw new InvalidDataException("Silent audio preview.");
                // CLAP expects waveform amplitudes, NOT MERT's zero-mean/unit-variance normalisation.
            }
            return result;
        }
    }
}
