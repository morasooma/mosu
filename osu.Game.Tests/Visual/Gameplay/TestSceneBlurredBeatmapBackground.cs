using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneBlurredBeatmapBackground : OsuTestScene, IBackgroundSceneSource
    {
        [Cached(typeof(IBackgroundSceneSource))]
        private readonly IBackgroundSceneSource source;

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private readonly Container viewport;
        private readonly Sprite backdrop;
        private BlurredBeatmapBackground panel = null!;
        private int revision;

        public Sprite BackgroundSprite => backdrop;

        public TestSceneBlurredBeatmapBackground()
        {
            source = this;
            Add(viewport = new Container
            {
                Size = new Vector2(800, 600),
                Position = new Vector2(25, 35),
                Child = backdrop = new Sprite
                {
                    Size = new Vector2(1000, 600),
                    Position = new Vector2(-100, 0),
                },
            });
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create local blur panel", () =>
            {
                backdrop.Position = new Vector2(-100, 0);
                backdrop.Texture = new Texture(renderer.WhitePixel);
                Add(panel = new BlurredBeatmapBackground { Position = new Vector2(150, 200) });
            });
            AddWaitStep("settle geometry", 3);
        }

        [TearDownSteps]
        public void TearDownSteps() => AddStep("remove panel", () => Remove(panel, true));

        [Test]
        public void TestWindowUsesActualBackgroundQuadNotFittedImage()
        {
            AddAssert("samples real cropped background", () => panel.SampleQuad, () => Is.EqualTo(backdrop.ScreenSpaceDrawQuad));
            AddAssert("does not fit image into panel", () => panel.SampleQuad.Size, () => Is.Not.EqualTo(panel.ScreenSpaceDrawQuad.Size));
            AddAssert("capture is panel plus blur margin", () => panel.CaptureSize, () => Is.EqualTo(new Vector2(472, 232)));
            AddAssert("panel masks output", () => panel.Masking);
            AddAssert("six background settings", () => panel.CreateSerialisedInfo().Settings.Count, () => Is.EqualTo(6));
            AddStep("move panel to another background region", () => panel.Position += new Vector2(220, 60));
            AddAssert("image stays aligned with background", () => panel.SampleQuad, () => Is.EqualTo(backdrop.ScreenSpaceDrawQuad));
            AddStep("resize and rotate panel", () =>
            {
                panel.Size = new Vector2(180, 300);
                panel.Rotation = 20;
            });
            AddAssert("image still aligns through rotated mask", () => panel.SampleQuad, () => Is.EqualTo(backdrop.ScreenSpaceDrawQuad));
            AddAssert("capture follows panel size", () => panel.CaptureSize, () => Is.EqualTo(new Vector2(252, 372)));
        }

        [Test]
        public void TestFittedImageCoversBothPanelAxes()
        {
            AddStep("fit background to panel", () => panel.ShowFullImage.Value = true);
            AddAssert("no empty horizontal strip", () => panel.SampleQuad.Width, () => Is.GreaterThanOrEqualTo(panel.DrawWidth));
            AddAssert("no empty vertical strip", () => panel.SampleQuad.Height, () => Is.GreaterThanOrEqualTo(panel.DrawHeight));
            AddStep("make panel tall", () => panel.Size = new Vector2(80, 420));
            AddAssert("tall panel filled horizontally", () => panel.SampleQuad.Width, () => Is.GreaterThanOrEqualTo(panel.DrawWidth));
            AddAssert("tall panel filled vertically", () => panel.SampleQuad.Height, () => Is.GreaterThanOrEqualTo(panel.DrawHeight));
            AddStep("make panel wide", () => panel.Size = new Vector2(600, 60));
            AddAssert("wide panel filled horizontally", () => panel.SampleQuad.Width, () => Is.GreaterThanOrEqualTo(panel.DrawWidth));
            AddAssert("wide panel filled vertically", () => panel.SampleQuad.Height, () => Is.GreaterThanOrEqualTo(panel.DrawHeight));
        }

        [Test]
        public void TestBlurStrengthAndNeutralTint()
        {
            AddAssert("no artificial theme tint by default", () => panel.TintOpacity.Value, () => Is.Zero);
            AddStep("record cache", () => revision = panel.CaptureRevision);
            AddStep("increase blur", () => panel.BlurStrength.Value = 20);
            AddAssert("strength refreshes capture", () => panel.CaptureRevision, () => Is.GreaterThan(revision));
            AddAssert("kernel margin follows strength", () => panel.CaptureSize, () => Is.EqualTo(new Vector2(520, 280)));
            AddStep("zero blur", () => panel.BlurStrength.Value = 0);
            AddAssert("sharp capture has no blur margin", () => panel.CaptureSize, () => Is.EqualTo(panel.DrawSize));
            AddStep("save blur settings", () =>
            {
                panel.BlurStrength.Value = 23;
                panel.TintOpacity.Value = 0.1f;
                using var copy = (BlurredBeatmapBackground)panel.CreateSerialisedInfo().CreateInstance();
                Assert.That(copy.BlurStrength.Value, Is.EqualTo(23));
                Assert.That(copy.TintOpacity.Value, Is.EqualTo(0.1f));
            });
        }

        [Test]
        public void TestStationaryPanelDoesNotRecapture()
        {
            AddStep("record cache revision", () => revision = panel.CaptureRevision);
            AddWaitStep("stationary gameplay frames", 30);
            AddAssert("no new capture or blur requested", () => panel.CaptureRevision, () => Is.EqualTo(revision));
            AddStep("move panel in skin editor", () => panel.X += 90);
            AddAssert("movement refreshes capture", () => panel.CaptureRevision, () => Is.GreaterThan(revision));
            AddStep("record settled cache", () => revision = panel.CaptureRevision);
            AddWaitStep("stationary frames after editing", 30);
            AddAssert("cache stable again", () => panel.CaptureRevision, () => Is.EqualTo(revision));
        }

        [Test]
        public void TestViewportAndBackgroundChangesRefreshCache()
        {
            AddStep("record cache", () => revision = panel.CaptureRevision);
            AddStep("move background viewport", () => viewport.X += 40);
            AddAssert("source movement refreshes", () => panel.CaptureRevision, () => Is.GreaterThan(revision));
            AddAssert("sample follows viewport", () => panel.SampleQuad, () => Is.EqualTo(backdrop.ScreenSpaceDrawQuad));
            AddStep("record cache", () => revision = panel.CaptureRevision);
            AddStep("replace safety-filtered background", () => backdrop.Texture = null);
            AddAssert("texture removal refreshes", () => panel.CaptureRevision, () => Is.GreaterThan(revision));
            AddAssert("no stale picture", () => !panel.HasBackground);
            AddStep("restore background", () => backdrop.Texture = new Texture(renderer.WhitePixel));
            AddAssert("new texture sampled", () => panel.HasBackground);
        }

        [Test]
        public void TestEditingWithPausedClock()
        {
            AddStep("freeze panel clock", () => panel.Clock = new FramedClock(new ManualClock()));
            AddStep("record paused cache", () => revision = panel.CaptureRevision);
            AddStep("move panel while paused", () => panel.X += 60);
            AddAssert("paused editing updates crop", () => panel.CaptureRevision, () => Is.GreaterThan(revision));
            AddAssert("paused sample remains aligned", () => panel.SampleQuad, () => Is.EqualTo(backdrop.ScreenSpaceDrawQuad));
            AddStep("record paused cache", () => revision = panel.CaptureRevision);
            AddWaitStep("paused frames", 10);
            AddAssert("paused cache remains stable", () => panel.CaptureRevision, () => Is.EqualTo(revision));
        }
    }
}
