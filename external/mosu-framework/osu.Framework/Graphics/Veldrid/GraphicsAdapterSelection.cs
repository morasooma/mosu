using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Veldrid;
using Vortice.DXGI;

namespace osu.Framework.Graphics.Veldrid
{
    internal static class GraphicsAdapterSelection
    {
        private static string getBaseId(IDXGIAdapter adapter)
        {
            var description = adapter.Description;
            // LUID changes across Windows reboots. Keep persisted choices tied to hardware instead.
            return $"dxgi:{description.VendorId:x8}:{description.DeviceId:x8}:{description.SubsystemId:x8}:{description.Revision:x8}:{description.Description.Trim()}";
        }

        private static string getId(IDXGIAdapter adapter, Dictionary<string, int> occurrences)
        {
            string baseId = getBaseId(adapter);
            occurrences.TryGetValue(baseId, out int occurrence);
            occurrences[baseId] = occurrence + 1;
            return $"{baseId}:{occurrence}";
        }

        internal static IReadOnlyList<GraphicsAdapterInfo> EnumerateDirect3D()
        {
            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            var result = new List<GraphicsAdapterInfo>();
            var occurrences = new Dictionary<string, int>();
            for (int i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
            {
                using (adapter)
                {
                    string id = getId(adapter, occurrences);
                    if ((adapter.Description1.Flags & AdapterFlags.Software) == 0)
                        result.Add(new GraphicsAdapterInfo(id, adapter.Description.Description.Trim()));
                }
            }
            return result;
        }

        internal static GraphicsDevice CreateDirect3D(GraphicsDeviceOptions options, SwapchainDescription swapchain, string preferred)
        {
            if (string.IsNullOrEmpty(preferred))
                return GraphicsDevice.CreateD3D11(options, swapchain);

            using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
            var occurrences = new Dictionary<string, int>();
            for (int i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
            {
                using (adapter)
                {
                    if (getId(adapter, occurrences) != preferred)
                        continue;

                    try
                    {
                        return GraphicsDevice.CreateD3D11(options, new D3D11DeviceOptions { AdapterPtr = adapter.NativePointer }, swapchain);
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Selected GPU could not initialise; retrying with automatic GPU selection.");
                        break;
                    }
                }
            }

            Logger.Log($"Requested GPU '{preferred}' is unavailable; using automatic GPU selection.");
            return GraphicsDevice.CreateD3D11(options, swapchain);
        }

        internal static IReadOnlyList<GraphicsAdapterInfo> GetAvailable(GraphicsDevice device)
        {
            if (device.BackendType == GraphicsBackend.Direct3D11)
                return EnumerateDirect3D();
            if (device.GetVulkanInfo(out var vulkan))
                return vulkan.AvailablePhysicalDevices.Select(d => new GraphicsAdapterInfo(d.Id, d.Name)).ToArray();
            return Array.Empty<GraphicsAdapterInfo>();
        }

        internal static string GetSelectedId(GraphicsDevice device)
        {
            if (device.GetD3D11Info(out var direct3D))
            {
                using var active = new IDXGIAdapter(direct3D.Adapter);
                // This wrapper does not own Veldrid's reference.
                active.AddRef();
                using var factory = DXGI.CreateDXGIFactory1<IDXGIFactory1>();
                var occurrences = new Dictionary<string, int>();
                for (int i = 0; factory.EnumAdapters1(i, out var adapter).Success; i++)
                {
                    using (adapter)
                    {
                        string id = getId(adapter, occurrences);
                        if (adapter.Description.Luid.Equals(active.Description.Luid))
                            return id;
                    }
                }
                return string.Empty;
            }
            return device.GetVulkanInfo(out var vulkan) ? vulkan.SelectedPhysicalDeviceId : string.Empty;
        }
    }
}
