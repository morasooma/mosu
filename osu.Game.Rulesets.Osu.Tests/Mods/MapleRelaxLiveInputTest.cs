using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
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
    public partial class MapleRelaxLiveInputTest : OsuManualInputManagerTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private DrawableOsuRuleset ruleset = null!;
        private ManualClock clock = null!;
        private OsuModMosuRelax mod = null!;
        private readonly List<JudgementResult> judgements = new List<JudgementResult>();
        private RelaxController controller => ruleset.Playfield.RelaxController;

        [TestCase(1f, 1.0)]
        [TestCase(0.7f, 0.75)]
        [TestCase(1.6f, 1.5)]
        public void TestActualHitsAndIndependentReleaseAtDifferentScalesAndRates(float scale, double rate)
        {
            HitCircle[] notes = Enumerable.Range(0, 5).Select(i => new HitCircle
            {
                StartTime = 1000 + i * 240,
                Position = new Vector2(100 + i * 65, 192),
            }).ToArray();
            createScene(notes, scale, rate);
            for (int i = 0; i < notes.Length; i++)
            {
                var note = notes[i];
                int hits = i + 1;
                AddStep("aim before note", () =>
                {
                    clock.CurrentTime = note.StartTime - 10;
                    InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(note.StackedPosition));
                });
                AddStep("advance to note", () => clock.CurrentTime = note.StartTime);
                AddUntilStep("actual judgement", () => judgements.Count == hits && judgements.All(j => j.IsHit));
                AddAssert("hold uses actual gameplay rate", () => controller.CurrentPlannedReleaseTime!.Value - controller.LastPressTime,
                    () => Is.EqualTo(Math.Truncate(80 * rate)).Within(1));
                AddStep("advance to release", () => clock.CurrentTime = controller.CurrentPlannedReleaseTime!.Value + 1);
                AddUntilStep("button released", () => !ruleset.KeyBindingInputManager.PressedActions.Any());
            }
            AddAssert("one press per note", () => controller.GeneratedPressCount == notes.Length);
            AddAssert("Maple branch active", () => controller.CurrentModeName == "maple");
        }

        [Test]
        public void TestLiveAlgorithmSwitchReleasesHeldInputAndKeepsMosuSettings()
        {
            createScene(new[] { new HitCircle { StartTime = 1000, Position = new Vector2(256, 192) } });
            AddStep("aim at circle", () => InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(new Vector2(256, 192))));
            AddStep("press Maple", () => clock.CurrentTime = 1000);
            AddUntilStep("Maple holds a button", () => controller.GeneratedPressCount == 1 && ruleset.KeyBindingInputManager.PressedActions.Any());
            AddStep("switch to Mosu", () =>
            {
                mod.Algorithm.Value = MosuRelaxAlgorithm.Mosu;
                clock.CurrentTime = 1001;
            });
            AddUntilStep("no Maple hold leaks", () => !ruleset.KeyBindingInputManager.PressedActions.Any() && controller.CurrentModeName != "maple");
            AddAssert("Mosu defaults unchanged", () => mod.Preset.Value == MosuRelaxPreset.Reliable && mod.TimingVariance.Value == 8);
            AddStep("switch back to Maple", () =>
            {
                mod.Algorithm.Value = MosuRelaxAlgorithm.Maple;
                clock.CurrentTime = 1002;
            });
            AddUntilStep("Maple active again", () => controller.CurrentModeName == "maple");
            AddAssert("already judged note not pressed again", () => controller.GeneratedPressCount == 1);
        }

        [Test]
        public void TestReplayDoesNotGenerateNewMaplePresses()
        {
            createScene(new[] { new HitCircle { StartTime = 1000, Position = new Vector2(256, 192) } });
            AddStep("install replay", () =>
            {
                ruleset.SetReplayScore(new osu.Game.Scoring.Score
                {
                    Replay = new osu.Game.Replays.Replay
                    {
                        Frames = new List<osu.Game.Rulesets.Replays.ReplayFrame>
                        {
                            new osu.Game.Rulesets.Osu.Replays.OsuReplayFrame(800, new Vector2(256, 192)),
                            new osu.Game.Rulesets.Osu.Replays.OsuReplayFrame(1200, new Vector2(256, 192)),
                        },
                    },
                    ScoreInfo = new osu.Game.Scoring.ScoreInfo(),
                });
                clock.CurrentTime = 1000;
            });
            AddUntilStep("replay installed", () => ruleset.KeyBindingInputManager.ReplayInputHandler != null && controller.Time.Current == 1000);
            AddAssert("no synthetic presses", () => controller.GeneratedPressCount == 0 && controller.CurrentModeName == "idle");
        }

        [Test]
        public void TestSliderHeadHitAndHoldToEnd()
        {
            var slider = new Slider
            {
                StartTime = 1000, Position = new Vector2(256, 192),
                Path = new osu.Game.Rulesets.Objects.SliderPath(PathType.LINEAR, new[] { Vector2.Zero, new Vector2(100, 0) }, 100),
            };
            createScene(new OsuHitObject[] { slider });
            AddStep("aim slider head", () => InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(slider.StackedPosition)));
            AddStep("press slider head", () => clock.CurrentTime = 1000);
            AddUntilStep("head genuinely hit", () => judgements.Any(j => j.HitObject == slider.HeadCircle && j.IsHit));
            AddStep("advance near slider end", () => clock.CurrentTime = slider.EndTime - 1);
            AddUntilStep("still holding", () => controller.Time.Current == clock.CurrentTime && ruleset.KeyBindingInputManager.PressedActions.Any());
            AddStep("pass end and hold offset", () => clock.CurrentTime = slider.EndTime + 66);
            AddUntilStep("released after slider", () => controller.Time.Current == clock.CurrentTime && !ruleset.KeyBindingInputManager.PressedActions.Any());
        }

        [Test]
        public void TestWaitLateHitsWhenCursorArrivesLate()
        {
            var note = new HitCircle { StartTime = 1000, Position = new Vector2(256, 192) };
            createScene(new[] { note });
            AddStep("stay away at start", () =>
            {
                InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(Vector2.Zero));
                clock.CurrentTime = 1000;
            });
            AddUntilStep("start processed", () => controller.Time.Current == 1000);
            AddAssert("not pressed without aim", () => controller.GeneratedPressCount == 0);
            AddStep("late aim", () =>
            {
                InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(note.StackedPosition));
                clock.CurrentTime = 1020;
            });
            AddUntilStep("late real hit", () => judgements.Count == 1 && judgements[0].IsHit);
        }

        private void createScene(OsuHitObject[] notes, float scale = 1, double rate = 1)
        {
            AddStep("create full MRX ruleset", () =>
            {
                config.SetValue(OsuSetting.ForkRelaxEnabled, false);
                config.SetValue(OsuSetting.ForkAimAssistEnabled, false);
                config.SetValue(OsuSetting.ForkVirtualCursorInputDelay, false);
                judgements.Clear();
                mod = new OsuModMosuRelax
                {
                    Algorithm = { Value = MosuRelaxAlgorithm.Maple },
                    MapleTargetUR = { Value = 0 },
                    MapleCircleHoldError = { Value = 0 },
                    MapleSliderHoldError = { Value = 0 },
                    MapleDirectionPrediction = { Value = false },
                };
                clock = new ManualClock { CurrentTime = 800, Rate = rate };
                var beatmap = new OsuBeatmap { Difficulty = new BeatmapDifficulty { OverallDifficulty = 8 } };
                foreach (var note in notes)
                {
                    note.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                    beatmap.HitObjects.Add(note);
                }
                Child = new SkinProvidingContainer(new TrianglesSkin(null!))
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = ruleset = new DrawableOsuRuleset(new OsuRuleset(), beatmap, new Mod[] { mod })
                    {
                        RelativeSizeAxes = Axes.Both,
                        Clock = new FramedClock(clock),
                        Scale = new Vector2(scale),
                    },
                };
                ruleset.NewResult += judgements.Add;
            });
            AddUntilStep("declared MRX loaded", () => ruleset.IsLoaded && controller.IsLoaded && controller.IsDeclaredMod);
            AddStep("apply gameplay rate", () => ruleset.FrameStableClock.AdjustmentsFromMods.AddAdjustment(AdjustableProperty.Frequency, new BindableDouble(rate)));
        }
    }
}
