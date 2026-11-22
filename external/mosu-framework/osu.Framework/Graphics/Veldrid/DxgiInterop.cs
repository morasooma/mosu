// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Runtime.InteropServices;
using Veldrid;

namespace osu.Framework.Graphics.Veldrid
{
    /// <summary>
    /// Mosu (MOSU_DXGI_MAX_FRAME_LATENCY): device-level DXGI interop not exposed by Veldrid.
    /// </summary>
    /// <remarks>
    /// The tearing swapchain path (ALLOW_TEARING) is created without FRAME_LATENCY_WAITABLE_OBJECT,
    /// so it runs at the DXGI device default of 3 queued frames — when GPU-bound the CPU records up to
    /// 3 frames ahead, adding up to ~3 GPU-frame-times of input latency. IDXGIDevice1::SetMaximumFrameLatency
    /// applies exactly to such swapchains (and is ignored where a per-swapchain latency is already set),
    /// and being device state it survives swapchain recreation.
    /// </remarks>
    internal static unsafe class DxgiInterop
    {
        private static readonly Guid iid_idxgidevice1 = new Guid("77db970f-6276-48ba-ba28-070143b4392c");

        /// <summary>
        /// Sets IDXGIDevice1::SetMaximumFrameLatency on the device backing a Veldrid D3D11 <see cref="GraphicsDevice"/>.
        /// </summary>
        public static bool TrySetMaximumFrameLatency(GraphicsDevice device, uint frames)
        {
            if (!device.GetD3D11Info(out BackendInfoD3D11 info))
                return false;

            Guid iid = iid_idxgidevice1;

            if (Marshal.QueryInterface(info.Device, ref iid, out IntPtr dxgiDevice1) != 0)
                return false;

            try
            {
                // IUnknown(0-2) + IDXGIObject(3-6) + IDXGIDevice(7-11) + IDXGIDevice1::SetMaximumFrameLatency = vtable slot 12.
                void** vtbl = *(void***)dxgiDevice1;
                int hr = ((delegate* unmanaged[Stdcall]<IntPtr, uint, int>)vtbl[12])(dxgiDevice1, frames);
                return hr >= 0;
            }
            finally
            {
                Marshal.Release(dxgiDevice1);
            }
        }
    }
}
