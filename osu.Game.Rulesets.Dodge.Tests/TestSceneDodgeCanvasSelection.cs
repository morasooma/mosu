using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Dodge.Edit.Blueprints;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Tests
{
    public partial class TestSceneDodgeEditor
    {
        [Test]
        public void TestExpiredBulletCannotBeSelectedAtReusedPosition()
        {
            var old = new DodgeBullet
            {
                StartTime = 1000, Duration = 250,
                Position = new Vector2(100, 100), EndPosition = new Vector2(400, 100),
            };
            var current = new DodgeBullet
            {
                StartTime = 2000, Duration = 1000,
                Position = old.Position, EndPosition = old.EndPosition,
            };
            AddStep("add old and current bullets", () => EditorBeatmap.AddRange(new[] { old, current }));
            AddStep("seek to new bullet", () => EditorClock.Seek(2000));
            AddUntilStep("current selectable but old excluded", () => blueprint(current).IsSelectable && !blueprint(old).IsSelectable);
            AddStep("click reused starting position", () =>
            {
                var playfield = this.ChildrenOfType<DodgePlayfield>().Single();
                InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(current.Position));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("only current bullet selected", () => EditorBeatmap.SelectedHitObjects.SingleOrDefault(), () => Is.SameAs(current));
            AddStep("clear selection", () => EditorBeatmap.SelectedHitObjects.Clear());
            AddStep("seek back to old bullet", () => EditorClock.Seek(1000));
            AddUntilStep("old becomes selectable again", () => blueprint(old).IsSelectable);

            DodgeBulletSelectionBlueprint blueprint(DodgeBullet bullet) => this.ChildrenOfType<DodgeBulletSelectionBlueprint>().Single(b => b.HitObject == bullet);
        }

        [Test]
        public void TestMovingBulletDoesNotLeaveSelectionTargetAtSource()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1000, Duration = 2000,
                Position = new Vector2(100, 100), EndPosition = new Vector2(400, 100),
            };
            AddStep("add moving bullet", () => EditorBeatmap.Add(bullet));
            AddStep("seek halfway through flight", () => EditorClock.Seek(2000));
            AddUntilStep("bullet selectable", () => blueprint().IsSelectable);
            AddAssert("source is no longer a click target", () => blueprint().ReceivePositionalInputAt(screen(bullet.Position)), () => Is.False);
            AddAssert("current bullet is a click target", () => blueprint().ReceivePositionalInputAt(screen(new Vector2(250, 100))));
            AddStep("click old source", () =>
            {
                InputManager.MoveMouseTo(screen(bullet.Position));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("old source selects nothing", () => EditorBeatmap.SelectedHitObjects, () => Is.Empty);
            AddStep("box-select current position", () =>
            {
                InputManager.MoveMouseTo(screen(new Vector2(220, 70)));
                InputManager.PressButton(MouseButton.Left);
            });
            AddStep("drag box around moving bullet", () => InputManager.MoveMouseTo(screen(new Vector2(280, 130))));
            AddStep("complete box", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("box selects moving bullet", () => EditorBeatmap.SelectedHitObjects.SingleOrDefault(), () => Is.SameAs(bullet));

            Vector2 screen(Vector2 position) => this.ChildrenOfType<DodgePlayfield>().Single().GamefieldToScreenSpace(position);
            DodgeBulletSelectionBlueprint blueprint() => this.ChildrenOfType<DodgeBulletSelectionBlueprint>().Single(b => b.HitObject == bullet);
        }

        [Test]
        public void TestExpiredEmitterCannotBeCapturedByFieldDragBox()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000, Duration = 250, Position = new Vector2(100, 100),
                AimPosition = new Vector2(400, 100), BurstCount = 1,
            };
            var current = new DodgeBullet
            {
                StartTime = 2000, Duration = 1000,
                Position = emitter.Position, EndPosition = emitter.AimPosition,
            };
            AddStep("add old emitter and new bullet", () =>
            {
                EditorBeatmap.Add(emitter);
                EditorBeatmap.Add(current);
            });
            AddStep("seek after emitter flight", () => EditorClock.Seek(2000));
            AddUntilStep("emitter no longer selectable", () => !this.ChildrenOfType<DodgeEmitterSelectionBlueprint>().Single().IsSelectable);
            AddStep("start box around reused source", () =>
            {
                InputManager.MoveMouseTo(screen(new Vector2(70, 70)));
                InputManager.PressButton(MouseButton.Left);
            });
            AddStep("drag selection box", () => InputManager.MoveMouseTo(screen(new Vector2(130, 130))));
            AddStep("complete selection box", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("only current bullet captured", () => EditorBeatmap.SelectedHitObjects.SingleOrDefault(), () => Is.SameAs(current));

            Vector2 screen(Vector2 position) => this.ChildrenOfType<DodgePlayfield>().Single().GamefieldToScreenSpace(position);
        }
    }
}
