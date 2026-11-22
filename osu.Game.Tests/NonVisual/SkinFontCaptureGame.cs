using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osuTK;
using SixLabors.ImageSharp;

namespace osu.Game.Tests.NonVisual
{
    /// <summary>Silent isolated real-renderer verification of arbitrary families replacing textured HUD fonts.</summary>
    public partial class SkinFontCaptureGame : OsuGameBase
    {
        private readonly string output;
        private FontSample argon = null!;
        private FontSample legacy = null!;
        private bool started;
        public bool Completed { get; private set; }
        protected override bool EnableOfficialBeatmapIntegration => false;

        public SkinFontCaptureGame(string output)
        {
            this.output = Path.GetFullPath(output);
            Storage = new NativeStorage(Path.Combine(this.output, "profile"));
        }

        protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => Storage;
        protected override Container CreateScalingContainer() => new Container { RelativeSizeAxes = Axes.Both };
        protected override IAPIProvider CreateAPIProvider(EndpointConfiguration endpoints)
        {
            var api = new DummyAPIAccess();
            api.SetState(APIState.Offline);
            return api;
        }

        protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults() => new Dictionary<FrameworkSetting, object>
        {
            [FrameworkSetting.VolumeUniversal] = 0.0,
            [FrameworkSetting.VolumeMusic] = 0.0,
            [FrameworkSetting.VolumeEffect] = 0.0,
            [FrameworkSetting.AudioDevice] = "No sound",
            [FrameworkSetting.WindowMode] = WindowMode.Windowed,
            [FrameworkSetting.WindowedSize] = new System.Drawing.Size(800, 600),
            [FrameworkSetting.FrameSync] = FrameSync.VSync,
        };

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);
            LocalConfig.DisableSaving();
            LocalConfig.SetValue(OsuSetting.ForkSkinPerformanceMode, false);
            host.Window.ShowOnFirstDraw = false;
            host.Window.Hide();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Add(argon = new FontSample
            {
                Position = new Vector2(50, 100),
                Child = new ArgonCounterTextComponent(Anchor.TopLeft, "ARGON")
                {
                    Text = "1234567890", ShowLabel = { Value = true }, WireframeOpacity = { BindTarget = new osu.Framework.Bindables.BindableFloat(0.25f) },
                },
            });
            Add(legacy = new FontSample
            {
                Position = new Vector2(50, 250),
                Child = new LegacySpriteText(LegacyFont.Score) { Text = "1234567890" },
            });
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
                for (int i = 0; i < 20; i++) await frame();
                using var original = await Host.TakeScreenshotAsync();
                await original.SaveAsPngAsync(Path.Combine(output, "original.png"));
                foreach (string family in new[] { "Inter", "Torus", "Nunito" })
                {
                    await update(() =>
                    {
                        SkinComponentFont.For(argon).Family.Value = family;
                        SkinComponentFont.For(legacy).Family.Value = family;
                    });
                    for (int i = 0; i < 12; i++) await frame();
                    using var image = await Host.TakeScreenshotAsync();
                    await image.SaveAsPngAsync(Path.Combine(output, family + ".png"));
                    await update(() =>
                    {
                        foreach (var text in new Drawable[] { argon, legacy }.SelectMany(d => d.ChildrenOfType<OsuSpriteText>()).Where(t => t.Alpha > 0))
                            if (text.Font.Family != family || text.Font.Size < 5 || text.DrawHeight < 5)
                                throw new InvalidOperationException($"Invalid {family} glyph layout: {text.Font}, size={text.DrawSize}");
                    });
                    foreach (int top in new[] { 95, 245 })
                    {
                        int changed = 0;
                        for (int y = top; y < top + 100; y++)
                        for (int x = 45; x < 650; x++)
                        {
                            var p = image[x, y];
                            if (p.R > 70 && p.G > 70 && p.B > 70 && !p.Equals(original[x, y])) changed++;
                        }
                        if (changed < 60) throw new InvalidOperationException($"No visible {family} glyph replacement in row {top}: {changed}");
                    }
                }
                await update(() =>
                {
                    SkinComponentFont.For(argon).Family.SetDefault();
                    SkinComponentFont.For(legacy).Family.SetDefault();
                });
                for (int i = 0; i < 12; i++) await frame();
                using var restored = await Host.TakeScreenshotAsync();
                await restored.SaveAsPngAsync(Path.Combine(output, "restored.png"));
                File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: Argon and legacy glyphs render Inter, Torus and Nunito at readable size; original font can be restored.");
                Completed = true;
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString()); }
            finally { Scheduler.Add(() => Host.Exit()); }
        }

        private Task update(Action action)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Scheduler.Add(() =>
            {
                try { action(); completion.SetResult(); }
                catch (Exception e) { completion.SetException(e); }
            });
            return completion.Task;
        }

        private Task frame()
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Scheduler.AddDelayed(() => completion.SetResult(), 35);
            return completion.Task;
        }

        private partial class FontSample : Container, ISerialisableDrawable
        {
            public bool UsesFixedAnchor { get; set; }
            public FontSample() { AutoSizeAxes = Axes.Both; }
        }
    }
}
