using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Online;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [HeadlessTest]
    public partial class AutopilotLiveInputTest : OsuManualInputManagerTestScene
    {
        private DrawableOsuRuleset ruleset = null!;
        private ManualClock clock = null!;
        private OsuBeatmap beatmap = null!;
        private string previousApiUrl = null!;

        [SetUp]
        public void SaveServer() => previousApiUrl = MosuServerEnvironment.ActiveApiUrl;

        [TearDown]
        public void RestoreServer() => MosuServerEnvironment.ActiveApiUrl = previousApiUrl;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [TearDownSteps]
        public void ReleaseKeys() => AddStep("release key", () => InputManager.ReleaseKey(Key.Z));

        [TestCase(1f)]
        [TestCase(0.7f)]
        public void TestCursorInterpolatesInActualPlayfield(float scale)
        {
            double startTime = 0;
            double endTime = 0;
            Vector2 startPosition = default;
            Vector2 endPosition = default;
            createScene(scale);
            AddStep("find movement samples", () =>
            {
                var frames = new OsuAutoGenerator(beatmap, new Mod[] { new OsuModAutopilot() }) { DelayedMovements = true }
                             .Generate().Frames.Cast<OsuReplayFrame>().ToList();
                int index = frames.FindIndex(f => f.Time > 850 && f.Time < 950);
                startTime = frames[index].Time;
                endTime = frames[index + 1].Time;
                startPosition = frames[index].Position;
                endPosition = frames[index + 1].Position;
                Assert.That(startPosition, Is.Not.EqualTo(endPosition));
            });
            AddStep("advance between samples", () => clock.CurrentTime = startTime + (endTime - startTime) * 0.25);
            AddUntilStep("clock caught up", () => ruleset.FrameStableClock.CurrentTime == clock.CurrentTime);
            AddAssert("cursor at interpolated position", () => cursorNear(Vector2.Lerp(startPosition, endPosition, 0.25f)));
            AddStep("advance within same interval", () => clock.CurrentTime = startTime + (endTime - startTime) * 0.75);
            AddUntilStep("clock caught up", () => ruleset.FrameStableClock.CurrentTime == clock.CurrentTime);
            AddAssert("cursor moved within interval", () => cursorNear(Vector2.Lerp(startPosition, endPosition, 0.75f)));
            AddAssert("no automatic key presses", () => !ruleset.KeyBindingInputManager.PressedActions.Any());
        }

        [Test]
        public void TestManualTapsAndSliderTracking()
        {
            createScene(1);
            advanceTo(1050);
            AddAssert("cursor still on first note for late tap", () => cursorNear(new Vector2(100)));
            AddStep("tap first note late", () => InputManager.PressKey(Key.Z));
            AddUntilStep("first note hit", () => ruleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableHitCircle>().Any(h => h.HitObject.StartTime == 1000 && h.IsHit));
            AddStep("release key", () => InputManager.ReleaseKey(Key.Z));
            advanceTo(1700);
            AddStep("tap second note", () => InputManager.PressKey(Key.Z));
            AddUntilStep("second note hit", () => ruleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableHitCircle>().Any(h => h.HitObject.StartTime == 1700 && h.IsHit));
            AddStep("release key", () => InputManager.ReleaseKey(Key.Z));
            advanceTo(2200);
            AddStep("hold slider", () => InputManager.PressKey(Key.Z));
            AddUntilStep("slider head hit", () => ruleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Single().HeadCircle.IsHit);
            advanceTo(2400);
            AddAssert("cursor follows slider ball", () => cursorNear(((Slider)beatmap.HitObjects[2]).StackedPositionAt(200 / ((Slider)beatmap.HitObjects[2]).Duration)));
            AddAssert("slider remains tracked", () => ruleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableSlider>().Single().SliderInputManager.Tracking);
        }

        [TestCase(50)]
        [TestCase(100)]
        [TestCase(200)]
        public void TestFastNotesCanBeTapped(double spacing)
        {
            createScene(1, spacing);
            advanceTo(1000);
            AddStep("tap first note", () => InputManager.PressKey(Key.Z));
            AddUntilStep("first note hit", () => ruleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableHitCircle>().Any(h => h.HitObject.StartTime == 1000 && h.IsHit));
            AddStep("release key", () => InputManager.ReleaseKey(Key.Z));
            advanceTo(1000 + spacing);
            AddAssert("cursor reached second note", () => cursorNear(new Vector2(400, 200)));
            AddStep("tap second note", () => InputManager.PressKey(Key.Z));
            AddUntilStep("second note hit", () => ruleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableHitCircle>().Any(h => h.HitObject.StartTime == 1000 + spacing && h.IsHit));
        }

        [TestCase(MosuServerEnvironment.PublicServerUrl, true)]
        [TestCase("https://osu.ppy.sh", false)]
        [TestCase("https://lazer-api.shikkesora.com", false)]
        public void TestCursorInputPathMatchesServer(string apiUrl, bool virtualCursor)
        {
            createScene(1, apiUrl: apiUrl);
            AddUntilStep("autopilot cursor updated", () => ruleset.KeyBindingInputManager.CurrentState.Mouse.Position != Vector2.Zero);
            AddAssert("virtual cursor ownership matches server", () => ruleset.KeyBindingInputManager.IsVirtualCursorActive == virtualCursor);
            AddAssert("physical input suppression matches server", () => ruleset.KeyBindingInputManager.ShouldSuppressPhysicalCursorMove == virtualCursor);
            AddAssert("user movement remains disabled", () => !ruleset.KeyBindingInputManager.AllowUserCursorMovement);
            AddAssert("no automatic key presses", () => !ruleset.KeyBindingInputManager.PressedActions.Any());
        }

        [Test]
        public void TestOtherServerKeepsOriginalCursorBetweenFrames()
        {
            Vector2 position = default;
            double time = 0;
            createScene(1, apiUrl: "https://osu.ppy.sh");
            AddStep("find original frame interval", () =>
            {
                var frames = new OsuAutoGenerator(beatmap, new Mod[] { new OsuModAutopilot() })
                             .Generate().Frames.Cast<OsuReplayFrame>().ToList();
                int index = frames.FindIndex(f => f.Time > 850 && f.Time < 950);
                time = frames[index].Time + (frames[index + 1].Time - frames[index].Time) * 0.1;
                position = frames[index].Position;
            });
            AddStep("advance between original samples", () => clock.CurrentTime = time);
            AddUntilStep("clock caught up", () => ruleset.FrameStableClock.CurrentTime == time);
            AddUntilStep("original cursor catches up", () => cursorNear(position));
            AddStep("advance within same nearest-frame interval", () => clock.CurrentTime = time + 0.1);
            AddUntilStep("clock caught up", () => ruleset.FrameStableClock.CurrentTime == time + 0.1);
            AddAssert("original cursor does not interpolate", () => cursorNear(position));
        }

        private bool cursorNear(Vector2 position)
            => Vector2.Distance(ruleset.Playfield.ToLocalSpace(ruleset.KeyBindingInputManager.CurrentState.Mouse.Position), position) < 0.01f;

        private void advanceTo(double time)
        {
            AddStep($"advance to {time}", () => clock.CurrentTime = time);
            AddUntilStep("clock caught up", () => ruleset.FrameStableClock.CurrentTime == time);
        }

        private void createScene(float scale, double spacing = 700, string apiUrl = MosuServerEnvironment.PublicServerUrl)
        {
            AddStep("create live autopilot", () =>
            {
                MosuServerEnvironment.ActiveApiUrl = apiUrl;
                config.SetValue(OsuSetting.ForkRelaxEnabled, false);
                config.SetValue(OsuSetting.ForkAimAssistEnabled, false);
                config.SetValue(OsuSetting.ForkVirtualCursorInputDelay, false);
                InputManager.ReleaseKey(Key.Z);
                clock = new ManualClock { CurrentTime = 800, Rate = 1 };
                beatmap = new OsuBeatmap();
                beatmap.HitObjects.Add(new HitCircle { StartTime = 1000, Position = new Vector2(100) });
                beatmap.HitObjects.Add(new HitCircle { StartTime = 1000 + spacing, Position = new Vector2(400, 200) });
                beatmap.HitObjects.Add(new Slider
                {
                    StartTime = 2200,
                    Position = new Vector2(100),
                    Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(200, 0) }, 200),
                });
                foreach (var obj in beatmap.HitObjects)
                    obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);

                Child = new SkinProvidingContainer(new TrianglesSkin(null!))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = ruleset = new DrawableOsuRuleset(new OsuRuleset(), beatmap, new Mod[] { new OsuModAutopilot() })
                    {
                        Clock = new FramedClock(clock),
                        Scale = new Vector2(scale),
                    },
                };
            });
            AddUntilStep("gameplay loaded", () => ruleset.IsLoaded && ruleset.Playfield.HitObjectContainer.AliveObjects.Any());
            AddAssert("live input, not replay", () => ruleset.KeyBindingInputManager.ReplayInputHandler == null);
        }
    }
}
