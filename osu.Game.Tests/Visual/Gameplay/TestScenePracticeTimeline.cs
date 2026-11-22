using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Play;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Gameplay
{
    [TestFixture]
    public partial class TestScenePracticeTimeline : OsuManualInputManagerTestScene
    {
        [Test]
        public void ClickSelectsTimeAndHiddenTimelineDoesNotSelect()
        {
            PracticeTimeline timeline = null!;
            double? selected = null;
            AddStep("create timeline", () =>
            {
                var beatmap = new Beatmap();
                beatmap.HitObjects.Add(new HitObject { StartTime = 1000 });
                beatmap.HitObjects.Add(new HitObject { StartTime = 11000 });
                Add(timeline = new PracticeTimeline(beatmap, time => selected = time)
                {
                    RelativeSizeAxes = Axes.None,
                    Width = 600,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                });
                timeline.Show();
            });
            AddUntilStep("density peaks present", () => timeline.ChildrenOfType<SquareGraph.Column>().Any(c => c.Filled > 0));
            AddRepeatStep("move timeline vertically", () => timeline.Y += 2, 10);
            AddAssert("density peaks remain during movement", () => timeline.ChildrenOfType<SquareGraph.Column>().Any(c => c.Filled > 0));
            AddStep("click centre", () =>
            {
                InputManager.MoveMouseTo(timeline);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("selected midpoint", () => selected == 6000);
            AddStep("hide timeline", () =>
            {
                selected = null;
                timeline.Hide();
            });
            AddStep("click hidden timeline", () => InputManager.Click(MouseButton.Left));
            AddAssert("no selection", () => selected == null);
        }
    }
}
