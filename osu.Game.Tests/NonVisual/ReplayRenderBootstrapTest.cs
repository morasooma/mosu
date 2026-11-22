using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Platform;
using osuTK.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Scoring.Render;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public partial class ReplayRenderBootstrapTest
    {
        [TestCase(true, false, RendererType.Direct3D11)]
        [TestCase(false, true, RendererType.Vulkan)]
        [TestCase(false, false, RendererType.Automatic)]
        public void TestCaptureRendererIsPlatformSpecific(bool windows, bool linux, RendererType expected)
            => Assert.That(ReplayRenderGame.GetCaptureRenderer(windows, linux), Is.EqualTo(expected));

        [TestCase(false)]
        [TestCase(true)]
        public void TestRenderGameBootstrapsWithItsOwnAssetStore(bool withSnapshot)
        {
            using var snapshotStorage = new TemporaryNativeStorage($"render-bootstrap-snapshot-{Guid.NewGuid():N}");
            string snapshotPath = snapshotStorage.GetFullPath("snapshot.json");
            new ReplayRenderSnapshot
            {
                Layout = new ReplayRenderSnapshot.RenderLayout
                {
                    Scaling = ScalingMode.ExcludeOverlays, SizeX = 0.65f, SizeY = 0.75f,
                    PositionX = 0.3f, PositionY = 0.7f, UIScale = 0.9f,
                    BackgroundDim = 0.9f, SafeAreaConsiderations = true, DisableInterfaceShear = true,
                },
            }.Write(snapshotPath);

            using var host = new PersistedConfigProbeHost();
            using var game = new BootstrapProbe(withSnapshot ? new[] { "--render-snapshot", snapshotPath } : Array.Empty<string>());
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            using var registration = timeout.Token.Register(host.Exit);

            // Real game bootstrap, including base/derived BDLs and their different DI scopes.
            // The headless host has no window and uses the "No sound" audio device.
            host.Run(game);

            Assert.That(timeout.IsCancellationRequested, Is.False, "Replay render bootstrap timed out.");
            RendererType preferred = ReplayRenderGame.GetCaptureRenderer(OperatingSystem.IsWindows(), OperatingSystem.IsLinux());
            Assert.That(host.RendererAtSelection, Is.EqualTo(preferred == RendererType.Automatic ? RendererType.OpenGL : preferred));
            Assert.That(host.ModeAtSelection, Is.EqualTo(WindowMode.Windowed));
            Assert.That(host.ExecutionAtSelection, Is.EqualTo(ExecutionMode.SingleThread));
            Assert.That(host.FrameSyncAtSelection, Is.EqualTo(FrameSync.Unlimited));
            Assert.That(host.ConfigSaveAllowed, Is.False);
            Assert.That(File.ReadAllText(host.ConfigPath), Is.EqualTo(PersistedConfigProbeHost.PERSISTED_CONFIG), "Export must not save its overrides into the user's framework.ini.");
            Assert.That(game.Bootstrapped, Is.True);
            Assert.That(game.AssetStoreAvailable, Is.True);
            Assert.That(game.RenderMixerAvailable, Is.True);
            Assert.That(game.UnlimitedFrameSync, Is.True);
            Assert.That(host.AllowTearing.Value, Is.True, "Offline export must not wait for DXGI presentation.");
            if (withSnapshot)
            {
                Assert.That(game.LayoutMode, Is.EqualTo(ScalingMode.ExcludeOverlays));
                Assert.That(game.LayoutWidth, Is.EqualTo(0.65f));
                Assert.That(game.StraightInterface, Is.True, "Render host must bind the same shear policy as OsuGame.");
            }
        }

        private sealed class PersistedConfigProbeHost : CleanRunHeadlessGameHost
        {
            internal const string PERSISTED_CONFIG = "Renderer = OpenGL\nWindowMode = Fullscreen\nExecutionMode = MultiThreaded\nFrameSync = VSync\nAudioDevice = No sound\n";
            public string ConfigPath => Storage.GetFullPath("framework.ini");
            public RendererType RendererAtSelection { get; private set; }
            public WindowMode ModeAtSelection { get; private set; }
            public ExecutionMode ExecutionAtSelection { get; private set; }
            public FrameSync FrameSyncAtSelection { get; private set; }
            public bool ConfigSaveAllowed { get; private set; }

            protected override void SetupForRun()
            {
                base.SetupForRun();
                File.WriteAllText(ConfigPath, PERSISTED_CONFIG);
            }

            protected override void ChooseAndSetupRenderer()
            {
                RendererAtSelection = Config.Get<RendererType>(FrameworkSetting.Renderer);
                ModeAtSelection = Config.Get<WindowMode>(FrameworkSetting.WindowMode);
                ExecutionAtSelection = Config.Get<ExecutionMode>(FrameworkSetting.ExecutionMode);
                FrameSyncAtSelection = Config.Get<FrameSync>(FrameworkSetting.FrameSync);
                ConfigSaveAllowed = Config.Save();
                base.ChooseAndSetupRenderer();
            }
        }

        [TestCase(ScalingMode.Off)]
        [TestCase(ScalingMode.Everything)]
        [TestCase(ScalingMode.ExcludeOverlays)]
        [TestCase(ScalingMode.Gameplay)]
        public void TestDesktopBootstrapSelectsCaptureBackendDespitePersistedOpenGL(ScalingMode scaling)
        {
            if ((!OperatingSystem.IsWindows() && !OperatingSystem.IsLinux())
                || Environment.GetEnvironmentVariable("MOSU_TEST_REPLAY_DESKTOP_CAPTURE") != "1")
                Assert.Ignore("Opt-in: hidden desktop host; run on a private display with the No sound audio device.");
            using var storage = new TemporaryNativeStorage($"render-desktop-bootstrap-{Guid.NewGuid():N}");
            string configPath = storage.GetFullPath("framework.ini");
            File.WriteAllText(configPath, PersistedConfigProbeHost.PERSISTED_CONFIG);
            using var host = Host.GetSuitableDesktopHost($"render-capture-probe-{Guid.NewGuid():N}");
            host.DisableFrameworkConfigSaving = true;
            string snapshotPath = storage.GetFullPath("snapshot.json");
            new ReplayRenderSnapshot
            {
                Layout = new ReplayRenderSnapshot.RenderLayout
                {
                    Scaling = scaling, SizeX = 0.65f, SizeY = 0.75f,
                    PositionX = 0.3f, PositionY = 0.7f, UIScale = 0.9f,
                    BackgroundDim = 0.9f, SafeAreaConsiderations = true, DisableInterfaceShear = true,
                },
            }.Write(snapshotPath);
            using var game = new DesktopCaptureProbe(storage, snapshotPath);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            using var registration = timeout.Token.Register(host.Exit);
            host.Run(game);
            Assert.That(timeout.IsCancellationRequested, Is.False);
            Assert.That(game.Error, Is.Null);
            Assert.That(host.ResolvedRenderer, Is.EqualTo(ReplayRenderGame.GetCaptureRenderer(OperatingSystem.IsWindows(), OperatingSystem.IsLinux())));
            Assert.That(game.Nv12Captured, Is.True, "The actual desktop export host must reach the offscreen NV12 path.");
            Assert.That(File.ReadAllText(configPath), Is.EqualTo(PersistedConfigProbeHost.PERSISTED_CONFIG));
        }

        private sealed partial class DesktopCaptureProbe : ReplayRenderGame
        {
            private readonly Storage storage;
            private Box captureColour = null!;
            public Exception? Error { get; private set; }
            public bool Nv12Captured { get; private set; }

            public DesktopCaptureProbe(Storage storage, string snapshotPath) : base(new[] { "--resolution", "1280x720", "--render-snapshot", snapshotPath })
            {
                this.storage = storage;
            }

            protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => storage;

            protected override void LoadComplete()
            {
                Add(captureColour = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Red });
                // Wait until the scene has produced draw nodes, then exercise the same host API as export.
                Scheduler.AddDelayed(() => _ = captureAsync(), 100);
            }

            private async Task captureAsync()
            {
                using var request = new GameHost.BufferedScreenshotRequest
                {
                    UseVideoCapture = true,
                    PreserveNativePixelFormat = true,
                };
                try
                {
                    int bufferSize = await Host.ConfigureVideoCaptureAsync(1280, 720).ConfigureAwait(false);
                    Assert.That(bufferSize, Is.EqualTo(1280 * 720 * 3 / 2));
                    request.Buffer = new byte[bufferSize];
                    var geometryChecked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                    Scheduler.AddDelayed(() =>
                    {
                        try
                        {
                            var mode = LocalConfig.Get<ScalingMode>(OsuSetting.Scaling);
                            bool scaledScene = mode is ScalingMode.Everything or ScalingMode.ExcludeOverlays;
                            Assert.That(RenderCaptureTarget.ScreenSpaceDrawQuad.Width, Is.EqualTo(1280 * (scaledScene ? 0.65f : 1)).Within(2));
                            Assert.That(RenderCaptureTarget.ScreenSpaceDrawQuad.Height, Is.EqualTo(720 * (scaledScene ? 0.75f : 1)).Within(2));
                            Assert.That(captureColour.ScreenSpaceDrawQuad.Width, Is.EqualTo(1280 * (mode == ScalingMode.Everything ? 0.65f : 1)).Within(2),
                                "Global UI and screen-only scaling must remain distinct.");
                            Assert.That(OsuGame.SHEAR, Is.EqualTo(osuTK.Vector2.Zero));
                            geometryChecked.TrySetResult();
                        }
                        catch (Exception ex) { geometryChecked.TrySetException(ex); }
                    }, 600);
                    await geometryChecked.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    await Host.SetVideoCaptureDrawOnDemandAsync(true).ConfigureAwait(false);
                    foreach (var (colour, expectedLuma) in new[] { (Color4.Blue, 32), (Color4.Red, 63), (Color4.Lime, 173) })
                    {
                        var submitted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                        Host.UpdateThread.Scheduler.Add(() =>
                        {
                            captureColour.Colour = colour;
                            _ = submitAsync();
                        });
                        async Task submitAsync()
                        {
                            try
                            {
                                await Host.SubmitScreenshotToBufferAsync(request).ConfigureAwait(false);
                                submitted.TrySetResult();
                            }
                            catch (Exception ex) { submitted.TrySetException(ex); }
                        }
                        await submitted.Task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                        var (_, byteCount) = await Host.CompleteScreenshotToBufferAsync(request).ConfigureAwait(false);
                        Nv12Captured = request.IsNv12 && byteCount == 1280 * 720 * 3 / 2;
                        Assert.That(request.Buffer[360 * 1280 + 640], Is.EqualTo(expectedLuma).Within(1),
                            "Capture must use the newly drawn scene, not the previous colour.");
                        Assert.That(request.MapMilliseconds, Is.GreaterThanOrEqualTo(0));
                        long draws = Host.VideoCaptureFrameCounts.Draws;
                        await Task.Delay(50).ConfigureAwait(false);
                        Assert.That(Host.VideoCaptureFrameCounts.Draws, Is.EqualTo(draws), "No extra draws while export is waiting.");
                    }
                    using var cancelled = new CancellationTokenSource();
                    cancelled.Cancel();
                    bool captureCancelled = false;
                    try { await Host.SubmitScreenshotToBufferAsync(request, cancelled.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) { captureCancelled = true; }
                    Assert.That(captureCancelled, Is.True, "On-demand capture must still honour cancellation.");
                    long pausedDraws = Host.VideoCaptureFrameCounts.Draws;
                    await Host.SetVideoCaptureDrawOnDemandAsync(false).ConfigureAwait(false);
                    await Task.Delay(50).ConfigureAwait(false);
                    Assert.That(Host.VideoCaptureFrameCounts.Draws, Is.GreaterThan(pausedDraws), "Disabling capture pacing must restore normal rendering.");
                    await Host.ReleaseVideoCaptureAsync(new[] { request }).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Error = ex;
                }
                finally
                {
                    Host.Exit();
                }
            }
        }

        private sealed partial class BootstrapProbe : ReplayRenderGame
        {
            public bool Bootstrapped { get; private set; }
            public bool AssetStoreAvailable { get; private set; }
            public bool RenderMixerAvailable { get; private set; }
            public bool UnlimitedFrameSync { get; private set; }
            public ScalingMode LayoutMode { get; private set; }
            public float LayoutWidth { get; private set; }
            public bool StraightInterface { get; private set; }

            public BootstrapProbe(string[] args) : base(args)
            {
            }

            protected override void LoadComplete()
            {
                // All real dependency loading has completed. Do not start FFmpeg or require
                // a user's score/database just to check the renderer's bootstrap contract.
                AssetStoreAvailable = Dependencies.Get<OnlineAssetCachingStore>() != null;
                RenderMixerAvailable = RenderSampleCaptureMixer != null;
                UnlimitedFrameSync = Dependencies.Get<FrameworkConfigManager>().Get<FrameSync>(FrameworkSetting.FrameSync) == FrameSync.Unlimited;
                LayoutMode = LocalConfig.Get<ScalingMode>(OsuSetting.Scaling);
                LayoutWidth = LocalConfig.Get<float>(OsuSetting.ScalingSizeX);
                StraightInterface = OsuGame.DisableShear.Value;
                Bootstrapped = true;
                Host.Exit();
            }
        }
    }
}
