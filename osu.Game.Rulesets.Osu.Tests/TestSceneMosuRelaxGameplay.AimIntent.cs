// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneMosuRelaxGameplay
    {
        [TestCase(0.75, 0.7f)]
        [TestCase(1, 0.7f)]
        [TestCase(1.5, 0.7f)]
        [TestCase(1, 2f)]
        public void TestNearPassClicksOnceWithoutGrantingAHit(double rate, float horizontalDistance)
        {
            var note = new HitCircle { Position = new Vector2(256, 192), StartTime = 1000 };
            createScene(preciseRhythmMod(), rate, note);
            moveNear(note, 1000 - 30 * rate, new Vector2(-horizontalDistance, 1.15f));
            moveNear(note, 1000 - 10 * rate, new Vector2(horizontalDistance, 1.15f));
            AddAssert("проход не вызывает преждевременный клик", () => relax.InputEvents.All(e => !e.IsPress));
            AddStep("дойти до ритма", () => clock.CurrentTime = 1000);
            AddAssert("один клик рядом с нотой", () => relax.InputEvents.Count(e => e.IsPress) == 1);
            AddAssert("нажатие мимо не получает попадание", () => judgements.All(r => !r.IsHit));
            moveNear(note, 1000 + 20 * rate, Vector2.Zero);
            AddAssert("доведение не вызывает повторный спасательный клик", () => relax.InputEvents.Count(e => e.IsPress) == 1);
            AddStep("закончить окно попадания", () => clock.CurrentTime = 1300);
            AddAssert("настоящий промах", () => judgements.Count == 1 && judgements[0].Type == HitResult.Miss);
        }

        [TestCase(true, 1.15f, false)]
        [TestCase(true, 2f, true)]
        [TestCase(false, 1.15f, true)]
        public void TestNearTapRequiresEnabledRecentNearbyMovement(bool enabled, float verticalDistance, bool moving)
        {
            var note = new HitCircle { Position = new Vector2(256, 192), StartTime = 1000 };
            OsuModMosuRelax mod = preciseRhythmMod();
            mod.AimIntentEnabled.Value = enabled;
            createScene(mod, 1, note);
            moveNear(note, 970, new Vector2(moving ? -0.7f : 0, verticalDistance));
            moveNear(note, 990, new Vector2(moving ? 0.7f : 0, verticalDistance));
            AddStep("пройти время нажатия и ожидания доведения", () => clock.CurrentTime = 1040);
            AddAssert("нет автоматического клика от одного соседства", () => relax.InputEvents.All(e => !e.IsPress));
        }

        [Test]
        public void TestOldNearPassDoesNotClickWhenRhythmArrives()
        {
            var note = new HitCircle { Position = new Vector2(256, 192), StartTime = 1000 };
            createScene(preciseRhythmMod(), 1, note);
            moveNear(note, 900, new Vector2(-0.7f, 1.15f));
            moveNear(note, 920, new Vector2(0.7f, 1.15f));
            AddStep("дойти до ритма без нового движения", () => clock.CurrentTime = 1040);
            AddAssert("давний проход не нажимает ноту", () => relax.InputEvents.All(e => !e.IsPress));
        }

        [TestCase(0.75)]
        [TestCase(1)]
        [TestCase(1.5)]
        public void TestLateNearPassDoesNotWaitForPreciseAim(double rate)
        {
            var note = new HitCircle { Position = new Vector2(256, 192), StartTime = 1000 };
            createScene(preciseRhythmMod(), rate, note);
            moveNear(note, 1000 + 10 * rate, new Vector2(-0.7f, 1.15f));
            moveNear(note, 1000 + 30 * rate, new Vector2(0.7f, 1.15f));
            AddAssert("поздний проход завершился нажатием без доведения в круг", () => relax.InputEvents.Count(e => e.IsPress) == 1);
            AddAssert("пустой клик не засчитан попаданием", () => judgements.All(r => !r.IsHit));
        }

        [Test]
        public void TestVirtualCursorCannotCreateNearTapIntent()
        {
            var note = new HitCircle { Position = new Vector2(256, 192), StartTime = 1000 };
            createScene(preciseRhythmMod(), 1, note);
            moveNear(note, 950, new Vector2(4, 0));
            AddStep("начать проход только виртуальным курсором", () =>
            {
                clock.CurrentTime = 970;
                ruleset.KeyBindingInputManager.MoveVirtualCursorTo(ruleset.Playfield.GamefieldToScreenSpace(
                    note.StackedPosition + new Vector2(-0.7f, 1.15f) * (float)note.Radius));
            });
            AddStep("закончить виртуальный проход", () =>
            {
                clock.CurrentTime = 990;
                ruleset.KeyBindingInputManager.MoveVirtualCursorTo(ruleset.Playfield.GamefieldToScreenSpace(
                    note.StackedPosition + new Vector2(0.7f, 1.15f) * (float)note.Radius));
            });
            AddAssert("реальный курсор остался далеко", () =>
                (ruleset.KeyBindingInputManager.OriginalUserCursorPosition - ruleset.Playfield.GamefieldToScreenSpace(note.StackedPosition)).Length > 100);
            AddStep("время ноты", () => clock.CurrentTime = 1040);
            AddAssert("виртуальное движение не создало намерение игрока", () => relax.InputEvents.All(e => !e.IsPress));
        }

        [Test]
        public void TestNearTapStillRespectsNotelock()
        {
            var note = new HitCircle { Position = new Vector2(256, 192), StartTime = 1000 };
            createScene(preciseRhythmMod(), 1, note);
            AddStep("заблокировать нажатие", () =>
                ruleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableHitCircle>().Single().CheckHittable = (_, _, _) => ClickAction.Ignore);
            moveNear(note, 970, new Vector2(-0.7f, 1.15f));
            moveNear(note, 990, new Vector2(0.7f, 1.15f));
            AddStep("время клика", () => clock.CurrentTime = 1000);
            AddAssert("намерение не обходит notelock", () => relax.InputEvents.All(e => !e.IsPress));
            AddStep("снять блокировку", () =>
                ruleset.Playfield.HitObjectContainer.AliveObjects.OfType<DrawableHitCircle>().Single().CheckHittable = (_, _, _) => ClickAction.Hit);
            AddAssert("обычный клик после снятия блокировки", () => relax.InputEvents.Count(e => e.IsPress) == 1);
            AddAssert("блокировка не превращает промах в попадание", () => judgements.All(r => !r.IsHit));
        }

        [Test]
        public void TestDisablingNearTapDiscardsPendingIntent()
        {
            var note = new HitCircle { Position = new Vector2(256, 192), StartTime = 1000 };
            OsuModMosuRelax mod = preciseRhythmMod();
            createScene(mod, 1, note);
            moveNear(note, 970, new Vector2(-0.7f, 1.15f));
            moveNear(note, 990, new Vector2(0.7f, 1.15f));
            AddStep("выключить нажатия рядом", () => mod.AimIntentEnabled.Value = false);
            AddStep("время ноты", () => clock.CurrentTime = 1000);
            AddStep("снова включить без нового движения", () => mod.AimIntentEnabled.Value = true);
            AddAssert("старое намерение не восстановилось", () => relax.InputEvents.All(e => !e.IsPress));
        }

        [Test]
        public void TestNearMissDoesNotBlockNextNote()
        {
            var first = new HitCircle { Position = new Vector2(150, 192), StartTime = 1000 };
            var second = new HitCircle { Position = new Vector2(350, 192), StartTime = 1080 };
            createScene(preciseRhythmMod(), 1, first, second);
            moveNear(first, 970, new Vector2(-0.7f, 1.15f));
            moveNear(first, 990, new Vector2(0.7f, 1.15f));
            AddStep("клик мимо первой ноты", () => clock.CurrentTime = 1000);
            AddAssert("первая попытка состоялась", () => relax.InputEvents.Count(e => e.IsPress) == 1);
            moveNear(second, 1060, Vector2.Zero);
            AddStep("время следующей ноты", () => clock.CurrentTime = 1080);
            AddAssert("следующая нота нажата без ожидания предыдущего промаха", () => relax.InputEvents.Count(e => e.IsPress) == 2);
            AddAssert("первая пропущена, вторая попала", () => judgements.Count == 2 && !judgements[0].IsHit && judgements[1].IsHit);
        }

        private void moveNear(OsuHitObject note, double time, Vector2 radiusOffset)
            => AddStep("провести курсор игрока", () =>
            {
                clock.CurrentTime = time;
                InputManager.MoveMouseTo(ruleset.Playfield.GamefieldToScreenSpace(note.StackedPosition + radiusOffset * (float)note.Radius));
            });

        private static OsuModMosuRelax preciseRhythmMod() => new OsuModMosuRelax
        {
            BaseOffset = { Value = 0 },
            TimingVariance = { Value = 0 },
            DynamicDrift = { Value = 0 },
        };
    }
}
