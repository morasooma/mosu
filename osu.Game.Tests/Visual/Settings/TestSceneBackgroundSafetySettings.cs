using System;
using System.Linq;
using System.Reflection;
using System.Globalization;
using System.Resources;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Graphics.Rendering.Dummy;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds.AI;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Fork;
using osuTK;

namespace osu.Game.Tests.Visual.Settings
{
    public partial class TestSceneBackgroundSafetySettings : OsuTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;
        private SettingsSection panel = null!;
        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        [TestCase("ru", 480, 1f)]
        [TestCase("ru", 480, 1.25f)]
        [TestCase("en", 360, 1f)]
        [TestCase("es", 480, 1f)]
        [TestCase("ko", 480, 1f)]
        [TestCase("zh", 480, 1f)]
        public void TestLongStatusWrapsInsideRealSubsection(string language, float width, float scale)
        {
            OsuTextFlowContainer status = null!;
            Container viewport = null!;
            OsuSpriteText nextSection = null!;
            OsuScrollContainer scroll = null!;
            FontStore fonts = null!;
            var resources = new ResourceManager("osu.Game.Localisation.AiFunctions", typeof(AiFunctionsStrings).Assembly);
            string previousFont = null!;
            AddStep("load actual font metrics", () =>
            {
                previousFont = config.Get<string>(OsuSetting.ForkCustomUIFont);
                config.SetValue(OsuSetting.ForkCustomUIFont, "Default");
                fonts = new FontStore(new DummyRenderer(), minFilterMode: TextureFilteringMode.Linear);
                var fontResources = new NamespacedResourceStore<byte[]>(new DllResourceStore(Assembly.Load("osu.Game.Resources")), "Fonts");
                foreach (string font in new[] { "Torus/Torus-Regular", "Noto/Noto-Basic", "Noto/Noto-Hangul", "Noto/Noto-CJK-Basic" })
                    fonts.AddTextureSource(new GlyphStore(fontResources, font));
            });
            AddStep("create narrow settings with following section", () => Child = viewport = new Container
            {
                Width = width,
                Height = 680,
                Child = scroll = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Scale = new Vector2(scale),
                        Width = 1 / scale,
                        Children = new Drawable[]
                        {
                            new DependencyProvidingContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                CachedDependencies = new (Type, object)[]
                                {
                                    (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Pink)),
                                    (typeof(FontStore), fonts),
                                },
                                Child = panel = new AiFunctionSettingsPanel.DistortionSection(),
                            },
                            nextSection = new OsuSpriteText { Text = "Next section", Font = osu.Game.Graphics.OsuFont.GetFont(size: 20) },
                        },
                    },
                },
            });
            AddUntilStep("subsection loaded", () => panel.IsLoaded);
            AddStep("show actual localised failure", () =>
            {
                status = panel.ChildrenOfType<OsuTextFlowContainer>().Single(c => c.Name == "background-distortion-status");
                status.Text = resources.GetString("StillLifeFailed", CultureInfo.GetCultureInfo(language))!;
                scroll.ScrollToEnd(false);
            });
            AddWaitStep("layout failure", 3);
            AddAssert("failure wraps within panel and next section follows", () => fits());
            AddStep("show localised download cause", () =>
            {
                status.Text = string.Format(resources.GetString("StillLifeDownloadFailed", CultureInfo.GetCultureInfo(language))!, "github.com");
            });
            AddWaitStep("layout download cause", 3);
            AddAssert("download cause wraps within panel and next section follows", () => fits());

            AddStep("release font cache", () =>
            {
                Clear();
                fonts.Dispose();
                resources.ReleaseAllResources();
                config.SetValue(OsuSetting.ForkCustomUIFont, previousFont);
            });

            bool fits()
            {
                var bounds = status.ScreenSpaceDrawQuad.AABBFloat;
                return status.DrawHeight > 0
                       && (language is not ("ru" or "en") || status.DrawHeight > 20)
                       && bounds.Right <= viewport.ScreenSpaceDrawQuad.AABBFloat.Right + 1
                       && status.ChildrenOfType<OsuSpriteText>().All(word =>
                       {
                           var quad = word.ScreenSpaceDrawQuad.AABBFloat;
                           return quad.Left >= bounds.Left - 1 && quad.Right <= bounds.Right + 1 && quad.Bottom <= bounds.Bottom + 1;
                       })
                       && nextSection.ScreenSpaceDrawQuad.AABBFloat.Top >= bounds.Bottom - 1;
            }
        }

        [TestCase("ru", 400, 1f)]
        [TestCase("ru", 320, 1.25f)]
        [TestCase("en", 400, 1f)]
        [TestCase("es", 400, 1f)]
        [TestCase("ko", 400, 1f)]
        [TestCase("zh", 400, 1f)]
        public void TestCompactRowsOpenSamePanelAtRequestedFunction(string language, float width, float scale)
        {
            AiFunctionSettingsPanel shared = null!;
            ForkAiSettings compact = null!;
            FontStore fonts = null!;
            string previousFont = null!;
            string previousLocale = null!;
            AddStep("load row font metrics", () =>
            {
                previousFont = config.Get<string>(OsuSetting.ForkCustomUIFont);
                config.SetValue(OsuSetting.ForkCustomUIFont, "Default");
                fonts = new FontStore(new DummyRenderer(), minFilterMode: TextureFilteringMode.Linear);
                var source = new NamespacedResourceStore<byte[]>(new DllResourceStore(Assembly.Load("osu.Game.Resources")), "Fonts");
                fonts.AddTextureSource(new GlyphStore(source, "Torus/Torus-Regular"));
                fonts.AddTextureSource(new GlyphStore(source, "Noto/Noto-Basic"));
                fonts.AddTextureSource(new GlyphStore(source, "Noto/Noto-Hangul"));
                fonts.AddTextureSource(new GlyphStore(source, "Noto/Noto-CJK-Basic"));
                previousLocale = frameworkConfig.Get<string>(FrameworkSetting.Locale);
                frameworkConfig.SetValue(FrameworkSetting.Locale, language);
            });
            AddStep("create shared panel and compact entry points", () => Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Pink)),
                    (typeof(FontStore), fonts),
                },
                Children = new Drawable[]
                {
                    shared = new AiFunctionSettingsPanel(),
                    new Container
                    {
                        Width = width,
                        Scale = new Vector2(scale),
                        AutoSizeAxes = Axes.Y,
                        Child = compact = new ForkAiSettings(shared),
                    },
                },
            });
            AddUntilStep("entries loaded", () => compact.IsLoaded && shared.IsLoaded);
            AddAssert("three feature switches and configure buttons", () => compact.ChildrenOfType<FormCheckBox>().Count() == 3
                && compact.ChildrenOfType<SettingsButtonV2>().Count() == 3
                && !compact.ChildrenOfType<FormSliderBar<double>>().Any()
                && !compact.ChildrenOfType<FormEnumDropdown<StillLifeBackend>>().Any());
            AddAssert("switches and buttons fit their rows without overlap", () =>
            {
                var checks = compact.ChildrenOfType<FormCheckBox>().ToArray();
                var buttons = compact.ChildrenOfType<SettingsButtonV2>().ToArray();
                return checks.Zip(buttons, (check, button) => check.ScreenSpaceDrawQuad.AABBFloat.Right < button.ScreenSpaceDrawQuad.AABBFloat.Left
                    && button.ScreenSpaceDrawQuad.AABBFloat.Right <= compact.ScreenSpaceDrawQuad.AABBFloat.Right + 1
                    && check.DrawHeight >= 52
                    && check.ChildrenOfType<OsuSpriteText>().All(word => word.ScreenSpaceDrawQuad.AABBFloat.Right <= check.ScreenSpaceDrawQuad.AABBFloat.Right + 1
                        && word.ScreenSpaceDrawQuad.AABBFloat.Bottom <= check.ScreenSpaceDrawQuad.AABBFloat.Bottom + 1)
                    && button.ChildrenOfType<OsuSpriteText>().All(word => word.ScreenSpaceDrawQuad.AABBFloat.Right <= button.ScreenSpaceDrawQuad.AABBFloat.Right + 1
                        && word.ScreenSpaceDrawQuad.AABBFloat.Bottom <= button.ScreenSpaceDrawQuad.AABBFloat.Bottom + 1)).All(v => v);
            });
            AddStep("toggle both entry switches", () =>
            {
                var switches = compact.ChildrenOfType<FormCheckBox>().ToArray();
                switches[0].Current.Value = true;
                switches[1].Current.Value = false;
            });
            AddAssert("entry switches use existing configuration", () => config.Get<bool>(OsuSetting.ForkAiBackgroundBlur)
                && !config.Get<bool>(OsuSetting.ForkAiStillLife));
            AddStep("configure distortion while panel is cold", () => compact.ChildrenOfType<SettingsButtonV2>().ElementAt(1).Action.Invoke());
            AddUntilStep("same panel loads and selects distortion", () => shared.SectionsContainer.Count() == 3
                && shared.SectionsContainer.SelectedSection.Value is AiFunctionSettingsPanel.DistortionSection);
            AddUntilStep("distortion controls loaded", () => shared.SectionsContainer.ChildrenOfType<FormEnumDropdown<StillLifeBackend>>().Any());
            AddStep("edit distortion backend", () => shared.SectionsContainer.ChildrenOfType<FormEnumDropdown<StillLifeBackend>>().Single().Current.Value = StillLifeBackend.Gpu);
            AddAssert("backend written", () => config.Get<StillLifeBackend>(OsuSetting.ForkAiStillLifeBackend) == StillLifeBackend.Gpu);
            AddStep("configure NSFW in same already visible panel", () => compact.ChildrenOfType<SettingsButtonV2>().First().Action.Invoke());
            AddUntilStep("NSFW section selected", () => shared.State.Value == Visibility.Visible
                && shared.SectionsContainer.SelectedSection.Value is AiFunctionSettingsPanel.NsfwSection);
            AddUntilStep("NSFW section scrolled into view", () =>
            {
                var section = shared.SectionsContainer.Single(s => s is AiFunctionSettingsPanel.NsfwSection);
                return section.ScreenSpaceDrawQuad.AABBFloat.Top >= 0 && section.ScreenSpaceDrawQuad.AABBFloat.Top < shared.DrawHeight / 2;
            });
            AddAssert("only one shared panel exists", () => this.ChildrenOfType<AiFunctionSettingsPanel>().Count() == 1);
            AddStep("hide shared panel", () => shared.Hide());
            AddStep("configure audio in same panel", () => compact.ChildrenOfType<SettingsButtonV2>().Last().Action.Invoke());
            AddUntilStep("audio section selected", () => shared.SectionsContainer.SelectedSection.Value is AiFunctionSettingsPanel.AudioSection);
            AddUntilStep("audio controls use existing shared panel", () => shared.SectionsContainer.ChildrenOfType<FormEnumDropdown<osu.Game.Overlays.Discovery.DiscoveryAudioDevice>>().Any());
            AddStep("select CPU for audio", () => shared.SectionsContainer.ChildrenOfType<FormEnumDropdown<osu.Game.Overlays.Discovery.DiscoveryAudioDevice>>()
                .Single().Current.Value = osu.Game.Overlays.Discovery.DiscoveryAudioDevice.Cpu);
            AddAssert("audio processing choice persisted", () => config.Get<osu.Game.Overlays.Discovery.DiscoveryAudioDevice>(OsuSetting.ForkAiAudioDevice)
                == osu.Game.Overlays.Discovery.DiscoveryAudioDevice.Cpu);
            AddStep("disable only background audio recommendations", () =>
            {
                var audio = shared.SectionsContainer.ChildrenOfType<AiFunctionSettingsPanel.AudioSection>().Single();
                audio.ChildrenOfType<FormCheckBox>().Single(c => c.Caption == DiscoveryStrings.AudioTitle).Current.Value = true;
                audio.ChildrenOfType<FormCheckBox>().Single(c => c.Caption == DiscoveryStrings.AudioBackgroundSearch).Current.Value = false;
            });
            AddAssert("background switch is independent of the main CLAP switch", () => config.Get<bool>(OsuSetting.ForkAiAudioRecommendations)
                && !config.Get<bool>(OsuSetting.ForkAiAudioBackgroundSearch));
            AddStep("hide audio panel", () => shared.Hide());
            AddStep("reopen at distortion", () => compact.ChildrenOfType<SettingsButtonV2>().ElementAt(1).Action.Invoke());
            AddUntilStep("requested section selected after reopening", () => shared.SectionsContainer.SelectedSection.Value is AiFunctionSettingsPanel.DistortionSection);
            AddAssert("previous values preserved", () => config.Get<StillLifeBackend>(OsuSetting.ForkAiStillLifeBackend) == StillLifeBackend.Gpu);
            AddStep("release row font metrics", () =>
            {
                Clear();
                fonts.Dispose();
                config.SetValue(OsuSetting.ForkCustomUIFont, previousFont);
                frameworkConfig.SetValue(FrameworkSetting.Locale, previousLocale);
            });
        }

        [Test]
        public void TestControlsGeometryBindingsAndReset()
        {
            FillFlowContainer details = null!;
            AddStep("create real settings subsection", () => Child = new DependencyProvidingContainer
            {
                Size = new Vector2(600, 680),
                CachedDependencies = new (Type, object)[] { (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Pink)) },
                Child = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = details = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[] { new AiFunctionSettingsPanel.NsfwSection(), new AiFunctionSettingsPanel.DistortionSection() },
                    },
                },
            });
            AddUntilStep("all controls loaded", () => details.IsLoaded && details.ChildrenOfType<FormSliderBar<double>>().Count() == 4);
            AddWaitStep("allow layout", 3);
            AddAssert("five checkboxes and four dropdowns", () => details.ChildrenOfType<FormCheckBox>().Count() == 5
                && details.ChildrenOfType<FormEnumDropdown<BackgroundFilterMode>>().Count() == 1
                && details.ChildrenOfType<FormEnumDropdown<BackgroundSafetyCombination>>().Count() == 1
                && details.ChildrenOfType<FormEnumDropdown<StillLifeBackend>>().Count() == 1
                && details.ChildrenOfType<FormEnumDropdown<StillLifeResolution>>().Count() == 1);
            AddAssert("real controls have usable non-overlapping geometry", () =>
            {
                var controls = details.ChildrenOfType<SettingsItemV2>().ToArray();
                return controls.All(c => c.DrawWidth > 300 && c.DrawHeight > 20)
                       && controls.Zip(controls.Skip(1), (a, b) => a.ScreenSpaceDrawQuad.AABBFloat.Bottom <= b.ScreenSpaceDrawQuad.AABBFloat.Top + 1).All(v => v);
            });
            AddStep("edit controls", () =>
            {
                details.ChildrenOfType<FormSliderBar<double>>().First().Current.Value = .12;
                details.ChildrenOfType<FormEnumDropdown<BackgroundSafetyCombination>>().Single().Current.Value = BackgroundSafetyCombination.All;
                details.ChildrenOfType<FormEnumDropdown<BackgroundFilterMode>>().Single().Current.Value = BackgroundFilterMode.Hentai;
            });
            AddAssert("controls write real config", () => config.Get<double>(OsuSetting.ForkAiGeneralThreshold) == .12
                && config.Get<BackgroundSafetyCombination>(OsuSetting.ForkAiCombination) == BackgroundSafetyCombination.All
                && config.Get<BackgroundFilterMode>(OsuSetting.ForkAiBackgroundFilterMode) == BackgroundFilterMode.Hentai);
            AddStep("reset via real button", () => details.ChildrenOfType<SettingsButtonV2>().First().Action.Invoke());
            AddAssert("strict defaults restored", () => config.Get<double>(OsuSetting.ForkAiGeneralThreshold) == .3
                && config.Get<double>(OsuSetting.ForkAiAnimeThreshold) == .3
                && config.Get<BackgroundSafetyCombination>(OsuSetting.ForkAiCombination) == BackgroundSafetyCombination.Any
                && config.Get<BackgroundFilterMode>(OsuSetting.ForkAiBackgroundFilterMode) == BackgroundFilterMode.Suggestive
                && config.Get<bool>(OsuSetting.ForkAiGeneralModel) && config.Get<bool>(OsuSetting.ForkAiAnimeModel));
        }
    }
}
