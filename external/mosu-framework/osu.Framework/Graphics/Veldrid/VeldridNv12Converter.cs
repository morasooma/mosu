using System;
using System.Diagnostics;
using System.Text;
using Veldrid;
using Veldrid.SPIRV;

namespace osu.Framework.Graphics.Veldrid
{
    /// <summary>
    /// Converts encoded RGB values to limited-range BT.709 NV12 on the GPU. Output is a
    /// tightly packed byte stream in a uint buffer, avoiding texture row-pitch padding.
    /// </summary>
    internal sealed class VeldridNv12Converter : IDisposable
    {
        private readonly GraphicsDevice device;
        private readonly Shader shader;
        private readonly ResourceLayout layout;
        private readonly Pipeline pipeline;

        internal const string SHADER_SOURCE = """
            #version 450
            layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;
            layout(set = 0, binding = 0) uniform texture2D Source;
            layout(set = 0, binding = 1) uniform sampler SourceSampler;
            layout(std430, set = 0, binding = 2) buffer OutputBytes { uint Words[]; };

            vec3 rgbAt(uint pixel, uint width)
            {
                return texelFetch(sampler2D(Source, SourceSampler), ivec2(pixel % width, pixel / width), 0).rgb;
            }

            uint quantize(float value)
            {
                return uint(clamp(floor(value + 0.5), 0.0, 255.0));
            }

            uint luma(uint pixel, uint width)
            {
                return quantize(16.0 + 219.0 * dot(rgbAt(pixel, width), vec3(0.2126, 0.7152, 0.0722)));
            }

            uint chromaPair(uint block, uint width)
            {
                uint pixel = (block / (width / 2)) * width * 2 + (block % (width / 2)) * 2;
                vec3 rgb = (rgbAt(pixel, width) + rgbAt(pixel + 1, width)
                          + rgbAt(pixel + width, width) + rgbAt(pixel + width + 1, width)) * 0.25;
                uint u = quantize(128.0 + 224.0 * dot(rgb, vec3(-0.114572106, -0.385427894, 0.5)));
                uint v = quantize(128.0 + 224.0 * dot(rgb, vec3(0.5, -0.454152908, -0.045847092)));
                return u | (v << 8);
            }

            void main()
            {
                uvec2 size = uvec2(textureSize(sampler2D(Source, SourceSampler), 0));
                uint yWords = size.x * size.y / 4;
                uint byteCount = size.x * size.y * 3 / 2;
                uint word = gl_GlobalInvocationID.x + gl_GlobalInvocationID.y * 65535u * 64u;
                if (word >= (byteCount + 3) / 4) return;
                if (word < yWords)
                {
                    uint p = word * 4;
                    Words[word] = luma(p, size.x) | (luma(p + 1, size.x) << 8)
                                | (luma(p + 2, size.x) << 16) | (luma(p + 3, size.x) << 24);
                }
                else
                {
                    uint block = (word - yWords) * 2;
                    uint pairs = size.x * size.y / 4;
                    uint packed = chromaPair(block, size.x);
                    if (block + 1 < pairs) packed |= chromaPair(block + 1, size.x) << 16;
                    Words[word] = packed;
                }
            }
            """;

        public VeldridNv12Converter(GraphicsDevice device)
        {
            this.device = device;
            shader = device.ResourceFactory.CreateFromSpirv(new ShaderDescription(ShaderStages.Compute,
                Encoding.UTF8.GetBytes(SHADER_SOURCE), "main"));
            try
            {
                layout = device.ResourceFactory.CreateResourceLayout(new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("Source", ResourceKind.TextureReadOnly, ShaderStages.Compute),
                    new ResourceLayoutElementDescription("SourceSampler", ResourceKind.Sampler, ShaderStages.Compute),
                    new ResourceLayoutElementDescription("OutputBytes", ResourceKind.StructuredBufferReadWrite, ShaderStages.Compute)));
                try
                {
                    pipeline = device.ResourceFactory.CreateComputePipeline(new ComputePipelineDescription(shader, new[] { layout }, 64, 1, 1));
                }
                catch
                {
                    layout.Dispose();
                    throw;
                }
            }
            catch
            {
                shader.Dispose();
                throw;
            }
        }

        internal static int GetByteCount(int width, int height)
        {
            if (width <= 0 || height <= 0 || (width & 1) != 0 || (height & 1) != 0)
                throw new ArgumentOutOfRangeException(nameof(width), "NV12 requires positive even dimensions.");
            return checked(width * height * 3 / 2);
        }

        internal sealed class Readback : IDisposable
        {
            public readonly int ByteCount;
            public double GpuWaitMilliseconds { get; private set; }
            public double MapMilliseconds { get; private set; }
            public double CopyMilliseconds { get; private set; }
            private readonly uint bufferSize;
            internal readonly DeviceBuffer Output;
            internal readonly DeviceBuffer Staging;
            internal readonly CommandList Commands;
            internal readonly Fence Fence;
            private readonly TextureView sourceView;
            private readonly ResourceSet resources;
            private bool submitted;

            public Readback(GraphicsDevice device, Texture source, ResourceLayout layout)
            {
                ByteCount = GetByteCount((int)source.Width, (int)source.Height);
                bufferSize = (uint)((ByteCount + 3) & ~3);
                var factory = device.ResourceFactory;
                try
                {
                    Output = factory.CreateBuffer(new BufferDescription(bufferSize, BufferUsage.StructuredBufferReadWrite, 4));
                    Staging = factory.CreateBuffer(new BufferDescription(bufferSize, BufferUsage.Staging));
                    Commands = factory.CreateCommandList();
                    Fence = factory.CreateFence(false);
                    sourceView = factory.CreateTextureView(source);
                    resources = factory.CreateResourceSet(new ResourceSetDescription(layout, sourceView, device.PointSampler, Output));
                }
                catch
                {
                    Dispose();
                    throw;
                }
            }

            internal void Submit(GraphicsDevice device, Pipeline pipeline)
            {
                if (submitted)
                    throw new InvalidOperationException("The previous NV12 readback has not been completed.");
                device.ResetFence(Fence);
                Commands.Begin();
                Commands.SetPipeline(pipeline);
                Commands.SetComputeResourceSet(0, resources);
                uint groups = (bufferSize / 4 + 63) / 64;
                Commands.Dispatch(Math.Min(groups, 65535), (groups + 65534) / 65535, 1);
                Commands.CopyBuffer(Output, 0, Staging, 0, bufferSize);
                Commands.End();
                device.SubmitCommands(Commands, Fence);
                submitted = true;
            }

            public unsafe bool Complete(GraphicsDevice device, Span<byte> destination)
            {
                if (!submitted)
                    throw new InvalidOperationException("No NV12 readback is pending.");
                if (destination.Length < ByteCount)
                    throw new ArgumentException("NV12 destination buffer is too small.", nameof(destination));
                long startedAt = Stopwatch.GetTimestamp();
                if (!device.WaitForFence(Fence, 5_000_000_000))
                    throw new TimeoutException("NV12 frame readback timed out.");
                long fenceCompletedAt = Stopwatch.GetTimestamp();
                var mapped = device.Map(Staging, MapMode.Read);
                long mappedAt = Stopwatch.GetTimestamp();
                try
                {
                    new ReadOnlySpan<byte>(mapped.Data.ToPointer(), ByteCount).CopyTo(destination);
                }
                finally
                {
                    device.Unmap(Staging);
                }
                GpuWaitMilliseconds = Stopwatch.GetElapsedTime(startedAt, fenceCompletedAt).TotalMilliseconds;
                MapMilliseconds = Stopwatch.GetElapsedTime(fenceCompletedAt, mappedAt).TotalMilliseconds;
                CopyMilliseconds = Stopwatch.GetElapsedTime(mappedAt).TotalMilliseconds;
                submitted = false;
                return true;
            }

            public void Dispose()
            {
                resources?.Dispose();
                sourceView?.Dispose();
                Commands?.Dispose();
                Fence?.Dispose();
                Staging?.Dispose();
                Output?.Dispose();
            }
        }

        public Readback CreateReadback(Texture source) => new Readback(device, source, layout);

        public void Submit(Readback request) => request.Submit(device, pipeline);

        public void Dispose()
        {
            pipeline.Dispose();
            layout.Dispose();
            shader.Dispose();
        }
    }
}
