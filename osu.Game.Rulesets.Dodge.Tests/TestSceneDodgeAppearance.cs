using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Rulesets.Dodge.Edit;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    public partial class TestSceneDodgeEditor
    {
        [Test]
        public void TestPasteAppearanceChangesOnlyVisualsAndUndoesAsOneAction()
        {
            var source = new DodgeBullet
            {
                StartTime = 1000, Duration = 300, Position = new Vector2(50), EndPosition = new Vector2(400),
                Colour = Colour4.Red, OutlineColour = Colour4.Blue, Opacity = 0.4f,
                OutlineThickness = 3, Shape = DodgeBulletShape.Triangle,
            };
            var bullet = new DodgeBullet
            {
                StartTime = 2000, Duration = 900, Position = new Vector2(80), EndPosition = new Vector2(300),
                Shape = DodgeBulletShape.Square, ContinueUntilExit = true,
            };
            var emitter = new DodgeEmitter
            {
                StartTime = 3000, Duration = 700, Position = new Vector2(90), AimPosition = new Vector2(350),
                BulletCount = 7, BurstCount = 3, SpreadAngle = 120,
            };
            AddStep("add source and targets", () =>
            {
                EditorBeatmap.AddRange(new DodgeHitObject[] { source, bullet, emitter });
                EditorBeatmap.SelectedHitObjects.Add(source);
            });
            AddStep("copy source appearance", () => composer().CopyAppearance());
            AddStep("select two targets", () =>
            {
                EditorBeatmap.SelectedHitObjects.Clear();
                EditorBeatmap.SelectedHitObjects.AddRange(new DodgeHitObject[] { bullet, emitter });
            });
            AddStep("paste appearance", () => composer().PasteAppearance());
            AddAssert("both targets receive source visuals", () =>
                bullet.Colour == Colour4.Red && emitter.Colour == Colour4.Red
                && bullet.Shape == DodgeBulletShape.Triangle && emitter.Shape == DodgeBulletShape.Triangle
                && bullet.OutlineColour == Colour4.Blue && emitter.Opacity == 0.4f && emitter.OutlineThickness == 3);
            AddAssert("timing positions and pattern unchanged", () =>
                bullet.StartTime == 2000 && bullet.Duration == 900 && bullet.Position == new Vector2(80)
                && bullet.EndPosition == new Vector2(300) && bullet.ContinueUntilExit
                && emitter.StartTime == 3000 && emitter.Duration == 700 && emitter.Position == new Vector2(90)
                && emitter.AimPosition == new Vector2(350) && emitter.BulletCount == 7 && emitter.BurstCount == 3 && emitter.SpreadAngle == 120);
            AddStep("undo single paste", () => Editor.Undo());
            AddUntilStep("both target styles restored", () =>
                EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single(b => b.StartTime == 2000).Shape == DodgeBulletShape.Square
                && EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().Colour == Colour4.White);
            AddStep("redo single paste", () => Editor.Redo());
            AddUntilStep("both target styles reapplied", () =>
                EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single(b => b.StartTime == 2000).Shape == DodgeBulletShape.Triangle
                && EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().Colour == Colour4.Red);

            DodgeHitObjectComposer composer() => this.ChildrenOfType<DodgeHitObjectComposer>().Single();
        }

        [Test]
        public void TestAppearanceClipboardIsSnapshotAndPreservesIncompatibleFields()
        {
            var source = new DodgeArenaChange { Colour = Colour4.Red, BorderOpacity = 0.3f };
            var snapshot = DodgeObjectAppearance.Capture(source);
            source.Colour = Colour4.Blue;
            var bullet = new DodgeBullet { Shape = DodgeBulletShape.Square };
            snapshot.ApplyTo(bullet);
            var arena = new DodgeArenaChange { TargetSize = new Vector2(256), TargetRotation = 25 };
            snapshot.ApplyTo(arena);
            AddAssert("snapshot does not track source mutations", () => bullet.Colour, () => Is.EqualTo(Colour4.Red));
            AddAssert("arena style does not replace bullet shape", () => bullet.Shape, () => Is.EqualTo(DodgeBulletShape.Square));
            AddAssert("arena border copied without geometry", () => arena.BorderOpacity == 0.3f && arena.TargetSize == new Vector2(256) && arena.TargetRotation == 25);
        }
    }
}
