// MIT-licensed test tooling. Exercises the real framework renderer, not DummyRenderer.
using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.IO.Stores;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Game.Graphics;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osu.Game.Screens.Play.HUD.ArgonHealthDisplayParts;
using osuTK;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Rulesets.Osu.Skinning.Argon;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>Explicit test-runner-only entry point; no network, library, IPC or interactive profile.</summary>
    public partial class MorasoomaShaderCaptureGame : osu.Framework.Game
    {
        private readonly string output;
        private readonly string bank;
        private readonly string textureBank;
        private readonly ManualClock manualClock = new ManualClock();
        private readonly FramedClock frozenClock;
        private Container stage = null!;
        private bool started;
        public bool Completed { get; private set; }
        public Exception? Error { get; private set; }

        public MorasoomaShaderCaptureGame(string output, string bank, string textureBank)
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            // This disposable process must exercise decorative shaders even when normal
            // client performance defaults skip them. No persisted setting is touched.
            osu.Game.Skinning.SkinPerformanceMode.Enabled = false;
            this.output = Path.GetFullPath(output);
            this.bank = bank == "embedded" ? bank : Path.GetFullPath(bank);
            this.textureBank = Path.GetFullPath(textureBank);
            frozenClock = new FramedClock(manualClock);
        }

        protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => new NativeStorage(Path.Combine(output, "profile"));

        protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults() => new Dictionary<FrameworkSetting, object>
        {
            [FrameworkSetting.AudioDevice] = "No sound",
            [FrameworkSetting.VolumeUniversal] = 0.0,
            [FrameworkSetting.VolumeMusic] = 0.0,
            [FrameworkSetting.VolumeEffect] = 0.0,
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

        [BackgroundDependencyLoader]
        private void load()
        {
            // Framework resources are already present. This directory supplies only game shaders.
            if (bank == "embedded")
                Resources.AddStore(new NamespacedResourceStore<byte[]>(new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly), "ShaderOverrides"));
            else
                Resources.AddStore(new ShaderDirectoryStore(bank));
            Resources.AddStore(new StorageBackedResourceStore(new NativeStorage(textureBank)));
            stage = new Container { Size = new Vector2(800, 600), Clock = frozenClock };
            Add(stage);
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
                int index = 0;
                foreach (var test in cases())
                {
                    Drawable drawable = null!;
                    await onUpdate(() =>
                    {
                        stage.Clear();
                        manualClock.CurrentTime = test.Time;
                        frozenClock.ProcessFrame();
                        stage.Add(new Box { RelativeSizeAxes = Axes.Both, Colour = new Colour4(24, 27, 35, 255) });
                        drawable = test.Create();
                        stage.Add(drawable);
                    });
                    // Wait for actual asynchronous dependency loading and draw-node generation.
                    for (int n = 0; n < 8 || !drawable.IsLoaded; n++)
                    {
                        if (n > 600) throw new TimeoutException(test.Name);
                        await nextFrame();
                    }
                    if (test.Name.StartsWith("trail-", StringComparison.Ordinal))
                    {
                        await onUpdate(() => { manualClock.CurrentTime += 180; frozenClock.ProcessFrame(); });
                        for (int n = 0; n < 4; n++) await nextFrame();
                    }
                    // Logo's historic SPIR-V workaround intentionally suppresses offscreen passes.
                    // Capture the real backbuffer so the reference is not a falsely passing blank.
                    using var image = await Host.TakeScreenshotAsync();
                    image.Mutate(c => c.Crop(new SixLabors.ImageSharp.Rectangle(0, 0, 800, 600)));
                    await image.SaveAsPngAsync(Path.Combine(output, $"{index++:D3}-{test.Name}.png"));
                }
                Completed = true;
            }
            catch (Exception e) { Error = e; File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString()); }
            finally { Scheduler.Add(() => Host.Exit()); }
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

        private IEnumerable<(string Name, double Time, Func<Drawable> Create)> cases()
        {
            foreach (float scale in new[] { 0.65f, 1f, 1.6f })
            foreach (float fill in new[] { 0f, 0.02f, 0.25f, 0.5f, 0.92f, 1f })
            {
                float s = scale, f = fill;
                yield return ($"health-{s}-{f}", 0, () => new Container
                {
                    Position = new Vector2(65, 100), Scale = new Vector2(s), Size = new Vector2(420, 70),
                    Children = new Drawable[]
                    {
                        new ArgonHealthDisplayBackground { RelativeSizeAxes = Axes.Both },
                        new ArgonHealthDisplayBar { RelativeSizeAxes = Axes.Both, ProgressRange = new Vector2(0, f), GlowPortion = 0.65f, BarColour = Colour4.Cyan, GlowColour = Colour4.White },
                    }
                });
            }
            foreach (float height in new[] { 12f, 20f, 100f })
            {
                float h = height;
                yield return ($"health-height-{h}", 0, () => new ArgonHealthDisplayBar { Position = new Vector2(60, 150), Size = new Vector2(400, h), ProgressRange = new Vector2(0.2f, 0.8f), GlowPortion = 0, BarColour = Colour4.Cyan });
            }
            foreach (float width in new[] { 20f, 50f, 80f, 120f })
            {
                float w = width;
                yield return ($"health-narrow-{w}", 0, () => new ArgonHealthDisplayBar { Position = new Vector2(60, 100), Size = new Vector2(w, 70), ProgressRange = new Vector2(0.1f, 0.9f), GlowPortion = 0.8f, BarColour = Colour4.Cyan, GlowColour = Colour4.White });
            }
            foreach (bool rectangular in new[] { false, true })
            foreach (float scale in new[] { 0.4f, 1f, 2.5f })
            foreach (float dim in new[] { 0f, 0.5f, 1f })
            {
                bool r = rectangular; float s = scale, d = dim;
                yield return ($"flashlight-{r}-{s}-{d}", 0, () =>
                {
                    var tracker = new ModFlashlight<HitObject>.PlayfieldDrawInfoTracker();
                    return new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            tracker,
                            new Box { Position = new Vector2(20, 20), Size = new Vector2(760, 560), Colour = Colour4.Cyan },
                            new CaptureFlashlight(r, new Vector2(360, 260), new Vector2(125, 90) * s)
                            {
                                RelativeSizeAxes = Axes.Both, Colour = Colour4.Black, FlashlightDim = d,
                                PlayfieldDrawInfoTracker = tracker,
                            },
                        }
                    };
                });
            }
            foreach (double t in new[] { 0d, 250d, 750d, 1400d })
                yield return ($"ghost-{t}", t, () => new GhostIcon { Position = new Vector2(160, 130), Size = new Vector2(240), Colour = Colour4.Cyan });
            foreach (float p in new[] { 0f, 0.2f, 0.5f, 0.8f, 1.01f })
            foreach (string key in new[] { "Intro/Triangles/logo-background", "Online/RankedPlay/vs-animation" })
            {
                float progress = p; string texture = key;
                yield return ($"logo-{(key.StartsWith("Intro", StringComparison.Ordinal) ? "menu" : "vs")}-{p}", 0, () => new LogoAnimation
                {
                    Position = new Vector2(130, 60), Size = new Vector2(450), Texture = Textures.Get(texture), AnimationProgress = progress,
                });
            }
            foreach (float scale in new[] { 0.75f, 1.5f, 3f })
            {
                float s = scale;
                yield return ($"triangles-{s}", 1250, () => new Triangles(83715)
                {
                    RelativeSizeAxes = Axes.Both, TriangleScale = s, SpawnRatio = 0.2f, Velocity = 0,
                    ColourLight = osuTK.Graphics.Color4.White, ColourDark = osuTK.Graphics.Color4.Cyan,
                });
            }
            foreach (float thickness in new[] { 0.01f, 0.05f, 0.4f })
            {
                float t = thickness;
                yield return ($"triangle-outline-{t}", 1250, () => new TrianglesV2(83715)
                {
                    RelativeSizeAxes = Axes.Both, SpawnRatio = 0.15f, Velocity = 0, Thickness = t, ScaleAdjust = 1.5f,
                });
            }
            foreach (float exponent in new[] { 1f, 2f, 4f })
            {
                float e = exponent;
                yield return ($"trail-{e}", 1000, () => new CaptureTrail(frozenClock, e)
                {
                    Texture = Textures.Get("Cursor/cursortrail"), CursorScale = new Vector2(3),
                });
            }
            foreach (float progress in new[] { 0f, 0.02f, 0.08f, 0.15f, 0.5f, 1f })
            {
                float p = progress;
                yield return ($"spinner-{p}", 0, () =>
                {
                    // Instantiate the real private pass rather than a duplicate shader-only sprite.
                    var type = typeof(ArgonSpinnerProgressArc).GetNestedType("ProgressFill", BindingFlags.NonPublic)!;
                    var fill = (CircularProgress)Activator.CreateInstance(type, true)!;
                    if ((bool)type.GetField("dropGlow", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(fill)!)
                        throw new InvalidOperationException("Spinner glow was skipped; this cannot count as a shader comparison");
                    fill.Position = new Vector2(200, 120);
                    fill.Size = new Vector2(320);
                    fill.Progress = p;
                    fill.InnerRadius = 0.12f;
                    fill.RoundedCaps = true;
                    fill.Colour = Colour4.Cyan;
                    return fill;
                });
            }
        }

        private sealed class ShaderDirectoryStore : IResourceStore<byte[]>
        {
            private readonly string path;
            public ShaderDirectoryStore(string path) => this.path = path;
            public byte[] Get(string name) => name.StartsWith("Shaders/", StringComparison.Ordinal) && File.Exists(Path.Combine(path, name[8..])) ? File.ReadAllBytes(Path.Combine(path, name[8..])) : null!;
            public Task<byte[]> GetAsync(string name, System.Threading.CancellationToken token = default) => Task.FromResult(Get(name));
            public Stream GetStream(string name) => Get(name) is { } bytes ? new MemoryStream(bytes) : null!;
            public IEnumerable<string> GetAvailableResources() => Array.Empty<string>();
            public void Dispose() { }
        }

        private partial class CaptureFlashlight : ModFlashlight<HitObject>.Flashlight
        {
            private readonly bool rectangular;
            public CaptureFlashlight(bool rectangular, Vector2 position, Vector2 size) : base(new OsuModFlashlight())
            {
                this.rectangular = rectangular;
                FlashlightPosition = position;
                FlashlightSize = size;
            }
            protected override string FragmentShader => rectangular ? "RectangularFlashlight" : "CircularFlashlight";
            protected override void UpdateFlashlightSize(float size) { }
        }

        private partial class CaptureTrail : CursorTrail
        {
            private readonly IFrameBasedClock clock;
            private bool seeded;
            private readonly float exponent;
            protected override float FadeExponent => exponent;
            public CaptureTrail(IFrameBasedClock clock, float exponent) { this.clock = clock; this.exponent = exponent; }
            protected override void LoadComplete() { base.LoadComplete(); UseExternalClock(clock); }
            protected override void Update()
            {
                base.Update();
                if (seeded) return;
                seeded = true;
                for (int n = 0; n < 35; n++) AddTrail(new Vector2(80 + n * 16, 270 + MathF.Sin(n * 0.25f) * 80));
            }
        }

    }
}
