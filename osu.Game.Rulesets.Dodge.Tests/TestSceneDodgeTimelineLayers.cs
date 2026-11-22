using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Rulesets.Dodge.Edit.Blueprints;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Tests
{
    public partial class TestSceneDodgeEditor
    {
        [Test]
        public void TestEachTypeKeepsItsObjectLayersWithoutFiltering()
        {
            var bullets = Enumerable.Range(0, 8).Select(i => new DodgeBullet
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(64, 48 + i * 32),
                EndPosition = new Vector2(400, 48 + i * 32),
            }).ToArray();
            var emitters = Enumerable.Range(0, 3).Select(i => new DodgeEmitter
            {
                StartTime = 1000,
                Duration = 1000,
                Position = new Vector2(64 + i * 64, 32),
                AimPosition = new Vector2(256, 300),
            }).ToArray();
            AddStep("add overlapping types", () => EditorBeatmap.AddRange(bullets.Cast<DodgeHitObject>().Concat(emitters)));
            AddStep("seek to objects", () => EditorClock.Seek(1000));
            AddUntilStep("all markers visible", () => markers().Length == bullets.Length + emitters.Length
                                                       && markers().All(b => b.IsGroupMarkerVisible));
            AddAssert("bullet layers distinct", () => markers().Where(b => b.Item is DodgeBullet)
                .Select(b => MathF.Round(b.ScreenSpaceSelectionPoint.Y, 3)).Distinct().Count(), () => Is.EqualTo(bullets.Length));
            AddAssert("emitter layers distinct", () => markers().Where(b => b.Item is DodgeEmitter)
                .Select(b => MathF.Round(b.ScreenSpaceSelectionPoint.Y, 3)).Distinct().Count(), () => Is.EqualTo(emitters.Length));
            AddAssert("types remain separated", () => markers().Where(b => b.Item is DodgeBullet).Max(b => b.Y)
                                                      < markers().Where(b => b.Item is DodgeEmitter).Min(b => b.Y));
            AddAssert("all layers stay inside timeline", () =>
            {
                var bounds = this.ChildrenOfType<Timeline>().Single().ScreenSpaceDrawQuad.AABBFloat;
                return markers().All(b => bounds.Contains(b.ScreenSpaceSelectionPoint));
            });

            LaneTimelineHitObjectBlueprint[] markers() => this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                .Where(b => bullets.Contains(b.Item) || emitters.Contains(b.Item)).ToArray();
        }

        [Test]
        public void TestFieldSelectedBulletCanBeMovedOnTimelineWithoutChangingSelection()
        {
            var bullets = Enumerable.Range(0, 8).Select(i => new DodgeBullet
            {
                StartTime = 1000,
                Duration = 2000,
                Position = new Vector2(64, 48 + i * 32),
                EndPosition = new Vector2(400, 48 + i * 32),
                ContinueUntilExit = true,
            }).ToArray();
            DodgeBullet selected = bullets[3];
            Vector2 dragStart = Vector2.Zero;
            AddStep("add same-time bullets", () => EditorBeatmap.AddRange(bullets));
            AddStep("seek to starts", () => EditorClock.Seek(1000));
            AddUntilStep("field marker ready", () => this.ChildrenOfType<DodgeBulletSelectionBlueprint>()
                .Any(b => b.HitObject == selected));
            AddStep("select bullet on field", () =>
            {
                var field = this.ChildrenOfType<DodgeBulletSelectionBlueprint>().Single(b => b.HitObject == selected);
                InputManager.MoveMouseTo(field.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("chosen field object selected", () => EditorBeatmap.SelectedHitObjects.SingleOrDefault(), () => Is.SameAs(selected));
            AddUntilStep("selected timeline row visible", () => marker().IsGroupMarkerVisible && marker().IsSelected);
            AddStep("click selected timeline body", () =>
            {
                InputManager.MoveMouseTo(marker().ScreenSpaceSelectionPoint + new Vector2(25, 0));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("click does not cycle selection", () => EditorBeatmap.SelectedHitObjects.SingleOrDefault(), () => Is.SameAs(selected));
            AddStep("begin move selected timeline body", () =>
            {
                dragStart = marker().ScreenSpaceSelectionPoint + new Vector2(25, 0);
                InputManager.MoveMouseTo(dragStart);
                InputManager.PressButton(MouseButton.Left);
            });
            AddStep("drag later", () => InputManager.MoveMouseTo(dragStart + new Vector2(180, 0)));
            AddStep("finish move", () => InputManager.ReleaseButton(MouseButton.Left));
            AddUntilStep("only chosen bullet time moved", () => selected.StartTime > 1000
                                                                       && bullets.Where(b => b != selected).All(b => b.StartTime == 1000));
            AddAssert("selection remains chosen bullet", () => EditorBeatmap.SelectedHitObjects.SingleOrDefault(), () => Is.SameAs(selected));
            AddAssert("flight duration unchanged", () => selected.Duration, () => Is.EqualTo(2000));
            AddAssert("timeline end is authored end", () => marker().TimelineEndTime, () => Is.EqualTo(selected.EndTime));

            LaneTimelineHitObjectBlueprint marker() => this.ChildrenOfType<LaneTimelineHitObjectBlueprint>().Single(b => b.Item == selected);
        }
    }
}
