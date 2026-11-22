using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Fork;
using osu.Game.Overlays.Settings.Sections.Graphics;
using osuTK;

namespace osu.Game.Tests.Visual.Settings
{
    public partial class TestSceneGraphicsAdapterSettings : OsuTestScene
    {
        private DependencyProvidingContainer panel = null!;
        private TemporaryNativeStorage storage = null!;
        private FrameworkConfigManager config = null!;
        private AdapterTestHost adapterHost = null!;
        private readonly OsuGame game = new OsuGame();

        [Test]
        public void TestBothLocationsShareChoiceWithoutRestarting()
        {
            AddStep("create real graphics and performance subsections", () => create(true));
            AddUntilStep("both GPU controls loaded", () => dropdowns().Count() == 2 && dropdowns().All(d => d.IsLoaded));
            AddWaitStep("layout settings", 3);
            AddAssert("GPU controls have usable geometry", () => dropdowns().All(d => d.DrawWidth > 300 && d.DrawHeight > 20));
            AddAssert("GPU dropdown and apply button do not overlap", () => panel.ChildrenOfType<GraphicsAdapterSettings>().All(p =>
            {
                var dropdown = p.ChildrenOfType<SettingsItemV2>().Single();
                var button = p.ChildrenOfType<SettingsButtonV2>().Single();
                return dropdown.ScreenSpaceDrawQuad.AABBFloat.Bottom <= button.ScreenSpaceDrawQuad.AABBFloat.Top + 1;
            }));
            AddStep("choose GPU from graphics", () => dropdowns().First().Current.Value = "vulkan:gpu-two");
            AddAssert("performance and framework config synchronised", () => dropdowns().Last().Current.Value == "vulkan:gpu-two"
                && config.Get<string>(FrameworkSetting.GraphicsAdapter) == "vulkan:gpu-two");
            AddAssert("both apply buttons enabled", () => panel.ChildrenOfType<GraphicsAdapterSettings>()
                .All(p => p.ChildrenOfType<SettingsButtonV2>().Single().Enabled.Value));
            AddStep("choose automatic from performance", () => dropdowns().Last().Current.SetDefault());
            AddAssert("graphics and framework reset too", () => dropdowns().First().Current.Value == string.Empty
                && config.Get<string>(FrameworkSetting.GraphicsAdapter) == string.Empty);
            AddAssert("both apply buttons disabled again", () => panel.ChildrenOfType<GraphicsAdapterSettings>()
                .All(p => !p.ChildrenOfType<SettingsButtonV2>().Single().Enabled.Value));
            AddAssert("current GPU still reports running device", () => adapterHost.GraphicsAdapterName == "GPU one");
        }

        [Test]
        public void TestReopeningSettingsRetainsPendingApply()
        {
            AddStep("create supported backend", () => create(true));
            AddUntilStep("GPU controls loaded", () => dropdowns().Count() == 2 && dropdowns().All(d => d.IsLoaded));
            AddStep("change selection and reopen control", () =>
            {
                dropdowns().First().Current.Value = "vulkan:gpu-two";
                var parent = (FillFlowContainer)panel.ChildrenOfType<RendererSettings>().Single().Parent!;
                parent.Add(new GraphicsAdapterSettings());
            });
            AddUntilStep("reopened control loaded", () => dropdowns().Count() == 3 && dropdowns().All(d => d.IsLoaded));
            AddAssert("reopened control still needs apply", () => panel.ChildrenOfType<GraphicsAdapterSettings>()
                .All(p => p.ChildrenOfType<SettingsButtonV2>().Single().Enabled.Value));
        }

        [Test]
        public void TestUnsupportedRendererDoesNotOverwriteSavedPreference()
        {
            AddStep("create unsupported backend", () => create(false));
            AddUntilStep("both GPU controls loaded", () => dropdowns().Count() == 2 && dropdowns().All(d => d.IsLoaded));
            AddAssert("controls are disabled", () => dropdowns().All(d => d.Current.Disabled));
            AddAssert("saved preference is retained", () => config.Get<string>(FrameworkSetting.GraphicsAdapter) == "vulkan:gpu-two");
        }

        private IEnumerable<FormDropdown<string>> dropdowns() => panel.ChildrenOfType<GraphicsAdapterSettings>()
            .SelectMany(p => p.ChildrenOfType<FormDropdown<string>>());

        private void create(bool supported)
        {
            Clear();
            config?.Dispose();
            storage?.Dispose();
            adapterHost?.Dispose();
            storage = new TemporaryNativeStorage("gpu-ui-" + Guid.NewGuid());
            config = new FrameworkConfigManager(storage);
            if (!supported)
                config.SetValue(FrameworkSetting.GraphicsAdapter, "vulkan:gpu-two");
            adapterHost = new AdapterTestHost(supported);
            Child = panel = new DependencyProvidingContainer
            {
                Size = new Vector2(640, 680),
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(GameHost), adapterHost),
                    (typeof(FrameworkConfigManager), config),
                    (typeof(OsuGame), game),
                    (typeof(OverlayColourProvider), new OverlayColourProvider(OverlayColourScheme.Pink)),
                },
                Child = new OsuScrollContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new RendererSettings(),
                            new ForkPerformanceSettings(new PerformanceOptimisationSettingsPanel()),
                        },
                    },
                },
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            config?.Dispose();
            storage?.Dispose();
            adapterHost?.Dispose();
            game.Dispose();
        }

        private class AdapterTestHost : HeadlessGameHost
        {
            public AdapterTestHost(bool supported)
            {
                SupportsGraphicsAdapterSelection = supported;
                GraphicsAdapterName = "GPU one";
                AvailableGraphicsAdapters = new[] { new GraphicsAdapterInfo("vulkan:gpu-one", "GPU one"), new GraphicsAdapterInfo("vulkan:gpu-two", "GPU two") };
            }
        }
    }
}
