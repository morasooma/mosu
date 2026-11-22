using System;
using System.Collections.Generic;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Resources;
using osu.Game.Scoring.Render;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ReplayRenderPipelineTest
    {
        [Test]
        public void TestCompactHevcIsSecondAndOptIn()
        {
            var options = RenderEncodingSelector.GetEncoderOptions();
            Assert.That(options[0], Is.EqualTo(RenderEncodingSelector.AutoEncoderLabel));
            Assert.That(options[1], Is.EqualTo("libx265"));
            Assert.That(RenderEncodingSelector.ResolveEncoderArgument(options[1], null!), Is.EqualTo("libx265"));
        }

        [TestCase("en", "Auto — fast", "HEVC — smaller file (slower)")]
        [TestCase("ru", "Авто — быстро", "HEVC — меньший файл (медленнее)")]
        [TestCase("es", "Automático — rápido", "HEVC — archivo más pequeño (más lento)")]
        [TestCase("ko", "자동 — 빠름", "HEVC — 작은 파일 (느림)")]
        [TestCase("zh", "自动 — 快速", "HEVC — 文件更小（较慢）")]
        public void TestEncoderLabelsResolveAtRuntime(string locale, string auto, string hevc)
        {
            _ = OsuResources.ResourceAssembly;
            using var config = new TestConfig();
            using var manager = new LocalisationManager(config);
            manager.AddLanguage(locale, new ResourceManagerLocalisationStore(locale));
            config.SetValue(FrameworkSetting.Locale, locale);
            Assert.That(manager.GetLocalisedString(ReplayRenderStrings.AutoEncoder), Is.EqualTo(auto));
            Assert.That(manager.GetLocalisedString(ReplayRenderStrings.CompactHevcEncoder), Is.EqualTo(hevc));
        }

        private sealed class TestConfig : FrameworkConfigManager
        {
            protected override string Filename => null!;

            public TestConfig() : base(null!)
            {
            }
        }

        [TestCase(ReplayRenderQualityPreset.Fast, 26)]
        [TestCase(ReplayRenderQualityPreset.Balanced, 24)]
        [TestCase(ReplayRenderQualityPreset.Quality, 22)]
        public void TestCompactEncoderUsesConstantQualityInsteadOfBitrate(ReplayRenderQualityPreset preset, int crf)
        {
            Assert.That(FFmpegEncoder.BuildRateControlArgs("libx265", preset, 2), Is.EqualTo($"-crf {crf}"));
            Assert.That(FFmpegEncoder.BuildRateControlArgs("libx265", preset, 40), Is.EqualTo($"-crf {crf}"));
            Assert.That(FFmpegEncoder.BuildRateControlArgs("h264_vaapi", preset, 7), Is.EqualTo("-bf 0 -b:v 7M"));
        }

        [TestCase(ReplayRenderQualityPreset.Fast, "p1")]
        [TestCase(ReplayRenderQualityPreset.Balanced, "p4")]
        [TestCase(ReplayRenderQualityPreset.Quality, "p6")]
        public void TestNvencRetainsQualityWithoutForcingSynchronousOutput(ReplayRenderQualityPreset quality, string preset)
        {
            string arguments = FFmpegEncoder.BuildEncoderTuningArgs("h264_nvenc", quality);
            Assert.Multiple(() =>
            {
                Assert.That(arguments, Does.Contain($"-preset {preset}"));
                Assert.That(arguments, Does.Contain("-tune ll"));
                Assert.That(arguments, Does.Contain("-rc cbr"));
                Assert.That(arguments, Does.Not.Contain("-delay"));
            });
        }

        [TestCase("h264_nvenc")]
        [TestCase("h264_amf")]
        [TestCase("h264_qsv")]
        public void TestWindowsHardwareProbeUsesSupportedFrameSize(string encoder)
        {
            string arguments = FFmpegEncoder.BuildEncoderValidationArguments(encoder);
            Assert.Multiple(() =>
            {
                Assert.That(arguments, Does.Contain("s=256x256"));
                Assert.That(arguments, Does.Contain("-frames:v 1"));
                Assert.That(arguments, Does.Contain("-pix_fmt yuv420p"));
                Assert.That(arguments, Does.Contain($"-c:v {encoder}"));
                Assert.That(arguments, Does.Not.Contain("hwupload"));
            });
        }

        [TestCase("h264_vaapi")]
        [TestCase("hevc_vaapi")]
        public void TestVaapiProbeUploadsToTheSameDeviceAsExport(string encoder)
        {
            string arguments = FFmpegEncoder.BuildEncoderValidationArguments(encoder);
            Assert.Multiple(() =>
            {
                Assert.That(arguments, Does.Contain("-vaapi_device /dev/dri/renderD128"));
                Assert.That(arguments, Does.Contain("-vf format=nv12,hwupload"));
                Assert.That(arguments, Does.Contain("-frames:v 1"));
            });
        }

        [TestCase(false, false, "rgba")]
        [TestCase(true, false, "bgra")]
        [TestCase(false, true, "nv12")]
        public void TestRawVideoFormatAndColourMetadata(bool bgra, bool nv12, string expected)
        {
            string arguments = FFmpegEncoder.BuildRawVideoInputArguments(1920, 1080, 120, bgra, nv12);
            Assert.That(arguments, Does.Contain($"-pix_fmt {expected}"));
            Assert.That(arguments, Does.Contain("-s 1920x1080 -r 120 -i pipe:0"));
            if (nv12)
            {
                Assert.That(arguments, Does.Contain("-colorspace bt709"));
                Assert.That(arguments, Does.Contain("-color_range tv"));
            }
            else
                Assert.That(arguments, Does.Not.Contain("-colorspace"));
        }

        [Test]
        public void TestRejectsAmbiguousOrUnalignedNv12()
        {
            Assert.Throws<ArgumentException>(() => FFmpegEncoder.BuildRawVideoInputArguments(1920, 1080, 60, true, true));
            Assert.Throws<ArgumentException>(() => FFmpegEncoder.BuildRawVideoInputArguments(1919, 1080, 60, false, true));
        }

        [TestCase(16)]
        [TestCase(1382400)]
        public void TestPoolTracksAllResourcesEvenWhenRequestsAreNotReturned(int frameByteCount)
        {
            var owned = new List<GameHost.BufferedScreenshotRequest>();
            var pool = RenderReplayPlayer.CreateFreeFrameBufferChannel(frameByteCount, owned);
            try
            {
                while (pool.Reader.TryRead(out var request))
                {
                    Assert.That(owned, Does.Contain(request));
                    Assert.That(request.Buffer.Length, Is.EqualTo(frameByteCount), "The pool must not expand NV12 frames to RGBA size.");
                }
                Assert.That(owned, Has.Count.EqualTo(RenderReplayPlayer.FRAME_BUFFER_COUNT));
            }
            finally
            {
                foreach (var request in owned)
                    request.Dispose();
            }
        }

        [Test]
        public void TestCaptureCanContinueWhileWriterOwnsPreviousFrames()
        {
            var freeBuffers = RenderReplayPlayer.CreateFreeFrameBufferChannel(16);
            var pending = new Queue<GameHost.BufferedScreenshotRequest>();
            var writing = new List<GameHost.BufferedScreenshotRequest>();
            var allocated = new HashSet<byte[]>();
            try
            {
                // Three GPU readbacks, then three writer requests. The next capture must not
                // wait for the writer: it only retires GPU requests once three are pending.
                for (int frame = 0; frame < 6; frame++)
                {
                    if (pending.Count == 3)
                        writing.Add(pending.Dequeue());
                    Assert.That(freeBuffers.Reader.TryRead(out var request), Is.True, $"No capture buffer for frame {frame}");
                    Assert.That(request!.PreserveNativePixelFormat, Is.True);
                    Assert.That(request.UseVideoCapture, Is.True);
                    Assert.That(request.Buffer.Length, Is.EqualTo(16));
                    Assert.That(allocated.Add(request.Buffer), Is.True, "An in-flight frame buffer was reused.");
                    pending.Enqueue(request);
                }

                Assert.That(writing, Has.Count.EqualTo(3));
                Assert.That(pending, Has.Count.EqualTo(3));
                Assert.That(freeBuffers.Reader.TryRead(out var finalBuffer), Is.True, "The active pipe write needs its own buffer.");
                finalBuffer!.Dispose();
            }
            finally
            {
                foreach (var request in pending)
                    request.Dispose();
                foreach (var request in writing)
                    request.Dispose();
                while (freeBuffers.Reader.TryRead(out var request))
                    request.Dispose();
            }
        }
    }
}
