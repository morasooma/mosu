// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Beatmaps;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Game.Tests.Beatmaps
{
    [TestFixture]
    public class CarouselPreviewTextureLoaderStoreTest
    {
        [TestCase(100, 1024, 160)]
        [TestCase(50, 512, 80)]
        [TestCase(25, 256, 40)]
        public void TestModernPreviewResolution(int resolutionPercent, int expectedWidth, int expectedHeight)
        {
            var source = new TestTextureStore(1280, 720);
            using var store = new BeatmapPanelBackgroundTextureLoaderStore(source);
            using TextureUpload upload = store.Get(CarouselPreviewTextureRequest.Create("background.jpg", resolutionPercent));

            Assert.Multiple(() =>
            {
                Assert.That(source.LastRequestedName, Is.EqualTo("background.jpg"));
                Assert.That(upload.Width, Is.EqualTo(expectedWidth));
                Assert.That(upload.Height, Is.EqualTo(expectedHeight));
            });
        }

        [TestCase(100, 512, 288)]
        [TestCase(50, 256, 144)]
        [TestCase(25, 128, 72)]
        public void TestLegacyPreviewResolution(int resolutionPercent, int expectedWidth, int expectedHeight)
        {
            var source = new TestTextureStore(1280, 720);
            using var store = new BeatmapLegacyPreviewBackgroundTextureLoaderStore(source);
            using TextureUpload upload = store.Get(CarouselPreviewTextureRequest.Create("background.jpg", resolutionPercent));

            Assert.Multiple(() =>
            {
                Assert.That(source.LastRequestedName, Is.EqualTo("background.jpg"));
                Assert.That(upload.Width, Is.EqualTo(expectedWidth));
                Assert.That(upload.Height, Is.EqualTo(expectedHeight));
            });
        }

        [TestCase(100, false)]
        [TestCase(50, false)]
        [TestCase(25, false)]
        [TestCase(100, true)]
        [TestCase(50, true)]
        public async Task TestClassicPreviewIsFourByThree(int resolutionPercent, bool stream)
        {
            using IResourceStore<TextureUpload> source = stream ? new StreamTextureStore(1280, 720) : new TestTextureStore(1280, 720);
            using var store = new BeatmapLegacyPreviewBackgroundTextureLoaderStore(source);
            string request = CarouselPreviewTextureRequest.CreateClassic("background.jpg", resolutionPercent);
            using TextureUpload sync = store.Get(request);
            using TextureUpload asyncUpload = await store.GetAsync(request);
            Assert.Multiple(() =>
            {
                Assert.That(sync.Width, Is.EqualTo(512 * resolutionPercent / 100));
                Assert.That(sync.Height, Is.EqualTo(384 * resolutionPercent / 100));
                Assert.That(asyncUpload.Width, Is.EqualTo(sync.Width));
                Assert.That(asyncUpload.Height, Is.EqualTo(sync.Height));
                Assert.That(CarouselPreviewTextureRequest.ParseLegacy(request).ResourceName, Is.EqualTo("background.jpg"));
                Assert.That(request, Is.Not.EqualTo(CarouselPreviewTextureRequest.Create("background.jpg", resolutionPercent)));
            });
        }

        [Test]
        public void TestClassicPreviewDoesNotLoseVerticalDetailToIntermediateCrop()
        {
            var source = new TestTextureStore(800, 600, markTop: true);
            using var store = new BeatmapLegacyPreviewBackgroundTextureLoaderStore(source);
            using TextureUpload classic = store.Get(CarouselPreviewTextureRequest.CreateClassic("background.jpg", 100));
            using TextureUpload legacy = store.Get(CarouselPreviewTextureRequest.Create("background.jpg", 100));
            Assert.Multiple(() =>
            {
                Assert.That(classic.Data[classic.Width / 2].R, Is.GreaterThan(240), "4:3 keeps the source's red top edge");
                Assert.That(legacy.Data[legacy.Width / 2].R, Is.LessThan(10), "16:9 intentionally crops that edge away");
            });
        }

        [TestCase(false, 1024, 160)]
        [TestCase(true, 512, 288)]
        public void TestStreamDecodeProducesExpectedSize(bool legacy, int expectedWidth, int expectedHeight)
        {
            var source = new StreamTextureStore(3840, 2160);
            using IResourceStore<TextureUpload> store = legacy
                ? new BeatmapLegacyPreviewBackgroundTextureLoaderStore(source)
                : new BeatmapPanelBackgroundTextureLoaderStore(source);

            using TextureUpload upload = store.Get(CarouselPreviewTextureRequest.Create("background.jpg", 100));

            Assert.Multiple(() =>
            {
                Assert.That(source.UploadRequestCount, Is.Zero);
                Assert.That(upload.Width, Is.EqualTo(expectedWidth));
                Assert.That(upload.Height, Is.EqualTo(expectedHeight));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public async Task TestCorruptPngIsTreatedAsMissingArtwork(bool legacy)
        {
            using var source = new StreamTextureStore(16, 16, corruptPng: true);
            using IResourceStore<TextureUpload> store = legacy
                ? new BeatmapLegacyPreviewBackgroundTextureLoaderStore(source)
                : new BeatmapPanelBackgroundTextureLoaderStore(source);
            string request = CarouselPreviewTextureRequest.Create("background.png", 100);

            using (var png = source.GetStream("background.png"))
                Assert.Throws<InvalidImageContentException>(() => Image.Identify(png));

            Assert.That(store.Get(request), Is.Null);
            Assert.That(await store.GetAsync(request), Is.Null);
            Assert.That(source.UploadRequestCount, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestCancelledLoadDisposesDecodedUpload(bool legacy)
        {
            using var cancellation = new CancellationTokenSource();
            var source = new CancellingTextureStore(cancellation);
            using IResourceStore<TextureUpload> store = legacy
                ? new BeatmapLegacyPreviewBackgroundTextureLoaderStore(source)
                : new BeatmapPanelBackgroundTextureLoaderStore(source);

            Assert.ThrowsAsync<OperationCanceledException>(async () =>
                await store.GetAsync(CarouselPreviewTextureRequest.Create("background.jpg", 100), cancellation.Token));
            Assert.That(source.UploadDisposed, Is.True);
        }

        [Test]
        public void TestRecentPreviewRemainsCachedWithoutConsumer()
        {
            var source = new TestTextureStore(16, 16);
            using var store = new CarouselPreviewTextureStore(new DummyRenderer(), source, 2);

            store.Get("background-a.jpg").Dispose();
            store.Get("background-a.jpg").Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(source.RequestCount, Is.EqualTo(1));
                Assert.That(store.CachedCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void TestOldestPreviewIsEvicted()
        {
            var source = new TestTextureStore(16, 16);
            using var store = new CarouselPreviewTextureStore(new DummyRenderer(), source, 2);

            store.Get("background-a.jpg").Dispose();
            store.Get("background-b.jpg").Dispose();
            store.Get("background-c.jpg").Dispose();
            store.Get("background-a.jpg").Dispose();

            Assert.Multiple(() =>
            {
                Assert.That(source.RequestCount, Is.EqualTo(4));
                Assert.That(store.CachedCount, Is.EqualTo(2));
            });
        }

        private sealed class StreamTextureStore : IResourceStore<TextureUpload>
        {
            private readonly byte[] encodedImage;

            public int UploadRequestCount { get; private set; }

            public StreamTextureStore(int width, int height, bool corruptPng = false)
            {
                using var image = new Image<Rgba32>(width, height);
                using var stream = new MemoryStream();

                if (corruptPng)
                    image.SaveAsPng(stream);
                else
                    image.SaveAsJpeg(stream);

                encodedImage = stream.ToArray();
                if (corruptPng)
                {
                    // Flip the first byte of the IDAT CRC, without changing the encoded pixels.
                    int idat = encodedImage.AsSpan().IndexOf("IDAT"u8);
                    int length = System.Buffers.Binary.BinaryPrimitives.ReadInt32BigEndian(encodedImage.AsSpan(idat - 4, 4));
                    encodedImage[idat + 4 + length] ^= 0x80;
                }
            }

            public TextureUpload Get(string name)
            {
                UploadRequestCount++;
                throw new AssertionException("Stream decoding should not request a full TextureUpload.");
            }

            public Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = default)
                => Task.FromResult(Get(name));

            public Stream GetStream(string name) => new MemoryStream(encodedImage, writable: false);

            public IEnumerable<string> GetAvailableResources() => Array.Empty<string>();

            public void Dispose()
            {
            }
        }

        private sealed class CancellingTextureStore : IResourceStore<TextureUpload>
        {
            private readonly CancellationTokenSource cancellation;

            public bool UploadDisposed { get; private set; }

            public CancellingTextureStore(CancellationTokenSource cancellation)
            {
                this.cancellation = cancellation;
            }

            public TextureUpload Get(string name) => throw new NotSupportedException();

            public Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = default)
            {
                var upload = new TrackingTextureUpload(() => UploadDisposed = true);
                cancellation.Cancel();
                return Task.FromResult<TextureUpload>(upload);
            }

            public Stream? GetStream(string name) => null;

            public IEnumerable<string> GetAvailableResources() => Array.Empty<string>();

            public void Dispose()
            {
            }
        }

        private sealed class TrackingTextureUpload : TextureUpload
        {
            private readonly Action onDispose;

            public TrackingTextureUpload(Action onDispose)
                : base(new Image<Rgba32>(16, 16))
            {
                this.onDispose = onDispose;
            }

            protected override void Dispose(bool isDisposing)
            {
                base.Dispose(isDisposing);
                onDispose();
            }
        }

        private sealed class TestTextureStore : IResourceStore<TextureUpload>
        {
            private readonly int width;
            private readonly int height;
            private readonly bool markTop;

            public string? LastRequestedName { get; private set; }

            public int RequestCount { get; private set; }

            public TestTextureStore(int width, int height, bool markTop = false)
            {
                this.width = width;
                this.height = height;
                this.markTop = markTop;
            }

            public TextureUpload Get(string name)
            {
                LastRequestedName = name;
                RequestCount++;
                var image = new Image<Rgba32>(width, height);
                if (markTop)
                {
                    for (int y = 0; y < 40; y++)
                        for (int x = 0; x < width; x++)
                            image[x, y] = new Rgba32(255, 0, 0, 255);
                }
                return new TextureUpload(image);
            }

            public Task<TextureUpload> GetAsync(string name, CancellationToken cancellationToken = default)
                => Task.FromResult(Get(name));

            public Stream? GetStream(string name) => null;

            public IEnumerable<string> GetAvailableResources() => Array.Empty<string>();

            public void Dispose()
            {
            }
        }
    }
}
