using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace osu.Game.Graphics.Backgrounds.AI
{
    /// <summary>Admission control, not a reservation of specific cores. The OS still schedules the one worker.</summary>
    internal sealed class BackgroundCpuBudget
    {
        private (ulong Total, ulong Idle)? previous;
        private long lastSample;
        public int AvailableCores { get; private set; }

        internal static int Calculate(int processors, double idleFraction)
            => Math.Max(0, (int)Math.Floor(processors * Math.Clamp(idleFraction, 0, 1)) - Math.Min(2, processors - 1));

        public bool CanRun()
        {
            if (Stopwatch.GetElapsedTime(lastSample).TotalMilliseconds < 500)
                return AvailableCores > 0;
            lastSample = Stopwatch.GetTimestamp();
            var sample = read();
            if (sample == null)
            {
                // Unsupported platform: conservative single worker with work/rest pacing.
                AvailableCores = 1;
                return true;
            }
            if (previous is { } old && sample.Value.Total > old.Total)
                AvailableCores = Calculate(Environment.ProcessorCount, (double)(sample.Value.Idle - old.Idle) / (sample.Value.Total - old.Total));
            previous = sample;
            return AvailableCores > 0;
        }

        private static (ulong Total, ulong Idle)? read()
        {
            try
            {
                if (OperatingSystem.IsLinux())
                {
                    using var reader = new StreamReader("/proc/stat");
                    ulong[] fields = reader.ReadLine()!.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).Take(8).Select(ulong.Parse).ToArray();
                    return (fields.Aggregate(0UL, (sum, value) => sum + value), fields[3] + fields[4]);
                }
                if (OperatingSystem.IsWindows() && GetSystemTimes(out ulong idle, out ulong kernel, out ulong user))
                    return (kernel + user, idle);
            }
            catch (IOException)
            {
                // CPU telemetry is optional; don't break classification on restricted systems.
            }
            return null;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetSystemTimes(out ulong idle, out ulong kernel, out ulong user);
    }
}
