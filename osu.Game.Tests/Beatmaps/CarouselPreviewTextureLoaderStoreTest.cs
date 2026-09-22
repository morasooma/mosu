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

            public StreamTextureStore(int width, int height)
            {
                using var image = new Image<Rgba32>(width, height);
                using var stream = new MemoryStream();
                image.SaveAsJpeg(stream);
                encodedImage = stream.ToArray();
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

            public string? LastRequestedName { get; private set; }

            public int RequestCount { get; private set; }

            public TestTextureStore(int width, int height)
            {
                this.width = width;
                this.height = height;
            }

            public TextureUpload Get(string name)
            {
                LastRequestedName = name;
                RequestCount++;
                return new TextureUpload(new Image<Rgba32>(width, height));
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
