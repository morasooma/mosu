// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using osu.Framework.Platform;

namespace osu.Game.Performance.Diagnostics
{
    public static class MosuDiagnosticsMachineProfileCollector
    {
        private const string machine_id_relative_path = "performance/diagnostics/machine-id.txt";

        public static MosuDiagnosticsMachineProfile Collect(Storage storage, string? gpuDescription = null)
        {
            using var process = Process.GetCurrentProcess();

            return new MosuDiagnosticsMachineProfile
            {
                MachineId = getOrCreateMachineId(storage),
                OsDescription = RuntimeInformation.OSDescription,
                CpuDescription = getCpuDescription(),
                ProcessorCount = Environment.ProcessorCount,
                WorkingSetMb = process.WorkingSet64 / 1024 / 1024,
                GpuDescription = gpuDescription,
            };
        }

        private static string getOrCreateMachineId(Storage storage)
        {
            string path = storage.GetFullPath(machine_id_relative_path);

            if (File.Exists(path))
            {
                string existing = File.ReadAllText(path).Trim();

                if (!string.IsNullOrWhiteSpace(existing))
                    return existing;
            }

            string? directory = Path.GetDirectoryName(path);

            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string machineId = computeMachineId();
            File.WriteAllText(path, machineId);
            return machineId;
        }

        private static string computeMachineId()
        {
            string seed = $"{Environment.MachineName}|{Environment.UserName}|{Environment.ProcessorCount}|{RuntimeInformation.OSArchitecture}";
            byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed));
            return Convert.ToHexString(hash)[..16].ToLowerInvariant();
        }

        private static string getCpuDescription()
        {
            string? model = null;

            try
            {
                if (OperatingSystem.IsWindows())
                {
                    model = Registry.GetValue(
                        @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                        "ProcessorNameString",
                        null) as string;
                }
                else if (OperatingSystem.IsLinux() && File.Exists("/proc/cpuinfo"))
                {
                    foreach (string line in File.ReadLines("/proc/cpuinfo"))
                    {
                        if (!line.StartsWith("model name", StringComparison.OrdinalIgnoreCase))
                            continue;

                        int separator = line.IndexOf(':');

                        if (separator >= 0)
                            model = line[(separator + 1)..].Trim();

                        break;
                    }
                }
            }
            catch
            {
            }

            string platform = $"{RuntimeInformation.ProcessArchitecture}, {Environment.ProcessorCount} logical processors";
            return string.IsNullOrWhiteSpace(model) ? platform : $"{model.Trim()} ({platform})";
        }
    }
}
