using System;
using Veldrid;

namespace osu.Framework.Graphics.Veldrid
{
    /// <summary>A fixed-size render target that is never presented to the desktop compositor.</summary>
    internal sealed class VeldridVideoCaptureTarget : IDisposable
    {
        public readonly Texture Colour;
        private readonly Texture depth;
        public readonly Framebuffer Framebuffer;

        public VeldridVideoCaptureTarget(GraphicsDevice device, uint width, uint height)
        {
            var factory = device.ResourceFactory;
            // Match the normal non-sRGB swapchain: gameplay shaders already output encoded RGB.
            Colour = factory.CreateTexture(TextureDescription.Texture2D(width, height, 1, 1, PixelFormat.R8G8B8A8UNorm,
                TextureUsage.RenderTarget | TextureUsage.Sampled));
            try
            {
                depth = factory.CreateTexture(TextureDescription.Texture2D(width, height, 1, 1, PixelFormat.R16UNorm, TextureUsage.DepthStencil));
                try
                {
                    Framebuffer = factory.CreateFramebuffer(new FramebufferDescription(depth, Colour));
                }
                catch
                {
                    depth.Dispose();
                    throw;
                }
            }
            catch
            {
                Colour.Dispose();
                throw;
            }
        }

        public void Dispose()
        {
            Framebuffer.Dispose();
            depth.Dispose();
            Colour.Dispose();
        }
    }
}
