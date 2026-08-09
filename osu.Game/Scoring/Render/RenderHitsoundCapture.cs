// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Audio;
using osu.Framework.Audio.Mixing;

namespace osu.Game.Scoring.Render
{
    internal sealed class RenderHitsoundCapture : IDisposable
    {
        public const int SampleRate = 44100;
        private const int channel_count = 2;
        private const float silence_threshold = 0.002f;

        private readonly AudioManager audio;
        private readonly AudioMixer mixer;
        private readonly WaveFileWriter writer;
        private readonly int fps;

        private int writtenSampleFrames;
        private float[] sampleBuffer = Array.Empty<float>();

        public string OutputPath { get; }
        public int TotalRequestedSampleFrames { get; private set; }
        public int TotalReadSampleFrames { get; private set; }
        public int TotalSilentSampleFrames { get; private set; }
        public int TotalReadBytes { get; private set; }
        public float PeakAmplitude { get; private set; }
        public int? FirstAudibleSampleFrame { get; private set; }
        public int? LastAudibleSampleFrame { get; private set; }
        public double? FirstAudibleTimeMs => FirstAudibleSampleFrame * 1000.0 / SampleRate;
        public double? LastAudibleTimeMs => LastAudibleSampleFrame * 1000.0 / SampleRate;
        public bool HasAudibleData => PeakAmplitude >= silence_threshold;
        public double RecommendedMixGain
        {
            get
            {
                // Hitsounds are already captured at full volume (master=1, sample=1 forced during render).
                // We do NOT normalise them — the user heard them at whatever their effect volume was,
                // and the post-mix applies that volume ratio. Any additional gain here makes them
                // disproportionately loud relative to the music.
                return 1;
            }
        }

        public RenderHitsoundCapture(AudioManager audio, AudioMixer mixer, string outputPath, int fps)
        {
            this.audio = audio;
            this.mixer = mixer;
            this.fps = fps;
            OutputPath = outputPath;
            writer = new WaveFileWriter(outputPath, SampleRate, channel_count);
        }

        public async Task CaptureFrameAsync(int frameIndexExclusive, CancellationToken token)
        {
            int targetTotalFrames = (int)Math.Round(frameIndexExclusive * SampleRate / (double)fps);
            int framesToCapture = Math.Max(0, targetTotalFrames - writtenSampleFrames);

            if (framesToCapture == 0)
                return;

            TotalRequestedSampleFrames += framesToCapture;

            ensureBufferCapacity(framesToCapture * channel_count);

            await Task.CompletedTask.ConfigureAwait(false);
            int bytesRead = 0;
            token.ThrowIfCancellationRequested();

            int framesRead = bytesRead > 0
                ? Math.Min(framesToCapture, bytesRead / (sizeof(float) * channel_count))
                : 0;

            TotalReadBytes += Math.Max(0, bytesRead);
            TotalReadSampleFrames += framesRead;

            if (framesRead > 0)
            {
                analyseAudibleRange(framesRead);
                writer.WriteInterleavedFloatSamples(sampleBuffer, framesRead);
            }

            if (framesRead < framesToCapture)
            {
                writer.WriteSilence(framesToCapture - framesRead);
                TotalSilentSampleFrames += framesToCapture - framesRead;
            }

            writtenSampleFrames += framesToCapture;
        }

        private void analyseAudibleRange(int sampleFrames)
        {
            for (int frame = 0; frame < sampleFrames; frame++)
            {
                float amplitude = 0;
                int sampleBaseIndex = frame * channel_count;

                for (int channel = 0; channel < channel_count; channel++)
                    amplitude = Math.Max(amplitude, Math.Abs(sampleBuffer[sampleBaseIndex + channel]));

                if (amplitude > PeakAmplitude)
                    PeakAmplitude = amplitude;

                if (amplitude < silence_threshold)
                    continue;

                int absoluteFrameIndex = writtenSampleFrames + frame;
                FirstAudibleSampleFrame ??= absoluteFrameIndex;
                LastAudibleSampleFrame = absoluteFrameIndex + 1;
            }
        }

        private void ensureBufferCapacity(int requiredLength)
        {
            if (sampleBuffer.Length >= requiredLength)
                return;

            sampleBuffer = new float[requiredLength];
        }

        public void Dispose() => writer.Dispose();
    }
}
