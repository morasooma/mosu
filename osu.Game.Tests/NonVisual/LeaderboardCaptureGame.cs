using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osu.Game.Tests.Visual;
using osuTK;
using SixLabors.ImageSharp;

namespace osu.Game.Tests.NonVisual
{
    public partial class LeaderboardCaptureGame : OsuGameBase
    {
        private readonly string output;
        private readonly List<DrawableGameplayLeaderboardScore> rows = new List<DrawableGameplayLeaderboardScore>();
        private bool started;
        private DrawableGameplayLeaderboard actualLeaderboard = null!;
        private readonly EmptyGameplayLeaderboardProvider leaderboardProvider = new EmptyGameplayLeaderboardProvider();
        private BeatmapAttributeText fixedText = null!;
        private BeatmapDifficultyDisplay difficultyPill = null!;
        private StarRatingDisplay carouselPill = null!;
        public bool Completed { get; private set; }
        protected override bool EnableOfficialBeatmapIntegration => false;
        public LeaderboardCaptureGame(string output)
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
            [FrameworkSetting.VolumeUniversal] = 0.0, [FrameworkSetting.VolumeMusic] = 0.0, [FrameworkSetting.VolumeEffect] = 0.0,
            [FrameworkSetting.AudioDevice] = "No sound", [FrameworkSetting.WindowMode] = WindowMode.Windowed,
            [FrameworkSetting.WindowedSize] = new System.Drawing.Size(800, 600), [FrameworkSetting.FrameSync] = FrameSync.VSync,
        };
        public override void SetHost(GameHost host)
        {
            base.SetHost(host);
            LocalConfig.DisableSaving();
            host.Window.ShowOnFirstDraw = false;
            host.Window.Hide();
        }
        [BackgroundDependencyLoader]
        private void load()
        {
            Add(new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.DarkSlateGray });
            for (int i = 0; i < 4; i++)
            {
                var info = new ScoreInfo { User = new APIUser { Username = i == 0 ? "Top player" : i == 1 ? "Your score" : "Another player" }, TotalScore = 987654 - i * 12345, Accuracy = 0.9876, MaxCombo = 456, PP = i == 3 ? null : 321.45 };
                info.Statistics[HitResult.Miss] = i;
                info.Mods = new osu.Game.Rulesets.Mods.Mod[] { new osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime(), new osu.Game.Rulesets.Osu.Mods.OsuModHidden() };
                var score = new GameplayLeaderboardScore(info, i == 1, GameplayLeaderboardScore.ComboDisplayMode.Highest);
                score.Position.Value = i + 1;
                var row = new DrawableGameplayLeaderboardScore(score) { Expanded = { Value = true } };
                rows.Add(row);
                Add(new Container { Position = new Vector2(50, 90 + i * 90), Width = 460, Height = 70, Child = row });
            }
            var actualScores = (osu.Framework.Bindables.BindableList<GameplayLeaderboardScore>)leaderboardProvider.Scores;
            for (int i = 0; i < 2; i++)
            {
                var score = new GameplayLeaderboardScore(new ScoreInfo { User = new APIUser { Username = "NikoN1nja" }, TotalScore = 657876 - i * 10000, Accuracy = 0.9824, MaxCombo = 1313, PP = 311.79 }, i == 1, GameplayLeaderboardScore.ComboDisplayMode.Highest);
                score.Position.Value = i + 1;
                actualScores.Add(score);
            }
            Add(new DependencyProvidingContainer
            {
                Position = new Vector2(650, 90),
                Size = new Vector2(400, 120),
                CachedDependencies = new (Type, object)[] { (typeof(IGameplayLeaderboardProvider), leaderboardProvider) },
                Child = actualLeaderboard = new DrawableGameplayLeaderboard
                {
                    AlwaysShown = true,
                    CollapseDuringGameplay = { Value = false },
                    MosuLeaderboardStyle = { Value = GameplayLeaderboardStyle.Morasooma },
                    MosuLeaderboardPp = { Value = true },
                    MosuLeaderboardMisses = { Value = true },
                    Size = new Vector2(400, 120),
                },
            });
            Add(difficultyPill = new BeatmapDifficultyDisplay { Position = new Vector2(1050, 320), Scale = new Vector2(2) });
            Add(carouselPill = new StarRatingDisplay(default, StarRatingDisplaySize.Small)
            {
                Position = new Vector2(1050, 370), Scale = new Vector2(2),
            });
            Add(fixedText = new BeatmapAttributeText
            {
                Position = new Vector2(50, 470),
                Attribute = { Value = BeatmapAttribute.Title },
                Template = { Value = "A long beatmap title inside a resizable text area" },
                UseCustomSize = { Value = true },
                Size = new Vector2(260, 48),
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
                for (int i = 0; i < 30; i++) await frame();
                using (var image = await Host.TakeScreenshotAsync()) await image.SaveAsPngAsync(Path.Combine(output, "classic.png"));
                await update(() =>
                {
                    var theme = new CustomThemeDefinition();
                    theme.SetOverride(OverlayColour.Background3, Colour4.FromHex("20202b"));
                    theme.SetOverride(OverlayColour.Background4, Colour4.FromHex("303044"));
                    theme.SetOverride(OverlayColour.Content1, Colour4.FromHex("f0eafa"));
                    theme.SetOverride(OverlayColour.Content2, Colour4.FromHex("b8b2d0"));
                    theme.SetOverride(OverlayColour.Highlight1, Colour4.FromHex("ff8dbd"));
                    theme.SetRadius(GeometryRole.Card, 30);
                    OverlayColourProvider.CustomTheme.Value = theme;
                    OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                    OsuGame.DisableShear.Value = true;
                    foreach (var row in rows)
                    {
                        row.Style.Value = GameplayLeaderboardStyle.Morasooma;
                        row.PanelStyle.Value = GameplayLeaderboardPanelStyle.Solid;
                        row.ShowMisses.Value = row.ShowPp.Value = row.ShowMods.Value = true;
                    }
                });
                for (int i = 0; i < 25; i++) await frame();
                using (var image = await Host.TakeScreenshotAsync()) await image.SaveAsPngAsync(Path.Combine(output, "morasooma-solid.png"));
                await update(() => OsuGame.DisableShear.Value = false);
                for (int i = 0; i < 12; i++) await frame();
                using (var image = await Host.TakeScreenshotAsync()) await image.SaveAsPngAsync(Path.Combine(output, "morasooma-sheared.png"));
                await update(() =>
                {
                    foreach (var row in rows)
                        if ((row.AvatarCentre - row.AvatarSlotCentre).Length > 0.1f)
                            throw new InvalidOperationException("Avatar shifted under shear.");
                    OsuGame.DisableShear.Value = true;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        rows[i].Scale = new Vector2(i == 0 ? 1.25f : i == 1 ? 0.85f : 1);
                        rows[i].Position = new Vector2(0.3f, 0.4f);
                    }
                });
                for (int i = 0; i < 12; i++) await frame();
                using (var image = await Host.TakeScreenshotAsync())
                {
                    await image.SaveAsPngAsync(Path.Combine(output, "morasooma-fractional-scale.png"));
                    // The old mask border broke into dim dots along this 125% corner.
                    // Measure stroke contrast, not merely the presence of a screenshot.
                    for (int y = 91; y <= 103; y++)
                    {
                        int contrast = 0;
                        for (int x = 50; x < 66; x++)
                            contrast = Math.Max(contrast, image[x, y].R - image[x, y].G);
                        if (contrast < 80)
                            throw new InvalidOperationException($"Outline has a broken/dim corner at y={y}, contrast={contrast}.");
                    }
                }
                await update(() =>
                {
                    foreach (var row in rows)
                    {
                        if ((row.AvatarCentre - row.AvatarSlotCentre).Length > 0.1f)
                            throw new InvalidOperationException("Avatar shifted with shear disabled.");
                        row.Scale = Vector2.One;
                        row.Position = Vector2.Zero;
                    }
                    foreach (var row in rows)
                        if (row.Shear != Vector2.Zero || row.Height != 54 || row.PanelBackground.TopLeft.Linear.A != 1 || row.PanelRadius > 10 || row.AvatarSize != 54 || row.ModIconCount != 2 || string.IsNullOrEmpty(row.ExtraInformation))
                            throw new InvalidOperationException("Invalid themed row geometry, opacity or statistics.");
                    foreach (var row in rows) row.PanelStyle.Value = GameplayLeaderboardPanelStyle.Transparent;
                });
                for (int i = 0; i < 10; i++) await frame();
                using (var image = await Host.TakeScreenshotAsync()) await image.SaveAsPngAsync(Path.Combine(output, "morasooma-transparent.png"));
                await update(() =>
                {
                    foreach (var row in rows)
                    {
                        row.Style.Value = GameplayLeaderboardStyle.Classic;
                        row.ShowMisses.Value = row.ShowPp.Value = row.ShowMods.Value = false;
                    }
                });
                for (int i = 0; i < 10; i++) await frame();
                using (var image = await Host.TakeScreenshotAsync()) await image.SaveAsPngAsync(Path.Combine(output, "restored.png"));
                await update(() =>
                {
                    fixedText.Size = new Vector2(180, 48);
                    fixedText.ScrollOverflow.Value = false;
                    if (fixedText.AutoSizeAxes != Axes.None || fixedText.DrawWidth != 180 || !fixedText.Masking)
                        throw new InvalidOperationException("Custom text size is not resizable independently of its text.");
                });
                for (int i = 0; i < 12; i++) await frame();
                using (var image = await Host.TakeScreenshotAsync()) await image.SaveAsPngAsync(Path.Combine(output, "fixed-text-no-scroll.png"));
                await update(() =>
                {
                    foreach (var row in rows)
                    {
                        row.Style.Value = GameplayLeaderboardStyle.Morasooma;
                        row.ShowMisses.Value = row.ShowPp.Value = true;
                        SkinComponentGeometry.Restore(row, 200);
                    }
                    SkinComponentGeometry.Restore(actualLeaderboard, 200);
                });
                for (int i = 0; i < 15; i++) await frame();
                using (var image = await Host.TakeScreenshotAsync())
                {
                    await image.SaveAsPngAsync(Path.Combine(output, "capsule-outline.png"));
                    // Real scroll viewport, not isolated rows: its radius previously
                    // clipped the top-left and bottom-right portions of these strokes.
                    foreach (int y in new[] { 100, 135, 191 })
                    {
                        int left = y == 191 ? 1025 : 670;
                        int right = y == 191 ? 1048 : 700;
                        int contrast = 0;
                        for (int x = left; x < right; x++)
                            contrast = Math.Max(contrast, image[x, y].R - image[x, y].G);
                        if (contrast < 70)
                            throw new InvalidOperationException($"Scroll viewport clipped a row outline at y={y}: {contrast}.");
                    }
                }
                foreach (double stars in new[] { 2.0, 5.5, 8.0 })
                {
                    await update(() => difficultyPill.Current.Value = carouselPill.Current.Value = new StarDifficulty(stars, 0));
                    for (int i = 0; i < 5; i++) await frame();
                    using var image = await Host.TakeScreenshotAsync();
                    await image.SaveAsPngAsync(Path.Combine(output, $"difficulty-pill-{stars.ToString(CultureInfo.InvariantCulture)}.png"));
                    for (int y = 0; y < 40; y++)
                    for (int x = 0; x < 120; x++)
                        if (!image[1050 + x, 320 + y].Equals(image[1050 + x, 370 + y]))
                            throw new InvalidOperationException("Skin difficulty pill differs from carousel rendering.");
                }
                File.WriteAllText(Path.Combine(output, "result.txt"), "PASS: classic/themed rows, straight geometry, opaque/transparent panels, optional statistics, classic restoration and pixel-identical carousel/skin difficulty pills.");
                Completed = true;
            }
            catch (Exception e) { File.WriteAllText(Path.Combine(output, "error.txt"), e.ToString()); }
            finally { Scheduler.Add(() => Host.Exit()); }
        }
        private Task update(Action action)
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Scheduler.Add(() => { try { action(); completion.SetResult(); } catch (Exception e) { completion.SetException(e); } });
            return completion.Task;
        }
        private Task frame()
        {
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Scheduler.AddDelayed(() => completion.SetResult(), 35);
            return completion.Task;
        }
    }
}
