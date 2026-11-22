using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Mania;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components.Timelines.Summary;
using osu.Game.Screens.Edit.Components.Timelines.Summary.Parts;
using osu.Game.Screens.Edit.Timing;
using osuTK;

namespace osu.Game.Tests.Visual.Editing
{
    [TestFixture]
    public partial class TestSceneTimingControlPointDisplay : EditorClockTestScene
    {
        [Cached]
        private readonly EditorBeatmap editorBeatmap;

        private SummaryTimeline timeline = null!;

        public TestSceneTimingControlPointDisplay()
        {
            editorBeatmap = new EditorBeatmap(CreateBeatmap(new ManiaRuleset().RulesetInfo));
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create timeline", () =>
            {
                editorBeatmap.ControlPointInfo.Clear();
                editorBeatmap.ControlPointInfo.GroupAt(1000, true).Add(new TimingControlPoint());
                editorBeatmap.ControlPointInfo.GroupAt(2000, true).Add(new EffectControlPoint { KiaiMode = true });
                editorBeatmap.ControlPointInfo.GroupAt(3000, true).Add(new EffectControlPoint());
                editorBeatmap.ControlPointInfo.GroupAt(4000, true).Add(new TimingControlPoint { BeatLength = 600 });
                Beatmap.Value = CreateWorkingBeatmap(editorBeatmap.PlayableBeatmap);
                Child = timeline = new SummaryTimeline
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(800, 50)
                };
            });
            AddUntilStep("initial markers loaded", () => timeline.ChildrenOfType<ControlPointVisualisation>().Count(), () => Is.EqualTo(4));
        }

        [Test]
        public void TestTimelineMarkersFollowUndoRedo()
        {
            BeatmapEditorChangeHandler changeHandler = null!;
            AddStep("move effects in editor transaction", () =>
            {
                changeHandler = new BeatmapEditorChangeHandler(editorBeatmap);
                changeHandler.SaveState();
                var primary = new Bindable<ControlPointGroup?>();
                var selection = new ControlPointSelection(primary);
                selection.Select(editorBeatmap.ControlPointInfo.GroupAt(2000), editorBeatmap.ControlPointInfo.Groups);
                selection.Select(editorBeatmap.ControlPointInfo.GroupAt(3000), editorBeatmap.ControlPointInfo.Groups, toggle: true);
                selection.ChangeTime(editorBeatmap, 3010, false);
            });
            assertMarkerTimes(1000, 2010, 3010, 4000);
            AddStep("undo offsets", () => changeHandler.RestoreState(-1));
            assertMarkerTimes(1000, 2000, 3000, 4000);
            AddStep("redo offsets", () => changeHandler.RestoreState(1));
            assertMarkerTimes(1000, 2010, 3010, 4000);
        }

        [Test]
        public void TestPopulatingNewGroupNearExistingMarker()
        {
            AddStep("add empty group next to a marker", () => editorBeatmap.ControlPointInfo.GroupAt(2010, true));
            AddStep("populate group with effect", () => editorBeatmap.ControlPointInfo.GroupAt(2010).Add(new EffectControlPoint { ScrollSpeed = 2 }));
            assertMarkerTimes(1000, 2000, 2010, 3000, 4000);
        }

        [Test]
        public void TestMovingEffectsKeepsTimelineMarkers()
        {
            AddStep("move both effects", () => TimingSectionAdjustments.MoveControlPointGroups(editorBeatmap,
                new[] { editorBeatmap.ControlPointInfo.GroupAt(2000), editorBeatmap.ControlPointInfo.GroupAt(3000) }, 10, false));
            assertMarkerTimes(1000, 2010, 3010, 4000);
            AddStep("move effects again", () => TimingSectionAdjustments.MoveControlPointGroups(editorBeatmap,
                new[] { editorBeatmap.ControlPointInfo.GroupAt(2010), editorBeatmap.ControlPointInfo.GroupAt(3010) }, -20, false));
            assertMarkerTimes(1000, 1990, 2990, 4000);
        }

        [Test]
        public void TestMovingTimingKeepsDependentEffectMarkers()
        {
            AddStep("move timing and related effects", () => TimingSectionAdjustments.MoveControlPointGroups(editorBeatmap,
                new[] { editorBeatmap.ControlPointInfo.GroupAt(1000) }, 10, false));
            assertMarkerTimes(1010, 2010, 3010, 4000);
        }

        private void assertMarkerTimes(params double[] times)
        {
            AddUntilStep("markers follow control points", () => timeline.ChildrenOfType<ControlPointVisualisation>().Select(v => v.StartTime).Order().ToArray(),
                () => Is.EqualTo(times));
            AddAssert("kiai bar follows effect offsets", () =>
            {
                var section = timeline.ChildrenOfType<KiaiPart>().Single().Children.Single();
                double start = editorBeatmap.ControlPointInfo.EffectPoints.First(p => p.KiaiMode).Time;
                double end = editorBeatmap.ControlPointInfo.EffectPoints.First(p => !p.KiaiMode && p.Time > start).Time;
                return section.X == start && section.Width == Math.Max(200, end - start);
            });
            AddAssert("marker groups have correct positions", () => timeline.ChildrenOfType<GroupVisualisation>()
                .Where(v => v.ChildrenOfType<ControlPointVisualisation>().Any()).OrderBy(v => v.Group.Time).Select(v => (double)v.X).ToArray(),
                () => Is.EqualTo(times));
        }
    }
}
