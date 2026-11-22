using System.Linq;
using NUnit.Framework;
using osu.Framework.Testing;
using osu.Game.Rulesets.Dodge.Edit;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    public partial class TestSceneDodgeEditor
    {
        [Test]
        public void TestApplyGroupLoopUsesBeatPeriodAndUndoRestoresOriginals()
        {
            var first = new DodgeBullet { StartTime = 1000, Duration = 250, Position = new Vector2(100), EndPosition = new Vector2(300) };
            var second = new DodgeBullet { StartTime = 1500, Duration = 500, Position = new Vector2(200), EndPosition = new Vector2(400) };
            AddStep("add selected pair", () =>
            {
                EditorBeatmap.AddRange(new[] { first, second });
                EditorBeatmap.SelectedHitObjects.AddRange(new[] { first, second });
            });
            AddStep("apply translated circle", () =>
            {
                var tools = this.ChildrenOfType<DodgeLoopToolboxGroup>().Single();
                tools.Shape.Value = DodgeLoopToolboxGroup.LoopShape.Clockwise;
                tools.Scope.Value = DodgeLoopToolboxGroup.LoopScope.TranslatePattern;
                tools.PeriodBeats.Value = 4;
                tools.Repetitions.Value = 3;
                tools.OffsetX.Value = 128;
                tools.OffsetY.Value = 0;
                tools.ApplyLoop();
            });
            AddAssert("period is four beats repeated three times", () => first.Duration == 6000 && second.Duration == 6000);
            AddAssert("same group translation at shared time", () =>
                ((second.PositionAt(2500) - first.PositionAt(2500)) - new Vector2(100)).Length, () => Is.LessThan(0.001));
            AddStep("undo loop application", () => Editor.Undo());
            AddUntilStep("original duration and movement restored", () => EditorBeatmap.HitObjects.OfType<DodgeBullet>()
                .All(b => b.MovementType == DodgeMovementType.Linear && b.Duration is 250 or 500));
            AddStep("redo loop application", () => Editor.Redo());
            AddUntilStep("loops restored", () => EditorBeatmap.HitObjects.OfType<DodgeBullet>()
                .All(b => b.MovementType == DodgeMovementType.OrbitClockwise && b.Duration == 6000));
        }

        [Test]
        public void TestWholeEmitterPatternStaysEditableAndUndoesTogether()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 1000, Duration = 1000, Position = new Vector2(256, 192),
                AimPosition = new Vector2(356, 192), BulletCount = 8, BurstCount = 1, SpreadAngle = 360,
            };
            AddStep("add selected ring emitter", () =>
            {
                EditorBeatmap.Add(emitter);
                EditorBeatmap.SelectedHitObjects.Add(emitter);
            });
            AddStep("apply whole-pattern rotation", () =>
            {
                var tools = this.ChildrenOfType<DodgeLoopToolboxGroup>().Single();
                tools.Scope.Value = DodgeLoopToolboxGroup.LoopScope.RotatePattern;
                tools.Shape.Value = DodgeLoopToolboxGroup.LoopShape.Clockwise;
                tools.ApplyLoop();
            });
            AddAssert("same emitter remains selected alone", () => EditorBeatmap.HitObjects.Single() == emitter
                && EditorBeatmap.SelectedHitObjects.Single() == emitter && !EditorBeatmap.HitObjects.OfType<DodgeBullet>().Any());
            AddAssert("ring radius preserved while rotating", () => Enumerable.Range(0, emitter.EffectiveBulletCount)
                .All(ray => System.Math.Abs((emitter.PositionAt(0, ray, 1250) - emitter.Position).Length - 100) < 0.01));
            AddStep("edit fan after applying loop", () =>
            {
                EditorBeatmap.BeginChange();
                emitter.BulletCount = 12;
                emitter.AimPosition = emitter.Position + new Vector2(150, 0);
                EditorBeatmap.Update(emitter);
                EditorBeatmap.EndChange();
            });
            AddAssert("edited emitter generates new ring geometry", () => emitter.EffectiveBulletCount == 12
                && Enumerable.Range(0, 12).All(ray => System.Math.Abs((emitter.PositionAt(0, ray, 1250) - emitter.Position).Length - 150) < 0.01));
            AddStep("undo fan edit", () => Editor.Undo());
            AddStep("undo loop", () => Editor.Undo());
            AddUntilStep("original emitter restored alone", () => EditorBeatmap.HitObjects.Count == 1
                && EditorBeatmap.HitObjects.Single() is DodgeEmitter { MovementType: DodgeMovementType.Linear, BulletCount: 8 });
        }
    }
}
