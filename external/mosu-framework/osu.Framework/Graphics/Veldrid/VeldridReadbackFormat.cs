using System;
using Veldrid;

namespace osu.Framework.Graphics.Veldrid
{
    internal static class VeldridReadbackFormat
    {
        internal static void CopyPixels(ReadOnlySpan<byte> source, int rowPitch, Span<byte> destination, int width, int height, bool swizzle)
        {
            int stride = checked(width * 4);
            int totalBytes = checked(stride * height);
            if (rowPitch == stride)
                source[..totalBytes].CopyTo(destination);
            else
            {
                for (int y = 0; y < height; y++)
                    source.Slice(y * rowPitch, stride).CopyTo(destination.Slice(y * stride, stride));
            }

            if (swizzle)
            {
                for (int i = 0; i < totalBytes; i += 4)
                    (destination[i], destination[i + 2]) = (destination[i + 2], destination[i]);
            }
        }

        public static bool RequiresBgraToRgbaSwizzle(PixelFormat format)
            => format switch
            {
                PixelFormat.B8G8R8A8UNorm or PixelFormat.B8G8R8A8UNormSRgb => true,
                PixelFormat.R8G8B8A8UNorm or PixelFormat.R8G8B8A8UNormSRgb => false,
                _ => throw new NotSupportedException($"Unsupported backbuffer readback format: {format}."),
            };
    }
}
