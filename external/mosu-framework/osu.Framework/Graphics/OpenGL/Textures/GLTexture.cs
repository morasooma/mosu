// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;
using osu.Framework.Development;
using osu.Framework.Extensions.ImageExtensions;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using osuTK.Graphics;
using osuTK.Graphics.ES30;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace osu.Framework.Graphics.OpenGL.Textures
{
    internal class GLTexture : INativeTexture
    {
        protected readonly GLRenderer Renderer;
        private readonly Queue<ITextureUpload> uploadQueue = new Queue<ITextureUpload>();

        // A full-HD image fits the renderer's aggregate frame budget but can still spend several
        // milliseconds in one native transfer. Bound each independent image slice to 2 MiB RGBA.
        private const int maxPixelsPerUploadSlice = 512 * 1024;

        private ITextureUpload pendingUpload;
        private int pendingUploadRows;
        private IntPtr pendingUploadFence;
        private bool pendingUploadFenceFailed;
        private bool uploading;
        private bool discardUploads;

        // Mirrors queued and in-progress uploads while holding uploadQueue's lock.
        private volatile bool uploadComplete = true;

        IRenderer INativeTexture.Renderer => Renderer;

        public string Identifier
        {
            get
            {
                if (!Available || textureId == 0)
                    return "-";

                return textureId.ToString();
            }
        }

        public int MaxSize => Renderer.MaxTextureSize;

        public virtual int Width { get; set; }
        public virtual int Height { get; set; }
        public virtual int GetByteSize() => Width * Height * 4;
        public bool Available { get; private set; } = true;

        private int? mipLevel;

        public int? MipLevel
        {
            get => mipLevel;
            set
            {
                mipLevel = value;
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinLod, mipLevel ?? 0);
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLod, mipLevel ?? IRenderer.MAX_MIPMAP_LEVELS);
            }
        }

        ulong INativeTexture.TotalBindCount { get; set; }

        public bool BypassTextureUploadQueueing { get; set; }

        protected TextureComponentCount InternalFormat { get; private set; }
        protected PixelFormat PixelFormat { get; private set; }
        protected PixelType PixelType { get; private set; }

        private int internalWidth;
        private int internalHeight;
        private bool manualMipmaps;

        private readonly List<RectangleI> uploadedRegions = new List<RectangleI>();

        private readonly All filteringMode;
        private readonly Color4? initialisationColour;

        internal bool CanShareTextLod(GLTexture other) => GetType() == typeof(GLTexture) && other.GetType() == typeof(GLTexture)
                                                         && Width == other.Width && Height == other.Height
                                                         && manualMipmaps == other.manualMipmaps && filteringMode == other.filteringMode
                                                         && MipLevel == other.MipLevel && InternalFormat == other.InternalFormat;

        /// <summary>
        /// Creates a new <see cref="GLTexture"/>.
        /// </summary>
        /// <param name="renderer"></param>
        /// <param name="width">The width of the texture.</param>
        /// <param name="height">The height of the texture.</param>
        /// <param name="textureFormat">The pixel format of the texture.</param>
        /// <param name="manualMipmaps">Whether manual mipmaps will be uploaded to the texture. If false, the texture will compute mipmaps automatically.</param>
        /// <param name="filteringMode">The filtering mode.</param>
        /// <param name="initialisationColour">The colour to initialise texture levels with (in the case of sub region initial uploads). If null, no initialisation is provided out-of-the-box.</param>
        public GLTexture(GLRenderer renderer, int width, int height, TextureComponentCount textureFormat = TextureComponentCount.Rgba8, bool manualMipmaps = false, All filteringMode = All.Linear, Color4? initialisationColour = null)
        {
            Renderer = renderer;
            Width = width;
            Height = height;

            setFormat(textureFormat);

            this.manualMipmaps = manualMipmaps;
            this.filteringMode = filteringMode;
            this.initialisationColour = initialisationColour;
        }

        private void setFormat(TextureComponentCount internalFormat)
        {
            InternalFormat = internalFormat;

            // reference: https://registry.khronos.org/OpenGL-Refpages/gl4/html/glTexImage2D.xhtml
            // add more formats as desired
            switch (internalFormat)
            {
                case TextureComponentCount.Rgba8:
                    PixelFormat = PixelFormat.Rgba;
                    PixelType = PixelType.UnsignedByte;
                    break;

                case TextureComponentCount.R8:
                    PixelFormat = PixelFormat.Red;
                    PixelType = PixelType.UnsignedByte;
                    break;

                case TextureComponentCount.R16f:
                    PixelFormat = PixelFormat.Red;
                    PixelType = PixelType.HalfFloat;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(internalFormat), internalFormat, null);
            }
        }

        #region Disposal

        private bool isDisposed;

        public void Dispose()
        {
            if (isDisposed)
                return;

            isDisposed = true;

            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~GLTexture()
        {
            Dispose(false);
        }

        protected virtual void Dispose(bool isDisposing)
        {
            Renderer.ScheduleDisposal(texture =>
            {
                texture.FlushUploads();
                texture.clearUploadFence();

                int disposableId = texture.textureId;

                if (disposableId <= 0)
                    return;

                GL.DeleteTextures(1, new[] { disposableId });

                texture.memoryLease?.Dispose();

                texture.textureId = 0;
                texture.Available = false;
            }, this);
        }

        #endregion

        #region Memory Tracking

        private List<long> levelMemoryUsage = new List<long>();

        private NativeMemoryTracker.NativeMemoryLease memoryLease;

        private void updateMemoryUsage(int level, long newUsage)
        {
            levelMemoryUsage ??= new List<long>();

            while (level >= levelMemoryUsage.Count)
                levelMemoryUsage.Add(0);

            levelMemoryUsage[level] = newUsage;

            memoryLease?.Dispose();
            memoryLease = NativeMemoryTracker.AddMemory(this, getMemoryUsage());
        }

        private long getMemoryUsage()
        {
            long usage = 0;

            for (int i = 0; i < levelMemoryUsage.Count; i++)
                usage += levelMemoryUsage[i];

            return usage;
        }

        #endregion

        private int textureId;

        public virtual int TextureId
        {
            get
            {
                ObjectDisposedException.ThrowIf(!Available, this);

                return textureId;
            }
        }

        public void FlushUploads()
        {
            lock (uploadQueue)
            {
                if (uploading)
                {
                    discardUploads = true;
                    return;
                }

                pendingUpload?.Dispose();
                pendingUpload = null;
                pendingUploadRows = 0;

                while (uploadQueue.TryDequeue(out var upload))
                    upload.Dispose();

                uploadComplete = true;
            }
        }

        public void SetData(ITextureUpload upload)
        {
            lock (uploadQueue)
            {
                if (uploadQueue.Count >= 100 && uploadQueue.Count % 100 == 0)
                    Logger.Log($"Texture {Identifier}'s upload queue is large ({uploadQueue.Count})");

                bool requireUpload = uploadQueue.Count == 0 && pendingUpload == null;
                uploadQueue.Enqueue(upload);
                uploadComplete = false;

                if (requireUpload && !BypassTextureUploadQueueing)
                    Renderer.EnqueueTextureUpload(this);
            }
        }

        public bool Upload() => upload(int.MaxValue, false, out _);

        public int Upload(int maxPixels)
        {
            upload(Math.Clamp(maxPixels, 1, maxPixelsPerUploadSlice), true, out int uploadedPixels);
            return uploadedPixels;
        }

        private bool upload(int maxPixels, bool budgeted, out int uploadedPixels)
        {
            uploadedPixels = 0;
            if (!Available)
                return false;

            ThreadSafety.EnsureDrawThread();

            // FlushUploads may run on a decoder/loader thread. Keep the owner of the pixel memory
            // alive throughout each native call, including when an upload spans several frames.
            lock (uploadQueue)
            {
                // Binding the texture can flush a batch and re-enter the renderer. The upload
                // already owns its pending data; a nested call must not submit or dispose it twice.
                if (uploading)
                    return false;

                uploadedRegions.Clear();
                uploading = true;
                try
                {
                    return performUploads(maxPixels, budgeted, out uploadedPixels);
                }
                finally
                {
                    uploading = false;
                    if (discardUploads)
                    {
                        discardUploads = false;
                        FlushUploads();
                    }
                }
            }
        }

        private unsafe bool performUploads(int maxPixels, bool budgeted, out int uploadedPixels)
        {
            uploadedPixels = 0;
            if (pendingUploadFence != IntPtr.Zero)
            {
                // A failed poll cannot be bypassed by a later synchronous bind.
                if (!budgeted && pendingUploadFenceFailed)
                    throw new InvalidOperationException("OpenGL texture upload fence wait failed.");

                // A zero timeout only polls. Never stall Draw waiting for an earlier slice.
                if (budgeted)
                {
                    switch (GL.ClientWaitSync(pendingUploadFence, ClientWaitSyncFlags.None, 0L))
                    {
                        case WaitSyncStatus.TimeoutExpired:
                            return false;

                        case WaitSyncStatus.WaitFailed:
                            pendingUploadFenceFailed = true;
                            // Keep the fence and pending upload state for an explicit retry or disposal.
                            // In particular, the final slice must never become upload-ready on a failed poll.
                            throw new InvalidOperationException("OpenGL texture upload fence wait failed.");
                    }
                }

                clearUploadFence();
            }

            while (pendingUpload != null || uploadQueue.TryDequeue(out pendingUpload))
            {
                ITextureUpload upload = pendingUpload;
                bool complete = true;
                bool slicing = pendingUploadRows > 0 || (budgeted && canSplitInitialUpload(upload));
                int pixels;

                try
                {
                    fixed (Rgba32* ptr = upload.Data)
                    {
                        if (slicing)
                        {
                            // Allocate once, without copying the full image into driver staging memory.
                            if (pendingUploadRows == 0)
                                DoUpload(upload, IntPtr.Zero);

                            int rows = budgeted ? Math.Max(1, (maxPixels - uploadedPixels) / upload.Bounds.Width) : upload.Bounds.Height;
                            rows = Math.Min(rows, upload.Bounds.Height - pendingUploadRows);
                            Renderer.BindTexture(this);
                            GL.TexSubImage2D(TextureTarget2d.Texture2D, 0, 0, pendingUploadRows, upload.Bounds.Width, rows,
                                PixelFormat, PixelType, (IntPtr)(ptr + pendingUploadRows * upload.Bounds.Width));

                            uploadedRegions.Add(new RectangleI(0, pendingUploadRows, upload.Bounds.Width, rows));
                            pendingUploadRows += rows;
                            pixels = rows * upload.Bounds.Width;
                            complete = pendingUploadRows == upload.Bounds.Height;
                            if (budgeted)
                            {
                                pendingUploadFence = GL.FenceSync(SyncCondition.SyncGpuCommandsComplete, WaitSyncFlags.None);
                                GL.Flush();
                            }
                        }
                        else
                        {
                            DoUpload(upload, (IntPtr)ptr);
                            uploadedRegions.Add(upload.Bounds);
                            pixels = (int)Math.Min(int.MaxValue, Math.Max(1L, (long)Width * Height));
                        }
                    }
                }
                catch
                {
                    pendingUpload = null;
                    pendingUploadRows = 0;
                    upload.Dispose();
                    throw;
                }

                uploadedPixels = slicing ? (int)Math.Min(int.MaxValue, (long)uploadedPixels + pixels) : Math.Max(uploadedPixels, pixels);
                if (complete)
                {
                    pendingUpload = null;
                    pendingUploadRows = 0;
                    upload.Dispose();
                }

                // Atlas, video, mipmap and partial updates retain their original all-at-once path.
                if (slicing && budgeted && (uploadedPixels >= maxPixels || !complete || pendingUploadFence != IntPtr.Zero))
                    break;
            }

            generateMipmaps();
            // Publish completion only after all native writes (and automatic mip generation).
            uploadComplete = pendingUpload == null && uploadQueue.Count == 0 && pendingUploadFence == IntPtr.Zero;
            return uploadedRegions.Count != 0;
        }

        private void generateMipmaps()
        {
            #region Custom mipmap generation (disabled)

            // Generate mipmaps for just the updated regions of the texture.
            // This implementation is functionally equivalent to GL.GenerateMipmap(),
            // only that it is much more efficient if only small parts of the texture
            // have been updated.

            // The implementation has been tried in a release, but the user reception has been mixed
            // due to various issues on various platforms (most prominently android).
            // As it's difficult to ascertain a reasonable heuristic as to when it should be safe to use this implementation,
            // for now it is unconditionally disabled, and it can be revisited at a later date.

            // if (uploadedRegions.Count != 0 && !manualMipmaps)
            // {
            //     // Merge overlapping upload regions to prevent redundant mipmap generation.
            //     // i goes through the list left-to-right, j goes through it right-to-left
            //     // until both indices meet somewhere in the middle.
            //     // This algorithm needs multiple passes until no possible merges are found.
            //     bool mergeFound;

            //     do
            //     {
            //         mergeFound = false;

            //         for (int i = 0; i < uploadedRegions.Count; ++i)
            //         {
            //             RectangleI toMerge = uploadedRegions[i];

            //             for (int j = uploadedRegions.Count - 1; j > i; --j)
            //             {
            //                 RectangleI mergeCandidate = uploadedRegions[j];

            //                 if (!toMerge.Intersect(mergeCandidate).IsEmpty)
            //                 {
            //                     uploadedRegions[i] = toMerge = RectangleI.Union(toMerge, mergeCandidate);
            //                     uploadedRegions.RemoveAt(j);
            //                     mergeFound = true;
            //                 }
            //             }
            //         }
            //     } while (mergeFound);

            //     // Mipmap generation using the merged upload regions follows
            //     using var frameBuffer = new GLFrameBuffer(Renderer, this);

            //     BlendingParameters previousBlendingParameters = Renderer.CurrentBlendingParameters;

            //     // Use a simple render state (no blending, masking, scissoring, stenciling, etc.)
            //     Renderer.SetBlend(BlendingParameters.None);
            //     Renderer.PushDepthInfo(new DepthInfo(false, false));
            //     Renderer.PushStencilInfo(new StencilInfo(false));
            //     Renderer.PushScissorState(false);

            //     Renderer.BindFrameBuffer(frameBuffer);

            //     // Create render state for mipmap generation
            //     Renderer.BindTexture(this);
            //     Renderer.GetMipmapShader().Bind();

            //     while (uploadedRegions.Count > 0)
            //     {
            //         int width = internalWidth;
            //         int height = internalHeight;

            //         int count = Math.Min(uploadedRegions.Count, IRenderer.MAX_QUADS);

            //         // Generate quad buffer that will hold all the updated regions
            //         var quadBuffer = new GLQuadBuffer<UncolouredVertex2D>(Renderer, count, BufferUsageHint.StreamDraw);

            //         // Compute mipmap by iteratively blitting coarser and coarser versions of the updated regions
            //         for (int level = 1; level < IRenderer.MAX_MIPMAP_LEVELS + 1 && (width > 1 || height > 1); ++level)
            //         {
            //             width /= 2;
            //             height /= 2;

            //             // Fill quad buffer with downscaled (and conservatively rounded) draw rectangles
            //             for (int i = 0; i < count; ++i)
            //             {
            //                 // Conservatively round the draw rectangles. Rounding to integer coords is required
            //                 // in order to ensure all the texels affected by linear interpolation are touched.
            //                 // We could skip the rounding & use a single vertex buffer for all levels if we had
            //                 // conservative raster, but alas, that's only supported on NV and Intel.
            //                 Vector2I topLeft = uploadedRegions[i].TopLeft;
            //                 topLeft = new Vector2I(topLeft.X / 2, topLeft.Y / 2);
            //                 Vector2I bottomRight = uploadedRegions[i].BottomRight;
            //                 bottomRight = new Vector2I(MathUtils.DivideRoundUp(bottomRight.X, 2), MathUtils.DivideRoundUp(bottomRight.Y, 2));
            //                 uploadedRegions[i] = new RectangleI(topLeft.X, topLeft.Y, bottomRight.X - topLeft.X, bottomRight.Y - topLeft.Y);

            //                 // Normalize the draw rectangle into the unit square, which doubles as texture sampler coordinates.
            //                 RectangleF r = (RectangleF)uploadedRegions[i] / new Vector2(width, height);

            //                 quadBuffer.SetVertex(i * 4 + 0, new UncolouredVertex2D { Position = r.BottomLeft });
            //                 quadBuffer.SetVertex(i * 4 + 1, new UncolouredVertex2D { Position = r.BottomRight });
            //                 quadBuffer.SetVertex(i * 4 + 2, new UncolouredVertex2D { Position = r.TopRight });
            //                 quadBuffer.SetVertex(i * 4 + 3, new UncolouredVertex2D { Position = r.TopLeft });
            //             }

            //             // Read the texture from 1 mip level higher...
            //             GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinLod, level - 1);
            //             GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLod, level - 1);

            //             // ...than the one we're writing to via frame buffer.
            //             GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget2d.Texture2D, TextureId, level);

            //             // Perform the actual mip level draw
            //             Renderer.PushViewport(new RectangleI(0, 0, width, height));

            //             quadBuffer.Update();
            //             quadBuffer.Draw();

            //             Renderer.PopViewport();
            //         }

            //         uploadedRegions.RemoveRange(0, count);
            //     }

            //     // Restore previous render state
            //     Renderer.GetMipmapShader().Unbind();

            //     Renderer.PopScissorState();
            //     Renderer.PopStencilInfo();
            //     Renderer.PopDepthInfo();

            //     Renderer.SetBlend(previousBlendingParameters);

            //     Renderer.UnbindFrameBuffer(frameBuffer);

            //     GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinLod, 0);
            //     GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLod, IRenderer.MAX_MIPMAP_LEVELS);
            // }

            #endregion

            #region GL-provided mipmap generation

            if (uploadedRegions.Count != 0 && !manualMipmaps)
            {
                GL.Hint(HintTarget.GenerateMipmapHint, HintMode.Nicest);
                GL.GenerateMipmap(TextureTarget.Texture2D);
            }

            #endregion
        }

        private void clearUploadFence()
        {
            if (pendingUploadFence == IntPtr.Zero)
                return;

            GL.DeleteSync(pendingUploadFence);
            pendingUploadFence = IntPtr.Zero;
            pendingUploadFenceFailed = false;
        }

        private bool canSplitInitialUpload(ITextureUpload upload) => Renderer.SupportsBudgetedTextureUploads && GetType() == typeof(GLTexture)
            && textureId == 0 && manualMipmaps && initialisationColour == null && InternalFormat == TextureComponentCount.Rgba8
            && upload.Level == 0 && upload.Bounds == new RectangleI(0, 0, Width, Height)
            && Width > 0 && Height > 0 && (long)Width * Height > Math.Clamp(Renderer.MaxPixelsUploadedPerFrame, 1, maxPixelsPerUploadSlice)
            && upload.Data.Length >= (long)Width * Height;

        public bool UploadComplete => uploadComplete;

        /// <summary>
        /// Whether the texture is currently queued for upload.
        /// </summary>
        public bool IsQueuedForUpload { get; set; }

        protected virtual void DoUpload(ITextureUpload upload, IntPtr dataPointer)
        {
            // Do we need to generate a new texture?
            if (textureId <= 0 || internalWidth != Width || internalHeight != Height)
            {
                internalWidth = Width;
                internalHeight = Height;

                // We only need to generate a new texture if we don't have one already. Otherwise just re-use the current one.
                if (textureId <= 0)
                {
                    int[] textures = new int[1];
                    GL.GenTextures(1, textures);

                    textureId = textures[0];

                    Renderer.BindTexture(this);

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureBaseLevel, 0);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, IRenderer.MAX_MIPMAP_LEVELS);

                    // These shouldn't be required, but on some older Intel drivers the MAX_LOD chosen by the shader isn't clamped to the MAX_LEVEL from above, resulting in disappearing textures.
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinLod, 0);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLod, IRenderer.MAX_MIPMAP_LEVELS);

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter,
                        (int)(manualMipmaps ? filteringMode : filteringMode == All.Linear ? All.LinearMipmapLinear : All.Nearest));
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)filteringMode);

                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
                    GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
                }
                else
                    Renderer.BindTexture(this);

                if ((Width == upload.Bounds.Width && Height == upload.Bounds.Height) || dataPointer == IntPtr.Zero)
                {
                    updateMemoryUsage(upload.Level, (long)Width * Height * 4);
                    bool inspect = StutterTraceEventSource.FramebufferProbe && dataPointer == IntPtr.Zero && StutterTraceEventSource.Log.IsEnabled();
                    long started = inspect ? Stopwatch.GetTimestamp() : 0;
                    long cpu = inspect ? StutterTraceEventSource.ThreadCpuNanoseconds() : 0;
                    GL.TexImage2D(TextureTarget2d.Texture2D, upload.Level, InternalFormat, Width, Height, 0, PixelFormat, PixelType, dataPointer);
                    if (inspect && (GetType().Name == "FrameBufferTexture" || Stopwatch.GetElapsedTime(started).TotalMilliseconds >= 1))
                    {
                        long endCpu = StutterTraceEventSource.ThreadCpuNanoseconds();
                        var elapsed = Stopwatch.GetElapsedTime(started);
                        StutterTraceEventSource.Log.Marker("GL.EmptyTexImage2D",
                            FormattableString.Invariant($"type={GetType().Name};texture={textureId};size={Width}x{Height};format={InternalFormat};wall_ms={elapsed.TotalMilliseconds:F3};cpu_ns={(cpu < 0 || endCpu < cpu ? -1 : endCpu - cpu)}"), started);
                    }
                }
                else
                {
                    initializeLevel(upload.Level, Width, Height);

                    GL.TexSubImage2D(TextureTarget2d.Texture2D, upload.Level, upload.Bounds.X, upload.Bounds.Y, upload.Bounds.Width, upload.Bounds.Height, PixelFormat,
                        PixelType, dataPointer);
                }
            }
            // Just update content of the current texture
            else if (dataPointer != IntPtr.Zero)
            {
                Renderer.BindTexture(this);

                if (!manualMipmaps && upload.Level > 0)
                {
                    //allocate mipmap levels
                    int level = 1;
                    int d = 2;

                    while (Width / d > 0)
                    {
                        initializeLevel(level, Width / d, Height / d);
                        level++;
                        d *= 2;
                    }

                    manualMipmaps = true;
                }

                int div = (int)Math.Pow(2, upload.Level);

                GL.TexSubImage2D(TextureTarget2d.Texture2D, upload.Level, upload.Bounds.X / div, upload.Bounds.Y / div, upload.Bounds.Width / div, upload.Bounds.Height / div,
                    PixelFormat, PixelType, dataPointer);
            }
        }

        private void initializeLevel(int level, int width, int height)
        {
            if (initialisationColour == null)
                return;

            var rgbaColour = new Rgba32(new Vector4(initialisationColour.Value.R, initialisationColour.Value.G, initialisationColour.Value.B, initialisationColour.Value.A));

            // it is faster to initialise without a background specification if transparent black is all that's required.
            using var image = initialisationColour == null
                ? new Image<Rgba32>(width, height)
                : new Image<Rgba32>(width, height, rgbaColour);

            using (var pixels = image.CreateReadOnlyPixelSpan())
            {
                updateMemoryUsage(level, (long)width * height * 4);
                GL.TexImage2D(TextureTarget2d.Texture2D, level, InternalFormat, width, height, 0, PixelFormat, PixelType,
                    ref MemoryMarshal.GetReference(pixels.Span));
            }
        }
    }
}
