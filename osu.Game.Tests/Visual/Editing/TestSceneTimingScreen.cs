// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Timing;
using osu.Game.Screens.Edit.Timing.RowAttributes;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Editing
{
    [TestFixture]
    public partial class TestSceneTimingScreen : EditorClockTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Blue);

        private TimingScreen timingScreen;
        private EditorBeatmap editorBeatmap;
        private BeatmapEditorChangeHandler changeHandler;

        protected override bool ScrollUsingMouseWheel => false;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Beatmap.Value = CreateWorkingBeatmap(Ruleset.Value);
            Beatmap.Disabled = true;
        }

        private void reloadEditorBeatmap()
        {
            editorBeatmap = new EditorBeatmap(Beatmap.Value.GetPlayableBeatmap(Ruleset.Value));
            changeHandler = new BeatmapEditorChangeHandler(editorBeatmap);

            Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(EditorBeatmap), editorBeatmap),
                    (typeof(IEditorChangeHandler), changeHandler),
                    (typeof(IBeatSnapProvider), editorBeatmap)
                },
                Child = timingScreen = new TimingScreen
                {
                    State = { Value = Visibility.Visible },
                },
            };
        }

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("Stop clock", () => EditorClock.Stop());

            AddStep("Reload Editor Beatmap", reloadEditorBeatmap);

            AddUntilStep("Wait for rows to load", () => Child.ChildrenOfType<EffectRowAttribute>().Any());
        }

        // TODO: this is best-effort for now, but the comment out test below should probably be how things should work.
        // Was originally working as of https://github.com/ppy/osu/pull/26141; Regressed at some point.
        [Test]
        public void TestSelectionDismissedOnUndo()
        {
            AddStep("Select first timing point", () =>
            {
                InputManager.MoveMouseTo(Child.ChildrenOfType<TimingRowAttribute>().First());
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("Selection changed", () => timingScreen.SelectedGroup.Value.Time == 2170);
            AddUntilStep("Ensure seeked to correct time", () => EditorClock.CurrentTimeAccurate == 2170);

            AddStep("Adjust offset", () =>
            {
                InputManager.MoveMouseTo(timingScreen.ChildrenOfType<DiscreteAdjustmentControl<double>>().First().ScreenSpaceDrawQuad.Centre + new Vector2(20, 0));
                InputManager.Click(MouseButton.Left);
            });

            AddUntilStep("wait for offset changed", () =>
            {
                return timingScreen.SelectedGroup.Value.ControlPoints.Any(c => c is TimingControlPoint) && timingScreen.SelectedGroup.Value.Time > 2170;
            });

            AddStep("undo", () => changeHandler?.RestoreState(-1));

            AddUntilStep("selection dismissed", () => timingScreen.SelectedGroup.Value, () => Is.Null);
        }

        // [Test]
        // public void TestSelectedRetainedOverUndo()
        // {
        //     AddStep("Select first timing point", () =>
        //     {
        //         InputManager.MoveMouseTo(Child.ChildrenOfType<TimingRowAttribute>().First());
        //         InputManager.Click(MouseButton.Left);
        //     });
        //
        //     AddUntilStep("Selection changed", () => timingScreen.SelectedGroup.Value.Time == 2170);
        //     AddUntilStep("Ensure seeked to correct time", () => EditorClock.CurrentTimeAccurate == 2170);
        //
        //     AddStep("Adjust offset", () =>
        //     {
        //         InputManager.MoveMouseTo(timingScreen.ChildrenOfType<TimingAdjustButton>().First().ScreenSpaceDrawQuad.Centre + new Vector2(20, 0));
        //         InputManager.Click(MouseButton.Left);
        //     });
        //
        //     AddUntilStep("wait for offset changed", () =>
        //     {
        //         return timingScreen.SelectedGroup.Value.ControlPoints.Any(c => c is TimingControlPoint) && timingScreen.SelectedGroup.Value.Time > 2170;
        //     });
        //
        //     AddStep("undo", () => changeHandler?.RestoreState(-1));
        //
        //     AddUntilStep("selection retained", () =>
        //     {
        //         return timingScreen.SelectedGroup.Value.ControlPoints.Any(c => c is TimingControlPoint) && timingScreen.SelectedGroup.Value.Time > 2170;
        //     });
        //
        //     AddAssert("check group count", () => editorBeatmap.ControlPointInfo.Groups.Count, () => Is.EqualTo(10));
        //
        //     AddStep("Adjust offset", () =>
        //     {
        //         InputManager.MoveMouseTo(timingScreen.ChildrenOfType<TimingAdjustButton>().First().ScreenSpaceDrawQuad.Centre + new Vector2(20, 0));
        //         InputManager.Click(MouseButton.Left);
        //     });
        //
        //     AddAssert("check group count", () => editorBeatmap.ControlPointInfo.Groups.Count, () => Is.EqualTo(10));
        // }

        [Test]
        public void TestScrollControlGroupIntoView()
        {
            AddStep("Add many control points", () =>
            {
                editorBeatmap.ControlPointInfo.Clear();

                editorBeatmap.ControlPointInfo.Add(0, new TimingControlPoint());

                for (int i = 0; i < 100; i++)
                {
                    editorBeatmap.ControlPointInfo.Add((i + 1) * 1000, new EffectControlPoint
                    {
                        KiaiMode = Convert.ToBoolean(i % 2),
                    });
                }
            });

            AddStep("Select first effect point", () =>
            {
                InputManager.MoveMouseTo(Child.ChildrenOfType<EffectRowAttribute>().First());
                InputManager.Click(MouseButton.Left);
            });

            AddStep("Seek to beginning", () => EditorClock.Seek(0));

            AddStep("Seek to last point", () => EditorClock.Seek(101 * 1000));

            AddUntilStep("Scrolled to end", () => timingScreen.ChildrenOfType<OsuScrollContainer>().First().IsScrolledToEnd());
        }

        [Test]
        public void TestEditThenClickAwayAppliesChanges()
        {
            AddStep("Add two control points", () =>
            {
                editorBeatmap.ControlPointInfo.Clear();
                editorBeatmap.ControlPointInfo.Add(1000, new TimingControlPoint());
                editorBeatmap.ControlPointInfo.Add(2000, new TimingControlPoint());
            });

            AddStep("Select second timing point", () =>
            {
                InputManager.MoveMouseTo(Child.ChildrenOfType<TimingRowAttribute>().Last());
                InputManager.Click(MouseButton.Left);
            });

            AddStep("Scroll to end", () => timingScreen.ChildrenOfType<ControlPointSettings>().Single().ChildrenOfType<OsuScrollContainer>().Single().ScrollToEnd(false));
            AddStep("Modify time signature", () =>
            {
                var timeSignatureTextBox = Child.ChildrenOfType<LabelledTimeSignature.TimeSignatureBox>().Single().ChildrenOfType<TextBox>().Single();
                InputManager.MoveMouseTo(timeSignatureTextBox);
                InputManager.Click(MouseButton.Left);

                Debug.Assert(!timeSignatureTextBox.Current.Value.Equals("1", StringComparison.Ordinal));
                timeSignatureTextBox.Current.Value = "1";
            });

            AddStep("Select first timing point", () =>
            {
                InputManager.MoveMouseTo(Child.ChildrenOfType<TimingRowAttribute>().First());
                InputManager.Click(MouseButton.Left);
            });

            AddAssert("Second timing point changed time signature", () => editorBeatmap.ControlPointInfo.TimingPoints.Last().TimeSignature.Numerator == 1);
            AddAssert("First timing point preserved time signature", () => editorBeatmap.ControlPointInfo.TimingPoints.First().TimeSignature.Numerator == 4);
        }

        [Test]
        public void TestShiftSelectionAndOffsetUndoRedo()
        {
            prepareSelectionRows();
            clickRow(0);
            clickRow(2, Key.ShiftLeft);
            AddAssert("three rows selected", () => timingScreen.Selection.SelectedGroups.Count, () => Is.EqualTo(3));

            AddStep("adjust offset by 10", () =>
                timingScreen.ChildrenOfType<FormDiscreteAdjustmentControl<double>>().First().Current.Value += 10);
            AddAssert("all selected and related effects moved once", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1010, 1110, 1210, 1310, 2000 }));
            AddAssert("selection retained", () => timingScreen.Selection.SelectedGroups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1010, 1110, 1210 }));

            AddStep("undo", () => changeHandler.RestoreState(-1));
            AddAssert("all offsets restored", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1000, 1100, 1200, 1300, 2000 }));
            AddAssert("detached selection cleared", () => timingScreen.Selection.SelectedGroups, () => Is.Empty);
            AddStep("redo", () => changeHandler.RestoreState(1));
            AddAssert("all offsets reapplied", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1010, 1110, 1210, 1310, 2000 }));
        }

        [Test]
        public void TestEffectOnlySelectionHasUsableOffsetPanel()
        {
            prepareSelectionRows();
            clickRow(1);
            clickRow(3, Key.ShiftLeft);
            AddUntilStep("offset panel is visible for effects", () =>
            {
                var form = timingScreen.ChildrenOfType<FormDiscreteAdjustmentControl<double>>().First(f => f.Caption.ToString() == "Offset");
                return form.PropagatePositionalInputSubTree && timingScreen.ChildrenOfType<ControlPointSettings>().Single()
                    .ScreenSpaceDrawQuad.AABBFloat.Contains(form.ScreenSpaceDrawQuad.Centre);
            });
            AddStep("click offset increment", () =>
            {
                var form = timingScreen.ChildrenOfType<FormDiscreteAdjustmentControl<double>>().First(f => f.Caption.ToString() == "Offset");
                var control = form.ChildrenOfType<DiscreteAdjustmentControl<double>>().Single();
                InputManager.MoveMouseTo(control.ScreenSpaceDrawQuad.Centre + new Vector2(5, 0));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("all selected effects nudged", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1000, 1101, 1201, 1301, 2000 }));
        }

        [Test]
        public void TestEffectOffsetTextInputAndUndo()
        {
            prepareSelectionRows();
            clickRow(1);
            clickRow(3, Key.ControlLeft);
            AddStep("enter absolute offset for primary effect", () =>
            {
                var form = timingScreen.ChildrenOfType<FormDiscreteAdjustmentControl<double>>().First(f => f.Caption.ToString() == "Offset");
                InputManager.MoveMouseTo(form.ChildrenOfType<TextBox>().Single());
                InputManager.Click(MouseButton.Left);
                Dependencies.Get<Clipboard>().SetText("1350");
                InputManager.Keys(PlatformAction.SelectAll);
                InputManager.Keys(PlatformAction.Paste);
            });
            AddStep("commit effect offset", () => InputManager.Key(Key.Enter));
            AddAssert("both selected effects shifted by 50", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1000, 1150, 1200, 1350, 2000 }));
            AddStep("undo effect offsets", () => changeHandler.RestoreState(-1));
            AddAssert("original effects restored", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1000, 1100, 1200, 1300, 2000 }));
        }

        [Test]
        public void TestOffsetPanelWithoutSelection()
        {
            prepareSelectionRows();
            AddStep("clear selection", () => timingScreen.Selection.Clear());
            AddStep("try offset increment without selection", () =>
            {
                var form = timingScreen.ChildrenOfType<FormDiscreteAdjustmentControl<double>>().First(f => f.Caption.ToString() == "Offset");
                var control = form.ChildrenOfType<DiscreteAdjustmentControl<double>>().Single();
                InputManager.MoveMouseTo(control.ScreenSpaceDrawQuad.Centre + new Vector2(5, 0));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("offsets unchanged", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1000, 1100, 1200, 1300, 2000 }));
        }

        [Test]
        public void TestControlSelectionAndTimeField()
        {
            prepareSelectionRows();
            clickRow(1);
            clickRow(3, Key.ControlLeft);
            AddAssert("two effects selected", () => timingScreen.Selection.SelectedGroups.Count, () => Is.EqualTo(2));
            AddStep("edit time of primary effect", () =>
            {
                var form = timingScreen.ChildrenOfType<FormTextBox>().Single(f => f.Caption.ToString() == "Time");
                var textBox = form.ChildrenOfType<TextBox>().Single();
                InputManager.MoveMouseTo(textBox);
                InputManager.Click(MouseButton.Left);
                Dependencies.Get<Clipboard>().SetText("1350");
                InputManager.Keys(PlatformAction.SelectAll);
                InputManager.Keys(PlatformAction.Paste);
            });
            AddStep("commit time", () => InputManager.Key(Key.Enter));
            AddAssert("only selected effects moved", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1000, 1150, 1200, 1350, 2000 }));
            AddAssert("selection retained after time edit", () => timingScreen.Selection.SelectedGroups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1150, 1350 }));
            clickRow(1);
            AddAssert("plain click resets selection", () => timingScreen.Selection.SelectedGroups.Count, () => Is.EqualTo(1));
        }

        [Test]
        public void TestTimingTimeFieldCarriesUnselectedEffects()
        {
            prepareSelectionRows();
            clickRow(0);
            AddStep("focus timing time", () =>
            {
                var form = timingScreen.ChildrenOfType<FormTextBox>().Single(f => f.Caption.ToString() == "Time");
                InputManager.MoveMouseTo(form.ChildrenOfType<TextBox>().Single());
                InputManager.Click(MouseButton.Left);
                Dependencies.Get<Clipboard>().SetText("1050");
                InputManager.Keys(PlatformAction.SelectAll);
                InputManager.Keys(PlatformAction.Paste);
            });
            AddStep("commit timing time", () => InputManager.Key(Key.Enter));
            AddAssert("related effects moved but next timing did not", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1050, 1150, 1250, 1350, 2000 }));
            AddAssert("only timing remains selected", () => timingScreen.Selection.SelectedGroups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1050 }));
        }

        [Test]
        public void TestUseCurrentTimeMovesSelectedEffects()
        {
            prepareSelectionRows();
            clickRow(1);
            clickRow(3, Key.ShiftLeft);
            AddStep("seek to target", () => EditorClock.Seek(1400));
            AddStep("use current time", () => timingScreen.ChildrenOfType<RoundedButton>().Single(b => b.Text.ToString() == "Use current time").TriggerClick());
            AddAssert("selected effects moved by same delta", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1000, 1200, 1300, 1400, 2000 }));
            AddStep("nudge selected effects again", () => timingScreen.Selection.ChangeTime(editorBeatmap, 1410, false));
            AddAssert("selection follows replacement rows", () => timingScreen.Selection.SelectedGroups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1210, 1310, 1410 }));
        }

        [Test]
        public void TestDeleteMultipleEffectsAndUndo()
        {
            prepareSelectionRows();
            clickRow(1);
            clickRow(3, Key.ShiftLeft);
            AddStep("delete selected rows", () => timingScreen.ChildrenOfType<RoundedButton>().Single(b => b.Text.ToString() == "-").TriggerClick());
            AddAssert("only timings remain", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1000, 2000 }));
            AddStep("undo deletion", () => changeHandler.RestoreState(-1));
            AddAssert("all effects restored in one undo", () => editorBeatmap.ControlPointInfo.Groups.Select(g => g.Time),
                () => Is.EqualTo(new double[] { 1000, 1100, 1200, 1300, 2000 }));
        }

        private void prepareSelectionRows()
        {
            AddStep("create rows for selection", () =>
            {
                timingScreen.Selection.Clear();
                editorBeatmap.ControlPointInfo.Clear();
                editorBeatmap.ControlPointInfo.GroupAt(1000, true).Add(new TimingControlPoint());
                editorBeatmap.ControlPointInfo.GroupAt(1100, true).Add(new EffectControlPoint { KiaiMode = true });
                editorBeatmap.ControlPointInfo.GroupAt(1200, true).Add(new EffectControlPoint());
                editorBeatmap.ControlPointInfo.GroupAt(1300, true).Add(new EffectControlPoint { KiaiMode = true });
                editorBeatmap.ControlPointInfo.GroupAt(2000, true).Add(new TimingControlPoint { BeatLength = 600 });
                EditorClock.Seek(1000);
                changeHandler.SaveState();
            });
            AddUntilStep("five rows ready", () => timingScreen.ChildrenOfType<ControlPointTable.DrawableControlGroup>().Count(), () => Is.EqualTo(5));
        }

        private void clickRow(int index, Key? modifier = null)
        {
            AddStep($"click row {index} with {modifier}", () =>
            {
                var row = timingScreen.ChildrenOfType<ControlPointTable.DrawableControlGroup>().OrderBy(r => r.Current.Value.Time).ElementAt(index);
                InputManager.MoveMouseTo(row);
                if (modifier.HasValue)
                    InputManager.PressKey(modifier.Value);
                InputManager.Click(MouseButton.Left);
                if (modifier.HasValue)
                    InputManager.ReleaseKey(modifier.Value);
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            Beatmap.Disabled = false;
            base.Dispose(isDisposing);
        }
    }
}
