// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;

namespace osu.Game.Scoring.Render
{
    internal sealed class WaveFileWriter : IDisposable
    {
        private readonly FileStream stream;
        private readonly BinaryWriter writer;
        private readonly int sampleRate;
        private readonly short channelCount;
        private int dataLengthBytes;
        private bool disposed;

        public WaveFileWriter(string path, int sampleRate, short channelCount)
        {
            this.sampleRate = sampleRate;
            this.channelCount = channelCount;

            stream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            writer = new BinaryWriter(stream);

            writeHeader();
        }

        public void WriteInterleavedFloatSamples(float[] samples, int sampleFrames)
        {
            ArgumentNullException.ThrowIfNull(samples);

            int sampleCount = sampleFrames * channelCount;

            for (int i = 0; i < sampleCount; i++)
            {
                short pcm = (short)Math.Clamp(samples[i] * short.MaxValue, short.MinValue, short.MaxValue);
                writer.Write(pcm);
            }

            dataLengthBytes += sampleCount * sizeof(short);
        }

        public void WriteSilence(int sampleFrames)
        {
            int sampleCount = sampleFrames * channelCount;

            for (int i = 0; i < sampleCount; i++)
                writer.Write((short)0);

            dataLengthBytes += sampleCount * sizeof(short);
        }

        private void writeHeader()
        {
            const short bitsPerSample = 16;
            int byteRate = sampleRate * channelCount * bitsPerSample / 8;
            short blockAlign = (short)(channelCount * bitsPerSample / 8);

            writer.Write("RIFF"u8.ToArray());
            writer.Write(0);
            writer.Write("WAVE"u8.ToArray());

            writer.Write("fmt "u8.ToArray());
            writer.Write(16);
            writer.Write((short)1);
            writer.Write(channelCount);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            writer.Write("data"u8.ToArray());
            writer.Write(0);
        }

        private void finaliseHeader()
        {
            writer.Flush();

            stream.Seek(4, SeekOrigin.Begin);
            writer.Write(36 + dataLengthBytes);

            stream.Seek(40, SeekOrigin.Begin);
            writer.Write(dataLengthBytes);

            writer.Flush();
        }

        public void Dispose()
        {
            if (disposed)
                return;

            disposed = true;
            finaliseHeader();
            writer.Dispose();
            stream.Dispose();
        }
    }
}
