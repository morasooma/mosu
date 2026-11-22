using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Audio;
using osuTK;
using osuTK.Input;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit.Compose.Components.Timeline;

namespace osu.Game.Tests.Visual.Editing
{
    public partial class TestSceneHitSoundTrackEditor : TimelineTestScene
    {
        public override Drawable CreateTestComponent() => new TimelineBlueprintContainer(Composer);

        protected override bool EnableHitSoundTracks => true;

        [SetUpSteps]
        public void ClearObjects()
        {
            AddStep("clear objects", () =>
            {
                EditorBeatmap.Clear();
                if (TimelineArea.Timeline.HitSoundTracksExpanded)
                    TimelineArea.Timeline.ToggleHitSoundTracks();
            });
        }

        [Test]
        public void TestSoundsButtonBesideZoom()
        {
            AddAssert("sounds left of zoom without overlap", () =>
            {
                var sounds = TimelineArea.ChildrenOfType<Drawable>().Single(d => d.Name == "Toggle hit sound tracks");
                var zoom = TimelineArea.ChildrenOfType<Drawable>().Single(d => d.Name == "Zoom controls");
                return sounds.ScreenSpaceDrawQuad.AABBFloat.Right <= zoom.ScreenSpaceDrawQuad.AABBFloat.Left;
            });
            AddStep("click sounds button", () =>
            {
                InputManager.MoveMouseTo(TimelineArea.ChildrenOfType<Drawable>().Single(d => d.Name == "Toggle hit sound tracks"));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("tracks expanded", () => TimelineArea.Timeline.HitSoundTracksExpanded);
            AddStep("click sounds again", () =>
            {
                InputManager.MoveMouseTo(TimelineArea.ChildrenOfType<Drawable>().Single(d => d.Name == "Toggle hit sound tracks"));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("tracks collapsed", () => !TimelineArea.Timeline.HitSoundTracksExpanded);
        }

        [Test]
        public void TestSliderNodesRebuilt()
        {
            var slider = new Slider { StartTime = 2500, RepeatCount = 1 };
            AddStep("add slider", () => EditorBeatmap.Add(slider));
            AddStep("expand sounds", () => TimelineArea.Timeline.ToggleHitSoundTracks());
            AddUntilStep("body and three nodes", () => this.ChildrenOfType<HitSoundTrackEditor>().Single().Children.Count == 4);
            AddStep("increase repeats", () => { EditorBeatmap.BeginChange(); slider.RepeatCount = 3; EditorBeatmap.Update(slider); EditorBeatmap.EndChange(); });
            AddUntilStep("body and five nodes", () => this.ChildrenOfType<HitSoundTrackEditor>().Single().Children.Count == 6);
            AddStep("decrease repeats", () => { EditorBeatmap.BeginChange(); slider.RepeatCount = 0; EditorBeatmap.Update(slider); EditorBeatmap.EndChange(); });
            AddUntilStep("body and two nodes", () => this.ChildrenOfType<HitSoundTrackEditor>().Single().Children.Count == 3);
            AddStep("remove slider", () => EditorBeatmap.Remove(slider));
            AddStep("collapse sounds", () => TimelineArea.Timeline.ToggleHitSoundTracks());
        }

        [Test]
        public void TestExpandAndObjectUpdates()
        {
            var circle = new HitCircle { StartTime = 2500 };
            AddStep("add circle", () => EditorBeatmap.Add(circle));
            AddStep("expand sounds", () => TimelineArea.Timeline.ToggleHitSoundTracks());
            AddAssert("expanded", () => TimelineArea.Timeline.Height > 80);
            AddUntilStep("one sample column", () => this.ChildrenOfType<HitSoundTrackEditor>().Single().Children.Count == 1);
            AddStep("toggle whistle", () =>
            {
                var column = this.ChildrenOfType<HitSoundTrackEditor>().Single().Children.Single();
                InputManager.MoveMouseTo(column.ToScreenSpace(new Vector2(9, 10)));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("whistle enabled", () => circle.Samples.Any(s => s.Name == HitSampleInfo.HIT_WHISTLE));
            AddWaitStep("wait for updated controls", 3);
            AddStep("set volume", () =>
            {
                var column = this.ChildrenOfType<HitSoundTrackEditor>().Single().Children.Single();
                InputManager.MoveMouseTo(column.ToScreenSpace(new Vector2(9, 120)));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("volume 50", () => circle.Samples.All(s => s.Volume == 50));
            AddStep("move circle", () => { circle.StartTime = 3000; EditorBeatmap.Update(circle); });
            AddStep("remove circle", () => EditorBeatmap.Remove(circle));
            AddUntilStep("columns removed", () => this.ChildrenOfType<HitSoundTrackEditor>().Single().Children.Count == 0);
            AddStep("collapse", () => TimelineArea.Timeline.ToggleHitSoundTracks());
            AddAssert("collapsed", () => TimelineArea.Timeline.Height == 80);
        }
    }
}
