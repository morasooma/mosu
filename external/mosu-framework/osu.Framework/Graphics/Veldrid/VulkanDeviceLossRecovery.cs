using System;
using osu.Framework.Configuration;
using Veldrid;

namespace osu.Framework.Graphics.Veldrid
{
    internal static class VulkanDeviceLossRecovery
    {
        public static bool ShouldFallback(RendererType renderer, Exception exception)
        {
            if (renderer != RendererType.Mora_Vulkan && renderer != RendererType.Deferred_Vulkan && renderer != RendererType.Vulkan)
                return false;

            if (exception is GraphicsDeviceLostException)
                return true;

            if (exception is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    if (ShouldFallback(renderer, inner))
                        return true;
                }
            }

            return exception.InnerException != null && ShouldFallback(renderer, exception.InnerException);
        }
    }
}
