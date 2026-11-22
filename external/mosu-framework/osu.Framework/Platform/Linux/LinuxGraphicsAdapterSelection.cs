using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace osu.Framework.Platform.Linux
{
    /// <summary>
    /// Mesa PRIME and NVIDIA render offload must be configured before SDL loads EGL/GLX.
    /// No global driver configuration or display-server settings are changed.
    /// </summary>
    internal static class LinuxGraphicsAdapterSelection
    {
        internal static IReadOnlyList<GraphicsAdapterInfo> Enumerate()
        {
            var result = new List<GraphicsAdapterInfo>();
            if (!Directory.Exists("/sys/class/drm"))
                return result;

            var devices = Directory.EnumerateDirectories("/sys/class/drm", "card*")
                                   .Where(p => Regex.IsMatch(Path.GetFileName(p), @"^card\d+$"))
                                   .Select(p => Path.Combine(p, "device"))
                                   .Where(Directory.Exists).ToArray();
            int nvidiaCount = devices.Count(p => File.ReadAllText(Path.Combine(p, "vendor")).Trim() == "0x10de");

            foreach (string device in devices)
            {
                string uevent = File.ReadAllText(Path.Combine(device, "uevent"));
                string? pci = uevent.Split('\n').FirstOrDefault(l => l.StartsWith("PCI_SLOT_NAME=", StringComparison.Ordinal))?.Split('=')[1];
                if (pci == null)
                    continue;

                string vendor = File.ReadAllText(Path.Combine(device, "vendor")).Trim()[2..];
                string product = File.ReadAllText(Path.Combine(device, "device")).Trim()[2..];
                string name = getDeviceName(vendor, product);
                bool proprietaryNvidia = uevent.Split('\n').Contains("DRIVER=nvidia");

                // A single NVIDIA offload device is unambiguous on both EGL/Wayland and GLX/X11.
                // Multiple proprietary NVIDIA cards need an explicit EGL device or RandR provider,
                // neither of which SDL's standard context creation exposes across both window systems.
                if (proprietaryNvidia)
                {
                    if (nvidiaCount == 1)
                        result.Add(new GraphicsAdapterInfo("opengl:nvidia:" + pci, name));
                }
                else
                    result.Add(new GraphicsAdapterInfo("opengl:mesa:" + pci, name));
            }
            return result;
        }

        internal static IReadOnlyDictionary<string, string> GetEnvironment(string id)
        {
            var parts = id.Split(':', 3);
            if (parts.Length != 3 || parts[0] != "opengl" || !Regex.IsMatch(parts[2], @"^[0-9a-fA-F]{4}:[0-9a-fA-F]{2}:[0-9a-fA-F]{2}\.[0-7]$"))
                throw new ArgumentException("Not a Linux OpenGL PCI adapter ID.", nameof(id));

            return parts[1] switch
            {
                "mesa" => new Dictionary<string, string> { ["DRI_PRIME"] = "pci-" + parts[2].Replace(':', '_').Replace('.', '_') },
                "nvidia" => new Dictionary<string, string> { ["__NV_PRIME_RENDER_OFFLOAD"] = "1", ["__GLX_VENDOR_LIBRARY_NAME"] = "nvidia" },
                _ => throw new ArgumentException("Unknown OpenGL driver.", nameof(id)),
            };
        }

        internal static IDisposable Apply(string id) => new EnvironmentScope(GetEnvironment(id));

        private static string getDeviceName(string vendor, string product)
        {
            // Use the distro's hardware database, not a new package or a subprocess per GPU.
            const string database = "/usr/share/hwdata/pci.ids";
            if (File.Exists(database))
            {
                bool foundVendor = false;
                foreach (string line in File.ReadLines(database))
                {
                    if (!foundVendor)
                        foundVendor = line.StartsWith(vendor + "  ", StringComparison.OrdinalIgnoreCase);
                    else if (line.StartsWith('\t'))
                    {
                        if (line.StartsWith("\t" + product + "  ", StringComparison.OrdinalIgnoreCase))
                            return line[7..].Trim();
                    }
                    else if (line.Length > 0 && line[0] != '#')
                        break;
                }
            }
            return $"GPU {vendor}:{product}";
        }

        private sealed class EnvironmentScope : IDisposable
        {
            private static readonly string[] variables = { "DRI_PRIME", "__NV_PRIME_RENDER_OFFLOAD", "__NV_PRIME_RENDER_OFFLOAD_PROVIDER", "__GLX_VENDOR_LIBRARY_NAME" };
            private readonly Dictionary<string, string?> previous = new Dictionary<string, string?>();

            public EnvironmentScope(IReadOnlyDictionary<string, string> values)
            {
                foreach (string key in variables)
                {
                    IntPtr native = getenv(key);
                    previous[key] = native == IntPtr.Zero ? null : Marshal.PtrToStringUTF8(native);
                    set(key, values.GetValueOrDefault(key));
                }
            }

            public void Dispose()
            {
                foreach (var pair in previous)
                    set(pair.Key, pair.Value);
                previous.Clear();
            }

            private static void set(string key, string? value)
            {
                // Environment.SetEnvironmentVariable alone doesn't reliably update libc's environment
                // on modern .NET. GL drivers read getenv(), not .NET's managed environment snapshot.
                if (value == null)
                    unsetenv(key);
                else
                    setenv(key, value, 1);
            }

            [DllImport("libc", CallingConvention = CallingConvention.Cdecl)]
            private static extern IntPtr getenv(string name);
            [DllImport("libc", CallingConvention = CallingConvention.Cdecl)]
            private static extern int setenv(string name, string value, int overwrite);
            [DllImport("libc", CallingConvention = CallingConvention.Cdecl)]
            private static extern int unsetenv(string name);
        }
    }
}
