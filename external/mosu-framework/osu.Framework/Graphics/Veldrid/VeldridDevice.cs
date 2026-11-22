// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using osu.Framework.Development;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using Veldrid;
using Veldrid.OpenGL;
using Veldrid.OpenGLBindings;

namespace osu.Framework.Graphics.Veldrid
{
    /// <summary>
    /// A Veldrid graphics device that provides support for device pipelines.
    /// </summary>
    internal class VeldridDevice
    {
        /// <summary>
        /// The platform graphics device.
        /// </summary>
        public GraphicsDevice Device { get; }

        /// <summary>
        /// The platform graphics resource factory.
        /// </summary>
        public ResourceFactory Factory
            => Device.ResourceFactory;

        /// <summary>
        /// The graphics surface type.
        /// </summary>
        public GraphicsSurfaceType SurfaceType
            => graphicsSurface.Type;

        /// <summary>
        /// Enables or disables vertical sync.
        /// </summary>
        public bool VerticalSync
        {
            get => Device.SyncToVerticalBlank;
            set => Device.SyncToVerticalBlank = value;
        }

        /// <summary>
        /// Gets or sets whether the device should render new frames without waiting for previous ones to finish compositing.
        /// </summary>
        public bool AllowTearing
        {
            get => Device.AllowTearing;
            set => Device.AllowTearing = value;
        }

        /// <summary>
        /// Whether the depth is in the range [0, 1] (i.e. Reversed-Z). If <c>false</c>, depth is in the range [-1, 1].
        /// </summary>
        public bool IsDepthRangeZeroToOne
            => Device.IsDepthRangeZeroToOne;

        /// <summary>
        /// Whether the texture coordinates begin in the top-left of the texture. If <c>false</c>, (0, 0) corresponds to the bottom-left texel of the texture.
        /// </summary>
        public bool IsUvOriginTopLeft
            => Device.IsUvOriginTopLeft;

        /// <summary>
        /// Whether the y-coordinate ranges from -1 (top) to 1 (bottom). If <c>false</c>, the y-coordinate ranges from -1 (bottom) to 1 (top).
        /// </summary>
        public bool IsClipSpaceYInverted
            => Device.IsClipSpaceYInverted;

        /// <summary>
        /// Whether shader storage buffer objects can be used.
        /// </summary>
        public bool UseStructuredBuffers
            => !FrameworkEnvironment.NoStructuredBuffers && Device.Features.StructuredBuffer;

        /// <summary>
        /// The maximum allowed texture size.
        /// </summary>
        public int MaxTextureSize { get; }

        private readonly IGraphicsSurface graphicsSurface;
        private Vector2I currentWindowSize;
        private Texture? cachedExtractStagingTexture;
        private CommandList? cachedExtractCommandList;
        private Fence? cachedExtractFence;
        private uint cachedExtractWidth;
        private uint cachedExtractHeight;
        private uint cachedExtractMipLevels;
        private uint cachedExtractArrayLayers;
        private PixelFormat cachedExtractFormat;

        internal sealed class PendingTextureReadback : IDisposable
        {
            public Texture? StagingTexture;
            public CommandList? CommandList;
            public Fence? Fence;
            public uint Width;
            public uint Height;
            public uint MipLevels;
            public uint ArrayLayers;
            public PixelFormat Format;
            public bool Submitted;
            public bool RequiresBgraToRgbaSwizzle;

            public void Dispose()
            {
                StagingTexture?.Dispose();
                CommandList?.Dispose();
                Fence?.Dispose();
                StagingTexture = null;
                CommandList = null;
                Fence = null;
                Submitted = false;
            }
        }

        /// <summary>
        /// Creates a new <see cref="VeldridDevice"/>
        /// </summary>
        /// <param name="graphicsSurface"></param>
        /// <param name="preferredAdapter">Backend-specific preferred GPU identifier, or empty for automatic selection.</param>
        /// <exception cref="InvalidOperationException"></exception>
        public VeldridDevice(IGraphicsSurface graphicsSurface, string preferredAdapter = "")
        {
            // Veldrid must either be initialised on the main/"input" thread, or in a separate thread away from the draw thread at least.
            // Otherwise the window may not render anything on some platforms (macOS at least).
            Debug.Assert(!ThreadSafety.IsDrawThread, "Veldrid cannot be initialised on the draw thread.");

            this.graphicsSurface = graphicsSurface;

            var options = new GraphicsDeviceOptions
            {
                HasMainSwapchain = true,
                SwapchainDepthFormat = PixelFormat.R16UNorm,
                SyncToVerticalBlank = true,
                ResourceBindingModel = ResourceBindingModel.Improved,
            };

            var size = this.graphicsSurface.GetDrawableSize();

            var swapchain = new SwapchainDescription
            {
                Width = (uint)size.Width,
                Height = (uint)size.Height,
                ColorSrgb = options.SwapchainSrgbFormat,
                DepthFormat = options.SwapchainDepthFormat,
                SyncToVerticalBlank = options.SyncToVerticalBlank,
            };

            int maxTextureSize;

            switch (RuntimeInfo.OS)
            {
                case RuntimeInfo.Platform.Windows:
                {
                    swapchain.Source = SwapchainSource.CreateWin32(this.graphicsSurface.WindowHandle, IntPtr.Zero);
                    break;
                }

                case RuntimeInfo.Platform.macOS:
                {
                    // OpenGL doesn't use a swapchain, so it's only needed on Metal.
                    // Creating a Metal surface in general would otherwise destroy the GL context.
                    if (this.graphicsSurface.Type == GraphicsSurfaceType.Metal)
                    {
                        var metalGraphics = (IMetalGraphicsSurface)this.graphicsSurface;
                        swapchain.Source = SwapchainSource.CreateNSView(metalGraphics.CreateMetalView());
                    }

                    break;
                }

                case RuntimeInfo.Platform.iOS:
                {
                    // OpenGL doesn't use a swapchain, so it's only needed on Metal.
                    // Creating a Metal surface in general would otherwise destroy the GL context.
                    if (this.graphicsSurface.Type == GraphicsSurfaceType.Metal)
                    {
                        var metalGraphics = (IMetalGraphicsSurface)this.graphicsSurface;
                        swapchain.Source = SwapchainSource.CreateUIView(metalGraphics.CreateMetalView());
                    }

                    break;
                }

                case RuntimeInfo.Platform.Linux:
                {
                    var linuxGraphics = (ILinuxGraphicsSurface)this.graphicsSurface;
                    swapchain.Source = linuxGraphics.IsWayland
                        ? SwapchainSource.CreateWayland(linuxGraphics.DisplayHandle, this.graphicsSurface.WindowHandle)
                        : SwapchainSource.CreateXlib(linuxGraphics.DisplayHandle, this.graphicsSurface.WindowHandle);
                    break;
                }

                case RuntimeInfo.Platform.Android:
                {
                    var androidGraphics = (IAndroidGraphicsSurface)this.graphicsSurface;
                    swapchain.Source = SwapchainSource.CreateAndroidSurface(androidGraphics.SurfaceHandle, androidGraphics.JniEnvHandle);
                    break;
                }
            }

            switch (this.graphicsSurface.Type)
            {
                case GraphicsSurfaceType.OpenGL:
                    var openGLGraphics = (IOpenGLGraphicsSurface)this.graphicsSurface;
                    var openGLInfo = new OpenGLPlatformInfo(
                        openGLContextHandle: openGLGraphics.WindowContext,
                        getProcAddress: openGLGraphics.GetProcAddress,
                        makeCurrent: openGLGraphics.MakeCurrent,
                        getCurrentContext: () => openGLGraphics.CurrentContext,
                        clearCurrentContext: openGLGraphics.ClearCurrent,
                        deleteContext: openGLGraphics.DeleteContext,
                        swapBuffers: openGLGraphics.SwapBuffers,
                        setSyncToVerticalBlank: v => openGLGraphics.VerticalSync = v,
                        setSwapchainFramebuffer: () => OpenGLNative.glBindFramebuffer(FramebufferTarget.Framebuffer, (uint)(openGLGraphics.BackbufferFramebuffer ?? 0)),
                        null);

                    Device = GraphicsDevice.CreateOpenGL(options, openGLInfo, swapchain.Width, swapchain.Height);
                    Device.LogOpenGL(out maxTextureSize);
                    break;

                case GraphicsSurfaceType.Vulkan:
                    Device = GraphicsDevice.CreateVulkan(options, swapchain, new VulkanDeviceOptions { PreferredPhysicalDeviceId = preferredAdapter });
                    Device.LogVulkan(out maxTextureSize);
                    break;

                case GraphicsSurfaceType.Direct3D11:
                    Device = GraphicsAdapterSelection.CreateDirect3D(options, swapchain, preferredAdapter);
                    Device.LogD3D11(out maxTextureSize);

                    // Mosu experiment: the tearing swapchain has no latency cap (DXGI device default of
                    // 3 queued frames); allow capping it for input latency when GPU-bound. Unset = no change.
                    if (uint.TryParse(Environment.GetEnvironmentVariable("MOSU_DXGI_MAX_FRAME_LATENCY"), out uint frameLatency)
                        && frameLatency is >= 1 and <= 3)
                    {
                        Logger.Log($"IDXGIDevice1.SetMaximumFrameLatency({frameLatency}): {(DxgiInterop.TrySetMaximumFrameLatency(Device, frameLatency) ? "ok" : "failed")}");
                    }

                    break;

                case GraphicsSurfaceType.Metal:
                    Device = GraphicsDevice.CreateMetal(options, swapchain);
                    Device.LogMetal(out maxTextureSize);
                    break;

                default:
                    throw new InvalidOperationException();
            }

            Logger.Log($"{nameof(UseStructuredBuffers)}: {UseStructuredBuffers}");

            MaxTextureSize = maxTextureSize;
        }

        /// <summary>
        /// Notifies the device that a new frame has started.
        /// </summary>
        /// <param name="windowSize">The window size.</param>
        public void Resize(Vector2I windowSize)
        {
            if (windowSize != currentWindowSize)
            {
                Device.ResizeMainWindow((uint)windowSize.X, (uint)windowSize.Y);
                currentWindowSize = windowSize;
            }
        }

        /// <summary>
        /// Swaps the back buffer with the front buffer to display the new frame.
        /// </summary>
        public void SwapBuffers()
        {
            if (!FrameworkEnvironment.RendererCounters)
            {
                Device.SwapBuffers();
                return;
            }

            long start = Stopwatch.GetTimestamp();
            Device.SwapBuffers();
            FrameStatistics.Add(StatisticsCounterType.VeldridSwapBuffersUs, elapsedMicroseconds(start));
        }

        private static long elapsedMicroseconds(long startTimestamp)
            => (Stopwatch.GetTimestamp() - startTimestamp) * 1_000_000 / Stopwatch.Frequency;

        /// <summary>
        /// Waits until all renderer commands have been fully executed GPU-side, as signaled by the graphics backend.
        /// </summary>
        /// <remarks>
        /// This is equivalent to a <c>glFinish</c> call.
        /// </remarks>
        public void WaitUntilIdle()
            => Device.WaitForIdle();

        /// <summary>
        /// Waits until the GPU signals that the next frame is ready to be rendered.
        /// </summary>
        public void WaitUntilNextFrameReady()
            => Device.WaitForNextFrameReady();

        /// <summary>
        /// Invoked when the rendering thread is active and commands will be enqueued.
        /// This is mainly required for OpenGL renderers to mark context as current before performing GL calls.
        /// </summary>
        public void MakeCurrent()
        {
            if (graphicsSurface.Type == GraphicsSurfaceType.OpenGL)
            {
                var openGLGraphics = (IOpenGLGraphicsSurface)graphicsSurface;
                openGLGraphics.MakeCurrent(openGLGraphics.WindowContext);
            }
        }

        /// <summary>
        /// Invoked when the rendering thread is suspended and no more commands will be enqueued.
        /// This is mainly required for OpenGL renderers to mark context as current before performing GL calls.
        /// </summary>
        public void ClearCurrent()
        {
            if (graphicsSurface.Type == GraphicsSurfaceType.OpenGL)
            {
                var openGLGraphics = (IOpenGLGraphicsSurface)graphicsSurface;
                openGLGraphics.ClearCurrent();
            }
        }

        /// <summary>
        /// Returns an image containing the current content of the backbuffer, i.e. takes a screenshot.
        /// </summary>
        public unsafe Image<Rgba32> TakeScreenshot()
        {
            var texture = Device.SwapchainFramebuffer.ColorTargets[0].Target;

            switch (graphicsSurface.Type)
            {
                // Veldrid doesn't support copying content from a swapchain framebuffer texture on OpenGL.
                // OpenGL already provides a method for reading pixels directly from the active framebuffer, so let's just use that for now.
                case GraphicsSurfaceType.OpenGL:
                {
                    var pixelData = SixLabors.ImageSharp.Configuration.Default.MemoryAllocator.Allocate<Rgba32>((int)(texture.Width * texture.Height));

                    var info = Device.GetOpenGLInfo();

                    info.ExecuteOnGLThread(() =>
                    {
                        fixed (Rgba32* data = pixelData.Memory.Span)
                            OpenGLNative.glReadPixels(0, 0, texture.Width, texture.Height, GLPixelFormat.Rgba, GLPixelType.UnsignedByte, data);
                    });

                    var glImage = Image.LoadPixelData<Rgba32>(pixelData.Memory.Span, (int)texture.Width, (int)texture.Height);
                    glImage.Mutate(i => i.Flip(FlipMode.Vertical));
                    return glImage;
                }

                default:
                    return VeldridReadbackFormat.RequiresBgraToRgbaSwizzle(texture.Format)
                        ? ExtractTexture<Bgra32>(texture, flipVertical: !Device.IsUvOriginTopLeft)
                        : ExtractTexture<Rgba32>(texture, flipVertical: !Device.IsUvOriginTopLeft);
            }
        }

        public unsafe Image<Rgba32> ExtractTexture<TPixel>(Texture texture, bool flipVertical = false)
            where TPixel : unmanaged, IPixel<TPixel>
        {
            uint width = texture.Width;
            uint height = texture.Height;

            ensureCachedExtractResources(texture);

            Device.ResetFence(cachedExtractFence!);
            cachedExtractCommandList!.Begin();
            cachedExtractCommandList.CopyTexture(texture, cachedExtractStagingTexture!);
            cachedExtractCommandList.End();
            Device.SubmitCommands(cachedExtractCommandList, cachedExtractFence);

            if (!waitForFence(cachedExtractFence!, 5000))
            {
                Logger.Log("Failed to capture framebuffer content within reasonable time.", level: LogLevel.Important);
                return new Image<Rgba32>((int)width, (int)height);
            }

            var resource = Device.Map(cachedExtractStagingTexture, MapMode.Read);
            var span = new Span<TPixel>(resource.Data.ToPointer(), (int)(resource.SizeInBytes / Marshal.SizeOf<TPixel>()));

            // on some backends (Direct3D11, in particular), the staging resource data may contain padding at the end of each row for alignment,
            // which means that for the image width, we cannot use the framebuffer's width raw.
            Image<TPixel> image = Image.LoadPixelData<TPixel>(span, (int)(resource.RowPitch / Marshal.SizeOf<TPixel>()), (int)height);

            if (flipVertical)
                image.Mutate(i => i.Flip(FlipMode.Vertical));

            // if the image width doesn't match the framebuffer, it means that we still have padding at the end of each row mentioned above to get rid of.
            // snip it to get a clean image.
            if (image.Width != width)
                image.Mutate(i => i.Crop((int)texture.Width, (int)texture.Height));

            Device.Unmap(cachedExtractStagingTexture);

            if (!flipVertical && image.Width == width && typeof(TPixel) == typeof(Rgba32))
                return (Image<Rgba32>)(object)image;

            using (image)
                return image.CloneAs<Rgba32>();
        }

        /// <summary>
        /// Copies the contents of a GPU texture directly into a caller-supplied byte span (RGBA, row-major),
        /// skipping the ImageSharp intermediate allocation entirely.
        /// </summary>
        /// <returns><c>true</c> on success; <c>false</c> if the GPU copy timed out or the destination is too small.</returns>
        public unsafe bool ExtractTextureToBuffer(Texture texture, Span<byte> destination)
        {
            uint width = texture.Width;
            uint height = texture.Height;
            int dstStride = (int)(width * 4);
            int totalBytes = dstStride * (int)height;

            if (destination.Length < totalBytes)
                return false;

            ensureCachedExtractResources(texture);

            Device.ResetFence(cachedExtractFence!);
            cachedExtractCommandList!.Begin();
            cachedExtractCommandList.CopyTexture(texture, cachedExtractStagingTexture!);
            cachedExtractCommandList.End();
            Device.SubmitCommands(cachedExtractCommandList, cachedExtractFence);

            if (!waitForFence(cachedExtractFence!, 5000))
            {
                Logger.Log("Failed to capture framebuffer content (buffer path) within reasonable time.", level: LogLevel.Important);
                return false;
            }

            var resource = Device.Map(cachedExtractStagingTexture, MapMode.Read);
            int srcStride = (int)resource.RowPitch;

            if (srcStride == dstStride)
            {
                // No row padding — single flat copy.
                new Span<byte>(resource.Data.ToPointer(), totalBytes).CopyTo(destination[..totalBytes]);
            }
            else
            {
                // Strip D3D11 row-pitch padding row by row.
                byte* src = (byte*)resource.Data.ToPointer();
                for (int y = 0; y < (int)height; y++)
                    new Span<byte>(src + y * srcStride, dstStride).CopyTo(destination.Slice(y * dstStride, dstStride));
            }

            Device.Unmap(cachedExtractStagingTexture);
            return true;
        }

        internal PendingTextureReadback BeginTextureReadback(Texture texture, PendingTextureReadback? request = null)
        {
            request ??= new PendingTextureReadback();

            ensureReadbackResources(texture, request);

            Device.ResetFence(request.Fence!);
            request.CommandList!.Begin();
            request.CommandList.CopyTexture(texture, request.StagingTexture!);
            request.CommandList.End();
            Device.SubmitCommands(request.CommandList, request.Fence);
            request.Submitted = true;

            return request;
        }

        internal PendingTextureReadback BeginBackbufferReadback(PendingTextureReadback? request = null)
        {
            var texture = Device.SwapchainFramebuffer.ColorTargets[0].Target;
            bool requiresSwizzle = VeldridReadbackFormat.RequiresBgraToRgbaSwizzle(texture.Format);
            request = BeginTextureReadback(texture, request);
            request.RequiresBgraToRgbaSwizzle = requiresSwizzle;
            return request;
        }

        internal unsafe bool CompleteTextureReadback(PendingTextureReadback request, Span<byte> destination, int millisecondsTimeout = 5000, bool preserveBgra = false)
        {
            if (!request.Submitted)
                return false;

            int dstStride = (int)(request.Width * 4);
            int totalBytes = dstStride * (int)request.Height;

            if (destination.Length < totalBytes)
                return false;

            if (!waitForFence(request.Fence!, millisecondsTimeout))
            {
                Logger.Log("Failed to complete framebuffer content readback within reasonable time.", level: LogLevel.Important);
                return false;
            }

            var resource = Device.Map(request.StagingTexture!, MapMode.Read);
            int srcStride = (int)resource.RowPitch;

            try
            {
                var source = new ReadOnlySpan<byte>(resource.Data.ToPointer(), checked(srcStride * ((int)request.Height - 1) + dstStride));
                VeldridReadbackFormat.CopyPixels(source, srcStride, destination, (int)request.Width, (int)request.Height,
                    request.RequiresBgraToRgbaSwizzle && !preserveBgra);
            }
            finally
            {
                Device.Unmap(request.StagingTexture!);
            }

            request.Submitted = false;
            return true;
        }

        private void ensureCachedExtractResources(Texture texture)
        {
            bool needsNewStagingTexture = cachedExtractStagingTexture == null
                                          || cachedExtractWidth != texture.Width
                                          || cachedExtractHeight != texture.Height
                                          || cachedExtractMipLevels != texture.MipLevels
                                          || cachedExtractArrayLayers != texture.ArrayLayers
                                          || cachedExtractFormat != texture.Format;

            if (needsNewStagingTexture)
            {
                cachedExtractStagingTexture?.Dispose();
                cachedExtractStagingTexture = Factory.CreateTexture(TextureDescription.Texture2D(texture.Width, texture.Height, texture.MipLevels, texture.ArrayLayers, texture.Format, TextureUsage.Staging));
                cachedExtractWidth = texture.Width;
                cachedExtractHeight = texture.Height;
                cachedExtractMipLevels = texture.MipLevels;
                cachedExtractArrayLayers = texture.ArrayLayers;
                cachedExtractFormat = texture.Format;
            }

            cachedExtractCommandList ??= Factory.CreateCommandList();
            cachedExtractFence ??= Factory.CreateFence(false);
        }

        private void ensureReadbackResources(Texture texture, PendingTextureReadback request)
        {
            bool needsNewStagingTexture = request.StagingTexture == null
                                          || request.Width != texture.Width
                                          || request.Height != texture.Height
                                          || request.MipLevels != texture.MipLevels
                                          || request.ArrayLayers != texture.ArrayLayers
                                          || request.Format != texture.Format;

            if (needsNewStagingTexture)
            {
                request.StagingTexture?.Dispose();
                request.StagingTexture = Factory.CreateTexture(TextureDescription.Texture2D(texture.Width, texture.Height, texture.MipLevels, texture.ArrayLayers, texture.Format, TextureUsage.Staging));
                request.Width = texture.Width;
                request.Height = texture.Height;
                request.MipLevels = texture.MipLevels;
                request.ArrayLayers = texture.ArrayLayers;
                request.Format = texture.Format;
            }

            request.CommandList ??= Factory.CreateCommandList();
            request.Fence ??= Factory.CreateFence(false);
        }

        /// <summary>
        /// Waits for a <see cref="Fence"/> to be signalled.
        /// </summary>
        /// <param name="fence">The fence.</param>
        /// <param name="millisecondsTimeout">The maximum amount of time to wait.</param>
        /// <returns>Whether the fence was signalled.</returns>
        private bool waitForFence(Fence fence, int millisecondsTimeout)
        {
            // todo: Metal doesn't support WaitForFence due to lack of implementation and bugs with supporting MTLSharedEvent.notifyListener,
            // until that is fixed in some way or another, poll on the signal state.
            if (graphicsSurface.Type == GraphicsSurfaceType.Metal)
            {
                const int sleep_time = 10;

                while (!fence.Signaled && (millisecondsTimeout -= sleep_time) > 0)
                    Thread.Sleep(sleep_time);

                return fence.Signaled;
            }

            return Device.WaitForFence(fence, (ulong)(millisecondsTimeout * 1_000_000));
        }
    }
}
