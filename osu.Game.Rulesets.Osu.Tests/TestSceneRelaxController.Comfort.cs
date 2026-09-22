// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Timing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneRelaxController
    {
        [Test]
        public void TestComfortKeepsLongSpinnerHeldWithoutMouseMovement()
        {
            ManualClock clock = prepareManualRelax();
            DrawableSpinner spinner = null!;
            AddStep("добавить длинный спиннер", () => replaceHitObjects(spinner = createSpinner(1000, 2400)));
            AddUntilStep("спиннер загружен", () => spinner.IsLoaded);
            configureRelax();
            AddStep("начать спиннер", () => clock.CurrentTime = 1000);
            AddAssert("клавиша нажата", () => pressCount() == 1);
            AddStep("не двигать мышь больше полутора секунд", () => clock.CurrentTime = 3000);
            AddAssert("удержание не сброшено таймером бездействия", () => relaxController.InputEvents.Count == 1);
            AddStep("завершить спиннер", () => clock.CurrentTime = 3410);
            AddAssert("одно отпускание после окончания", () => relaxController.InputEvents.Count == 2 && firstRelease().Time >= spinner.HitObject.EndTime);
        }

        [Test]
        public void TestComfortKeepsLongSliderHeldWithoutMouseMovement()
        {
            ManualClock clock = prepareManualRelax();
            DrawableSlider slider = null!;
            AddStep("добавить длинный слайдер", () => replaceHitObjects(slider = createSlider(new Vector2(256, 192), 1000, 2400, new Vector2(1, 0))));
            AddUntilStep("голова загружена", () => slider.HeadCircle.IsLoaded);
            configureRelax();
            AddStep("навести на голову", () => InputManager.MoveMouseTo(slider.HeadCircle.ScreenSpaceDrawQuad.Centre));
            AddStep("начать слайдер", () => clock.CurrentTime = 1000);
            AddAssert("голова нажата", () => slider.HeadCircle.IsHit && pressCount() == 1);
            AddStep("продолжать удержание", () => clock.CurrentTime = 3000);
            AddAssert("удержание не сброшено таймером бездействия", () => relaxController.InputEvents.Count == 1);
            AddStep("пройти хвост слайдера", () => clock.CurrentTime = 3500);
            AddAssert("отпускание только после хвоста", () => relaxController.InputEvents.Count == 2 && firstRelease().Time >= slider.HitObject.EndTime);
        }

        [Test]
        public void TestComfortUsesFreeFingerForCirclesDuringSliderHold()
        {
            ManualClock clock = prepareManualRelax();
            DrawableSlider slider = null!;
            DrawableHitCircle first = null!;
            DrawableHitCircle second = null!;
            AddStep("слайдер и две пересекающиеся по времени ноты", () => replaceHitObjects(
                slider = createSlider(new Vector2(256, 192), 1000, 600, new Vector2(100, 0)),
                first = createCircle(new Vector2(128, 100), 1150),
                second = createCircle(new Vector2(384, 100), 1230)));
            AddUntilStep("цели загружены", () => slider.HeadCircle.IsLoaded && first.IsLoaded && second.IsLoaded);
            configureRelax(alternateThreshold: 0, stableBpm: 300);
            AddStep("навести на голову", () => InputManager.MoveMouseTo(slider.HeadCircle.ScreenSpaceDrawQuad.Centre));
            AddStep("нажать голову", () => clock.CurrentTime = 1000);
            AddAssert("голова нажата", () => slider.HeadCircle.IsHit);
            AddStep("время первой ноты", () => clock.CurrentTime = 1150);
            AddStep("навести на первую ноту", () => InputManager.MoveMouseTo(first.ScreenSpaceDrawQuad.Centre));
            AddAssert("первая нота нажата", () => first.IsHit);
            AddStep("отпустить палец первой ноты", () => clock.CurrentTime = 1200);
            AddStep("время второй ноты", () => clock.CurrentTime = 1230);
            AddStep("навести на вторую ноту", () => InputManager.MoveMouseTo(second.ScreenSpaceDrawQuad.Centre));
            AddAssert("вторая нота нажата без задержки", () => second.IsHit && pressCount() == 3);
            AddAssert("слайдер удерживается другим пальцем", () =>
                relaxController.InputEvents.Where(e => e.IsPress).Skip(1).All(e => e.Action != firstPress().Action)
                && !relaxController.InputEvents.Any(e => !e.IsPress && e.TargetStartTime == slider.HitObject.StartTime));
        }

        [Test]
        public void TestComfortDisablingRelaxReleasesHeldAction()
        {
            ManualClock clock = prepareManualRelax();
            configureRelax(holdTime: 90);
            AddStep("навести курсор", () => InputManager.MoveMouseTo(hitCircle.ScreenSpaceDrawQuad.Centre));
            AddStep("нажать ноту", () => clock.CurrentTime = 1000);
            AddAssert("клавиша нажата", () => osuInputManager.PressedActions.Contains(OsuAction.LeftButton));
            AddStep("отключить релакс", () => config.SetValue(OsuSetting.ForkRelaxEnabled, false));
            AddAssert("залипших клавиш нет", () => !osuInputManager.PressedActions.Any() && osuInputManager.AllowGameplayInputs);
            AddStep("пройти старое время отпускания", () => clock.CurrentTime = 1150);
            AddAssert("новых нажатий нет", () => pressCount() == 1);
        }
    }
}
