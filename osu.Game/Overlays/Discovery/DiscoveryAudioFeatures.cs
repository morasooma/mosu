using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using MathNet.Numerics.IntegralTransforms;

namespace osu.Game.Overlays.Discovery
{
    /// <summary>CLAP's published 48 kHz, periodic Hann, reflected STFT, Slaney log-mel preprocessing.</summary>
    internal static class DiscoveryAudioFeatures
    {
        private static readonly double[] mel = LoadFilters();
        private static readonly double[] hann = Enumerable.Range(0, 1024).Select(i => .5 - .5 * Math.Cos(2 * Math.PI * i / 1024)).ToArray();

        private static double[] LoadFilters()
        {
            using var stream = typeof(DiscoveryAudioFeatures).Assembly.GetManifestResourceStream("osu.Game.Resources.AI.clap-music-slaney.f64")
                ?? throw new InvalidDataException("Missing CLAP mel filter bank.");
            using var reader = new BinaryReader(stream);
            if (stream.Length != 513 * 64 * sizeof(double)) throw new InvalidDataException("Invalid CLAP mel filter bank.");
            return Enumerable.Range(0, 513 * 64).Select(_ => reader.ReadDouble()).ToArray();
        }

        internal static float[] Extract(float[] samples, CancellationToken token)
        {
            if (samples.Length != DiscoveryAudioModel.Window || samples.Any(v => !float.IsFinite(v))) throw new InvalidDataException("Invalid ten-second CLAP window.");
            var result = new float[1001 * 64];
            var fft = new Complex[1024];
            var power = new double[513];
            for (int frame = 0; frame < 1001; frame++)
            {
                token.ThrowIfCancellationRequested();
                int start = frame * 480 - 512;
                for (int i = 0; i < 1024; i++)
                {
                    int index = start + i;
                    // np.pad(..., mode='reflect'), without repeating the edge sample.
                    if (index < 0) index = -index;
                    if (index >= samples.Length) index = 2 * samples.Length - index - 2;
                    fft[i] = new Complex(samples[index] * hann[i], 0);
                }
                Fourier.Forward(fft, FourierOptions.Matlab);
                for (int bin = 0; bin < 513; bin++)
                {
                    // Transformers stores complex64 before squaring in float64.
                    double real = (float)fft[bin].Real, imaginary = (float)fft[bin].Imaginary;
                    power[bin] = real * real + imaginary * imaginary;
                }
                for (int filter = 0; filter < 64; filter++)
                {
                    double value = 0;
                    for (int bin = 0; bin < 513; bin++) value += mel[bin * 64 + filter] * power[bin];
                    result[frame * 64 + filter] = (float)(10 * Math.Log10(Math.Max(1e-10, value)));
                }
            }
            return result;
        }
    }
}
