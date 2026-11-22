using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [HeadlessTest]
    public partial class MapleAimAssistLiveInputTest : OsuManualInputManagerTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private DrawableOsuRuleset ruleset = null!;
        private ManualClock clock = null!;
        private OsuModMosuAimAssist mod = null!;
        private OsuBeatmap beatmap = null!;
        private AimAssistController controller => ruleset.Playfield.AimAssistController;

        [TestCase(MosuAimAssistAlgorithm.MapleV2, 1f)]
        [TestCase(MosuAimAssistAlgorithm.MapleV2, 0.7f)]
        [TestCase(MosuAimAssistAlgorithm.MapleV3, 1f)]
        [TestCase(MosuAimAssistAlgorithm.MapleV3, 0.7f)]
        public void TestMovesActualVirtualCursorAndResetsOnAlgorithmSwitch(MosuAimAssistAlgorithm algorithm, float scale)
        {
            Vector2 raw = default;
            Vector2 heldOffset = default;
            createScene(algorithm, scale);
            for (int i = 0; i < 9; i++)
            {
                int step = i;
                AddStep("move hand and advance clock", () =>
                {
                    clock.CurrentTime = 720 + step * 40;
                    raw = ruleset.Playfield.GamefieldToScreenSpace(new Vector2(256 + (step % 2 == 0 ? 20 : 25), 192));
                    InputManager.MoveMouseTo(raw);
                });
                AddUntilStep("clock caught up", () => controller.Time.Current == clock.CurrentTime);
            }

            AddAssert("declared mod drives actual cursor", () => controller.IsAimAssistDeclaredMod && controller.IsDrivingCursor);
            AddAssert("algorithm has its own mode", () => controller.CurrentModeName == (algorithm == MosuAimAssistAlgorithm.MapleV2 ? "maple-v2" : "maple-v3"));
            AddAssert("original input remains physical", () => Vector2.Distance(ruleset.KeyBindingInputManager.OriginalUserCursorPosition, raw) < 0.01f);
            AddAssert("assisted cursor closer to target", () =>
                Vector2.Distance(ruleset.KeyBindingInputManager.CurrentState.Mouse.Position, controller.CurrentTargetPosition!.Value)
                < Vector2.Distance(raw, controller.CurrentTargetPosition!.Value));
            AddAssert("correction recorded in telemetry", () => controller.AdjustedFrameCount > 0 && controller.MaxAdjustmentMagnitude > 0);
            AddStep("hold hand still", () =>
            {
                heldOffset = ruleset.KeyBindingInputManager.CurrentState.Mouse.Position - raw;
                clock.CurrentTime += 10;
            });
            AddUntilStep("stationary frame updated", () => controller.Time.Current == clock.CurrentTime);
            AddAssert("stationary hand is not recentered", () =>
                Vector2.Distance(ruleset.KeyBindingInputManager.CurrentState.Mouse.Position - raw, heldOffset) < 0.001f);
            AddStep("switch back to disabled Mosu", () =>
            {
                mod.Strength.Value = 0;
                mod.Algorithm.Value = MosuAimAssistAlgorithm.Mosu;
                clock.CurrentTime += 10;
            });
            AddUntilStep("Maple offset cleared on switch", () => controller.CurrentOffsetMagnitude < 0.001f);
            AddAssert("no Maple mode left", () => !controller.CurrentModeName.StartsWith("maple", System.StringComparison.Ordinal));
        }

        [TestCase(MosuAimAssistAlgorithm.MapleV2)]
        [TestCase(MosuAimAssistAlgorithm.MapleV3)]
        public void TestSliderPathAndSettingsAreApplied(MosuAimAssistAlgorithm algorithm)
        {
            createScene(algorithm, 1, slider: true);
            AddStep("move to active slider", () =>
            {
                mod.MapleV2AssistOnSliders.Value = true;
                clock.CurrentTime = 900;
                InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(new Vector2(300, 192)));
            });
            AddUntilStep("clock caught up", () => controller.Time.Current == clock.CurrentTime);
            AddStep("advance slider a little", () => clock.CurrentTime = 920);
            AddUntilStep("path target follows slider", () =>
            {
                var slider = (Slider)beatmap.HitObjects[0];
                Vector2 expected = ruleset.Playfield.GamefieldToScreenSpace(slider.StackedPositionAt((920 - slider.StartTime) / slider.Duration));
                return controller.CurrentTargetPosition.HasValue && Vector2.Distance(controller.CurrentTargetPosition.Value, expected) < 0.01f;
            });
            if (algorithm == MosuAimAssistAlgorithm.MapleV2)
            {
                AddStep("disable slider following", () =>
                {
                    mod.MapleV2AssistOnSliders.Value = false;
                    clock.CurrentTime = 930;
                });
                AddUntilStep("unhit slider head keeps its late hit window", () => controller.Time.Current == 930 && controller.CurrentTargetPosition.HasValue &&
                    Vector2.Distance(controller.CurrentTargetPosition.Value, ruleset.Playfield.GamefieldToScreenSpace(beatmap.HitObjects[0].StackedPosition)) < 0.01f);
                AddStep("expire slider head window", () => clock.CurrentTime = 970);
                AddUntilStep("expired slider head is not followed", () => controller.Time.Current == 970 && !controller.CurrentTargetPosition.HasValue);
            }
            else
            {
                AddStep("disable V3 power", () =>
                {
                    mod.MapleV3Power.Value = 0;
                    clock.CurrentTime = 940;
                });
                AddUntilStep("zero power stops correction", () => controller.CurrentOffsetMagnitude < 0.001f && !controller.PassedActivationFilters);
            }
        }

        [TestCase(MosuAimAssistAlgorithm.MapleV2)]
        [TestCase(MosuAimAssistAlgorithm.MapleV3)]
        public void TestSpinnerDoesNotAcquireNearbyCircle(MosuAimAssistAlgorithm algorithm)
        {
            createScene(algorithm, 1, spinner: true);
            AddStep("move next to circle during spinner", () =>
            {
                clock.CurrentTime = 1050;
                InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(new Vector2(276, 192)));
            });
            AddUntilStep("spinner releases assistance", () => controller.Time.Current == 1050 && controller.CurrentAssistStateName == "spinner-release");
            AddAssert("no offset or cursor ownership", () => !controller.IsDrivingCursor && controller.CurrentOffsetMagnitude == 0);
        }

        [TestCase(MosuAimAssistAlgorithm.MapleV2)]
        [TestCase(MosuAimAssistAlgorithm.MapleV3)]
        public void TestReplayIsNotAssistedAgain(MosuAimAssistAlgorithm algorithm)
        {
            createScene(algorithm, 1);
            AddStep("attach already recorded cursor", () =>
            {
                mod.ShowRealCursor.Value = true;
                ruleset.Playfield.Cursor.Show();
                ruleset.SetReplayScore(new osu.Game.Scoring.Score
                {
                    Replay = new osu.Game.Replays.Replay
                    {
                        Frames = new System.Collections.Generic.List<osu.Game.Rulesets.Replays.ReplayFrame>
                        {
                            new osu.Game.Rulesets.Osu.Replays.OsuReplayFrame(700, new Vector2(280, 192)),
                            new osu.Game.Rulesets.Osu.Replays.OsuReplayFrame(1200, new Vector2(270, 192)),
                        },
                    },
                    ScoreInfo = new osu.Game.Scoring.ScoreInfo(),
                });
                clock.CurrentTime = 1050;
            });
            AddUntilStep("replay input installed", () => ruleset.KeyBindingInputManager.ReplayInputHandler != null && controller.Time.Current == 1050);
            AddAssert("no second correction applied", () => !controller.IsDrivingCursor && controller.CurrentOffsetMagnitude == 0);
            AddAssert("replay does not show spectator mouse as player input", () =>
                ruleset.Playfield.Cursor.ChildrenOfType<osu.Game.Rulesets.Osu.UI.Cursor.AimAssistRealCursor>().Single()
                       .ChildrenOfType<osu.Framework.Graphics.Containers.CircularContainer>().Single().Alpha == 0);
        }

        [TestCase(MosuAimAssistAlgorithm.Mosu, 1f)]
        [TestCase(MosuAimAssistAlgorithm.MapleV2, 0.7f)]
        [TestCase(MosuAimAssistAlgorithm.MapleV3, 1.6f)]
        public void TestRealCursorMarkerIsIndependentOfAssistedCursor(MosuAimAssistAlgorithm algorithm, float scale)
        {
            osu.Game.Rulesets.Osu.UI.Cursor.AimAssistRealCursor overlay = null!;
            osu.Framework.Graphics.Containers.CircularContainer marker = null!;
            Vector2 raw = default;
            createScene(algorithm, scale);
            AddStep("show gameplay cursor and find real cursor overlay", () =>
            {
                ruleset.Playfield.Cursor.Show();
                overlay = ruleset.Playfield.Cursor.ChildrenOfType<osu.Game.Rulesets.Osu.UI.Cursor.AimAssistRealCursor>().Single();
                marker = overlay.ChildrenOfType<osu.Framework.Graphics.Containers.CircularContainer>().Single();
                raw = ruleset.Playfield.GamefieldToScreenSpace(new Vector2(276, 192));
                InputManager.MoveMouseTo(raw);
                clock.CurrentTime = 710;
            });
            AddAssert("off by default", () => marker.Alpha == 0);
            AddStep("enable only real cursor", () =>
            {
                mod.ShowRealCursor.Value = true;
                clock.CurrentTime = 720;
            });
            AddUntilStep("red marker appears without debug", () => marker.Alpha == 1 && !mod.ShowDebug.Value);
            AddAssert("marker is red and small", () => marker.BorderColour == osuTK.Graphics.Color4.Red &&
                System.Math.Abs((marker.ScreenSpaceDrawQuad.TopRight - marker.ScreenSpaceDrawQuad.TopLeft).Length - 24) < 0.01f);
            AddAssert("marker follows physical position", () => Vector2.Distance(marker.ScreenSpaceDrawQuad.Centre, raw) < 0.01f);
            AddAssert("marker is above skinned cursor", () => overlay.Parent == ruleset.Playfield.Cursor.ActiveCursor.Parent &&
                overlay.Depth < ruleset.Playfield.Cursor.ActiveCursor.Depth);
            AddStep("separate virtual cursor from physical mouse", () => ruleset.KeyBindingInputManager.MoveVirtualCursorTo(raw + new Vector2(80, 0)));
            AddAssert("real marker did not move with virtual cursor", () => Vector2.Distance(marker.ScreenSpaceDrawQuad.Centre, raw) < 0.01f);
            AddStep("move physical mouse independently", () =>
            {
                raw += new Vector2(7, 9);
                InputManager.MoveMouseTo(raw);
                clock.CurrentTime = 730;
            });
            AddUntilStep("marker follows new physical position", () => Vector2.Distance(marker.ScreenSpaceDrawQuad.Centre, raw) < 0.01f);
            AddStep("disable marker", () => mod.ShowRealCursor.Value = false);
            AddUntilStep("marker hidden", () => marker.Alpha == 0);
        }

        private void createScene(MosuAimAssistAlgorithm algorithm, float scale, bool slider = false, bool spinner = false)
        {
            AddStep("create declared AA gameplay", () =>
            {
                config.SetValue(OsuSetting.ForkAimAssistEnabled, false);
                config.SetValue(OsuSetting.ForkRelaxEnabled, false);
                config.SetValue(OsuSetting.ForkVirtualCursorInputDelay, false);
                clock = new ManualClock { CurrentTime = 700, Rate = 1 };
                mod = new OsuModMosuAimAssist { Algorithm = { Value = algorithm } };
                beatmap = new OsuBeatmap();
                if (slider)
                {
                    beatmap.HitObjects.Add(new Slider
                    {
                        StartTime = 800,
                        Position = new Vector2(256, 192),
                        Path = new SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }, 100),
                    });
                }
                else
                {
                    beatmap.HitObjects.Add(new HitCircle { StartTime = 800, Position = new Vector2(100, 192) });
                    beatmap.HitObjects.Add(new HitCircle { StartTime = 1100, Position = new Vector2(256, 192) });
                }
                if (spinner)
                    beatmap.HitObjects.Add(new Spinner { StartTime = 900, EndTime = 1300 });
                foreach (var obj in beatmap.HitObjects)
                    obj.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                Child = new SkinProvidingContainer(new TrianglesSkin(null!))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = ruleset = new DrawableOsuRuleset(new OsuRuleset(), beatmap, new Mod[] { mod })
                    {
                        Clock = new FramedClock(clock),
                        Scale = new Vector2(scale),
                    },
                };
            });
            AddUntilStep("gameplay loaded", () => ruleset.IsLoaded && ruleset.Playfield.HitObjectContainer.AliveObjects.Any());
        }
    }
}
