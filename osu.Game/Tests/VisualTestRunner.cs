// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework;
using osu.Framework.Platform;

namespace osu.Game.Tests
{
    public static class VisualTestRunner
    {
        [STAThread]
        public static int Main(string[] args)
        {
#if DEBUG
            if (args.Length == 2 && args[0] == "--capture-leaderboard")
            {
                using var captureHost = Host.GetSuitableDesktopHost("mosu-leaderboard-" + Guid.NewGuid().ToString("N"), new HostOptions { IPCPipeName = null });
                captureHost.DisableFrameworkConfigSaving = true;
                var type = System.Reflection.Assembly.GetEntryAssembly()!.GetType("osu.Game.Tests.NonVisual.LeaderboardCaptureGame", true)!;
                using var game = (osu.Framework.Game)Activator.CreateInstance(type, args[1])!;
                captureHost.Run(game);
                return (bool)type.GetProperty("Completed")!.GetValue(game)! ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--capture-skin-fonts")
            {
                using var captureHost = Host.GetSuitableDesktopHost("mosu-skin-fonts-" + Guid.NewGuid().ToString("N"), new HostOptions { IPCPipeName = null });
                captureHost.DisableFrameworkConfigSaving = true;
                var type = System.Reflection.Assembly.GetEntryAssembly()!.GetType("osu.Game.Tests.NonVisual.SkinFontCaptureGame", true)!;
                using var game = (osu.Framework.Game)Activator.CreateInstance(type, args[1])!;
                captureHost.Run(game);
                return (bool)type.GetProperty("Completed")!.GetValue(game)! ? 0 : 1;
            }
            if (args.Length == 2 && args[0] == "--capture-skin-backdrop")
            {
                string name = "mosu-skin-backdrop-" + Guid.NewGuid().ToString("N");
                using var captureHost = Host.GetSuitableDesktopHost(name, new HostOptions { IPCPipeName = null, BypassCompositor = false });
                captureHost.DisableFrameworkConfigSaving = true;
                var type = System.Reflection.Assembly.GetEntryAssembly()!.GetType("osu.Game.Tests.NonVisual.SkinBackdropCaptureGame", true)!;
                using var game = (osu.Framework.Game)Activator.CreateInstance(type, args[1])!;
                captureHost.Run(game);
                return (bool)type.GetProperty("Completed")!.GetValue(game)! ? 0 : 1;
            }
            if (args.Length == 4 && args[0] == "--capture-morasooma-shaders")
            {
                string name = "mosu-shader-capture-" + Guid.NewGuid().ToString("N");
                string? cache = null;
                try
                {
                    using var captureHost = Host.GetSuitableDesktopHost(name, new HostOptions { IPCPipeName = null, BypassCompositor = false });
                    captureHost.DisableFrameworkConfigSaving = true;
                    // Resolved only by the main test assembly, not by other ruleset test runners.
                    var type = System.Reflection.Assembly.GetEntryAssembly()!.GetType("osu.Game.Tests.NonVisual.MorasoomaShaderCaptureGame", true)!;
                    using var game = (osu.Framework.Game)Activator.CreateInstance(type, args[1], args[2], args[3])!;
                    try { captureHost.Run(game); }
                    finally { cache = captureHost.CacheStorage == null ? null : System.IO.Path.GetDirectoryName(captureHost.CacheStorage.GetFullPath(string.Empty).TrimEnd(System.IO.Path.DirectorySeparatorChar)); }
                    return (bool)type.GetProperty("Completed")!.GetValue(game)! ? 0 : 1;
                }
                finally
                {
                    if (cache != null && System.IO.Path.GetFileName(cache) == name && System.IO.Directory.Exists(cache))
                        System.IO.Directory.Delete(cache, true);
                }
            }
#endif
            using (DesktopGameHost host = Host.GetSuitableDesktopHost(@"osu-development"))
            {
                host.Run(new OsuTestBrowser());
                return 0;
            }
        }
    }
}
