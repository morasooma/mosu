// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Rulesets.Mania.Edit;
using osu.Game.Rulesets.Mania.UI;
using osu.Game.Screens.Edit.Components.Menus;
using osu.Game.Tests.Visual;
using osuTK.Input;

namespace osu.Game.Rulesets.Mania.Tests.Editor
{
    public partial class TestSceneObjectPlacement : EditorTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        protected override Ruleset CreateEditorRuleset() => new ManiaRuleset();

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Test]
        public void TestPlacementBeforeTrackStart()
        {
            AddStep("Seek to 0", () => EditorClock.Seek(0));
            AddStep("Select note", () => InputManager.Key(Key.Number2));
            AddStep("clear beatmap", () => EditorBeatmap.Clear());
            AddStep("hover before track start", () => InputManager.MoveMouseTo(this.ChildrenOfType<Column>().First().ScreenSpacePositionAtTime(-100)));
            AddAssert("cursor is in the placement area", () => this.ChildrenOfType<ManiaHitObjectComposer>().Single().CursorInPlacementArea);
            AddAssert("hovered time before snapping is negative", () =>
                this.ChildrenOfType<Column>().First().TimeAtScreenSpacePosition(InputManager.CurrentState.Mouse.Position), () => Is.LessThan(0));
            AddStep("Click", () => InputManager.Click(MouseButton.Left));
            AddAssert("No notes placed", () => EditorBeatmap.HitObjects.Count == 0);
        }

        [Test]
        public void TestSeekOnNotePlacement()
        {
            double? initialTime = null;

            AddStep("store initial time", () => initialTime = EditorClock.CurrentTime);
            AddStep("enable mania auto-scroll", () => config.SetValue(OsuSetting.ForkManiaEditorAutoSeekOnPlacement, true));
            AddAssert("editor menu reflects the mania setting", () => autoSeekMenuItem().State.Value);
            placeObject();
            AddUntilStep("wait for seek to complete", () => !EditorClock.IsSeeking);
            AddAssert("seeked forward to object", () => EditorClock.CurrentTime, () => Is.GreaterThan(initialTime!));
        }

        [Test]
        public void TestNoSeekOnNotePlacementByDefault()
        {
            double initialTime = 0;

            AddStep("store initial time", () => initialTime = EditorClock.CurrentTime);
            AddStep("clear beatmap", () => EditorBeatmap.Clear());
            AddAssert("auto-scroll is off in the mania editor menu", () => !autoSeekMenuItem().State.Value);
            AddAssert("auto-scroll remains on by default for other rulesets", () => config.Get<bool>(OsuSetting.EditorAutoSeekOnPlacement));
            placeObject();
            AddAssert("a note was placed ahead of the current time", () => EditorBeatmap.HitObjects.Single().StartTime, () => Is.GreaterThan(initialTime));
            AddAssert("not seeking", () => !EditorClock.IsSeeking);
            AddAssert("time is unchanged", () => EditorClock.CurrentTime, () => Is.EqualTo(initialTime));
        }

        [Test]
        public void TestEditorMenuChangesOnlyManiaAutoScroll()
        {
            AddStep("enable mania auto-scroll from the editor menu", () => autoSeekMenuItem().Action.Value?.Invoke());
            AddAssert("mania setting is enabled", () => config.Get<bool>(OsuSetting.ForkManiaEditorAutoSeekOnPlacement));
            AddStep("disable mania auto-scroll from the editor menu", () => autoSeekMenuItem().Action.Value?.Invoke());
            AddAssert("mania setting is disabled", () => !config.Get<bool>(OsuSetting.ForkManiaEditorAutoSeekOnPlacement));
            AddAssert("other rulesets keep their setting", () => config.Get<bool>(OsuSetting.EditorAutoSeekOnPlacement));
        }

        [Test]
        public void TestNoTwoObjectsAtSameTimeAndColumn()
        {
            AddStep("disable mania auto-scroll", () => config.SetValue(OsuSetting.ForkManiaEditorAutoSeekOnPlacement, false));
            AddStep("clear beatmap", () => EditorBeatmap.Clear());

            AddStep("select note placement tool", () => InputManager.Key(Key.Number2));
            AddStep("move mouse to centre of last column", () => InputManager.MoveMouseTo(this.ChildrenOfType<Column>().Last().ScreenSpaceDrawQuad.Centre));
            AddStep("place note", () => InputManager.Click(MouseButton.Left));
            AddAssert("beatmap has 1 object", () => EditorBeatmap.HitObjects, () => Has.Count.EqualTo(1));

            AddStep("select note placement tool", () => InputManager.Key(Key.Number2));
            AddStep("move mouse to centre of first column", () => InputManager.MoveMouseTo(this.ChildrenOfType<Column>().First().ScreenSpaceDrawQuad.Centre));
            AddStep("place note", () => InputManager.Click(MouseButton.Left));
            AddAssert("beatmap has 2 objects", () => EditorBeatmap.HitObjects, () => Has.Count.EqualTo(2));

            AddStep("select note placement tool", () => InputManager.Key(Key.Number2));
            AddStep("move mouse to centre of last column", () => InputManager.MoveMouseTo(this.ChildrenOfType<Column>().Last().ScreenSpaceDrawQuad.Centre));
            AddStep("place note", () => InputManager.Click(MouseButton.Left));
            AddAssert("beatmap has 2 objects", () => EditorBeatmap.HitObjects, () => Has.Count.EqualTo(2));
        }

        private ToggleMenuItem autoSeekMenuItem() => Editor.ChildrenOfType<EditorMenuBar>().Single().Items
                                                          .SelectMany(item => item.Items)
                                                          .OfType<ToggleMenuItem>()
                                                          .Single(item => item.Text.Value.Equals(EditorStrings.AutoSeekOnPlacement));

        private void placeObject()
        {
            AddStep("select note placement tool", () => InputManager.Key(Key.Number2));
            AddStep("move mouse to centre of last column", () => InputManager.MoveMouseTo(this.ChildrenOfType<Column>().Last().ScreenSpaceDrawQuad.Centre));
            AddStep("place note", () => InputManager.Click(MouseButton.Left));
        }
    }
}
