// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Timing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Storyboards;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneReplayRecording : PlayerTestScene
    {
        protected override Ruleset CreatePlayerRuleset() => new OsuRuleset();

        [Resolved]
        private AudioManager audioManager { get; set; } = null!;

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset) => new Beatmap
        {
            HitObjects =
            {
                new HitCircle
                {
                    Position = OsuPlayfield.BASE_SIZE / 2,
                    StartTime = 0,
                },
                new HitCircle
                {
                    Position = OsuPlayfield.BASE_SIZE / 2,
                    StartTime = 5000,
                },
                new HitCircle
                {
                    Position = OsuPlayfield.BASE_SIZE / 2,
                    StartTime = 10000,
                },
                new HitCircle
                {
                    Position = OsuPlayfield.BASE_SIZE / 2,
                    StartTime = 15000,
                }
            }
        };

        protected override WorkingBeatmap CreateWorkingBeatmap(IBeatmap beatmap, Storyboard? storyboard = null) =>
            new ClockBackedTestWorkingBeatmap(beatmap, storyboard, new FramedClock(new ManualClock { Rate = 1 }), audioManager);

        [Test]
        public void TestRecording()
        {
            seekTo(0);
            AddStep("move cursor to circle", () => InputManager.MoveMouseTo(Player.DrawableRuleset.Playfield.HitObjectContainer.AliveObjects.Single()));
            AddStep("press X", () => InputManager.PressKey(Key.X));
            seekTo(15);
            AddStep("release X", () => InputManager.ReleaseKey(Key.X));
            AddAssert("right button press recorded to replay", () => Player.Score.Replay.Frames.OfType<OsuReplayFrame>().Any(f => f.Actions.SequenceEqual([OsuAction.RightButton])));

            seekTo(5000);
            AddStep("move cursor to circle", () => InputManager.MoveMouseTo(Player.DrawableRuleset.Playfield.HitObjectContainer.AliveObjects.Single()));
            AddStep("press Z", () => InputManager.PressKey(Key.Z));
            seekTo(5015);
            AddStep("release Z", () => InputManager.ReleaseKey(Key.Z));
            AddAssert("left button press recorded to replay", () => Player.Score.Replay.Frames.OfType<OsuReplayFrame>().Any(f => f.Actions.SequenceEqual([OsuAction.LeftButton])));

            seekTo(10000);
            AddStep("move cursor to circle", () => InputManager.MoveMouseTo(Player.DrawableRuleset.Playfield.HitObjectContainer.AliveObjects.Single()));
            AddStep("press C", () => InputManager.PressKey(Key.C));
            seekTo(10015);
            AddStep("release C", () => InputManager.ReleaseKey(Key.C));
            AddAssert("smoke button press recorded to replay", () => Player.Score.Replay.Frames.OfType<OsuReplayFrame>().Any(f => f.Actions.SequenceEqual([OsuAction.Smoke])));
        }

        [Test]
        public void TestPressAndReleaseOnSameFrame()
        {
            seekTo(0);
            AddStep("move cursor to circle", () => InputManager.MoveMouseTo(Player.DrawableRuleset.Playfield.HitObjectContainer.AliveObjects.Single()));
            AddStep("press X", () => InputManager.PressKey(Key.X));
            AddStep("release X", () => InputManager.ReleaseKey(Key.X));
            AddAssert("right button press recorded to replay", () => Player.Score.Replay.Frames.OfType<OsuReplayFrame>().Any(f => f.Actions.SequenceEqual([OsuAction.RightButton])));
        }

        [Test]
        public void TestVirtualCursorRecordingPreservesOriginalPosition()
        {
            DrawableOsuRuleset drawableRuleset = null!;
            OsuInputManager inputManager = null!;
            Vector2 offCirclePosition = Vector2.Zero;
            Vector2 virtualCursorPosition = Vector2.Zero;

            seekTo(0);

            AddStep("cache osu ruleset", () =>
            {
                drawableRuleset = (DrawableOsuRuleset)Player.DrawableRuleset;
                inputManager = drawableRuleset.KeyBindingInputManager;
            });

            AddStep("move real cursor away", () =>
            {
                offCirclePosition = drawableRuleset.Playfield.ScreenSpaceDrawQuad.TopLeft + new Vector2(40);
                InputManager.MoveMouseTo(offCirclePosition);
            });
            AddAssert("real cursor stored", () => inputManager.OriginalUserCursorPosition, () => Is.EqualTo(offCirclePosition));

            AddStep("move virtual cursor to circle", () =>
            {
                virtualCursorPosition = drawableRuleset.Playfield.HitObjectContainer.AliveObjects.Single().ScreenSpaceDrawQuad.Centre;
                inputManager.MoveVirtualCursorTo(virtualCursorPosition);
            });

            AddAssert("virtual cursor active", () => inputManager.IsVirtualCursorActive);
            AddAssert("real cursor preserved", () => inputManager.OriginalUserCursorPosition, () => Is.EqualTo(offCirclePosition));
            AddAssert("gameplay cursor moved virtually", () => inputManager.CurrentState.Mouse.Position, () => Is.EqualTo(virtualCursorPosition));

            AddStep("press X", () => InputManager.PressKey(Key.X));
            seekTo(15);
            AddStep("release X", () => InputManager.ReleaseKey(Key.X));

            AddAssert("virtual position recorded to replay", () => Player.Score.Replay.Frames.OfType<OsuReplayFrame>().Any(f =>
                f.Actions.SequenceEqual([OsuAction.RightButton]) &&
                f.Position == OsuPlayfield.BASE_SIZE / 2));
        }

        [Test]
        public void TestMorasoomaEndTagIsRecordedIntoReplay()
        {
            OsuInputManager inputManager = null!;
            OsuPlayfield playfield = null!;
            Vector2 physicalScreenPosition = Vector2.Zero;
            Vector2 physicalGamefieldPosition = Vector2.Zero;

            AddStep("enable Morasooma end tag", () => LocalConfig.SetValue(OsuSetting.ForkMorasoomaEndTag, true));
            seekTo(15000);
            AddStep("move cursor to final circle", () => InputManager.MoveMouseTo(Player.DrawableRuleset.Playfield.HitObjectContainer.AliveObjects.Single()));
            AddStep("press final circle", () => InputManager.PressKey(Key.Z));
            seekTo(15015);
            AddStep("release final circle", () => InputManager.ReleaseKey(Key.Z));
            AddUntilStep("wait for score completion", () => Player.ScoreProcessor.HasCompleted.Value);
            AddStep("move physical cursor while tag is drawing", () =>
            {
                inputManager = ((DrawableOsuRuleset)Player.DrawableRuleset).KeyBindingInputManager;
                playfield = (OsuPlayfield)Player.DrawableRuleset.Playfield;
                physicalScreenPosition = playfield.GamefieldToScreenSpace(Vector2.Zero);
                physicalGamefieldPosition = playfield.ScreenSpaceToGamefield(physicalScreenPosition);
                InputManager.MoveMouseTo(physicalScreenPosition);
            });
            AddAssert("physical position is preserved", () => inputManager.OriginalUserCursorPosition, () => Is.EqualTo(physicalScreenPosition));
            AddAssert("virtual cursor retains ownership", () => inputManager.CurrentState.Mouse.Position, () => Is.Not.EqualTo(physicalScreenPosition));
            AddAssert("physical movement is excluded from smoke", () => playfield.Smoke.LastMousePosition,
                () => Is.Not.EqualTo(playfield.Smoke.ToLocalSpace(physicalScreenPosition)));
            AddUntilStep("smoke tag frames recorded", () => Player.Score.Replay.Frames
                                                                              .OfType<OsuReplayFrame>()
                                                                              .Count(frame => frame.Time > 15000 && frame.Actions.Contains(OsuAction.Smoke)) >= 5);
            AddAssert("physical movement is excluded from replay", () => Player.Score.Replay.Frames
                                                                                       .OfType<OsuReplayFrame>()
                                                                                       .Where(frame => frame.Time > 15000 && frame.Actions.Contains(OsuAction.Smoke))
                                                                                       .All(frame => frame.Position != physicalGamefieldPosition));
            AddAssert("tag records cursor movement", () => Player.Score.Replay.Frames
                                                                       .OfType<OsuReplayFrame>()
                                                                       .Where(frame => frame.Time > 15000 && frame.Actions.Contains(OsuAction.Smoke))
                                                                       .Select(frame => frame.Position)
                                                                       .Distinct()
                                                                       .Count(), () => Is.GreaterThanOrEqualTo(5));
            AddAssert("tag smoke survives legacy replay conversion", () => Player.Score.Replay.Frames
                                                                                       .OfType<OsuReplayFrame>()
                                                                                       .Where(frame => frame.Time > 15000 && frame.Actions.Contains(OsuAction.Smoke))
                                                                                       .Any(frame => frame.ToLegacy(Player.Beatmap.Value.Beatmap).Smoke));
            AddStep("disable Morasooma end tag", () => LocalConfig.SetValue(OsuSetting.ForkMorasoomaEndTag, false));
        }

        private void seekTo(double time)
        {
            AddStep($"seek to {time}ms", () => Player.GameplayClockContainer.Seek(time));
            AddUntilStep("wait for seek to finish", () => Player.DrawableRuleset.FrameStableClock.CurrentTime, () => Is.EqualTo(time).Within(500));
        }
    }
}
