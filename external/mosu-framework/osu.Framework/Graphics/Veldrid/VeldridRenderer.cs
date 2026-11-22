// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Vertices;
using osu.Framework.Graphics.Shaders;
using osu.Framework.Graphics.Textures;
using osu.Framework.Graphics.Veldrid.Batches;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using osu.Framework.Graphics.Veldrid.Buffers;
using osu.Framework.Graphics.Veldrid.Buffers.Staging;
using osu.Framework.Graphics.Veldrid.Pipelines;
using osu.Framework.Graphics.Veldrid.Shaders;
using osu.Framework.Graphics.Veldrid.Textures;
using osu.Framework.Graphics.Veldrid.Vertices;
using osuTK;
using osuTK.Graphics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Veldrid;
using PrimitiveTopology = osu.Framework.Graphics.Rendering.PrimitiveTopology;

namespace osu.Framework.Graphics.Veldrid
{
    internal class VeldridRenderer : Renderer, IVeldridRenderer
    {
        protected internal override bool VerticalSync
        {
            get => veldridDevice.VerticalSync;
            set => veldridDevice.VerticalSync = value;
        }

        protected internal override bool AllowTearing
        {
            get => veldridDevice.AllowTearing;
            set => veldridDevice.AllowTearing = value;
        }

        public override bool IsDepthRangeZeroToOne
            => veldridDevice.IsDepthRangeZeroToOne;

        public override bool IsUvOriginTopLeft
            => veldridDevice.IsUvOriginTopLeft;

        public override bool IsClipSpaceYInverted
            => veldridDevice.IsClipSpaceYInverted;

        public bool UseStructuredBuffers
            => veldridDevice.UseStructuredBuffers;

        public GraphicsDevice Device
            => veldridDevice.Device;

        public ResourceFactory Factory
            => veldridDevice.Factory;

        public GraphicsSurfaceType SurfaceType
            => veldridDevice.SurfaceType;

        private readonly HashSet<IVeldridUniformBuffer> uniformBufferResetList = new HashSet<IVeldridUniformBuffer>();

        internal string PreferredGraphicsAdapter { get; set; } = string.Empty;
        private Vector2I? videoCaptureSize;
        private VeldridVideoCaptureTarget? videoCaptureTarget;
        private VeldridNv12Converter? videoConverter;
        internal bool IsVideoCaptureActive => videoCaptureSize.HasValue;
        internal Vector2I? VideoCaptureSize => videoCaptureSize;
        internal bool VideoCaptureUsesNv12 => videoConverter != null;

        internal void ConfigureVideoCapture(int width, int height)
        {
            if (IsVideoCaptureActive)
                throw new InvalidOperationException("Video capture has already been configured.");
            // Other backends keep their existing screenshot/orientation contract until tested.
            if (Device.BackendType != GraphicsBackend.Direct3D11 && Device.BackendType != GraphicsBackend.Vulkan)
                return;
            videoCaptureTarget = new VeldridVideoCaptureTarget(Device, (uint)width, (uint)height);
            videoCaptureSize = new Vector2I(width, height);
            graphicsPipeline.DefaultFramebuffer = videoCaptureTarget.Framebuffer;
            if (Device.Features.ComputeShader && Device.Features.StructuredBuffer && (width & 1) == 0 && (height & 1) == 0)
            {
                try
                {
                    videoConverter = new VeldridNv12Converter(Device);
                }
                catch (Exception ex)
                {
                    osu.Framework.Logging.Logger.Log($"GPU NV12 conversion unavailable; retaining offscreen RGBA capture ({ex.Message}).",
                        level: osu.Framework.Logging.LogLevel.Important);
                }
            }
        }

        internal void DisposeVideoCapture()
        {
            if (!IsVideoCaptureActive)
                return;
            Device.WaitForIdle();
            graphicsPipeline.DefaultFramebuffer = null;
            videoConverter?.Dispose();
            videoConverter = null;
            videoCaptureTarget?.Dispose();
            videoCaptureTarget = null;
            videoCaptureSize = null;
        }

        internal void SubmitVideoCapture(GameHost.BufferedScreenshotRequest request)
        {
            var target = videoCaptureTarget ?? throw new InvalidOperationException("Offscreen capture is not configured.");
            if (videoConverter != null)
            {
                request.Nv12Readback ??= videoConverter.CreateReadback(target.Colour);
                videoConverter.Submit(request.Nv12Readback);
                request.IsNv12 = true;
                request.IsBgra = false;
                request.ByteCount = request.Nv12Readback.ByteCount;
            }
            else
            {
                request.VeldridReadback = veldridDevice.BeginTextureReadback(target.Colour, request.VeldridReadback);
                request.VeldridReadback.RequiresBgraToRgbaSwizzle = false;
                request.IsNv12 = false;
                request.IsBgra = false;
                request.ByteCount = checked((int)(target.Colour.Width * target.Colour.Height * 4));
            }
            request.Immediate = false;
        }

        private VeldridDevice veldridDevice = null!;
        private GraphicsPipeline graphicsPipeline = null!;
        private BasicPipeline bufferUpdatePipeline = null!;
        private BasicPipeline textureUploadPipeline = null!;
        private VeldridStagingTexturePool stagingTexturePool = null!;

        private bool beganTextureUploadPipeline;
        private VeldridIndexBuffer? linearIndexBuffer;
        private VeldridIndexBuffer? quadIndexBuffer;

        protected override void Initialise(IGraphicsSurface graphicsSurface)
        {
            veldridDevice = new VeldridDevice(graphicsSurface, PreferredGraphicsAdapter);
            graphicsPipeline = new GraphicsPipeline(veldridDevice);
            bufferUpdatePipeline = new BasicPipeline(veldridDevice);
            textureUploadPipeline = new BasicPipeline(veldridDevice);
            stagingTexturePool = new VeldridStagingTexturePool(graphicsPipeline);

            MaxTextureSize = veldridDevice.MaxTextureSize;
        }

        protected internal override void BeginFrame(Vector2 windowSize)
        {
            foreach (var ubo in uniformBufferResetList)
                ubo.ResetCounters();
            uniformBufferResetList.Clear();

            if (videoCaptureSize is Vector2I size)
                windowSize = new Vector2(size.X, size.Y);
            else
                veldridDevice.Resize(new Vector2I((int)windowSize.X, (int)windowSize.Y));
            graphicsPipeline.Begin();
            bufferUpdatePipeline.Begin();

            base.BeginFrame(windowSize);
        }

        protected internal override void FinishFrame()
        {
            base.FinishFrame();

            flushTextureUploadPipeline();

            bufferUpdatePipeline.End();
            graphicsPipeline.End();
        }

        protected internal override void SwapBuffers()
        {
            if (!IsVideoCaptureActive)
                veldridDevice.SwapBuffers();
        }

        protected internal override void WaitUntilIdle()
            => veldridDevice.WaitUntilIdle();

        protected internal override void WaitUntilNextFrameReady()
            => veldridDevice.WaitUntilNextFrameReady();

        protected internal override void MakeCurrent()
            => veldridDevice.MakeCurrent();

        protected internal override void ClearCurrent()
            => veldridDevice.ClearCurrent();

        protected override void ClearImplementation(ClearInfo clearInfo)
            => graphicsPipeline.Clear(clearInfo);

        protected override void SetScissorStateImplementation(bool enabled)
            => graphicsPipeline.SetScissorState(enabled);

        protected override bool SetTextureImplementation(INativeTexture? texture, int unit)
        {
            if (texture is not VeldridTexture veldridTexture)
                return false;

            graphicsPipeline.AttachTexture(unit, veldridTexture);
            return true;
        }

        protected override void SetShaderImplementation(IShader shader)
            => graphicsPipeline.SetShader((VeldridShader)shader);

        protected override void SetBlendImplementation(BlendingParameters blendingParameters)
            => graphicsPipeline.SetBlend(blendingParameters);

        protected override void SetBlendMaskImplementation(BlendingMask blendingMask)
            => graphicsPipeline.SetBlendMask(blendingMask);

        protected override void SetViewportImplementation(RectangleI viewport)
            => graphicsPipeline.SetViewport(viewport);

        protected override void SetScissorImplementation(RectangleI scissor)
            => graphicsPipeline.SetScissor(scissor);

        protected override void SetDepthInfoImplementation(DepthInfo depthInfo)
            => graphicsPipeline.SetDepthInfo(depthInfo);

        protected override void SetStencilInfoImplementation(StencilInfo stencilInfo)
            => graphicsPipeline.SetStencilInfo(stencilInfo);

        protected override void SetFrameBufferImplementation(IFrameBuffer? frameBuffer)
            => graphicsPipeline.SetFrameBuffer((VeldridFrameBuffer?)frameBuffer);

        protected override void DeleteFrameBufferImplementation(IFrameBuffer frameBuffer)
            => ((VeldridFrameBuffer)frameBuffer).DeleteResources(true);

        public override void DrawVerticesImplementation(PrimitiveTopology topology, int vertexStart, int verticesCount)
        {
            // normally we would flush/submit all texture upload commands at the end of the frame, since no actual rendering by the GPU will happen until then,
            // but turns out on macOS with non-apple GPU, this results in rendering corruption.
            // flushing the texture upload commands here before a draw call fixes the corruption, and there's no explanation as to why that's the case,
            // but there is nothing to be lost in flushing here except for a frame that contains many sprites with Texture.BypassTextureUploadQueue = true.
            // until that appears to be problem, let's just flush here.
            flushTextureUploadPipeline();

            graphicsPipeline.DrawVertices(topology.ToPrimitiveTopology(), vertexStart, verticesCount);
        }

        public void BindVertexBuffer<T>(IVeldridVertexBuffer<T> buffer)
            where T : unmanaged, IEquatable<T>, IVertex
            => graphicsPipeline.SetVertexBuffer(buffer.Buffer, VeldridVertexUtils<T>.Layout);

        public void BindIndexBuffer(VeldridIndexLayout layout, int verticesCount)
        {
            ref var indexBuffer = ref layout == VeldridIndexLayout.Quad
                ? ref quadIndexBuffer
                : ref linearIndexBuffer;

            if (indexBuffer == null || indexBuffer.VertexCapacity < verticesCount)
            {
                indexBuffer?.Dispose();
                indexBuffer = new VeldridIndexBuffer(bufferUpdatePipeline, layout, verticesCount);
            }

            graphicsPipeline.SetIndexBuffer(indexBuffer);
        }

        private void ensureTextureUploadPipelineBegan()
        {
            if (beganTextureUploadPipeline)
                return;

            textureUploadPipeline.Begin();
            beganTextureUploadPipeline = true;
        }

        private void flushTextureUploadPipeline()
        {
            if (!beganTextureUploadPipeline)
                return;

            FrameStatistics.Increment(StatisticsCounterType.TextureUploadFlushes);
            textureUploadPipeline.End();
            beganTextureUploadPipeline = false;
        }

        /// <summary>
        /// Checks whether the given frame buffer is currently bound.
        /// </summary>
        /// <param name="frameBuffer">The frame buffer to check.</param>
        public bool IsFrameBufferBound(IFrameBuffer frameBuffer)
            => FrameBuffer == frameBuffer;

        protected internal override Image<Rgba32> TakeScreenshot()
            => veldridDevice.TakeScreenshot();

        protected internal override Image<Rgba32>? ExtractFrameBufferData(IFrameBuffer frameBuffer)
            => ExtractTexture((VeldridTexture)frameBuffer.Texture.NativeTexture);

        protected internal Image<Rgba32>? ExtractTexture(VeldridTexture texture)
        {
            var resource = texture.GetResourceList().FirstOrDefault();
            if (resource == null)
                return null;

            return veldridDevice.ExtractTexture<Rgba32>(resource.Texture);
        }

        /// <summary>
        /// Fast path: copies the framebuffer pixel data directly into a caller-supplied byte span,
        /// avoiding the ImageSharp heap allocation and one 8 MB CPU memcpy per frame.
        /// </summary>
        internal bool ExtractFrameBufferDataToBuffer(IFrameBuffer frameBuffer, Span<byte> destination)
        {
            var resource = ((VeldridTexture)frameBuffer.Texture.NativeTexture).GetResourceList().FirstOrDefault();
            return resource != null && veldridDevice.ExtractTextureToBuffer(resource.Texture, destination);
        }

        internal VeldridDevice.PendingTextureReadback? BeginExtractFrameBufferDataToBuffer(IFrameBuffer frameBuffer, VeldridDevice.PendingTextureReadback? request = null)
        {
            var resource = ((VeldridTexture)frameBuffer.Texture.NativeTexture).GetResourceList().FirstOrDefault();
            return resource == null ? null : veldridDevice.BeginTextureReadback(resource.Texture, request);
        }

        internal bool CompleteExtractFrameBufferDataToBuffer(VeldridDevice.PendingTextureReadback request, Span<byte> destination, int millisecondsTimeout = 5000)
            => veldridDevice.CompleteTextureReadback(request, destination, millisecondsTimeout);

        internal VeldridDevice.PendingTextureReadback BeginBackbufferReadback(VeldridDevice.PendingTextureReadback? request = null)
            => veldridDevice.BeginBackbufferReadback(request);

        internal bool CompleteBackbufferReadback(VeldridDevice.PendingTextureReadback request, Span<byte> destination, int millisecondsTimeout = 5000, bool preserveBgra = false)
            => veldridDevice.CompleteTextureReadback(request, destination, millisecondsTimeout, preserveBgra);

        /// <summary>
        /// Updates a <see cref="global::Veldrid.Texture"/> with a <paramref name="data"/> at the specified coordinates.
        /// </summary>
        /// <param name="texture">The <see cref="global::Veldrid.Texture"/> to update.</param>
        /// <param name="x">The X coordinate of the update region.</param>
        /// <param name="y">The Y coordinate of the update region.</param>
        /// <param name="width">The width of the update region.</param>
        /// <param name="height">The height of the update region.</param>
        /// <param name="level">The texture level.</param>
        /// <param name="data">The texture data.</param>
        /// <typeparam name="T">The pixel type.</typeparam>
        public void UpdateTexture<T>(global::Veldrid.Texture texture, int x, int y, int width, int height, int level, ReadOnlySpan<T> data)
            where T : unmanaged
        {
            FrameStatistics.Increment(StatisticsCounterType.TextureUploads);

            if (!FrameworkEnvironment.RendererCounters)
            {
                ensureTextureUploadPipelineBegan();
                textureUploadPipeline.UpdateTexture(stagingTexturePool, texture, x, y, width, height, level, data);
                return;
            }

            long start = Stopwatch.GetTimestamp();
            ensureTextureUploadPipelineBegan();
            textureUploadPipeline.UpdateTexture(stagingTexturePool, texture, x, y, width, height, level, data);
            FrameStatistics.Add(StatisticsCounterType.VeldridTextureUploadUs, elapsedMicroseconds(start));
        }

        /// <summary>
        /// Updates a <see cref="global::Veldrid.Texture"/> with a <paramref name="data"/> at the specified coordinates.
        /// </summary>
        /// <param name="texture">The <see cref="global::Veldrid.Texture"/> to update.</param>
        /// <param name="x">The X coordinate of the update region.</param>
        /// <param name="y">The Y coordinate of the update region.</param>
        /// <param name="width">The width of the update region.</param>
        /// <param name="height">The height of the update region.</param>
        /// <param name="level">The texture level.</param>
        /// <param name="data">The texture data.</param>
        /// <param name="rowLengthInBytes">The number of bytes per row of the image to read from <paramref name="data"/>.</param>
        public void UpdateTexture(global::Veldrid.Texture texture, int x, int y, int width, int height, int level, IntPtr data, int rowLengthInBytes)
        {
            FrameStatistics.Increment(StatisticsCounterType.TextureUploads);

            if (!FrameworkEnvironment.RendererCounters)
            {
                bufferUpdatePipeline.UpdateTexture(stagingTexturePool, texture, x, y, width, height, level, data, rowLengthInBytes);
                return;
            }

            long start = Stopwatch.GetTimestamp();
            bufferUpdatePipeline.UpdateTexture(stagingTexturePool, texture, x, y, width, height, level, data, rowLengthInBytes);
            FrameStatistics.Add(StatisticsCounterType.VeldridTextureUploadUs, elapsedMicroseconds(start));
        }

        protected override void SetUniformImplementation<T>(IUniformWithValue<T> uniform)
        {
        }

        protected override void SetUniformBufferImplementation(string blockName, IUniformBuffer buffer)
            => graphicsPipeline.AttachUniformBuffer(blockName, (IVeldridUniformBuffer)buffer);

        public void RegisterUniformBufferForReset(IVeldridUniformBuffer buffer)
            => uniformBufferResetList.Add(buffer);

        public void GenerateMipmaps(VeldridTexture texture)
            => graphicsPipeline.GenerateMipmaps(texture);

        public CommandList BufferUpdateCommands
            => bufferUpdatePipeline.Commands;

        void IVeldridRenderer.BindShader(VeldridShader shader)
            => BindShader(shader);

        void IVeldridRenderer.UnbindShader(VeldridShader shader)
            => UnbindShader(shader);

        void IVeldridRenderer.EnqueueTextureUpload(VeldridTexture texture)
            => EnqueueTextureUpload(texture);

        protected override IShaderPart CreateShaderPart(IShaderStore store, string name, byte[]? rawData, ShaderPartType partType)
            => new VeldridShaderPart(this, rawData, partType, store);

        protected override IShader CreateShader(string name, IShaderPart[] parts, ShaderCompilationStore compilationStore)
            => new VeldridShader(this, name, parts.Cast<VeldridShaderPart>().ToArray(), compilationStore);

        public override IFrameBuffer CreateFrameBuffer(TexturePixelFormat textureFormat = TexturePixelFormat.R8G8B8A8Float, RenderBufferFormat[]? renderBufferFormats = null, TextureFilteringMode filteringMode = TextureFilteringMode.Linear)
            => new VeldridFrameBuffer(this, textureFormat.ToPixelFormat(), renderBufferFormats?.ToPixelFormats(), filteringMode.ToSamplerFilter());

        protected override IVertexBatch<TVertex> CreateLinearBatch<TVertex>(int size, int maxBuffers, PrimitiveTopology primitiveType)
            // maxBuffers is ignored because batches are not allowed to wrap around in Veldrid.
            => new VeldridLinearBatch<TVertex>(this, size, primitiveType);

        protected override IVertexBatch<TVertex> CreateQuadBatch<TVertex>(int size, int maxBuffers)
            // maxBuffers is ignored because batches are not allowed to wrap around in Veldrid.
            => new VeldridQuadBatch<TVertex>(this, size);

        protected override IUniformBuffer<TData> CreateUniformBuffer<TData>()
            => new VeldridUniformBuffer<TData>(this);

        protected override IShaderStorageBufferObject<TData> CreateShaderStorageBufferObject<TData>(int uboSize, int ssboSize)
            => new VeldridShaderStorageBufferObject<TData>(this, uboSize, ssboSize);

        protected override INativeTexture CreateNativeTexture(int width, int height, bool manualMipmaps = false, TextureFilteringMode filteringMode = TextureFilteringMode.Linear,
                                                              Color4? initialisationColour = null)
            => new VeldridTexture(this, width, height, PixelFormat.R8G8B8A8UNorm, manualMipmaps, filteringMode.ToSamplerFilter(), initialisationColour);

        protected override INativeTexture CreateNativeVideoTexture(int width, int height)
            => new VeldridVideoTexture(this, width, height);

        internal IStagingBuffer<T> CreateStagingBuffer<T>(uint count)
            where T : unmanaged
        {
            switch (FrameworkEnvironment.StagingBufferType)
            {
                case 0:
                    return new ManagedStagingBuffer<T>(this, count);

                case 1:
                    return new PersistentStagingBuffer<T>(this, count);

                case 2:
                    return new DeferredStagingBuffer<T>(this, count);

                default:
                    switch (Device.BackendType)
                    {
                        case GraphicsBackend.Direct3D11:
                            return new DeferredStagingBuffer<T>(this, count);

                        case GraphicsBackend.Vulkan:
                            return new PersistentStagingBuffer<T>(this, count);

                        default:
                        // Metal uses a more optimal path that elides a Blit Command Encoder.
                        case GraphicsBackend.Metal:
                        // OpenGL backends need additional work to support coherency and persistently mapped buffers.
                        case GraphicsBackend.OpenGL:
                        case GraphicsBackend.OpenGLES:
                            return new ManagedStagingBuffer<T>(this, count);
                    }
            }
        }

        private static long elapsedMicroseconds(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) * 1_000_000 / Stopwatch.Frequency;
    }
}
