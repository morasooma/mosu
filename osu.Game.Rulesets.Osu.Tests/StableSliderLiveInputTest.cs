using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Allocation;
using osu.Game.Configuration;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Online;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests
{
    [HeadlessTest]
    public partial class StableSliderLiveInputTest : OsuManualInputManagerTestScene
    {
        private bool previousStable;
        private DrawableOsuRuleset ruleset = null!;
        private ManualClock clock = null!;
        private DrawableSlider slider = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [SetUp]
        public void SaveProfile() => previousStable = MosuServerEnvironment.UsesStableProtocol;

        [TearDown]
        public void RestoreProfile() => MosuServerEnvironment.UsesStableProtocol = previousStable;

        [TearDownSteps]
        public void ReleaseKeys()
        {
            AddStep("release gameplay keys", () =>
            {
                InputManager.ReleaseKey(Key.Z);
                InputManager.ReleaseKey(Key.X);
            });
        }

        [TestCase(true, 1063, true)]
        [TestCase(true, 1065, false)]
        [TestCase(false, 1065, true)]
        public void TestTailRequiresAcquisitionBeforeCheckpointOnlyOnStable(bool stable, double acquireTime, bool hit)
        {
            createScene(stable);
            moveAt(1000, new Vector2(150, 150));
            AddStep("press slider head", () => InputManager.PressKey(Key.Z));
            AddStep("head judged as a hit", () => Assert.That(slider.HeadCircle.IsHit, Is.True,
                $"time={slider.Time.Current}, result={slider.HeadCircle.Result.Type}, actions={string.Join(",", ruleset.KeyBindingInputManager.PressedActions)}"));
            moveAt(1030, new Vector2(450, 350));
            AddAssert("tracking lost", () => !slider.SliderInputManager.Tracking);
            moveAt(acquireTime, new Vector2(150 + (float)(acquireTime - 1000) * 1.4f, 150));
            AddAssert("tracking reacquired", () => slider.SliderInputManager.Tracking);
            moveAt(1066, new Vector2(242.4f, 150));
            AddUntilStep("tail judged", () => slider.TailCircle.Judged);
            AddStep("tail result", () => Assert.That(slider.TailCircle.IsHit, Is.EqualTo(hit),
                $"end={slider.HitObject.EndTime}, history={string.Join(",", slider.Result.TrackingHistory)}, tail={slider.TailCircle.Result.Type}@{slider.TailCircle.Result.TimeAbsolute}"));
            AddStep("release key", () => InputManager.ReleaseKey(Key.Z));
        }

        [Test]
        public void TestEarlyHeadDoesNotExpandLiveFollowRadius()
        {
            createScene(true);
            moveAt(990, new Vector2(150, 150));
            AddStep("hit head early", () => InputManager.PressKey(Key.Z));
            AddAssert("head hit", () => slider.HeadCircle.IsHit);
            AddStep("leave unexpanded radius before start", () => InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(new Vector2(195, 150))));
            moveAt(1000, new Vector2(195, 150));
            AddAssert("no tracking outside unexpanded radius", () => !slider.SliderInputManager.Tracking);
            moveAt(1010, new Vector2(164, 150));
            AddAssert("tracking acquired inside slider ball", () => slider.SliderInputManager.Tracking);
            AddStep("release key", () => InputManager.ReleaseKey(Key.Z));
        }

        [Test]
        public void TestAlreadyHitHeadDoesNotChangeHeldKey()
        {
            createScene(true);
            moveAt(1000, new Vector2(150, 150));
            AddStep("hit head with old key", () => InputManager.PressKey(Key.Z));
            AddAssert("head hit", () => slider.HeadCircle.IsHit);
            AddStep("press new key on judged head", () => InputManager.PressKey(Key.X));
            moveAt(1010, new Vector2(164, 150));
            AddStep("release new key", () => InputManager.ReleaseKey(Key.X));
            AddAssert("old key retains tracking", () => slider.SliderInputManager.Tracking);
            AddStep("release old key", () => InputManager.ReleaseKey(Key.Z));
        }

        [Test]
        public void TestLiveKeyTransferUsesStableButtonHistory()
        {
            createScene(true);
            moveAt(800, new Vector2(450, 350));
            AddStep("hold old key before slider", () => InputManager.PressKey(Key.Z));
            moveAt(1000, new Vector2(450, 350));
            AddStep("press new key on head", () =>
            {
                InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(new Vector2(150, 150)));
                InputManager.PressKey(Key.X);
            });
            AddStep("head judged as a hit", () => Assert.That(slider.HeadCircle.IsHit, Is.True,
                $"time={slider.Time.Current}, result={slider.HeadCircle.Result.Type}, actions={string.Join(",", ruleset.KeyBindingInputManager.PressedActions)}"));
            AddAssert("tracking active", () => slider.SliderInputManager.Tracking);
            moveAt(1010, new Vector2(164, 150));
            AddStep("release new key", () => InputManager.ReleaseKey(Key.X));
            AddAssert("old key cannot retain tracking", () => !slider.SliderInputManager.Tracking);
            AddStep("release old key", () => InputManager.ReleaseKey(Key.Z));
        }

        private void moveAt(double time, Vector2 position)
        {
            AddStep($"advance to {time}", () => clock.CurrentTime = time);
            AddUntilStep("clock caught up", () => ruleset.FrameStableClock.CurrentTime == time);
            AddStep($"cursor at {time}", () => InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(position)));
        }

        private void createScene(bool stable)
        {
            AddStep("create live gameplay", () =>
            {
                config.SetValue(OsuSetting.ForkRelaxEnabled, false);
                config.SetValue(OsuSetting.ForkAimAssistEnabled, false);
                config.SetValue(OsuSetting.ForkVirtualCursorInputDelay, false);
                InputManager.ReleaseKey(Key.Z);
                InputManager.ReleaseKey(Key.X);
                MosuServerEnvironment.UsesStableProtocol = stable;
                clock = new ManualClock { CurrentTime = 800, Rate = 1 };
                var controlPoints = new ControlPointInfo();
                controlPoints.Add(0, new TimingControlPoint { BeatLength = 100 });
                var beatmap = new OsuBeatmap
                {
                    Difficulty = new BeatmapDifficulty { CircleSize = 4, OverallDifficulty = 8, SliderMultiplier = 1.4, SliderTickRate = 1 },
                    ControlPointInfo = controlPoints,
                };
                var hitObject = new Slider
                {
                    StartTime = 1000,
                    Position = new Vector2(150, 150),
                    ClassicSliderBehaviour = true,
                    Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(140, 0) }, 140),
                };
                hitObject.ApplyDefaults(controlPoints, beatmap.Difficulty);
                beatmap.HitObjects.Add(hitObject);
                Child = new SkinProvidingContainer(new TrianglesSkin(null!))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = ruleset = new DrawableOsuRuleset(new OsuRuleset(), beatmap, new Mod[] { new OsuModClassic() })
                    {
                        RelativeSizeAxes = Axes.Both,
                        Clock = new FramedClock(clock),
                    },
                };
            });
            AddUntilStep("slider loaded", () => ruleset.IsLoaded && (slider = ruleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().SingleOrDefault()!) != null);
            AddAssert("real input, not replay", () => ruleset.KeyBindingInputManager.ReplayInputHandler == null);
        }
    }
}
