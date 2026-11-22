// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using osu.Framework.Graphics.OpenGL.Textures;
using osu.Framework.Graphics.Primitives;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osuTK;
using osuTK.Graphics.ES30;

namespace osu.Framework.Graphics.OpenGL.Buffers
{
    internal class GLFrameBuffer : IFrameBuffer
    {
        private readonly Texture backingTexture;
        private Texture? retainedTextureView;

        public Texture Texture => retainedTextureView ?? backingTexture;

        private readonly List<GLRenderBuffer> attachedRenderBuffers = new List<GLRenderBuffer>();
        private readonly GLRenderer renderer;
        private readonly GLTexture glTexture;

        public readonly int FrameBuffer;

        private readonly bool externalTexture;

        public GLFrameBuffer(GLRenderer renderer, TextureComponentCount textureFormat, RenderbufferInternalFormat[]? renderBufferFormats = null, All filteringMode = All.Linear)
        {
            this.renderer = renderer;
            FrameBuffer = GL.GenFramebuffer();

            backingTexture = renderer.CreateTexture(glTexture = new FrameBufferTexture(renderer, textureFormat, filteringMode));

            renderer.BindFrameBuffer(this);

            Debug.Assert(glTexture.TextureId != 0);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget2d.Texture2D, glTexture.TextureId, 0);
            renderer.UnbindTexture();

            if (renderBufferFormats != null)
            {
                foreach (var format in renderBufferFormats)
                    attachedRenderBuffers.Add(new GLRenderBuffer(renderer, format));
            }

            renderer.UnbindFrameBuffer(this);
        }

        public GLFrameBuffer(GLRenderer renderer, GLTexture glTexture, int level = 0)
        {
            this.renderer = renderer;
            this.glTexture = glTexture;

            FrameBuffer = GL.GenFramebuffer();
            backingTexture = renderer.CreateTexture(glTexture);

            externalTexture = true;

            renderer.BindFrameBuffer(this);

            GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget2d.Texture2D, glTexture.TextureId, level);

            renderer.UnbindFrameBuffer(this);
        }

        private Vector2 size = Vector2.One;

        // Opt-in for animated buffered content with constant width. Desktop GL can render to
        // a shorter viewport while retaining storage; the logical texture view crops sampling.
        internal bool HasRetainedTextureArea => retainedTextureView != null && Texture.Height != glTexture.Height;

        internal Vector2 StorageSize => backingTexture.Size;

        internal void EnableRetainedTextureHeight()
        {
            if (!renderer.IsEmbedded && !externalTexture && attachedRenderBuffers.Count == 0)
                retainedTextureView ??= new FrameBufferTextureView(this, backingTexture);
        }

        private const int max_retained_height = 2048;
        private const int height_bucket = 128;

        /// <summary>
        /// Sets the size of the texture of this frame buffer.
        /// </summary>
        public Vector2 Size
        {
            get => size;
            set
            {
                if (value == size)
                    return;

                size = value;

                int width = (int)Math.Ceiling(size.X);
                int height = (int)Math.Ceiling(size.Y);
                if (retainedTextureView != null)
                {
                    width = Math.Clamp(width, 1, renderer.MaxTextureSize);
                    height = Math.Clamp(height, 1, renderer.MaxTextureSize);
                    if (height <= max_retained_height)
                    {
                        // Release a previous oversize exact allocation on return to the bounded range.
                        if (width == glTexture.Width && glTexture.Height >= height && glTexture.Height <= max_retained_height)
                            return;
                        height = Math.Min(renderer.MaxTextureSize, (height + height_bucket - 1) / height_bucket * height_bucket);
                    }

                    if (width == glTexture.Width && height == glTexture.Height)
                        return;
                }

                glTexture.Width = width;
                glTexture.Height = height;
                glTexture.SetData(new TextureUpload());
                glTexture.Upload();
            }
        }

        /// <summary>
        /// Binds the framebuffer.
        /// <para>Does not clear the buffer or reset the viewport/ortho.</para>
        /// </summary>
        public void Bind()
        {
            renderer.BindFrameBuffer(this);

            foreach (var buffer in attachedRenderBuffers)
                buffer.Bind(Size);
        }

        /// <summary>
        /// Unbinds the framebuffer.
        /// </summary>
        public void Unbind()
        {
            // See: https://community.arm.com/developer/tools-software/graphics/b/blog/posts/mali-performance-2-how-to-correctly-handle-framebuffers
            // Unbinding renderbuffers causes an invalidation of the relevant attachment of this framebuffer on embedded devices, causing the renderbuffers to remain transient.
            // This must be done _before_ the framebuffer is flushed via the framebuffer unbind process, otherwise the renderbuffer may be copied to system memory.
            foreach (var buffer in attachedRenderBuffers)
                buffer.Unbind();

            renderer.UnbindFrameBuffer(this);
        }

        #region Disposal

        ~GLFrameBuffer()
        {
            renderer.ScheduleDisposal(b => b.Dispose(false), this);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool isDisposed;

        protected virtual void Dispose(bool disposing)
        {
            if (isDisposed)
                return;

            if (!externalTexture)
                glTexture.Dispose();

            renderer.DeleteFrameBuffer(this);

            foreach (var buffer in attachedRenderBuffers)
                buffer.Dispose();

            isDisposed = true;
        }

        #endregion

        private sealed class FrameBufferTextureView : Texture
        {
            private readonly GLFrameBuffer buffer;
            private readonly Texture backing;

            public FrameBufferTextureView(GLFrameBuffer buffer, Texture backing)
                : base(backing)
            {
                this.buffer = buffer;
                this.backing = backing;
            }

            public override int Width
            {
                get => Math.Clamp((int)Math.Ceiling(buffer.Size.X), 1, backing.Width);
                set => backing.Width = value;
            }

            public override int Height
            {
                get => Math.Clamp((int)Math.Ceiling(buffer.Size.Y), 1, backing.Height);
                set => backing.Height = value;
            }

            public override RectangleF GetTextureRect(RectangleF? area = null)
            {
                RectangleF rect = (area ?? new RectangleF(0, 0, DisplayWidth, DisplayHeight)) * ScaleAdjust;
                return backing.GetTextureRect(rect / backing.ScaleAdjust);
            }
        }

        private class FrameBufferTexture : GLTexture
        {
            public FrameBufferTexture(GLRenderer renderer, TextureComponentCount textureFormat, All filteringMode = All.Linear)
                : base(renderer, 1, 1, textureFormat, true, filteringMode)
            {
                BypassTextureUploadQueueing = true;

                SetData(new TextureUpload());
                Upload();
            }

            public override int Width
            {
                get => base.Width;
                set => base.Width = Math.Clamp(value, 1, Renderer.MaxTextureSize);
            }

            public override int Height
            {
                get => base.Height;
                set => base.Height = Math.Clamp(value, 1, Renderer.MaxTextureSize);
            }
        }
    }
}
