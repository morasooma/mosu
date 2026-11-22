using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Game.Configuration;
using osu.Game.Scoring.Render;

namespace osu.Game.Tests.NonVisual;

[TestFixture]
public class ReplayRenderEncoderIntegrationTest
{
    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public Task NativePixelFormatEncodesCorrectColours(bool bgra, bool nv12)
    {
        if (Environment.GetEnvironmentVariable("MOSU_TEST_REPLAY_ENCODER") != "1")
            Assert.Ignore("Opt-in: runs FFmpeg using synthetic frames, without a game window or audio output.");
        return encodeCorrectColoursAsync(bgra, nv12, "libx264", 6);
    }

    [Test]
    public Task NvencBufferedOutputFlushesAllFrames()
    {
        if (!OperatingSystem.IsWindows() || Environment.GetEnvironmentVariable("MOSU_TEST_REPLAY_NVENC") != "1")
            Assert.Ignore("Opt-in: requires Windows with a working NVENC encoder; CPU fallback is a failure.");
        return encodeCorrectColoursAsync(false, true, "h264_nvenc", 60);
    }

    [Test]
    public Task CompactHevcFlushesAllFramesAndRemuxesWithoutReencoding()
    {
        if (Environment.GetEnvironmentVariable("MOSU_TEST_REPLAY_ENCODER") != "1")
            Assert.Ignore("Opt-in: requires FFmpeg with libx265.");
        return encodeCorrectColoursAsync(false, true, "libx265", 120, remux: true);
    }

    private static async Task encodeCorrectColoursAsync(bool bgra, bool nv12, string codec, int frameCount, bool remux = false)
    {
        string path = Path.Combine(Path.GetTempPath(), $"mosu-native-frame-{Guid.NewGuid():N}.mkv");
        string mp4Path = Path.ChangeExtension(path, ".mp4");
        const int size = 256;
        byte[] frame = new byte[size * size * 4];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int offset = (y * size + x) * 4;
            bool red = y < size / 2;
            frame[offset] = (byte)((red != bgra) ? 255 : 0);
            frame[offset + 2] = (byte)((red != bgra) ? 0 : 255);
            frame[offset + 3] = 255;
        }
        if (nv12)
        {
            frame = new byte[size * size * 3 / 2];
            for (int i = 0; i < size * size; i++)
                frame[i] = (byte)(i < size * size / 2 ? 63 : 32);
            for (int y = 0; y < size / 2; y++)
            for (int x = 0; x < size; x += 2)
            {
                int p = size * size + y * size + x;
                frame[p] = (byte)(y < size / 4 ? 102 : 240);
                frame[p + 1] = (byte)(y < size / 4 ? 240 : 118);
            }
        }
        try
        {
            using (var encoder = new FFmpegEncoder(size, size, size, size, 60, 8, codec, ReplayRenderQualityPreset.Balanced,
                       path, videoFrameCount: frameCount, inputIsBgra: bgra, inputIsNv12: nv12))
            {
                Assert.That(encoder.SelectedEncoder, Is.EqualTo(codec), "Hardware coverage must not silently use the CPU fallback.");
                for (int i = 0; i < frameCount; i++)
                    await encoder.InputStream.WriteAsync(frame);
                await encoder.FinishAsync();
            }
            if (remux)
                FFmpegPostProcessor.RemuxToMp4(path, mp4Path, hasAudio: false, compactHevc: true);

            string decodePath = remux ? mp4Path : path;
            using var decode = Process.Start(new ProcessStartInfo(FFmpegEncoder.ExecutablePath, $"-hide_banner -loglevel error -xerror -i \"{decodePath}\" -pix_fmt rgb24 -f rawvideo pipe:1")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true,
            })!;
            using var pixels = new MemoryStream();
            var stderr = decode.StandardError.ReadToEndAsync();
            await decode.StandardOutput.BaseStream.CopyToAsync(pixels);
            Assert.That(decode.WaitForExit(10000), Is.True);
            Assert.That(decode.ExitCode, Is.Zero, await stderr);
            byte[] decoded = pixels.ToArray();
            Assert.That(decoded.Length, Is.EqualTo(size * size * 3 * frameCount), "All frames must survive the bounded pipe.");
            for (int i = 0; i < frameCount; i++)
            {
                int top = (i * size * size + 32 * size + 32) * 3;
                int bottom = (i * size * size + 224 * size + 32) * 3;
                Assert.That(decoded[top], Is.GreaterThan(240));
                Assert.That(decoded[top + 2], Is.LessThan(15));
                Assert.That(decoded[bottom], Is.LessThan(15));
                Assert.That(decoded[bottom + 2], Is.GreaterThan(240));
            }
        }
        finally
        {
            File.Delete(path);
            File.Delete(mp4Path);
        }
    }
}
