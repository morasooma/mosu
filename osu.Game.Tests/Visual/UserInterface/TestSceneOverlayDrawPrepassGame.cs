using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings.Sections.Fork;
using osu.Game.Screens;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneOverlayDrawPrepassGame : OsuGameTestScene
    {
        private bool prepassRequested => Dependencies.Get<GameHost>().PreferFrontToBackPass;
        [Test]
        public void TestRealOverlaysAndNestedSettings()
        {
            AddStep("opaque overlays enabled", () =>
            {
                Game.LocalConfig.SetValue(OsuSetting.ForkOverlayTransparency, false);
                ((Bindable<OverlayActivation>)Game.OverlayActivationMode).Value = OverlayActivation.All;
                Game.CloseAllOverlays();
            });
            AddUntilStep("settings ready", () => Game.Settings.IsLoaded);
            AddStep("open settings", () => Game.Settings.Show());
            AddUntilStep("settings prepass enabled", () => prepassRequested);
            AddStep("open nested performance settings", () => Game.ChildrenOfType<PerformanceOptimisationSettingsPanel>().Single().Show());
            AddUntilStep("nested panel settled", () => Game.ChildrenOfType<PerformanceOptimisationSettingsPanel>().Single().CanUseOpaqueDrawPrepass);
            AddAssert("nested settings keep prepass", () => prepassRequested);
            AddStep("glass settings", () => Game.LocalConfig.SetValue(OsuSetting.ForkOverlayTransparency, true));
            AddUntilStep("glass disables request", () => !prepassRequested);
            AddStep("opaque again", () => Game.LocalConfig.SetValue(OsuSetting.ForkOverlayTransparency, false));
            AddUntilStep("opaque restores request", () => prepassRequested);
            AddStep("close nested panel", () => Game.ChildrenOfType<PerformanceOptimisationSettingsPanel>().Single().Hide());
            AddUntilStep("main settings settled", () => Game.Settings.CanUseOpaqueDrawPrepass);
            AddStep("switch to notifications", () => Game.Notifications.Show());
            AddUntilStep("notifications prepass enabled", () => Game.Notifications.CanUseOpaqueDrawPrepass && prepassRequested);
            AddStep("close notifications", () => Game.Notifications.Hide());
            AddUntilStep("no overlay request", () => !prepassRequested);
            AddStep("open chat", () => Game.ChildrenOfType<ChatOverlay>().Single().Show());
            AddUntilStep("chat prepass enabled", () => Game.ChildrenOfType<ChatOverlay>().Single().CanUseOpaqueDrawPrepass && prepassRequested);
            AddStep("close chat", () => Game.ChildrenOfType<ChatOverlay>().Single().Hide());
            AddUntilStep("chat releases request", () => !prepassRequested);
            AddUntilStep("news ready", () => Game.ChildrenOfType<NewsOverlay>().Any(o => o.IsLoaded));
            AddStep("open full-size news overlay", () => Game.ChildrenOfType<NewsOverlay>().Single().Show());
            AddUntilStep("news prepass enabled", () => Game.ChildrenOfType<NewsOverlay>().Single().CanUseOpaqueDrawPrepass && prepassRequested);
            AddStep("close news", () => Game.ChildrenOfType<NewsOverlay>().Single().Hide());
            AddUntilStep("news releases request", () => !prepassRequested);
        }

        [Test]
        public void TestRestrictedScreenDoesNotRequestPrepass()
        {
            AddStep("opaque overlays", () => Game.LocalConfig.SetValue(OsuSetting.ForkOverlayTransparency, false));
            PushAndConfirm(() => new RestrictedScreen());
            AddStep("allow test overlay", () => ((Bindable<OverlayActivation>)Game.OverlayActivationMode).Value = OverlayActivation.All);
            AddUntilStep("settings ready", () => Game.Settings.IsLoaded);
            AddStep("open settings", () => Game.Settings.Show());
            AddUntilStep("settings opaque", () => Game.Settings.CanUseOpaqueDrawPrepass);
            AddAssert("restricted screen keeps ordinary path", () => !prepassRequested);
        }

        private partial class RestrictedScreen : OsuScreen
        {
            public override bool DisallowExternalBeatmapRulesetChanges => true;
        }
    }
}
