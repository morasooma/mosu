using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneOverlayDrawPrepass : OsuTestScene
    {
        private TestOverlay overlay = null!;
        private bool previousTransparency;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create hidden overlay", () =>
            {
                previousTransparency = OverlayTransparency.Enabled.Value;
                OverlayTransparency.Enabled.Value = false;
                Child = overlay = new TestOverlay();
            });
            AddUntilStep("overlay loaded", () => overlay.IsLoaded);
            AddAssert("hidden overlay does not request prepass", () => !overlay.CanUseOpaqueDrawPrepass);
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("restore appearance", () => OverlayTransparency.Enabled.Value = previousTransparency);

        [Test]
        public void TestOpenCloseAndReopen()
        {
            AddStep("open", () => overlay.Show());
            AddAssert("opening uses ordinary path", () => !overlay.CanUseOpaqueDrawPrepass);
            AddUntilStep("settled opaque overlay requests prepass", () => overlay.CanUseOpaqueDrawPrepass);
            AddStep("close", () => overlay.Hide());
            AddAssert("closing immediately releases prepass", () => !overlay.CanUseOpaqueDrawPrepass);
            AddUntilStep("fully hidden", () => overlay.Alpha == 0);
            AddAssert("hidden has no prepass", () => !overlay.CanUseOpaqueDrawPrepass);
            AddStep("reopen", () => overlay.Show());
            AddUntilStep("reopened requests prepass", () => overlay.CanUseOpaqueDrawPrepass);
        }

        [Test]
        public void TestTransparencyAndThemeAlpha()
        {
            AddStep("open", () => overlay.Show());
            AddUntilStep("opaque active", () => overlay.CanUseOpaqueDrawPrepass);
            AddStep("enable glass", () => OverlayTransparency.Enabled.Value = true);
            AddAssert("glass uses ordinary path", () => !overlay.CanUseOpaqueDrawPrepass);
            AddStep("disable glass", () => OverlayTransparency.Enabled.Value = false);
            AddUntilStep("opaque active again", () => overlay.CanUseOpaqueDrawPrepass);
            AddStep("translucent theme background", () => overlay.Background.SurfaceColour = new Color4(0f, 0f, 0f, 0.8f));
            AddAssert("theme alpha respected", () => !overlay.CanUseOpaqueDrawPrepass);
            AddStep("restore opaque background", () => overlay.Background.SurfaceColour = Color4.Black);
            AddUntilStep("opaque restored", () => overlay.CanUseOpaqueDrawPrepass);
        }

        [Test]
        public void TestContentTransitionAndInheritedAlpha()
        {
            AddStep("open", () => overlay.Show());
            AddUntilStep("settled", () => overlay.CanUseOpaqueDrawPrepass);
            AddStep("move inner content", () => overlay.Panel.MoveToX(100, 300));
            AddAssert("content transition uses ordinary path", () => !overlay.CanUseOpaqueDrawPrepass);
            AddUntilStep("content settled", () => overlay.CanUseOpaqueDrawPrepass);
            AddStep("fade inner content", () => overlay.Panel.Alpha = 0.5f);
            AddAssert("inherited alpha respected", () => !overlay.CanUseOpaqueDrawPrepass);
            AddStep("restore content alpha", () => overlay.Panel.Alpha = 1);
            AddUntilStep("opaque again", () => overlay.CanUseOpaqueDrawPrepass);
            AddStep("hide inner content", () => overlay.Panel.Hide());
            AddAssert("hidden content is not opaque", () => !overlay.CanUseOpaqueDrawPrepass);
        }

        [Test]
        public void TestOverlayWithoutOpaqueBackground()
        {
            AddStep("no opaque background", () => overlay.UseBackground = false);
            AddStep("open", () => overlay.Show());
            AddUntilStep("opening finished", () => overlay.Alpha == 1 && overlay.Panel.X == 0);
            AddAssert("local alpha alone does not opt in", () => !overlay.CanUseOpaqueDrawPrepass);
        }

        private partial class TestOverlay : OsuFocusedOverlayContainer
        {
            public readonly Container Panel;
            public readonly BackdropBlurSurface Background;
            public bool UseBackground = true;

            protected override BackdropBlurSurface? OpaqueBackground => UseBackground ? Background : null;
            protected override string? PopInSampleName => null;
            protected override string? PopOutSampleName => null;

            public TestOverlay()
            {
                RelativeSizeAxes = Axes.Both;
                Child = Panel = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = Background = new BackdropBlurSurface
                    {
                        SurfaceColour = Color4.Black,
                    },
                };
            }

            protected override void PopIn()
            {
                this.FadeIn(300);
                Panel.MoveToX(0, 300);
            }

            protected override void PopOut()
            {
                base.PopOut();
                this.FadeOut(300);
                Panel.MoveToX(-100, 300);
            }
        }
    }
}
