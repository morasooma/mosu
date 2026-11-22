using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Shapes;
using osu.Game.Screens.Play;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Skinning.Components;
using osu.Game.Skinning;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using osuTK;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>Silent real-renderer checks of source alignment, local clipping and cache reuse.</summary>
    public partial class SkinBackdropCaptureGame : osu.Framework.Game, IBackgroundSceneSource
    {
        private readonly string output;
        private Sprite backdrop = null!;
        private readonly GameplaySkinUnderlay underlay = new GameplaySkinUnderlay();
        private BlurredBeatmapBackground panel = null!;
        private bool started;
        public bool Completed { get; private set; }
        public Sprite BackgroundSprite => backdrop;

        public SkinBackdropCaptureGame(string output)
        {
            this.output = Path.GetFullPath(output);
        }

        protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => new NativeStorage(Path.Combine(output, "profile"));

        protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults() => new Dictionary<FrameworkSetting, object>
        {
            [FrameworkSetting.AudioDevice] = "No sound",
            [FrameworkSetting.VolumeUniversal] = 0.0,
            [FrameworkSetting.WindowMode] = WindowMode.Windowed,
            [FrameworkSetting.WindowedSize] = new System.Drawing.Size(800, 600),
            [FrameworkSetting.ConfineMouseMode] = ConfineMouseMode.Never,
            [FrameworkSetting.FrameSync] = FrameSync.VSync,
        };

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);
            host.Window.ShowOnFirstDraw = false;
            host.Window.Hide();
            host.Window.CursorState |= CursorState.Hidden;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBackgroundSceneSource>(this);
            dependencies.CacheAs(underlay);
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var image = new Image<Rgba32>(1000, 600);
            for (int y = 0; y < image.Height; y++)
            for (int x = 0; x < image.Width; x++)
                image[x, y] = x < 500 ? new Rgba32(240, 30, 20) : new Rgba32(20, 60, 240);
            var texture = new DisposableTexture(Host.Renderer.CreateTexture(1000, 600));
            texture.SetData(new TextureUpload(image));
            Add(new Container
            {
                Position = new Vector2(25, 35),
                Size = new Vector2(800, 600),
                Child = backdrop = new Sprite { Texture = texture, Size = new Vector2(1000, 600), X = -100 },
            });
            Add(underlay);
            Add(new Box { Position = new Vector2(470, 225), Size = new Vector2(20), Colour = Colour4.Green });
            Add(panel = new BlurredBeatmapBackground { Position = new Vector2(300, 170), Size = new Vector2(250, 160), Alpha = 0 });
        }

        protected override void Update()
        {
            base.Update();
            if (started) return;
            started = true;
            _ = capture();
        }

        private async Task capture()
        {
            try
            {
                await Audio.SuspendLiveOutputAsync();
                for (int i = 0; i < 15; i++) await nextFrame();
                using var baseline = await Host.TakeScreenshotAsync();
                await baseline.SaveAsPngAsync(Path.Combine(output, "baseline.png"));
                await onUpdate(() => panel.Alpha = 1);
                for (int i = 0; i < 15; i++) await nextFrame();
                using var blurred = await Host.TakeScreenshotAsync();
                await blurred.SaveAsPngAsync(Path.Combine(output, "local-blur.png"));
                if (!blurred[475, 230].Equals(baseline[475, 230]))
                    throw new InvalidOperationException("Blur obscured a foreground gameplay object");
                if (!blurred[330, 240].Equals(baseline[330, 240]))
                    throw new InvalidOperationException("Blur added a tint to a flat background region");
                var left = blurred[330, 240];
                var right = blurred[520, 240];
                var edge = blurred[425, 240];
                if (left.R < left.B * 2 || right.B < right.R * 2)
                    throw new InvalidOperationException($"Background crop mismatch: left={left}, right={right}");
                if (edge.R < 15 || edge.B < 15)
                    throw new InvalidOperationException($"No Gaussian edge blending: {edge}");
                if (!blurred[100, 240].Equals(baseline[100, 240]))
                    throw new InvalidOperationException("Blur escaped panel mask");

                long version = await getDrawVersion();
                for (int i = 0; i < 40; i++) await nextFrame();
                long stableVersion = await getDrawVersion();
                if (version != stableVersion)
                    throw new InvalidOperationException($"Static panel re-rendered: {version} -> {stableVersion}");

                await onUpdate(() => SkinComponentGeometry.For(panel).Radius.Value = 40);
                for (int i = 0; i < 10; i++) await nextFrame();
                using var rounded = await Host.TakeScreenshotAsync();
                await rounded.SaveAsPngAsync(Path.Combine(output, "rounded-blur.png"));
                if (!rounded[310, 180].Equals(baseline[310, 180]) || !rounded[330, 240].Equals(blurred[330, 240]))
                    throw new InvalidOperationException("Per-element radius did not change the mask independently of its background");
                await onUpdate(() => SkinComponentGeometry.For(panel).Radius.Value = 0);
                for (int i = 0; i < 10; i++) await nextFrame();
                using var square = await Host.TakeScreenshotAsync();
                if (await getDrawVersion() != stableVersion)
                    throw new InvalidOperationException("Changing radius recaptured blur");
                await onUpdate(() => SkinComponentGeometry.For(panel).Reset());

                await onUpdate(() =>
                {
                    var theme = new CustomThemeDefinition();
                    theme.SetOverride(OverlayColour.Background3, Colour4.White);
                    OverlayColourProvider.CustomTheme.Value = theme;
                    OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                });
                for (int i = 0; i < 10; i++) await nextFrame();
                using var themed = await Host.TakeScreenshotAsync();
                await themed.SaveAsPngAsync(Path.Combine(output, "themed-blur.png"));
                if (!themed[330, 240].Equals(blurred[330, 240]))
                    throw new InvalidOperationException("Theme added an unsolicited white veil");
                await onUpdate(() => panel.TintOpacity.Value = 0.5f);
                for (int i = 0; i < 8; i++) await nextFrame();
                using var tinted = await Host.TakeScreenshotAsync();
                if (tinted[330, 240].G <= blurred[330, 240].G + 20)
                    throw new InvalidOperationException("Explicit tint opacity had no effect");
                if (await getDrawVersion() != stableVersion)
                    throw new InvalidOperationException("Changing tint reran GPU blur");
                await onUpdate(() => { OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default; panel.TintOpacity.Value = 0; });

                await onUpdate(() => panel.BlurStrength.Value = 30);
                for (int i = 0; i < 12; i++) await nextFrame();
                using var stronger = await Host.TakeScreenshotAsync();
                await stronger.SaveAsPngAsync(Path.Combine(output, "stronger-blur.png"));
                if (stronger[400, 240].B <= blurred[400, 240].B)
                    throw new InvalidOperationException("Increasing sigma did not broaden the blur");
                await onUpdate(() => panel.BlurStrength.Value = 0);
                for (int i = 0; i < 12; i++) await nextFrame();
                using var sharp = await Host.TakeScreenshotAsync();
                await sharp.SaveAsPngAsync(Path.Combine(output, "no-blur.png"));
                if (!sharp[424, 240].Equals(baseline[424, 240]) || !sharp[426, 240].Equals(baseline[426, 240]))
                    throw new InvalidOperationException("Disabling blur did not restore the sharp background edge");
                long sharpVersion = await getDrawVersion();
                await onUpdate(() => panel.Dim.Value = 1);
                for (int i = 0; i < 8; i++) await nextFrame();
                using var dimmed = await Host.TakeScreenshotAsync();
                if (dimmed[330, 240].R > 2 || dimmed[330, 240].B > 2)
                    throw new InvalidOperationException("Maximum dim did not produce black");
                if (await getDrawVersion() != sharpVersion)
                    throw new InvalidOperationException("Dimming recaptured the framebuffer");
                await onUpdate(() => panel.Opacity.Value = 0);
                for (int i = 0; i < 8; i++) await nextFrame();
                using var transparent = await Host.TakeScreenshotAsync();
                if (!transparent[330, 240].Equals(baseline[330, 240]))
                    throw new InvalidOperationException("Zero opacity did not reveal the original scene");
                await onUpdate(() => { panel.Opacity.Value = 1; panel.Dim.Value = 0; panel.ShowFullImage.Value = true; });
                for (int i = 0; i < 12; i++) await nextFrame();
                using var full = await Host.TakeScreenshotAsync();
                await full.SaveAsPngAsync(Path.Combine(output, "full-image.png"));
                if (full[410, 240].R < full[410, 240].B * 2 || full[440, 240].B < full[440, 240].R * 2)
                    throw new InvalidOperationException("Full image was cropped instead of fitted into the panel");
                if (full[350, 172].R < 230 || full[500, 328].B < 230)
                    throw new InvalidOperationException("Image does not fill the panel to its top/bottom edges");
                await onUpdate(() => panel.BlurStrength.Value = 40);
                for (int i = 0; i < 12; i++) await nextFrame();
                using var fullBlur = await Host.TakeScreenshotAsync();
                await fullBlur.SaveAsPngAsync(Path.Combine(output, "full-image-strong-blur.png"));
                if (fullBlur[302, 240].R < 230 || fullBlur[547, 240].B < 230 || fullBlur[330, 172].R < 230 || fullBlur[520, 327].B < 230)
                    throw new InvalidOperationException("Strong blur darkened image edges");
                await onUpdate(() =>
                {
                    panel.ShowFullImage.Value = false;
                    panel.Position = new Vector2(10, 40);
                });
                for (int i = 0; i < 12; i++) await nextFrame();
                using var sourceEdge = await Host.TakeScreenshotAsync();
                await sourceEdge.SaveAsPngAsync(Path.Combine(output, "source-edge-blur.png"));
                if (sourceEdge[40, 45].R < 230 || sourceEdge[40, 100].R < 230 || sourceEdge[12, 100].R < 230)
                    throw new InvalidOperationException("Blur samples empty pixels beyond the source edge");
                await onUpdate(() =>
                {
                    panel.Position = new Vector2(760, 530);
                    panel.Size = new Vector2(250, 160);
                });
                for (int i = 0; i < 12; i++) await nextFrame();
                using var farEdge = await Host.TakeScreenshotAsync();
                await farEdge.SaveAsPngAsync(Path.Combine(output, "beyond-bottom-right.png"));
                if (farEdge[950, 650].B < 230 || farEdge[1007, 640].B < 230)
                    throw new InvalidOperationException("Background leaves empty bottom/right panel areas");
                await onUpdate(() => panel.Position = new Vector2(300, 170));
                await onUpdate(() => { panel.ShowFullImage.Value = false; panel.BlurStrength.Value = 12; panel.BlurEnabled.Value = true; });
                for (int i = 0; i < 10; i++) await nextFrame();

                await onUpdate(() => panel.Position = new Vector2(100, 170));
                for (int i = 0; i < 15; i++) await nextFrame();
                using var moved = await Host.TakeScreenshotAsync();
                await moved.SaveAsPngAsync(Path.Combine(output, "moved-blur.png"));
                var movedRight = moved[320, 240];
                if (movedRight.R < movedRight.B * 2)
                    throw new InvalidOperationException("Moving panel moved/refitted image instead of changing sampled region");
                if (await getDrawVersion() == stableVersion)
                    throw new InvalidOperationException("Moving panel did not refresh framebuffer");
                await onUpdate(() =>
                {
                    panel.Scale = new Vector2(0.8f);
                    panel.Rotation = 20;
                });
                for (int i = 0; i < 15; i++) await nextFrame();
                using var rotated = await Host.TakeScreenshotAsync();
                await rotated.SaveAsPngAsync(Path.Combine(output, "rotated-blur.png"));
                File.WriteAllText(Path.Combine(output, "result.txt"), $"PASS: local crop, Gaussian blur toggle, dim without recapture, opacity, aspect-fill image without empty strips or dark blur edges, configurable round/square mask, movement, live interface tint without GPU blur recapture, static GPU buffer reuse. DrawVersion={version} remained {stableVersion} across 40 frames.");
                Completed = true;
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString()); }
            finally { Scheduler.Add(() => Host.Exit()); }
        }

        private async Task<long> getDrawVersion()
        {
            long version = -1;
            await onUpdate(() =>
            {
                var bufferField = typeof(BlurredBeatmapBackground).GetField("buffer", BindingFlags.Instance | BindingFlags.NonPublic)!;
                var buffer = bufferField.GetValue(panel)!;
                var shared = typeof(BufferedContainer<Drawable>).GetField("sharedData", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(buffer)!;
                version = (long)typeof(BufferedDrawNodeSharedData).GetField("DrawVersion", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(shared)!;
            });
            return version;
        }

        private Task onUpdate(Action action)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Scheduler.Add(() =>
            {
                try { action(); completion.SetResult(); }
                catch (Exception e) { completion.SetException(e); }
            });
            return completion.Task;
        }

        private Task nextFrame()
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Scheduler.AddDelayed(() => completion.SetResult(), 35);
            return completion.Task;
        }
    }
}
