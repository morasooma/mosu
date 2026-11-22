using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Platform;
using osu.Game;
using osu.Game.Screens.Menu;

namespace osu.Desktop.Performance
{
    /// <summary>Opt-in menu throughput probe. Not a gameplay or input-latency benchmark.</summary>
    internal partial class MoraVulkanMenuBenchmark : Component
    {
        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        private readonly Stopwatch timer = new Stopwatch();
        private readonly List<double> samples = new List<double>();
        private int lastSecond = -1;
        private double previousDrawInactive;
        private double previousUpdateInactive;
        private bool previousInactiveExecution;
        private bool finished;

        protected override void Update()
        {
            base.Update();
            if (finished || game.ScreenStack?.CurrentScreen is not MainMenu)
                return;

            if (!timer.IsRunning)
            {
                previousDrawInactive = host.DrawThread.InactiveHz;
                previousUpdateInactive = host.UpdateThread.InactiveHz;
                previousInactiveExecution = host.AllowBenchmarkInactiveExecution;
                host.AllowBenchmarkInactiveExecution = true;
                host.DrawThread.InactiveHz = host.DrawThread.ActiveHz;
                host.UpdateThread.InactiveHz = host.UpdateThread.ActiveHz;
                timer.Start();
            }

            int second = (int)timer.Elapsed.TotalSeconds;
            if (second == lastSecond)
                return;
            lastSecond = second;

            if (second >= 8 && second < 20)
            {
                double fps = host.DrawThread.Clock.FramesPerSecond;
                samples.Add(fps);
                Console.WriteLine(FormattableString.Invariant($"MORA_MENU_FPS second={second} draw_fps={fps:F2}"));
            }

            if (second >= 20)
            {
                finished = true;
                Console.WriteLine(FormattableString.Invariant(
                    $"MORA_MENU_RESULT renderer={host.ResolvedRenderer} samples={samples.Count} mean_sampled_fps={samples.Average():F2} min_sampled_fps={samples.Min():F2}"));
                game.Exit();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            if (timer.IsRunning)
            {
                host.DrawThread.InactiveHz = previousDrawInactive;
                host.UpdateThread.InactiveHz = previousUpdateInactive;
                host.AllowBenchmarkInactiveExecution = previousInactiveExecution;
            }
            base.Dispose(isDisposing);
        }
    }
}
