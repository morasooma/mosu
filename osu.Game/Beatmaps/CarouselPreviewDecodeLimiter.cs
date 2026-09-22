using System;
using System.Threading;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Bounds simultaneous carousel preview decodes to avoid multiplying full-resolution image buffers
    /// while many pooled panels become visible in the same frame.
    /// </summary>
    internal static class CarouselPreviewDecodeLimiter
    {
        public static readonly SemaphoreSlim Semaphore = new SemaphoreSlim(Math.Clamp(Environment.ProcessorCount / 2, 1, 2));
    }
}
