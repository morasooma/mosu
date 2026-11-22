using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Overlays.Toolbar;
using osu.Game.Screens;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace osu.Game.Overlays.CustomTheme
{
    /// <summary>Production offscreen capture of SoloSongSelect, not a separately drawn approximation.</summary>
    public partial class ThemePreviewGame : OsuGameBase
    {
        private readonly string directory;
#if DEBUG
        public bool CaptureFonts { get; init; }
        public bool CaptureSoundPacks { get; init; }
        public bool CaptureIcons { get; init; }
        private Settings.Sections.Fork.CustomThemeSettingsPanel? fontEditor;
        private bool fontEditorScrolled;
        public bool CaptureInterfaceSounds { get; init; }
        private Settings.Sections.Fork.InterfaceSoundSettingsPanel? soundEditor;
        private bool soundEditorScrolled;
        public bool CaptureGroupDeletion { get; init; }
        public bool CaptureGroupDeletionConfirmation { get; init; }
        private bool deletionOpened;
        private bool keepMarked;
#endif
        private OsuScreenStack stack = null!;
        private Bindable<bool> disableShear = null!;
        private Container capture = null!;
        private OsuScreen songSelect = null!;
        private RealmDetachedBeatmapStore store = null!;
        private readonly LocalUserStatisticsProvider statistics = new LocalUserStatisticsProvider();
        private readonly VolumeOverlay volume = new VolumeOverlay();
        private readonly NotificationOverlay notifications = new NotificationOverlay();
        private readonly DialogOverlay dialogs = new DialogOverlay();
        private bool capturing;
        private double readyAt;
        public Exception? CaptureError { get; private set; }
        public bool CaptureCompleted { get; private set; }
        protected override bool EnableOfficialBeatmapIntegration => false;

        public ThemePreviewGame(string directory)
        {
            this.directory = Path.GetFullPath(directory);
            Storage = new NativeStorage(Path.Combine(this.directory, "profile"));
        }

        protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults() => new Dictionary<FrameworkSetting, object>
        {
            [FrameworkSetting.WindowMode] = WindowMode.Windowed,
            [FrameworkSetting.WindowedSize] = new System.Drawing.Size(1920, 1080),
            [FrameworkSetting.ConfineMouseMode] = ConfineMouseMode.Never,
            [FrameworkSetting.VolumeUniversal] = 0.0,
            [FrameworkSetting.VolumeMusic] = 0.0,
            [FrameworkSetting.VolumeEffect] = 0.0,
            [FrameworkSetting.Locale] =
#if DEBUG
                CaptureGroupDeletion || CaptureInterfaceSounds || CaptureFonts || CaptureSoundPacks ? "ru" :
#endif
                "en",
            [FrameworkSetting.FrameSync] = FrameSync.VSync,
        };

        protected override Storage CreateStorage(GameHost host, Storage defaultStorage) => Storage;

        protected override Container CreateScalingContainer() => new DrawSizePreservingFillContainer
        {
            TargetDrawSize = new osuTK.Vector2(1366, 768),
        };

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);
            LocalConfig.DisableSaving();
            LocalConfig.SetValue(OsuSetting.ForkCustomUIFont, "Default");
            LocalConfig.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern);
            LocalConfig.SetValue(OsuSetting.ForkOverlayTransparency, false);
            LocalConfig.SetValue(OsuSetting.ForkSongSelectTransparency, false);
            LocalConfig.SetValue(OsuSetting.ForkDisableInterfaceShear, false);
            LocalConfig.SetValue(OsuSetting.ForkSongSelectStoryboardBackground, false);
            LocalConfig.SetValue(OsuSetting.ForkSongSelectCarouselPreviews, true);
            LocalConfig.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, false);
            LocalConfig.SetValue(OsuSetting.ForkInterfaceAnimations, InterfaceAnimationMode.Disabled);
            LocalConfig.SetValue(OsuSetting.SongSelectSortingMode, SortMode.Title);
            LocalConfig.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.None);
            ApplyCaptureTheme(LocalConfig, CustomThemeDefinition.FromJson(File.ReadAllText(Path.Combine(directory, "theme.json"))));
            // OsuGame normally supplies this binding, but capture only hosts OsuGameBase.
            // Keep the weakly bound config copy alive for the lifetime of this child process.
            disableShear = LocalConfig.GetBindable<bool>(OsuSetting.ForkDisableInterfaceShear);
            OsuGame.DisableShear.BindTo(disableShear);
            if (host.Window != null)
            {
                host.Window.ShowOnFirstDraw = false;
                host.Window.CursorState |= CursorState.Hidden;
                host.Window.Hide();
            }
        }

        internal static void ApplyCaptureTheme(OsuConfigManager config, CustomThemeDefinition theme)
        {
            config.SetValue(OsuSetting.ForkCustomThemeJson, theme.ToJson());
            config.SetValue(OsuSetting.ForkThemeMode, ThemeMode.Custom);
            theme.Interface?.Apply(config);
        }

        protected override IAPIProvider CreateAPIProvider(EndpointConfiguration endpoints)
        {
            var api = new DummyAPIAccess();
            api.SetState(APIState.Offline);
            return api;
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<BeatmapStore>(store = new RealmDetachedBeatmapStore());
            stack = new OsuScreenStack { RelativeSizeAxes = Axes.Both, Padding = new MarginPadding { Top = Toolbar.Toolbar.HEIGHT } };
            // Match OsuGame: both overlay glass and song-select glass sample this background-only source.
            dependencies.CacheAs<IBackdropBlurSource>(stack.BackgroundBlurSource);
            dependencies.CacheAs<ISongSelectBackdropBlurSource>(stack);
            dependencies.Cache(statistics);
            dependencies.Cache(volume);
            dependencies.CacheAs<INotificationOverlay>(notifications);
            dependencies.CacheAs<IDialogOverlay>(dialogs);
            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Audio.Volume.Value = Audio.VolumeTrack.Value = Audio.VolumeSample.Value = 0;
            Add(store);
            Add(statistics);
            Add(volume);
            Add(notifications);
            Add(dialogs);
        }

        protected override async void LoadComplete()
        {
            base.LoadComplete();
            try
            {
                await Audio.SuspendLiveOutputAsync();
                // Generated silent examples only. No retired intro archives or personal library.
                for (int i = 0; i < 4; i++)
                {
                    using var archive = CreatePreviewBeatmapArchive(i);
                    if (await BeatmapManager.Import(new ImportTask(archive, $"preview-{i}.osz")) == null)
                        throw new InvalidOperationException("Could not prepare a theme preview beatmap.");
                }
                Scheduler.Add(startScene);
            }
            catch (Exception e) { fail(e); }
        }

        internal static Stream CreatePreviewBeatmapArchive(int index)
        {
            if (index < 0 || index >= 4)
                throw new ArgumentOutOfRangeException(nameof(index));

            var stream = new MemoryStream();
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            using (var writer = new StreamWriter(archive.CreateEntry("preview.osu").Open(), new UTF8Encoding(false)))
            {
                writer.Write($"""
                    osu file format v14

                    [General]
                    AudioFilename:
                    PreviewTime: -1
                    Mode: 0

                    [Metadata]
                    Title: Morasooma Preview {index + 1}
                    Artist: Morasooma
                    Creator: Morasooma
                    Version: Preview
                    BeatmapID: 0
                    BeatmapSetID: -1

                    [Difficulty]
                    HPDrainRate: 5
                    CircleSize: 4
                    OverallDifficulty: 6
                    ApproachRate: 7
                    SliderMultiplier: 1.4
                    SliderTickRate: 1

                    [TimingPoints]
                    0,500,4,2,0,100,1,0

                    [HitObjects]
                    128,192,1000,1,0,0:0:0:0:
                    384,192,{90000 + index * 15000},1,0,0:0:0:0:
                    """);
            }
            stream.Position = 0;
            return stream;
        }

        private void startScene()
        {
            try
            {
#if DEBUG
                if (CaptureGroupDeletion)
                {
                    // Only the disposable capture library. Exercise both full and split sets in the real carousel.
                    MainRealm.Write(r =>
                    {
                        var sets = r.All<osu.Game.Beatmaps.BeatmapSetInfo>().ToArray();
                        foreach (var set in sets)
                            foreach (var map in set.Beatmaps)
                                map.Length = 90_000;
                        var split = sets.First(s => s.Metadata.Title == "Morasooma Preview 1");
                        var extra = split.Beatmaps[0].Detach();
                        extra.ID = Guid.NewGuid();
                        extra.Ruleset = split.Beatmaps[0].Ruleset;
                        extra.BeatmapSet = split;
                        extra.DifficultyName = "Capture-only second difficulty";
                        extra.Length = 300_000;
                        split.Beatmaps.Add(extra);
                    });
                    LocalConfig.SetValue(OsuSetting.SongSelectGroupMode, GroupMode.Length);
                }
#endif
                Ruleset.Value = RulesetStore.AvailableRulesets.First(r => r.OnlineID == 0);
                var beatmap = BeatmapManager.GetAllUsableBeatmapSets().First(s => s.Metadata.Title == "Morasooma Preview 4").Beatmaps.First();
                Beatmap.Value = BeatmapManager.GetWorkingBeatmap(beatmap);
                readyAt = Clock.CurrentTime + 8000;
                capture = new PopoverContainer { RelativeSizeAxes = Axes.Both };
                stack.BackgroundViewport = () => capture;
                var background = stack.DetachBackgroundSource();
                background.Depth = float.MaxValue;
                capture.Children = new Drawable[]
                {
                    background,
                    stack,
                    new Toolbar.Toolbar { State = { Value = Visibility.Visible }, HideClockForCapture = true },
                    new ScreenStackFooter(stack),
                };
#if DEBUG
                if (CaptureGroupDeletion)
                {
                    Remove(dialogs, false);
                    capture.Add(dialogs);
                }
#endif
#if DEBUG
                if (CaptureFonts || CaptureSoundPacks)
                {
                    capture.Add(new osu.Framework.Graphics.Shapes.Box { RelativeSizeAxes = Axes.Both, Colour = osuTK.Graphics.Color4.Black, Alpha = 0.5f });
                    capture.Add(fontEditor = new Settings.Sections.Fork.CustomThemeSettingsPanel
                    {
                        Anchor = Anchor.TopLeft, Origin = Anchor.TopLeft, State = { Value = Visibility.Visible },
                    });
                }
                if (CaptureInterfaceSounds)
                {
                    capture.Add(new osu.Framework.Graphics.Shapes.Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = osuTK.Graphics.Color4.Black,
                        Alpha = 0.5f,
                    });
                    capture.Add(soundEditor = new Settings.Sections.Fork.InterfaceSoundSettingsPanel
                    {
                        Anchor = Anchor.TopLeft,
                        Origin = Anchor.TopLeft,
                        State = { Value = Visibility.Visible },
                    });
                }
#endif
#if DEBUG
                if (CaptureIcons)
                {
                    capture.Add(new osu.Framework.Graphics.Shapes.Box { RelativeSizeAxes = Axes.Both, Colour = new Colour4(28, 26, 32, 255) });
                    var rows = new FillFlowContainer
                    {
                        AutoSizeAxes = Axes.Both,
                        Direction = FillDirection.Vertical,
                        Position = new osuTK.Vector2(32, 36),
                        Spacing = new osuTK.Vector2(0, 12),
                    };
                    foreach (bool active in new[] { false, true })
                    foreach (float scale in new[] { 0.8f, 1f, 1.5f })
                    {
                        var line = new FillFlowContainer { AutoSizeAxes = Axes.Both, Direction = FillDirection.Horizontal, Spacing = new osuTK.Vector2(8, 0) };
                        foreach (string name in new[] { "Flashlight", "DoubleTime", "HardRock", "Nightcore", "DifficultyAdjust", "MosuAntiAimAssist", "MosuMalevich" })
                        {
                            var type = Type.GetType($"osu.Game.Rulesets.Osu.Mods.OsuMod{name}, osu.Game.Rulesets.Osu", throwOnError: true)!;
                            var mod = (Rulesets.Mods.Mod)Activator.CreateInstance(type)!;
                            line.Add(new Rulesets.UI.ModSwitchSmall(mod) { Scale = new osuTK.Vector2(scale), Active = { Value = active } });
                        }
                        rows.Add(line);
                    }
                    var extendedModType = Type.GetType("osu.Game.Rulesets.Osu.Mods.OsuModDoubleTime, osu.Game.Rulesets.Osu", throwOnError: true)!;
                    var extendedMod = (Rulesets.Mods.ModRateAdjust)Activator.CreateInstance(extendedModType)!;
                    extendedMod.SpeedChange.Value = 1.25;
                    rows.Add(new Rulesets.UI.ModIcon(extendedMod));
                    using (var store = new osu.Framework.IO.Stores.DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly))
                    {
                        var entries = Newtonsoft.Json.Linq.JArray.Parse(System.Text.Encoding.UTF8.GetString(store.Get("icon-manifest.json")));
                        var ui = new FillFlowContainer { Width = 1100, AutoSizeAxes = Axes.Y, Direction = FillDirection.Full, Spacing = new osuTK.Vector2(6) };
                        foreach (var entry in entries.Where(e => (string?)e["generator"] == "build_ui_icons.py"))
                        {
                            string key = (string)entry["resource"]!;
                            ui.Add(new osu.Framework.Graphics.Sprites.Sprite
                            {
                                Texture = Textures.Get(key["Textures/".Length..^4]),
                                Size = new osuTK.Vector2(24),
                                FillMode = FillMode.Fit,
                            });
                        }
                        rows.Add(ui);
                    }
                    capture.Add(new osu.Game.Tests.Visual.DependencyProvidingContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        CachedDependencies = new (Type, object)[] { (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Green, ThemeSurfaceContext.ModSelect)) },
                        Child = rows,
                    });
                }
#endif
                Add(capture);
                songSelect = LocalConfig.Get<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle).UsesV1Screen()
                    ? new PreviewClassicSongSelect() : new PreviewSongSelect();
                stack.Push(songSelect);
            }
            catch (Exception e) { fail(e); }
        }

        protected override void Update()
        {
            base.Update();
            if (GlobalCursorDisplay != null)
            {
                GlobalCursorDisplay.ShowCursor = false;
                GlobalCursorDisplay.MenuCursor.Alpha = 0;
            }
            if (songSelect?.IsLoaded != true || capturing) return;
            if (songSelect is SoloSongSelect modern && modern.IsFiltering) return;
            if (songSelect is PreviewClassicSongSelect classic && !classic.BeatmapSetsLoaded) return;
#if DEBUG
            if (CaptureFonts || CaptureSoundPacks)
            {
                if (fontEditor?.SectionsReady != true) return;
                if (!fontEditorScrolled)
                {
                    fontEditorScrolled = true;
                    Drawable panel = CaptureSoundPacks
                        ? fontEditor.ChildrenOfType<Settings.Sections.Fork.GameResourceSettings>().First()
                        : fontEditor.ChildrenOfType<Settings.Sections.Fork.FontSettingsPanel>().First();
                    fontEditor.SectionsContainer.ScrollTo(panel);
                    readyAt = Clock.CurrentTime + 2500;
                    return;
                }
            }
            if (CaptureInterfaceSounds)
            {
                if (soundEditor?.SectionsReady != true) return;
                if (!soundEditorScrolled)
                {
                    soundEditorScrolled = true;
                    soundEditor.SectionsContainer.ScrollTo(soundEditor.SectionsContainer.Children.Skip(1).First());
                    readyAt = Clock.CurrentTime + 2500;
                    return;
                }
            }
#endif
            if (Clock.CurrentTime >= readyAt)
            {
#if DEBUG
                if (CaptureGroupDeletion && !deletionOpened && songSelect is PreviewSongSelect preview)
                {
                    preview.OpenDeletionCapture();
                    deletionOpened = true;
                    readyAt = Clock.CurrentTime + 2500;
                    return;
                }
                if (CaptureGroupDeletion && dialogs.CurrentDialog is BeatmapGroupDeletionDialog selection)
                {
                    if (!keepMarked)
                    {
                        var checkbox = selection.ChildrenOfType<osu.Game.Graphics.UserInterface.OsuTabControlCheckbox>().First(c => !c.Current.Disabled);
                        checkbox.Current.Value = true;
                        keepMarked = true;
                        readyAt = Clock.CurrentTime + 1000;
                        return;
                    }
                    if (CaptureGroupDeletionConfirmation)
                    {
                        selection.PerformOkAction();
                        readyAt = Clock.CurrentTime + 2500;
                        return;
                    }
                }
#endif
                capturing = true;
                _ = save();
            }
        }

        private async Task save()
        {
            try
            {
                using var image = await Host.TakeDrawableScreenshotAsync(capture);
#if DEBUG
                if (CaptureIcons)
                {
                    await image.SaveAsPngAsync(Path.Combine(directory, "icons.png"));
                    CaptureCompleted = true;
                    Scheduler.Add(() => Host.Exit());
                    return;
                }
                if (CaptureFonts || CaptureSoundPacks)
                {
                    await image.SaveAsPngAsync(Path.Combine(directory, CaptureSoundPacks ? "sound-packs.png" : "fonts.png"));
                    CaptureCompleted = true;
                    Scheduler.Add(() => Host.Exit());
                    return;
                }
                if (CaptureInterfaceSounds)
                {
                    await image.SaveAsPngAsync(Path.Combine(directory, "interface-sounds.png"));
                    CaptureCompleted = true;
                    Scheduler.Add(() => Host.Exit());
                    return;
                }
                if (CaptureGroupDeletion)
                {
                    await image.SaveAsPngAsync(Path.Combine(directory, CaptureGroupDeletionConfirmation ? "group-deletion-confirmation.png" : "group-deletion.png"));
                    CaptureCompleted = true;
                    Scheduler.Add(() => Host.Exit());
                    return;
                }
#endif
                image.Mutate(c => c.Resize(ThemePreviewRenderer.WIDTH, ThemePreviewRenderer.HEIGHT));
                using var output = new MemoryStream();
                image.SaveAsPng(output, new PngEncoder { ColorType = PngColorType.Rgb, CompressionLevel = PngCompressionLevel.BestCompression });
                byte[] png = output.ToArray();
                ThemePreviewRenderer.Validate(png);
                await File.WriteAllBytesAsync(Path.Combine(directory, "preview.png"), png);
                CaptureCompleted = true;
                Scheduler.Add(() => Host.Exit());
            }
            catch (Exception e) { fail(e); }
        }

        private void fail(Exception exception)
        {
            CaptureError = exception;
            osu.Framework.Logging.Logger.Error(exception, "Theme preview capture failed");
            Scheduler.Add(() => Host.Exit());
        }

        private partial class FixedBackground : osu.Game.Screens.Backgrounds.BackgroundScreenBeatmap
        {
            protected override osu.Game.Graphics.Backgrounds.BeatmapBackground CreateBackgroundDrawable(osu.Game.Beatmaps.WorkingBeatmap beatmap)
                => new FixedBeatmapBackground(beatmap);
        }

        private partial class FixedBeatmapBackground : osu.Game.Graphics.Backgrounds.BeatmapBackground
        {
            public FixedBeatmapBackground(osu.Game.Beatmaps.WorkingBeatmap beatmap) : base(beatmap) { }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                Sprite.Texture = Dependencies.Get<osu.Framework.Graphics.Textures.LargeTextureStore>().Get("Menu/menu-background-1");
            }
        }

        private partial class PreviewClassicSongSelect : osu.Game.Screens.SelectLegacy.PlaySongSelect
        {
            protected override bool ControlGlobalMusic => false;
            protected override BackgroundScreen CreateBackground() => new FixedBackground();
        }

        private partial class PreviewSongSelect : SoloSongSelect
        {
            public PreviewSongSelect() { ControlGlobalMusic = false; }

            protected override BackgroundScreen CreateBackground() => new FixedBackground();
#if DEBUG
            public void OpenDeletionCapture()
            {
                var panel = this.ChildrenOfType<PanelGroup>().First(p => p.Item?.Model is GroupDefinition group && Carousel.GetBeatmapsInGroup(group).Count >= 3);
                panel.TriggerClick();
                panel.ContextMenuItems.Single(i => i.Text.Value == osu.Game.Localisation.BeatmapGroupDeletionStrings.Open).Action.Value?.Invoke();
            }
#endif
        }
    }
}
