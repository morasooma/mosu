using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneSongSelectBackdropSurface : OsuTestScene, ISongSelectBackdropBlurSource
    {
        [Cached(typeof(ISongSelectBackdropBlurSource))]
        private readonly ISongSelectBackdropBlurSource source;

        private readonly BackdropBlurContainer backdrop = new BackdropBlurContainer(backgroundOnly: true)
        {
            SceneContent = new Box { RelativeSizeAxes = Axes.Both, Colour = Colour4.Blue },
        };

        IBackdropBlurSource ISongSelectBackdropBlurSource.Source => backdrop;

        [Cached(typeof(IBackdropBlurSource))]
        private readonly BackdropBlurContainer overlayBackdrop = new BackdropBlurContainer();

        private readonly Container overlayParent;
        private readonly Container surfaceParent;
        private bool previousEnabled;
        private bool previousSongSelect;

        public TestSceneSongSelectBackdropSurface()
        {
            source = this;
            Add(overlayBackdrop);
            overlayBackdrop.SceneContent = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    backdrop,
                    surfaceParent = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new SongSelectBackdropSurface(),
                    },
                },
            };
            Add(overlayParent = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Child = new BackdropBlurSurface(),
            });
        }

        [SetUp]
        public void SetUp()
        {
            previousEnabled = OverlayTransparency.Enabled.Value;
            previousSongSelect = OverlayTransparency.SongSelectEnabled.Value;
        }

        [TearDown]
        public void TearDown()
        {
            OverlayTransparency.Enabled.Value = previousEnabled;
            OverlayTransparency.SongSelectEnabled.Value = previousSongSelect;
        }

        [Test]
        public void TestStableAppearanceSkipsLayerWritesButUpdatesGeometry()
        {
            SongSelectBackdropSurface surface = null!;
            int updates = 0;
            float radius = 8;
            AddStep("create surface", () =>
            {
                surfaceParent.Clear();
                surfaceParent.Alpha = 1;
                surfaceParent.Add(surface = new SongSelectBackdropSurface { GetCornerRadius = () => radius });
                OverlayTransparency.SongSelectEnabled.Value = true;
            });
            AddUntilStep("appearance ready", () => surface.AppearanceUpdateCount > 0);
            AddWaitStep("settle", 3);
            AddStep("record writes", () => updates = surface.AppearanceUpdateCount);
            AddWaitStep("stable frames", 10);
            AddAssert("no redundant appearance writes", () => surface.AppearanceUpdateCount == updates);
            AddStep("change geometry", () => radius = 16);
            AddUntilStep("geometry still updates", () => surface.CornerRadius == 16);
            AddAssert("geometry does not rewrite colours", () => surface.AppearanceUpdateCount == updates);
        }

        [Test]
        public void TestSurfaceKeepsOwnRoundedMask()
        {
            SongSelectBackdropSurface surface = null!;
            float radius = 12;
            AddStep("create independently masked surface", () =>
            {
                surfaceParent.Clear();
                surfaceParent.Masking = false;
                surfaceParent.Add(surface = new SongSelectBackdropSurface { GetCornerRadius = () => radius });
            });
            AddUntilStep("rounded mask applied", () => surface.Masking && surface.CornerRadius == 12);
            AddStep("change card geometry", () => radius = 20);
            AddUntilStep("mask follows card", () => surface.CornerRadius == 20);
        }

        [Test]
        public void TestOpeningOverlayKeepsCarouselConsumerActive()
        {
            AddStep("enable carousel glass", () =>
            {
                surfaceParent.Clear();
                surfaceParent.Add(new SongSelectBackdropSurface());
                OverlayTransparency.Enabled.Value = true;
                OverlayTransparency.SongSelectEnabled.Value = true;
                surfaceParent.Alpha = 1;
                overlayParent.Alpha = 0;
            });
            AddUntilStep("carousel buffering active", () => backdrop.BufferingActive);
            AddAssert("sharp image keeps native resolution", () => backdrop.OriginalFrameBufferScale == osuTK.Vector2.One);
            AddAssert("effect alone uses reduced resolution", () => backdrop.EffectFrameBufferScale == new osuTK.Vector2(0.5f));
            AddUntilStep("reduced blur preserves radius", () => backdrop.EffectBlurSigma == new osuTK.Vector2((float)(OverlayTransparency.MAX_BLUR_SIGMA * OverlayTransparency.BlurStrength.Value * 0.5f)));
            AddAssert("outer capture disabled", () => !overlayBackdrop.BufferingActive);
            AddStep("open overlay", () => overlayParent.Alpha = 1);
            AddUntilStep("both captures active", () => backdrop.BufferingActive && overlayBackdrop.BufferingActive);
            AddAssert("carousel remains visible", () => surfaceParent.IsPresent && surfaceParent.Alpha == 1);
            AddStep("close overlay", () => overlayParent.Alpha = 0);
            AddUntilStep("only carousel capture remains", () => backdrop.BufferingActive && !overlayBackdrop.BufferingActive);
            AddStep("open overlay again", () => overlayParent.Alpha = 1);
            AddUntilStep("captures restored", () => backdrop.BufferingActive && overlayBackdrop.BufferingActive);
            AddStep("close overlay", () => overlayParent.Alpha = 0);
        }

        [Test]
        public void TestBufferingFollowsSettingsAndVisibility()
        {
            AddStep("disable carousel glass", () => OverlayTransparency.SongSelectEnabled.Value = false);
            AddStep("enable overlays", () => OverlayTransparency.Enabled.Value = true);
            AddUntilStep("surface registered", () => backdrop.RegisteredConsumers == 1);
            AddAssert("no blur without carousel setting", () => !backdrop.BufferingActive);
            AddStep("enable carousel glass", () => OverlayTransparency.SongSelectEnabled.Value = true);
            AddUntilStep("blur enabled", () => backdrop.BufferingActive);
            AddStep("hide parent", () => surfaceParent.Alpha = 0);
            AddUntilStep("hidden surface stops buffering", () => !backdrop.BufferingActive);
            AddStep("show parent", () => surfaceParent.Alpha = 1);
            AddUntilStep("blur restored", () => backdrop.BufferingActive);
            AddStep("disable overlays", () => OverlayTransparency.Enabled.Value = false);
            AddAssert("carousel blur stays enabled", () => backdrop.BufferingActive);
            AddStep("disable carousel", () => OverlayTransparency.SongSelectEnabled.Value = false);
            AddUntilStep("blur disabled", () => !backdrop.BufferingActive);
            AddStep("dispose surface", () => surfaceParent.Clear());
            AddUntilStep("consumer released", () => backdrop.RegisteredConsumers == 0);
        }
    }
}
