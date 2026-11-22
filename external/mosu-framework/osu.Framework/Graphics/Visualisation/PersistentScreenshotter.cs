// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Platform;
using osuTK;
using osuTK.Graphics;
using System.Buffers;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Framework.Graphics.Visualisation
{
    public partial class PersistentScreenshotter : Drawable, IBufferedDrawable
    {
        public sealed class BufferedCaptureRequest : IDisposable
        {
            public byte[] Buffer { get; set; } = Array.Empty<byte>();
            public int ByteCount { get; set; }
            internal osu.Framework.Graphics.Veldrid.VeldridDevice.PendingTextureReadback? VeldridReadback;
            public bool Immediate { get; set; }

            public void Dispose()
            {
                VeldridReadback?.Dispose();
                VeldridReadback = null;
            }
        }

        public readonly Drawable Target;
        private readonly IShader textureShader;

        private TaskCompletionSource<Image<Rgba32>?>? captureTcs;
        private TaskCompletionSource<(byte[] Buffer, int ByteCount)>? captureToBufferTcs;
        private TaskCompletionSource<BufferedCaptureRequest?>? submitCaptureToBufferTcs;
        private BufferedCaptureRequest? submittedCaptureRequest;
        private bool isCaptureRequested;
        private ulong captureVersion;

        public PersistentScreenshotter(Drawable target, IShader textureShader)
        {
            this.Target = target;
            this.textureShader = textureShader;
        }

        public Task<Image<Rgba32>?> CaptureAsync()
        {
            var tcs = new TaskCompletionSource<Image<Rgba32>?>(TaskCreationOptions.RunContinuationsAsynchronously);
            captureTcs = tcs;
            isCaptureRequested = true;
            captureVersion++;
            Invalidate(Invalidation.DrawNode);
            return tcs.Task;
        }

        public Task<(byte[] Buffer, int ByteCount)> CaptureToBufferAsync(int requiredBytes)
        {
            var tcs = new TaskCompletionSource<(byte[], int)>(TaskCreationOptions.RunContinuationsAsynchronously);
            captureToBufferTcs = tcs;
            isCaptureRequested = true;
            captureVersion++;
            Invalidate(Invalidation.DrawNode);
            return tcs.Task;
        }

        public Task<BufferedCaptureRequest?> SubmitCaptureToBufferAsync(BufferedCaptureRequest request)
        {
            var tcs = new TaskCompletionSource<BufferedCaptureRequest?>(TaskCreationOptions.RunContinuationsAsynchronously);
            submittedCaptureRequest = request;
            submitCaptureToBufferTcs = tcs;
            isCaptureRequested = true;
            captureVersion++;
            Invalidate(Invalidation.DrawNode);
            return tcs.Task;
        }

        public Task<(byte[] Buffer, int ByteCount)> CompleteCaptureToBufferAsync(BufferedCaptureRequest request)
        {
            if (request.Immediate || request.VeldridReadback == null)
                return Task.FromResult((request.Buffer, request.ByteCount));

            if (renderer is osu.Framework.Graphics.Veldrid.VeldridRenderer vr
                && vr.CompleteExtractFrameBufferDataToBuffer(request.VeldridReadback, request.Buffer.AsSpan(0, request.ByteCount)))
                return Task.FromResult((request.Buffer, request.ByteCount));

            return Task.FromResult((Array.Empty<byte>(), 0));
        }


        public override Quad ScreenSpaceDrawQuad => Target.ScreenSpaceDrawQuad;
        public override bool IsPresent => true;

        IShader ITexturedShaderDrawable.TextureShader => textureShader;
        Color4 IBufferedDrawable.BackgroundColour => new Color4(0, 0, 0, 0);
        DrawColourInfo? IBufferedDrawable.FrameBufferDrawColour => new DrawColourInfo(Color4.White);
        Vector2 IBufferedDrawable.FrameBufferScale => Vector2.One;

        public override DrawColourInfo DrawColourInfo => new DrawColourInfo(Color4.White);
        public override DrawInfo DrawInfo => Target.DrawInfo;

        private readonly BufferedDrawNodeSharedData sharedData = new BufferedDrawNodeSharedData(formats: new[] { RenderBufferFormat.D16 }, pixelSnapping: true, clipToRootNode: true);

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private void onRendered(IFrameBuffer frameBuffer)
        {
            var tcs = captureTcs;
            var bufTcs = captureToBufferTcs;
            var submitTcs = submitCaptureToBufferTcs;
            var submitRequest = submittedCaptureRequest;

            // Exit early only if neither capture mode is active.
            if (tcs == null && bufTcs == null && submitTcs == null)
                return;

            captureTcs = null;
            captureToBufferTcs = null;
            submitCaptureToBufferTcs = null;
            submittedCaptureRequest = null;

            if (submitTcs != null && submitRequest != null)
            {
                if (renderer is osu.Framework.Graphics.Veldrid.VeldridRenderer vr)
                {
                    submitRequest.VeldridReadback = vr.BeginExtractFrameBufferDataToBuffer(frameBuffer, submitRequest.VeldridReadback);
                    submitTcs.TrySetResult(submitRequest);
                    return;
                }

                var imageFallback = renderer.ExtractFrameBufferData(frameBuffer);

                if (imageFallback != null)
                {
                    try
                    {
                        if (imageFallback.DangerousTryGetSinglePixelMemory(out Memory<Rgba32> pixelMemory))
                            MemoryMarshal.AsBytes(pixelMemory.Span).CopyTo(submitRequest.Buffer.AsSpan(0, submitRequest.ByteCount));
                        else
                        {
                            int width = imageFallback.Width;
                            int height = imageFallback.Height;

                            imageFallback.ProcessPixelRows(accessor =>
                            {
                                for (int y = 0; y < height; y++)
                                    MemoryMarshal.AsBytes(accessor.GetRowSpan(y)).CopyTo(submitRequest.Buffer.AsSpan(y * width * 4, width * 4));
                            });
                        }

                        submitRequest.Immediate = true;
                        submitTcs.TrySetResult(submitRequest);
                    }
                    finally
                    {
                        imageFallback.Dispose();
                    }

                    return;
                }

                submitTcs.TrySetResult(submitRequest);
                return;
            }

            // Buffer fast path: copy pixels directly into a rented byte[] without ImageSharp overhead.
            if (bufTcs != null && renderer is osu.Framework.Graphics.Veldrid.VeldridRenderer bufferRenderer)
            {
                int w = (int)frameBuffer.Texture.Width;
                int h = (int)frameBuffer.Texture.Height;
                int byteCount = w * h * 4;
                byte[] buf = ArrayPool<byte>.Shared.Rent(byteCount);
                if (bufferRenderer.ExtractFrameBufferDataToBuffer(frameBuffer, buf.AsSpan(0, byteCount)))
                {
                    bufTcs.TrySetResult((buf, byteCount));
                    tcs?.TrySetResult(null);
                    return;
                }
                ArrayPool<byte>.Shared.Return(buf);
            }

            // Slow path: ImageSharp extraction (non-Veldrid fallback).
            var image = renderer.ExtractFrameBufferData(frameBuffer);
            tcs?.TrySetResult(image);
            bufTcs?.TrySetResult((System.Array.Empty<byte>(), 0));
        }

        internal override DrawNode? GenerateDrawNodeSubtree(ulong frame, int treeIndex, bool forceNewDrawNode)
        {
            if (!isCaptureRequested)
                return null;

            isCaptureRequested = false;

            var targetDrawNode = Target.GenerateDrawNodeSubtree(frame, treeIndex, forceNewDrawNode);
            if (targetDrawNode == null)
            {
                captureTcs?.TrySetResult(null);
                captureTcs = null;
                submitCaptureToBufferTcs?.TrySetResult(null);
                submitCaptureToBufferTcs = null;
                return null;
            }

            Target.GenerateDrawNodeSubtree(frame, treeIndex, forceNewDrawNode: true);

            var drawNode = new PersistentScreenshotterDrawNode(this, targetDrawNode, sharedData, onRendered, captureVersion);
            drawNode.ApplyState();
            return drawNode;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            sharedData.Dispose();
        }

        private class PersistentScreenshotterDrawNode : BufferedDrawNode
        {
            private readonly Action<IFrameBuffer> onRendered;
            private readonly ulong captureVersion;

            public PersistentScreenshotterDrawNode(IBufferedDrawable source, DrawNode child, BufferedDrawNodeSharedData sharedData, Action<IFrameBuffer> onRendered, ulong captureVersion)
                : base(source, child, sharedData)
            {
                this.onRendered = onRendered;
                this.captureVersion = captureVersion;
            }

            protected override long GetDrawVersion() => unchecked((long)captureVersion);

            protected override void DrawContents(IRenderer renderer) => onRendered(SharedData.MainBuffer);
        }
    }
}
