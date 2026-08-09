// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Extensions;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Cursor;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Edit;
using osu.Game.Rulesets.Dodge.Edit.Blueprints;
using osu.Game.Rulesets.Dodge.Edit.Design;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Storyboards;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual;
using osuTK;
using osuTK.Input;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public partial class TestSceneDodgeEditor : EditorTestScene
    {
        protected override Ruleset CreateEditorRuleset() => new DodgeRuleset();

        protected override IBeatmap CreateBeatmap(RulesetInfo ruleset)
        {
            var beatmap = new TestBeatmap(ruleset, false);
            beatmap.HitObjects.Clear();
            beatmap.ControlPointInfo.Add(0, new TimingControlPoint { BeatLength = 500 });
            return beatmap;
        }

        [Test]
        public void TestRulesetDesignScreenLoads()
        {
            AddStep("open Design", () => Editor.Mode.Value = EditorScreenMode.Design);
            AddUntilStep("Dodge Design loaded", () => Editor.ChildrenOfType<DodgeDesignScreen>().SingleOrDefault()?.IsLoaded, () => Is.True);
            AddUntilStep("one storyboard timeline", () => Editor.ChildrenOfType<DodgeStoryboardTimeline>().Count(), () => Is.EqualTo(1));
            AddUntilStep("one storyboard canvas", () => Editor.ChildrenOfType<DodgeStoryboardCanvas>().Count(), () => Is.EqualTo(1));
            AddAssert("storyboard uses native editor timeline", () =>
                Editor.ChildrenOfType<DodgeStoryboardTimeline>().Single().FindClosestParent<Timeline>() != null);
            AddAssert("storyboard timeline is above canvas", () =>
                Editor.ChildrenOfType<DodgeStoryboardTimeline>().Single().ScreenSpaceDrawQuad.AABBFloat.Bottom
                <= Editor.ChildrenOfType<DodgeStoryboardCanvas>().Single().ScreenSpaceDrawQuad.AABBFloat.Top);
            AddAssert("file selectors have a popover host", () =>
                Editor.ChildrenOfType<FormFileSelector>()
                      .All(selector => selector.FindClosestParent<PopoverContainer>() != null));
            AddStep("toggle gameplay preview", () => Editor.ChildrenOfType<DodgeStoryboardCanvas>().Single().ToggleGameplayPreview());
            AddStep("switch to widescreen", () =>
            {
                EditorBeatmap.WidescreenStoryboard = true;
                Editor.ChildrenOfType<DodgeStoryboardCanvas>().Single().Refresh();
            });
            AddAssert("widescreen is wider than normal", () => DodgeStoryboardCanvas.WIDESCREEN_WIDTH > DodgeStoryboardCanvas.NORMAL_WIDTH);
        }

        [Test]
        public void TestDesignVisualOptionsSynchroniseWithUndoRedo()
        {
            OsuCheckbox forceSkin = null!;
            OsuCheckbox forceStoryboard = null!;
            OsuCheckbox widescreen = null!;
            bool originalForceSkin = false;
            bool originalForceStoryboard = false;
            bool originalWidescreen = false;

            AddStep("open Design", () => Editor.Mode.Value = EditorScreenMode.Design);
            AddUntilStep("Design visual options available", () =>
            {
                forceSkin = getDesignCheckbox("Force map skin toggle");
                forceStoryboard = getDesignCheckbox("Force storyboard toggle");
                widescreen = getDesignCheckbox("Widescreen storyboard toggle");
                return forceSkin != null && forceStoryboard != null && widescreen != null;
            });
            AddStep("capture original visual options", () =>
            {
                originalForceSkin = forceSkin.Current.Value;
                originalForceStoryboard = forceStoryboard.Current.Value;
                originalWidescreen = widescreen.Current.Value;
            });

            AddStep("toggle force map skin", () => forceSkin.Current.Value = !originalForceSkin);
            AddUntilStep("force map skin stored", () => DodgeBeatmapSettings.GetForceBeatmapSkin(EditorBeatmap.Difficulty), () => Is.EqualTo(!originalForceSkin));
            AddStep("undo force map skin", () => Editor.Undo());
            AddUntilStep("force map skin restored", () => DodgeBeatmapSettings.GetForceBeatmapSkin(EditorBeatmap.Difficulty), () => Is.EqualTo(originalForceSkin));
            AddUntilStep("force map skin checkbox restored", () => forceSkin.Current.Value, () => Is.EqualTo(originalForceSkin));
            AddStep("redo force map skin", () => Editor.Redo());
            AddUntilStep("force map skin checkbox redone", () => forceSkin.Current.Value, () => Is.EqualTo(!originalForceSkin));

            AddStep("toggle force storyboard", () => forceStoryboard.Current.Value = !originalForceStoryboard);
            AddUntilStep("force storyboard stored", () => DodgeBeatmapSettings.GetForceStoryboard(EditorBeatmap.Difficulty), () => Is.EqualTo(!originalForceStoryboard));
            AddStep("undo force storyboard", () => Editor.Undo());
            AddUntilStep("force storyboard restored", () => DodgeBeatmapSettings.GetForceStoryboard(EditorBeatmap.Difficulty), () => Is.EqualTo(originalForceStoryboard));
            AddUntilStep("force storyboard checkbox restored", () => forceStoryboard.Current.Value, () => Is.EqualTo(originalForceStoryboard));

            AddStep("toggle widescreen storyboard", () => widescreen.Current.Value = !originalWidescreen);
            AddUntilStep("widescreen stored", () => EditorBeatmap.WidescreenStoryboard, () => Is.EqualTo(!originalWidescreen));
            AddStep("undo widescreen storyboard", () => Editor.Undo());
            AddUntilStep("widescreen restored", () => EditorBeatmap.WidescreenStoryboard, () => Is.EqualTo(originalWidescreen));
            AddUntilStep("widescreen checkbox restored", () => widescreen.Current.Value, () => Is.EqualTo(originalWidescreen));
        }

        private OsuCheckbox getDesignCheckbox(string name)
            => Editor.ChildrenOfType<DodgeDesignScreen>()
                     .SingleOrDefault()?
                     .ChildrenOfType<OsuCheckbox>()
                     .SingleOrDefault(checkbox => checkbox.Name == name)!;

        [Test]
        public void TestStoryboardTimelineObjectCanBeSelectedAndDragged()
        {
            const string path = "timeline-drag.png";
            DodgeStoryboardTimeline.StoryboardObjectBlueprint blueprint = null!;
            Drawable startHandle = null!;
            double originalStart = 0;
            double resizeOriginalStart = 0;
            Vector2 dragTarget = Vector2.Zero;

            AddStep("add storyboard sprite", () =>
            {
                var sprite = new StoryboardSprite(
                    StoryboardElementSource.Beatmap,
                    path,
                    Anchor.Centre,
                    new Vector2(320, 240));
                DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 0, 4000);
                EditorBeatmap.BeginChange();
                EditorBeatmap.Storyboard.GetLayer("Foreground").Add(sprite);
                EditorBeatmap.EndChange();
            });
            AddStep("open Design", () => Editor.Mode.Value = EditorScreenMode.Design);
            AddUntilStep("timeline object visible", () =>
                Editor.ChildrenOfType<DodgeStoryboardTimeline.StoryboardObjectBlueprint>().Any());
            AddAssert("timeline object is inside visible timeline", () =>
            {
                var objectBlueprint = Editor.ChildrenOfType<DodgeStoryboardTimeline.StoryboardObjectBlueprint>().Single();
                var nativeTimeline = objectBlueprint.FindClosestParent<Timeline>()!;
                return nativeTimeline.ScreenSpaceDrawQuad.AABBFloat.Contains(objectBlueprint.ScreenSpaceSelectionPoint);
            });
            AddStep("select timeline object", () =>
            {
                blueprint = Editor.ChildrenOfType<DodgeStoryboardTimeline.StoryboardObjectBlueprint>().Single();
                InputManager.MoveMouseTo(blueprint.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("blueprint selected", () => blueprint.IsSelected);
            AddAssert("timeline object selected", () =>
                Editor.ChildrenOfType<DodgeStoryboardTimeline>().Single().SelectedSprite?.Path == path);
            AddAssert("timeline object has visible width", () => blueprint.DrawWidth, () => Is.GreaterThan(10));
            AddAssert("command marker does not cover object body", () =>
            {
                float markerX = Editor.ChildrenOfType<DodgeStoryboardTimeline>()
                                      .Single()
                                      .ChildrenOfType<Drawable>()
                                      .First(drawable => drawable.GetType().Name.Contains("StoryboardCommandMarker"))
                                      .ScreenSpaceDrawQuad.Centre.X;
                return MathF.Abs(markerX - blueprint.ScreenSpaceSelectionPoint.X);
            }, () => Is.GreaterThan(10));
            AddStep("move away from timeline object", () =>
                InputManager.MoveMouseTo(blueprint.ScreenSpaceSelectionPoint + new Vector2(0, 30)));
            AddStep("move to selected timeline object", () =>
            {
                blueprint = Editor.ChildrenOfType<DodgeStoryboardTimeline.StoryboardObjectBlueprint>().Single();
                originalStart = Editor.ChildrenOfType<DodgeStoryboardTimeline>().Single().SelectedSprite!.StartTime;
                InputManager.MoveMouseTo(blueprint.ScreenSpaceSelectionPoint);
                dragTarget = blueprint.ScreenSpaceSelectionPoint + new Vector2(80, 0);
            });
            AddAssert("selected blueprint hovered", () => blueprint.IsHovered);
            AddStep("disable beat snapping", () => InputManager.PressKey(Key.LAlt));
            AddStep("begin timeline drag", () => InputManager.PressButton(MouseButton.Left));
            AddStep("move timeline object", () => InputManager.MoveMouseTo(dragTarget));
            AddStep("finish timeline drag", () => InputManager.ReleaseButton(MouseButton.Left));
            AddStep("release Alt", () => InputManager.ReleaseKey(Key.LAlt));
            AddUntilStep("object time changed", () =>
                Editor.ChildrenOfType<DodgeStoryboardTimeline>().Single().SelectedSprite?.StartTime, () => Is.GreaterThan(originalStart));
            AddUntilStep("replacement blueprint available", () =>
                Editor.ChildrenOfType<DodgeStoryboardTimeline.StoryboardObjectBlueprint>().SingleOrDefault()?.IsSelected, () => Is.True);
            AddStep("move to start handle", () =>
            {
                blueprint = Editor.ChildrenOfType<DodgeStoryboardTimeline.StoryboardObjectBlueprint>().Single();
                startHandle = blueprint.ChildrenOfType<Drawable>().Single(drawable => drawable.Name == "Storyboard start handle");
                resizeOriginalStart = Editor.ChildrenOfType<DodgeStoryboardTimeline>().Single().SelectedSprite!.StartTime;
                InputManager.MoveMouseTo(startHandle.ScreenSpaceDrawQuad.Centre);
                dragTarget = startHandle.ScreenSpaceDrawQuad.Centre + new Vector2(80, 0);
            });
            AddStep("disable snapping for resize", () => InputManager.PressKey(Key.LAlt));
            AddStep("begin start resize", () => InputManager.PressButton(MouseButton.Left));
            AddStep("trim timeline object", () => InputManager.MoveMouseTo(dragTarget));
            AddStep("finish start resize", () => InputManager.ReleaseButton(MouseButton.Left));
            AddStep("release resize Alt", () => InputManager.ReleaseKey(Key.LAlt));
            AddUntilStep("object start handle changed time", () =>
                Editor.ChildrenOfType<DodgeStoryboardTimeline>().Single().SelectedSprite?.StartTime, () => Is.GreaterThan(resizeOriginalStart));
        }

        [Test]
        public void TestStoryboardObjectCanBeDeletedByButtonAndHotkey()
        {
            const string path = "delete-storyboard-object.png";
            RoundedButton objectButton = null!;

            AddStep("add storyboard sprite", () =>
            {
                var sprite = new StoryboardSprite(
                    StoryboardElementSource.Beatmap,
                    path,
                    Anchor.Centre,
                    new Vector2(320, 240));
                DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 0, 4000);
                EditorBeatmap.BeginChange();
                EditorBeatmap.Storyboard.GetLayer("Foreground").Add(sprite);
                EditorBeatmap.EndChange();
            });
            AddStep("open Design", () => Editor.Mode.Value = EditorScreenMode.Design);
            AddUntilStep("storyboard object button available", () =>
            {
                objectButton = Editor.ChildrenOfType<RoundedButton>()
                                     .FirstOrDefault(button => button.Text.ToString().Contains(path))!;
                return objectButton != null;
            });
            AddStep("select object", () => objectButton.TriggerClick());
            AddStep("click inspector delete", () =>
            {
                RoundedButton deleteButton = Editor.ChildrenOfType<RoundedButton>()
                                                   .Single(button => button.Text.ToString().Contains("Удалить"));
                deleteButton.TriggerClick();
            });
            AddUntilStep("button removed storyboard object", () =>
                EditorBeatmap.Storyboard.GetLayer("Foreground").Elements.OfType<StoryboardSprite>().Any(), () => Is.False);
            AddUntilStep("button removed timeline blueprint", () =>
                Editor.ChildrenOfType<DodgeStoryboardTimeline.StoryboardObjectBlueprint>().Any(), () => Is.False);

            AddStep("undo button deletion", () => Editor.Undo());
            AddUntilStep("undo restored object", () =>
                EditorBeatmap.Storyboard.GetLayer("Foreground").Elements.OfType<StoryboardSprite>().Count(), () => Is.EqualTo(1));
            AddUntilStep("restored object button available", () =>
            {
                objectButton = Editor.ChildrenOfType<RoundedButton>()
                                     .FirstOrDefault(button => button.Text.ToString().Contains(path))!;
                return objectButton != null;
            });
            AddStep("select restored object", () => objectButton.TriggerClick());
            AddUntilStep("timeline selection restored", () =>
                Editor.ChildrenOfType<DodgeStoryboardTimeline.StoryboardObjectBlueprint>().SingleOrDefault()?.IsSelected, () => Is.True);
            AddStep("press Delete", () => InputManager.PressKey(Key.Delete));
            AddStep("release Delete", () => InputManager.ReleaseKey(Key.Delete));
            AddUntilStep("hotkey removed storyboard object", () =>
                EditorBeatmap.Storyboard.GetLayer("Foreground").Elements.OfType<StoryboardSprite>().Any(), () => Is.False);
            AddUntilStep("hotkey removed timeline blueprint", () =>
                Editor.ChildrenOfType<DodgeStoryboardTimeline.StoryboardObjectBlueprint>().Any(), () => Is.False);
        }

        [Test]
        public void TestStoryboardSelectionRebindsAcrossUndoRedo()
        {
            const string path = "undo-selection.png";
            DodgeStoryboardTimeline timeline = null!;
            RoundedButton objectButton = null!;
            StoryboardSprite editedSprite = null!;
            StoryboardSprite undoSprite = null!;
            StoryboardSprite redoSprite = null!;

            AddStep("add initial storyboard sprite", () =>
            {
                var sprite = new StoryboardSprite(
                    StoryboardElementSource.Beatmap,
                    path,
                    Anchor.Centre,
                    new Vector2(320, 240));
                DodgeStoryboardEditing.InitialiseVisibleRange(sprite, 1000, 2000);
                EditorBeatmap.BeginChange();
                EditorBeatmap.Storyboard.GetLayer("Foreground").Add(sprite);
                EditorBeatmap.EndChange();
            });
            AddStep("open Design", () => Editor.Mode.Value = EditorScreenMode.Design);
            AddUntilStep("Dodge Design loaded", () => Editor.ChildrenOfType<DodgeDesignScreen>().SingleOrDefault()?.IsLoaded, () => Is.True);
            AddUntilStep("storyboard timeline available", () => Editor.ChildrenOfType<DodgeStoryboardTimeline>().Count(), () => Is.EqualTo(1));
            AddStep("get storyboard timeline", () => timeline = Editor.ChildrenOfType<DodgeStoryboardTimeline>().Single());
            AddUntilStep("object button available", () =>
            {
                objectButton = Editor.ChildrenOfType<RoundedButton>()
                                     .FirstOrDefault(button => button.Text.ToString().Contains(path))!;
                return objectButton != null;
            });
            AddStep("select storyboard object", () => objectButton.TriggerClick());
            AddAssert("initial selection is bound", () =>
                timeline.SelectedSprite != null &&
                timeline.SelectedSprite.StartTime == 1000 &&
                timeline.SelectedSprite.EndTimeForDisplay == 3000);

            AddStep("retime through storyboard timeline", () => timeline.ObjectRangeChanged?.Invoke(1500, 3500));
            AddAssert("edited selection is bound", () =>
            {
                editedSprite = timeline.SelectedSprite!;
                return editedSprite.StartTime == 1500 &&
                       editedSprite.EndTimeForDisplay == 3500 &&
                       EditorBeatmap.Storyboard.GetLayer("Foreground").Elements.Contains(editedSprite);
            });

            AddStep("undo storyboard retime", () => Editor.Undo());
            AddUntilStep("undo rebinds original range", () =>
            {
                undoSprite = timeline.SelectedSprite!;
                return undoSprite != null &&
                       !ReferenceEquals(undoSprite, editedSprite) &&
                       undoSprite.StartTime == 1000 &&
                       undoSprite.EndTimeForDisplay == 3000 &&
                       EditorBeatmap.Storyboard.GetLayer("Foreground").Elements.Contains(undoSprite);
            });

            AddStep("redo storyboard retime", () => Editor.Redo());
            AddUntilStep("redo rebinds edited range", () =>
            {
                redoSprite = timeline.SelectedSprite!;
                return redoSprite != null &&
                       !ReferenceEquals(redoSprite, undoSprite) &&
                       redoSprite.StartTime == 1500 &&
                       redoSprite.EndTimeForDisplay == 3500 &&
                       EditorBeatmap.Storyboard.GetLayer("Foreground").Elements.Contains(redoSprite);
            });

            AddStep("retime rebound selection again", () => timeline.ObjectRangeChanged?.Invoke(2000, 5000));
            AddUntilStep("second edit succeeds on rebound selection", () =>
                timeline.SelectedSprite != null &&
                !ReferenceEquals(timeline.SelectedSprite, redoSprite) &&
                timeline.SelectedSprite.StartTime == 2000 &&
                timeline.SelectedSprite.EndTimeForDisplay == 5000 &&
                EditorBeatmap.Storyboard.GetLayer("Foreground").Elements.Contains(timeline.SelectedSprite));

            AddStep("select alpha command", () =>
                timeline.RestoreCommandSelection(timeline.SelectedSprite!.Commands.Alpha.Single()));
            AddStep("retime selected command", () =>
                timeline.CommandRangeChanged?.Invoke(timeline.SelectedCommand!, 2250, 4750));
            AddAssert("command selection survives immutable replacement", () =>
                timeline.SelectedCommand != null &&
                timeline.SelectedCommand.StartTime == 2250 &&
                timeline.SelectedCommand.EndTime == 4750 &&
                timeline.SelectedSprite!.Commands.AllCommands.Contains(timeline.SelectedCommand));
        }

        [Test]
        public void TestContextualInspectorAndBatchTiming()
        {
            DodgeBulletToolboxGroup bulletToolbox = null!;
            DodgeEmitterToolboxGroup emitterToolbox = null!;
            DodgeTimingToolboxGroup timingToolbox = null!;
            DodgeTransformToolboxGroup transformToolbox = null!;
            DodgePatternToolboxGroup patternToolbox = null!;
            DodgeArenaToolboxGroup arenaToolbox = null!;
            DodgeEditorContextToolboxGroup contextToolbox = null!;
            DodgeBullet bullet = null!;
            DodgeEmitter emitter = null!;

            AddStep("get contextual toolbox groups", () =>
            {
                bulletToolbox = this.ChildrenOfType<DodgeBulletToolboxGroup>().Single();
                emitterToolbox = this.ChildrenOfType<DodgeEmitterToolboxGroup>().Single();
                timingToolbox = this.ChildrenOfType<DodgeTimingToolboxGroup>().Single();
                transformToolbox = this.ChildrenOfType<DodgeTransformToolboxGroup>().Single();
                patternToolbox = this.ChildrenOfType<DodgePatternToolboxGroup>().Single();
                arenaToolbox = this.ChildrenOfType<DodgeArenaToolboxGroup>().Single();
                contextToolbox = this.ChildrenOfType<DodgeEditorContextToolboxGroup>().Single();
            });
            AddAssert("context help is visible", () => contextToolbox.IsPresent && !string.IsNullOrEmpty(contextToolbox.CurrentInstruction.ToString()));
            AddAssert("object panels hidden without selection", () => !bulletToolbox.IsPresent && !emitterToolbox.IsPresent && !arenaToolbox.IsPresent);
            AddAssert("selection actions disabled without selection", () => !patternToolbox.CanRepeatSelection && !patternToolbox.CanSavePrefab);

            AddStep("select bullet tool", () => InputManager.Key(Key.Number2));
            AddAssert("only bullet controls shown", () => bulletToolbox.IsPresent && !emitterToolbox.IsPresent && !arenaToolbox.IsPresent);
            AddAssert("bullet appearance starts collapsed", () => !bulletToolbox.AppearanceControlsVisible);
            AddStep("show bullet appearance", () => bulletToolbox.ShowAppearance = true);
            AddAssert("bullet appearance can be expanded", () => bulletToolbox.AppearanceControlsVisible);

            AddStep("add and select mixed objects", () =>
            {
                bullet = new DodgeBullet
                {
                    StartTime = 0,
                    Duration = 500,
                    Position = new Vector2(64),
                    EndPosition = new Vector2(448, 320),
                };
                emitter = new DodgeEmitter
                {
                    StartTime = 0,
                    Duration = 500,
                    Position = new Vector2(256, 32),
                    AimPosition = new Vector2(256, 192),
                    MovementEndPosition = new Vector2(256, 32),
                };

                EditorBeatmap.AddRange(new DodgeHitObject[] { bullet, emitter });
                InputManager.Key(Key.Number1);
                EditorBeatmap.SelectedHitObjects.AddRange(new DodgeHitObject[] { bullet, emitter });
            });
            AddAssert("mixed object controls shown", () => bulletToolbox.IsPresent && emitterToolbox.IsPresent);
            AddAssert("batch tools shown for selection", () => timingToolbox.IsPresent && transformToolbox.IsPresent);
            AddAssert("selection actions enabled", () => patternToolbox.CanRepeatSelection && patternToolbox.CanSavePrefab);
            AddAssert("duration read in snap units", () => timingToolbox.DurationSnapUnits.Value, () => Is.EqualTo(4));

            AddStep("set exact batch duration", () => timingToolbox.DurationSnapUnits.Value = 8);
            AddAssert("bullet duration updated", () => bullet.Duration, () => Is.EqualTo(1000).Within(1));
            AddAssert("emitter duration updated", () => emitter.Duration, () => Is.EqualTo(1000).Within(1));

            AddStep("change duration outside timing toolbox", () =>
            {
                EditorBeatmap.BeginChange();
                bullet.Duration = 1500;
                EditorBeatmap.Update(bullet);
                EditorBeatmap.EndChange();
            });
            AddUntilStep("timing toolbox follows timeline edit",
                () => timingToolbox.DurationSnapUnits.Value,
                () => Is.EqualTo(12));

            AddStep("select emitter tool", () => InputManager.Key(Key.Number4));
            AddAssert("only emitter controls shown", () => emitterToolbox.IsPresent && !bulletToolbox.IsPresent && !arenaToolbox.IsPresent);
            AddAssert("repeat controls hidden by default", () => !emitterToolbox.RepeatControlsVisible);
            AddStep("enable repeated bursts", () => emitterToolbox.Repeating.Value = true);
            AddAssert("repeat controls shown when applicable", () => emitterToolbox.RepeatControlsVisible);
            AddAssert("emitter appearance starts collapsed", () => !emitterToolbox.AppearanceControlsVisible);
            AddStep("show emitter appearance", () => emitterToolbox.ShowAppearance = true);
            AddAssert("emitter appearance can be expanded", () => emitterToolbox.AppearanceControlsVisible);
        }

        [Test]
        public void TestDenseObjectsUseSeparateTimelineLanes()
        {
            const int bullet_count = 48;
            const int emitter_count = 6;

            LaneTimelineHitObjectBlueprint collapsedBulletGroup = null!;
            LaneTimelineHitObjectBlueprint collapsedEmitterGroup = null!;
            LaneTimelineHitObjectBlueprint arenaBlueprint = null!;
            int transformCountAfterLayout = 0;

            AddStep("add dense mixed timeline objects", () =>
            {
                EditorBeatmap.AddRange(Enumerable.Range(0, bullet_count).Select(index => new DodgeBullet
                {
                    StartTime = 0,
                    Duration = 500 + index * 25,
                    Position = new Vector2(20 + index * 10, 20),
                    EndPosition = new Vector2(20 + index * 10, 360),
                }));
                EditorBeatmap.AddRange(Enumerable.Range(0, emitter_count).Select(index => new DodgeEmitter
                {
                    StartTime = 0,
                    Duration = 750 + index * 25,
                    Position = new Vector2(256 + index * 10, 20),
                    AimPosition = new Vector2(256 + index * 10, 300),
                    MovementEndPosition = new Vector2(256 + index * 10, 20),
                }));
                EditorBeatmap.Add(new DodgeArenaChange
                {
                    StartTime = 0,
                    Duration = 1000,
                    TargetPosition = new Vector2(32, 24),
                    TargetSize = new Vector2(448, 336),
                });
            });

            AddUntilStep("lane blueprints created",
                () => this.ChildrenOfType<LaneTimelineHitObjectBlueprint>().Count(),
                () => Is.EqualTo(bullet_count + emitter_count + 1));
            AddUntilStep("same-time bullets collapsed", () =>
            {
                LaneTimelineHitObjectBlueprint[] bullets = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                                                   .Where(blueprint => blueprint.Item is DodgeBullet)
                                                                   .ToArray();
                return bullets.Count(blueprint => blueprint.IsGroupMarkerVisible) == 1
                       && bullets.All(blueprint => blueprint.GroupCount == bullet_count);
            });
            AddStep("remember collapsed group marker", () =>
                collapsedBulletGroup = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                           .Single(blueprint => blueprint.Item is DodgeBullet && blueprint.IsGroupMarkerVisible));
            AddUntilStep("same-time emitters collapsed", () =>
            {
                LaneTimelineHitObjectBlueprint[] emitters = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                                                    .Where(blueprint => blueprint.Item is DodgeEmitter)
                                                                    .ToArray();
                return emitters.Count(blueprint => blueprint.IsGroupMarkerVisible) == 1
                       && emitters.All(blueprint => blueprint.GroupCount == emitter_count);
            });
            AddStep("remember collapsed emitter marker", () =>
                collapsedEmitterGroup = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                            .Single(blueprint => blueprint.Item is DodgeEmitter && blueprint.IsGroupMarkerVisible));
            AddAssert("types use ordered fixed lanes", () =>
            {
                arenaBlueprint = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                     .Single(blueprint => blueprint.Item is DodgeArenaChange);
                return collapsedBulletGroup.LaneIndex == 0
                       && collapsedEmitterGroup.LaneIndex == 1
                       && arenaBlueprint.LaneIndex == 2
                       && collapsedBulletGroup.Y < collapsedEmitterGroup.Y
                       && collapsedEmitterGroup.Y < arenaBlueprint.Y;
            });
            AddAssert("groups start collapsed", () => !collapsedBulletGroup.IsGroupExpanded && !collapsedEmitterGroup.IsGroupExpanded);
            AddStep("remember lane transform count", () =>
                transformCountAfterLayout = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                                .SelectMany(blueprint => blueprint.ChildrenOfType<Drawable>().Append(blueprint))
                                                .Sum(drawable => drawable.Transforms.Count()));
            AddWaitStep("run repeated lane layout", 30);
            AddAssert("lane layout does not accumulate transforms", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .SelectMany(blueprint => blueprint.ChildrenOfType<Drawable>().Append(blueprint))
                    .Sum(drawable => drawable.Transforms.Count()),
                () => Is.LessThanOrEqualTo(transformCountAfterLayout));

            AddStep("click bullet group", () =>
            {
                InputManager.MoveMouseTo(collapsedBulletGroup.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("group click selects one bullet",
                () => EditorBeatmap.SelectedHitObjects.OfType<DodgeBullet>().Count(),
                () => Is.EqualTo(1));
            AddUntilStep("selected group expands", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Count(blueprint => blueprint.Item is DodgeBullet && blueprint.IsGroupMarkerVisible),
                () => Is.EqualTo(bullet_count));
            AddAssert("expanded bullets have separate rows", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Where(blueprint => blueprint.Item is DodgeBullet)
                    .Select(blueprint => MathF.Round(blueprint.ScreenSpaceSelectionPoint.Y, 2))
                    .Distinct()
                    .Count(),
                () => Is.EqualTo(bullet_count));
            AddUntilStep("dense bullet bars do not overlap visually", () =>
            {
                LaneTimelineHitObjectBlueprint[] bullets = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                                                .Where(blueprint => blueprint.Item is DodgeBullet)
                                                                .OrderBy(blueprint => blueprint.ScreenSpaceSelectionPoint.Y)
                                                                .ToArray();

                return bullets.Zip(bullets.Skip(1), (first, second) =>
                                  first.SelectionQuad.AABBFloat.Bottom < second.SelectionQuad.AABBFloat.Top)
                              .All(separated => separated);
            });
            AddAssert("inactive layers are hidden", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Count(blueprint => blueprint.Item is not DodgeBullet && blueprint.IsGroupMarkerVisible),
                () => Is.Zero);
            AddAssert("active bullets use the full timeline height", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Where(blueprint => blueprint.Item is DodgeBullet)
                    .Max(blueprint => blueprint.ScreenSpaceSelectionPoint.Y) - this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                                      .Where(blueprint => blueprint.Item is DodgeBullet)
                                                      .Min(blueprint => blueprint.ScreenSpaceSelectionPoint.Y),
                () => Is.GreaterThan(this.ChildrenOfType<Timeline>().Single().ScreenSpaceDrawQuad.Height * 0.8f));
            AddAssert("active bullet layer is centred", () =>
            {
                LaneTimelineHitObjectBlueprint[] bullets = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                                                .Where(blueprint => blueprint.Item is DodgeBullet)
                                                                .ToArray();
                return (bullets.Min(blueprint => blueprint.ScreenSpaceSelectionPoint.Y)
                        + bullets.Max(blueprint => blueprint.ScreenSpaceSelectionPoint.Y)) / 2;
            }, () => Is.EqualTo(this.ChildrenOfType<Timeline>().Single().ScreenSpaceDrawQuad.Centre.Y).Within(0.01f));

            AddStep("click another expanded bullet", () =>
            {
                LaneTimelineHitObjectBlueprint target = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                                            .Where(blueprint => blueprint.Item is DodgeBullet
                                                                                && blueprint.Item != collapsedBulletGroup.Item)
                                                            .MinBy(blueprint => Math.Abs(blueprint.Y - collapsedBulletGroup.Y))!;
                InputManager.MoveMouseTo(target.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("second click selects one bullet",
                () => EditorBeatmap.SelectedHitObjects.Count,
                () => Is.EqualTo(1));
            AddAssert("different bullet selected", () => EditorBeatmap.SelectedHitObjects.Single() != collapsedBulletGroup.Item);
            AddAssert("single selected bullet stays detailed", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Single(blueprint => blueprint.Item == EditorBeatmap.SelectedHitObjects.Single()).IsDetailed);

            AddStep("clear bullet selection", () => EditorBeatmap.SelectedHitObjects.Clear());
            AddUntilStep("emitter group is shown again", () => collapsedEmitterGroup.IsGroupMarkerVisible);
            AddStep("click emitter group", () =>
            {
                InputManager.MoveMouseTo(collapsedEmitterGroup.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("group click selects one emitter",
                () => EditorBeatmap.SelectedHitObjects.Count,
                () => Is.EqualTo(1));
            AddAssert("previous bullet selection cleared", () => EditorBeatmap.SelectedHitObjects.Single(), () => Is.TypeOf<DodgeEmitter>());
            AddUntilStep("selected emitter group expands", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Count(blueprint => blueprint.Item is DodgeEmitter && blueprint.IsGroupMarkerVisible),
                () => Is.EqualTo(emitter_count));
            AddUntilStep("previous bullet layer is hidden", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Count(blueprint => blueprint.Item is DodgeBullet && blueprint.IsGroupMarkerVisible),
                () => Is.Zero);
            AddAssert("expanded emitters have separate rows", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Where(blueprint => blueprint.Item is DodgeEmitter)
                    .Select(blueprint => MathF.Round(blueprint.Y, 2))
                    .Distinct()
                    .Count(),
                () => Is.EqualTo(emitter_count));

            AddStep("click another expanded emitter", () =>
            {
                LaneTimelineHitObjectBlueprint target = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                                            .Where(blueprint => blueprint.Item is DodgeEmitter
                                                                                && blueprint.Item != collapsedEmitterGroup.Item)
                                                            .MinBy(blueprint => Math.Abs(blueprint.Y - collapsedEmitterGroup.Y))!;
                InputManager.MoveMouseTo(target.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("second emitter click replaces selection",
                () => EditorBeatmap.SelectedHitObjects.Count,
                () => Is.EqualTo(1));
            AddAssert("different emitter selected", () => EditorBeatmap.SelectedHitObjects.Single() != collapsedEmitterGroup.Item);
        }

        [Test]
        public void TestClickingTimelineObjectSpreadsWholeLayerVertically()
        {
            DodgeEmitter[] emitters = Enumerable.Range(0, 3).Select(index => new DodgeEmitter
            {
                StartTime = index * 500,
                Duration = 200,
                Position = new Vector2(64, 64 + index * 48),
                AimPosition = new Vector2(448, 64 + index * 48),
            }).ToArray();

            AddStep("add non-overlapping emitters and another layer", () =>
            {
                EditorBeatmap.AddRange(emitters);
                EditorBeatmap.Add(new DodgeBullet
                {
                    StartTime = 0,
                    Duration = 1000,
                    Position = new Vector2(32),
                    EndPosition = new Vector2(480, 32),
                });
            });
            AddUntilStep("timeline objects loaded", () => timelineEmitters().Length, () => Is.EqualTo(emitters.Length));
            AddStep("click emitter on timeline", () =>
            {
                LaneTimelineHitObjectBlueprint target = timelineEmitters()[1];
                InputManager.MoveMouseTo(target.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("emitter selected from timeline", () => EditorBeatmap.SelectedHitObjects.SingleOrDefault(), () => Is.SameAs(emitters[1]));
            AddUntilStep("other layers hidden", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Where(blueprint => blueprint.Item is not DodgeEmitter)
                    .All(blueprint => !blueprint.IsGroupMarkerVisible));
            AddAssert("every emitter has a separate vertical row", () =>
                timelineEmitters().Select(blueprint => MathF.Round(blueprint.ScreenSpaceSelectionPoint.Y, 2)).Distinct().Count(),
                () => Is.EqualTo(emitters.Length));
            AddAssert("emitter layer uses full timeline height", () =>
            {
                LaneTimelineHitObjectBlueprint[] blueprints = timelineEmitters();
                return blueprints.Max(blueprint => blueprint.ScreenSpaceSelectionPoint.Y)
                       - blueprints.Min(blueprint => blueprint.ScreenSpaceSelectionPoint.Y);
            }, () => Is.GreaterThan(timelineEmitters()[0].Parent!.DrawHeight * 0.6f));

            LaneTimelineHitObjectBlueprint[] timelineEmitters()
                => this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                       .Where(blueprint => emitters.Contains(blueprint.Item))
                       .OrderBy(blueprint => blueprint.Item.StartTime)
                       .ToArray();
        }

        [Test]
        public void TestEffectiveDodgeSpansRemainOnTimeline()
        {
            DodgeBullet[] bullets = Enumerable.Range(0, 8).Select(index => new DodgeBullet
            {
                StartTime = index * 250,
                Duration = 100,
                Position = new Vector2(256, 192),
                EndPosition = new Vector2(258, 192),
                ContinueUntilExit = true,
            }).ToArray();
            var emitter = new DodgeEmitter
            {
                StartTime = 4500,
                Duration = 1000,
                Position = new Vector2(256, 32),
                AimPosition = new Vector2(256, 192),
            };
            var arena = new DodgeArenaChange
            {
                StartTime = 0,
                Duration = 500,
                TargetPosition = new Vector2(32, 24),
                TargetSize = new Vector2(448, 336),
            };

            AddStep("add effective-span objects", () => EditorBeatmap.AddRange(bullets.Cast<DodgeHitObject>().Append(emitter).Append(arena)));
            AddStep("seek beyond nominal durations", () => EditorClock.Seek(5000));
            AddUntilStep("projectile timeline lanes retained", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Count(blueprint => bullets.Any(bullet => bullet == blueprint.Item) || blueprint.Item == emitter),
                () => Is.EqualTo(bullets.Length + 1));
            AddAssert("bullet bar reaches movement end", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>().Single(blueprint => blueprint.Item == bullets[0]).TimelineEndTime,
                () => Is.EqualTo(bullets[0].MovementEndTime).Within(0.01));
            AddAssert("completed arena transition is not stretched to the playhead", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>().Any(blueprint => blueprint.Item == arena),
                () => Is.False);
            AddUntilStep("staggered bullets reuse one row", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Where(blueprint => bullets.Any(bullet => bullet == blueprint.Item))
                    .Select(blueprint => MathF.Round(blueprint.Y, 2))
                    .Distinct()
                    .Count(),
                () => Is.EqualTo(1));
            AddAssert("lanes keep bullet emitter order", () =>
            {
                float bulletY = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>().Single(blueprint => blueprint.Item == bullets[0]).Y;
                float emitterY = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>().Single(blueprint => blueprint.Item == emitter).Y;
                return bulletY < emitterY;
            });
            AddStep("activate bullet layer", () => EditorBeatmap.SelectedHitObjects.Add(bullets[0]));
            AddUntilStep("active bullet layer exposes every visible object", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                    .Where(blueprint => bullets.Any(bullet => bullet == blueprint.Item))
                    .Select(blueprint => MathF.Round(blueprint.Y, 3))
                    .Distinct()
                    .Count(),
                () => Is.EqualTo(bullets.Length));
        }

        [Test]
        public void TestTimelineRemovesObjectsAfterSeekingBackwards()
        {
            var early = new DodgeBullet
            {
                StartTime = 1000,
                Duration = 250,
                Position = new Vector2(64, 64),
                EndPosition = new Vector2(256, 64),
            };
            var late = new DodgeBullet
            {
                StartTime = 9000,
                Duration = 250,
                Position = new Vector2(64, 128),
                EndPosition = new Vector2(256, 128),
            };

            AddStep("add distant timeline objects", () => EditorBeatmap.AddRange(new[] { early, late }));
            AddStep("seek to late object", () => EditorClock.Seek(late.StartTime));
            AddUntilStep("late timeline object visible", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>().Any(blueprint => blueprint.Item == late));
            AddStep("seek back to early object", () => EditorClock.Seek(early.StartTime));
            AddUntilStep("early timeline object visible", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>().Any(blueprint => blueprint.Item == early));
            AddUntilStep("late timeline object removed", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>().Any(blueprint => blueprint.Item == late),
                () => Is.False);
        }

        [Test]
        public void TestOffscreenObjectsDoNotConsumeExpandedLayerRows()
        {
            // Mirrors the map which exposed this bug: 327 standalone bullets,
            // with 51 belonging to the local timeline window.
            const int offscreen_count = 276;
            const int visible_count = 51;
            const double visible_time = 1000;

            DodgeBullet[] offscreenBullets = Enumerable.Range(0, offscreen_count).Select(index => new DodgeBullet
            {
                StartTime = 50000 + index * 100,
                Duration = 250,
                Position = new Vector2(32, 32),
                EndPosition = new Vector2(480, 32),
            }).ToArray();
            DodgeBullet[] visibleBullets = Enumerable.Range(0, visible_count).Select(index => new DodgeBullet
            {
                StartTime = visible_time,
                Duration = 500,
                Position = new Vector2(20 + index * 27, 0),
                EndPosition = new Vector2(20 + index * 27, 384),
            }).ToArray();

            AddStep("add map-wide and local bullets", () => EditorBeatmap.AddRange(offscreenBullets.Concat(visibleBullets)));
            AddStep("seek to local bullet group", () => EditorClock.Seek(visible_time));
            AddUntilStep("all local timeline objects loaded", () => localBlueprints().Length, () => Is.EqualTo(visible_count));
            AddUntilStep("offscreen timeline objects removed", () =>
                this.ChildrenOfType<LaneTimelineHitObjectBlueprint>().Count(blueprint => blueprint.Item is DodgeBullet),
                () => Is.EqualTo(visible_count));
            AddStep("click local bullet on timeline", () =>
            {
                LaneTimelineHitObjectBlueprint target = localBlueprints()[visible_count / 2];
                InputManager.MoveMouseTo(target.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("local bullet selected", () =>
                EditorBeatmap.SelectedHitObjects.Count == 1
                && visibleBullets.Contains(EditorBeatmap.SelectedHitObjects.Single()));
            AddUntilStep("every local bullet has its own row", () =>
                localBlueprints().Select(blueprint => MathF.Round(blueprint.ScreenSpaceSelectionPoint.Y, 2)).Distinct().Count(),
                () => Is.EqualTo(visible_count));
            AddAssert("local rows fill timeline height", () =>
            {
                LaneTimelineHitObjectBlueprint[] blueprints = localBlueprints();
                return blueprints.Max(blueprint => blueprint.ScreenSpaceSelectionPoint.Y)
                       - blueprints.Min(blueprint => blueprint.ScreenSpaceSelectionPoint.Y);
            }, () => Is.GreaterThan(this.ChildrenOfType<Timeline>().Single().ScreenSpaceDrawQuad.Height * 0.8f));

            LaneTimelineHitObjectBlueprint[] localBlueprints()
                => this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                       .Where(blueprint => visibleBullets.Contains(blueprint.Item))
                       .ToArray();
        }

        [Test]
        public void TestOverlappingEmittersUseSeparateTimelineRows()
        {
            var longEmitter = new DodgeEmitter
            {
                StartTime = 0,
                Duration = 1000,
                Position = new Vector2(100),
                AimPosition = new Vector2(300, 100),
            };
            var shortEmitter = new DodgeEmitter
            {
                StartTime = 250,
                Duration = 250,
                Position = new Vector2(100, 200),
                AimPosition = new Vector2(300, 200),
            };

            LaneTimelineHitObjectBlueprint longBlueprint = null!;
            LaneTimelineHitObjectBlueprint shortBlueprint = null!;

            AddStep("add overlapping emitters", () => EditorBeatmap.AddRange(new[] { longEmitter, shortEmitter }));
            AddUntilStep("emitter timeline blueprints loaded", () =>
            {
                longBlueprint = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                    .SingleOrDefault(blueprint => blueprint.Item == longEmitter)!;
                shortBlueprint = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                     .SingleOrDefault(blueprint => blueprint.Item == shortEmitter)!;
                return longBlueprint != null && shortBlueprint != null;
            });
            AddUntilStep("inactive emitter layer shares one row",
                () => Math.Abs(longBlueprint.Y - shortBlueprint.Y),
                () => Is.LessThan(0.1f));

            AddStep("activate emitter layer", () =>
            {
                InputManager.MoveMouseTo(shortBlueprint.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("one emitter selected", () => EditorBeatmap.SelectedHitObjects.Count, () => Is.EqualTo(1));
            AddUntilStep("active emitter layer exposes overlap",
                () => Math.Abs(longBlueprint.ScreenSpaceSelectionPoint.Y - shortBlueprint.ScreenSpaceSelectionPoint.Y),
                () => Is.EqualTo(longBlueprint.Parent!.DrawHeight / 2).Within(0.1));
            AddStep("select shorter emitter", () =>
            {
                InputManager.MoveMouseTo(shortBlueprint.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("shorter emitter selected alone", () => EditorBeatmap.SelectedHitObjects.SingleOrDefault(), () => Is.SameAs(shortEmitter));
        }

        [Test]
        public void TestOverlappingArenasExpandOnlyWhenArenaLayerIsActive()
        {
            var firstArena = new DodgeArenaChange
            {
                StartTime = 0,
                Duration = 1000,
                TargetPosition = new Vector2(32, 24),
                TargetSize = new Vector2(448, 336),
            };
            var secondArena = new DodgeArenaChange
            {
                StartTime = 250,
                Duration = 1000,
                TargetPosition = new Vector2(48, 32),
                TargetSize = new Vector2(416, 320),
            };

            LaneTimelineHitObjectBlueprint firstBlueprint = null!;
            LaneTimelineHitObjectBlueprint secondBlueprint = null!;

            AddStep("add overlapping arenas", () => EditorBeatmap.AddRange(new[] { firstArena, secondArena }));
            AddUntilStep("arena timeline blueprints loaded", () =>
            {
                firstBlueprint = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                     .SingleOrDefault(blueprint => blueprint.Item == firstArena)!;
                secondBlueprint = this.ChildrenOfType<LaneTimelineHitObjectBlueprint>()
                                      .SingleOrDefault(blueprint => blueprint.Item == secondArena)!;
                return firstBlueprint != null && secondBlueprint != null;
            });
            AddUntilStep("inactive arena layer shares one row",
                () => Math.Abs(firstBlueprint.Y - secondBlueprint.Y),
                () => Is.LessThan(0.1f));
            AddStep("activate arena layer", () =>
            {
                InputManager.MoveMouseTo(firstBlueprint.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("one arena selected", () => EditorBeatmap.SelectedHitObjects.Count, () => Is.EqualTo(1));
            AddUntilStep("active arena layer exposes overlap",
                () => Math.Abs(firstBlueprint.ScreenSpaceSelectionPoint.Y - secondBlueprint.ScreenSpaceSelectionPoint.Y),
                () => Is.EqualTo(firstBlueprint.Parent!.DrawHeight / 2).Within(0.1));
            AddStep("select first arena", () =>
            {
                InputManager.MoveMouseTo(firstBlueprint.ScreenSpaceSelectionPoint);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("first arena selected", () => EditorBeatmap.SelectedHitObjects.SingleOrDefault(), () => Is.SameAs(firstArena));
        }

        [Test]
        public void TestEmitterSelectionDoesNotCoverEntireFanBounds()
        {
            var emitter = new DodgeEmitter
            {
                StartTime = 0,
                Duration = 1000,
                Position = new Vector2(100),
                AimPosition = new Vector2(300, 100),
                MovementEndPosition = new Vector2(100),
                BulletCount = 2,
                SpreadAngle = 90,
                TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.Path,
            };

            DodgePlayfield playfield = null!;
            DodgeEmitterSelectionBlueprint blueprint = null!;

            AddStep("add emitter with wide fan", () => EditorBeatmap.Add(emitter));
            AddUntilStep("emitter editor components loaded", () =>
            {
                playfield = this.ChildrenOfType<DodgePlayfield>().SingleOrDefault()!;
                blueprint = this.ChildrenOfType<DodgeEmitterSelectionBlueprint>().SingleOrDefault()!;
                return playfield != null && blueprint != null;
            });

            AddStep("click empty space inside fan bounds", () =>
            {
                InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(new Vector2(200, 100)));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("fan bounds do not select emitter", () => EditorBeatmap.SelectedHitObjects, () => Is.Empty);

            AddStep("click emitter source", () =>
            {
                InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(emitter.Position));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("source selects emitter", () => EditorBeatmap.SelectedHitObjects.SingleOrDefault(), () => Is.SameAs(emitter));
            AddAssert("selected fan guide shown", () => blueprint.ChildrenOfType<DodgeEmitterPathPiece>().Single().IsPresent);
        }

        [Test]
        public void TestSelectedBulletPathUpdatesWhenContinuingUntilExit()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 0,
                Duration = 1000,
                Position = new Vector2(100, 192),
                EndPosition = new Vector2(200, 192),
            };

            DodgeBulletPathPiece pathPiece = null!;
            int initialRebuildCount = 0;

            AddStep("add selected bullet", () =>
            {
                EditorBeatmap.Add(bullet);
                EditorBeatmap.SelectedHitObjects.Add(bullet);
            });
            AddUntilStep("selected path loaded", () =>
            {
                pathPiece = this.ChildrenOfType<DodgeBulletPathPiece>().SingleOrDefault()!;
                return pathPiece?.GeometryRebuildCount > 0;
            });
            AddAssert("path initially ends at control point", () =>
                Precision.AlmostEquals(pathPiece.EndMarkerPosition, bullet.EndPosition, 0.001f));
            AddStep("store initial rebuild count", () => initialRebuildCount = pathPiece.GeometryRebuildCount);

            AddStep("continue bullet until exit", () =>
            {
                EditorBeatmap.BeginChange();
                bullet.ContinueUntilExit = true;
                EditorBeatmap.Update(bullet);
                EditorBeatmap.EndChange();
            });
            AddUntilStep("editor path geometry rebuilt", () => pathPiece.GeometryRebuildCount, () => Is.GreaterThan(initialRebuildCount));
            AddAssert("selected path reaches exit", () =>
                Precision.AlmostEquals(pathPiece.EndMarkerPosition, bullet.TrajectoryEndPosition, 0.001f));
            AddAssert("selected path bounds include exit", () => pathPiece.GamefieldBounds.Right,
                () => Is.GreaterThanOrEqualTo(bullet.TrajectoryEndPosition.X));

            AddStep("stop bullet at control point", () =>
            {
                EditorBeatmap.BeginChange();
                bullet.ContinueUntilExit = false;
                EditorBeatmap.Update(bullet);
                EditorBeatmap.EndChange();
            });
            AddUntilStep("selected path contracts again", () =>
                Precision.AlmostEquals(pathPiece.EndMarkerPosition, bullet.EndPosition, 0.001f));
        }

        [Test]
        public void TestEmitterBulletCountCanChangeWithPathGuide()
        {
            DodgeEmitter emitter = null!;
            DodgeEmitterToolboxGroup toolbox = null!;
            DodgeEmitterPathPiece pathPiece = null!;
            int rebuildCount = 0;

            AddStep("add selected path emitter", () =>
            {
                emitter = new DodgeEmitter
                {
                    StartTime = 1000,
                    Duration = 1000,
                    Position = new Vector2(64, 64),
                    AimPosition = new Vector2(256, 64),
                    BurstCount = DodgeEmitter.MAX_BURST_COUNT,
                    BurstBeatDivisor = 0,
                    TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.Path,
                };
                EditorBeatmap.Add(emitter);
                EditorBeatmap.SelectedHitObjects.Add(emitter);
            });
            AddStep("get emitter toolbox", () => toolbox = this.ChildrenOfType<DodgeEmitterToolboxGroup>().Single());
            AddStep("keep path guide selected", () =>
                this.ChildrenOfType<DodgeHitObjectComposer>().Single().EditorSettings.EmitterTrajectoryGuideStyle.Value = DodgeTrajectoryGuideStyle.Path);
            AddUntilStep("editor path geometry available", () =>
            {
                pathPiece = this.ChildrenOfType<DodgeEmitterPathPiece>().SingleOrDefault()!;
                return pathPiece?.GeometryRebuildCount > 0;
            });
            AddStep("store editor path rebuild count", () => rebuildCount = pathPiece.GeometryRebuildCount);
            AddWaitStep("run unchanged editor frames", 5);
            AddAssert("editor path geometry stays cached", () => pathPiece.GeometryRebuildCount, () => Is.EqualTo(rebuildCount));
            AddStep("set maximum bullet count", () => toolbox.BulletCount.Value = DodgeEmitter.MAX_BULLET_COUNT);
            AddWaitStep("process maximum count", 1);
            AddAssert("maximum count applied", () => emitter.EffectiveBulletCount, () => Is.EqualTo(DodgeEmitter.MAX_BULLET_COUNT));
            AddStep("set minimum bullet count", () => toolbox.BulletCount.Value = DodgeEmitter.MIN_BULLET_COUNT);
            AddWaitStep("process minimum count", 1);
            AddAssert("minimum count applied", () => emitter.EffectiveBulletCount, () => Is.EqualTo(DodgeEmitter.MIN_BULLET_COUNT));
            AddStep("grow count again", () => toolbox.BulletCount.Value = 17);
            AddWaitStep("process regrown count", 1);
            AddAssert("emitter count updated", () => emitter.EffectiveBulletCount, () => Is.EqualTo(17));
        }

        [Test]
        public void TestPlacementUsesRememberedTimelineDuration()
        {
            var start = new Vector2(100, 100);
            var end = new Vector2(400, 300);
            var secondStart = new Vector2(200, 100);
            var secondEnd = start;
            Playfield playfield = null!;
            DodgeHitObjectComposer composer = null!;

            AddStep("select bullet tool", () => InputManager.Key(Key.Number2));
            AddStep("get playfield", () => playfield = this.ChildrenOfType<DodgePlayfield>().Single());
            AddStep("get composer", () => composer = this.ChildrenOfType<DodgeHitObjectComposer>().Single());
            AddStep("move to bullet start", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(start)));
            AddStep("fix bullet start", () => InputManager.Click(MouseButton.Left));
            AddStep("move bullet end", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(end)));
            AddStep("fix bullet end", () => InputManager.Click(MouseButton.Left));

            AddAssert("one bullet placed", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(1));
            AddAssert("placed object is bullet", () => EditorBeatmap.HitObjects.Single(), () => Is.TypeOf<DodgeBullet>());
            AddAssert("start position stored", () => Precision.AlmostEquals(((DodgeBullet)EditorBeatmap.HitObjects.Single()).Position, start));
            AddAssert("movement end stored", () => Precision.AlmostEquals(((DodgeBullet)EditorBeatmap.HitObjects.Single()).EndPosition, end));
            AddAssert("default duration is four snap gaps", () => Precision.AlmostEquals(((DodgeBullet)EditorBeatmap.HitObjects.Single()).Duration, 500, 1));

            AddStep("stretch duration like timeline handle", () =>
            {
                var bullet = (DodgeBullet)EditorBeatmap.HitObjects.Single();
                EditorBeatmap.BeginChange();
                bullet.Duration = 1000;
                EditorBeatmap.Update(bullet);
                EditorBeatmap.EndChange();
            });

            AddStep("move to second bullet start", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(secondStart)));
            AddStep("fix second bullet start", () => InputManager.Click(MouseButton.Left));
            AddStep("move second bullet end", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(secondEnd)));
            AddStep("fix second bullet end", () => InputManager.Click(MouseButton.Left));
            AddAssert("same-time bullet is not replaced", () => EditorBeatmap.HitObjects.Count, () => Is.EqualTo(2));
            AddAssert("bullet tool remains active", () => composer.BlueprintContainer.CurrentTool, () => Is.TypeOf<DodgeBulletCompositionTool>());
            AddAssert("both bullets have same timestamp", () => EditorBeatmap.HitObjects.Select(h => h.StartTime).Distinct().Count(), () => Is.EqualTo(1));
            AddAssert("next bullet keeps duration", () => EditorBeatmap.HitObjects.Cast<DodgeBullet>().All(b => Precision.AlmostEquals(b.Duration, 1000, 1)));
            AddAssert("second movement end stored", () => Precision.AlmostEquals(EditorBeatmap.HitObjects.Cast<DodgeBullet>().Last().EndPosition, secondEnd));

            AddStep("undo second placement", () => Editor.Undo());
            AddAssert("one bullet remains", () => EditorBeatmap.HitObjects.Count == 1);
            AddStep("undo duration change", () => Editor.Undo());
            AddAssert("duration returned to four gaps", () => Precision.AlmostEquals(((DodgeBullet)EditorBeatmap.HitObjects.Single()).Duration, 500, 1));
            AddStep("undo first placement", () => Editor.Undo());
            AddAssert("bullet removed", () => EditorBeatmap.HitObjects.Count == 0);
            AddStep("redo first placement", () => Editor.Redo());
            AddStep("redo duration change", () => Editor.Redo());
            AddStep("redo second placement", () => Editor.Redo());
            AddAssert("both bullets restored", () => EditorBeatmap.HitObjects.Count == 2);
        }

        [Test]
        public void TestBulletEndpointsCanBeOutsideArena()
        {
            var start = new Vector2(-20, 100);
            var end = new Vector2(DodgePlayfield.WIDTH + 20, 280);
            Playfield playfield = null!;

            AddStep("select bullet tool", () => InputManager.Key(Key.Number2));
            AddStep("get playfield", () => playfield = this.ChildrenOfType<DodgePlayfield>().Single());
            AddStep("move outside arena to start", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(start)));
            AddStep("fix outside start", () => InputManager.Click(MouseButton.Left));
            AddStep("move outside arena to end", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(end)));
            AddStep("fix outside end", () => InputManager.Click(MouseButton.Left));

            AddAssert("outside bullet placed", () => EditorBeatmap.HitObjects.OfType<DodgeBullet>().Count(), () => Is.EqualTo(1));
            AddAssert("outside start stored", () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single().Position, start));
            AddAssert("outside end stored", () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single().EndPosition, end));
        }

        [Test]
        public void TestGridDisplayPlacementAndMovementSnapping()
        {
            var unsnappedStart = new Vector2(101, 102);
            var unsnappedEnd = new Vector2(207, 213);
            var snappedStart = new Vector2(96, 96);
            var snappedEnd = new Vector2(208, 208);
            var unsnappedMoveTarget = new Vector2(173, 141);
            var snappedMoveTarget = new Vector2(176, 144);
            DodgePlayfield playfield = null!;
            DodgeHitObjectComposer composer = null!;
            DodgeEditorViewToolboxGroup viewToolbox = null!;
            DodgeEditorGrid grid = null!;

            AddStep("get editor grid components", () =>
            {
                playfield = this.ChildrenOfType<DodgePlayfield>().Single();
                composer = this.ChildrenOfType<DodgeHitObjectComposer>().Single();
                viewToolbox = this.ChildrenOfType<DodgeEditorViewToolboxGroup>().Single();
                grid = this.ChildrenOfType<DodgeEditorGrid>().Single();
            });
            AddAssert("grid initially hidden", () => !grid.IsGridVisible);
            AddStep("set grid spacing", () => composer.GridToolbox.GridLineSpacing.Value = 16);
            AddStep("enable grid snap", () => viewToolbox.GridSnapEnabled = true);
            AddAssert("grid becomes visible", () => grid.IsGridVisible);

            AddStep("select bullet tool", () => InputManager.Key(Key.Number2));
            AddStep("move to unsnapped start", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(unsnappedStart)));
            AddStep("fix snapped start", () => InputManager.Click(MouseButton.Left));
            AddStep("move to unsnapped end", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(unsnappedEnd)));
            AddStep("fix snapped end", () => InputManager.Click(MouseButton.Left));
            AddAssert("start snapped to grid",
                () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single().Position, snappedStart));
            AddAssert("end snapped to grid",
                () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single().EndPosition, snappedEnd));

            AddStep("select selection tool", () => InputManager.Key(Key.Number1));
            AddStep("move to bullet start", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(snappedStart)));
            AddStep("select bullet", () => InputManager.Click(MouseButton.Left));
            AddStep("begin drag", () => InputManager.PressButton(MouseButton.Left));
            AddStep("drag to unsnapped target", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(unsnappedMoveTarget)));
            AddStep("finish drag", () => InputManager.ReleaseButton(MouseButton.Left));
            AddAssert("moved start snapped to grid",
                () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single().Position, snappedMoveTarget));
            AddAssert("movement preserves snapped path offset",
                () => Precision.AlmostEquals(
                    EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single().EndPosition,
                    snappedEnd + snappedMoveTarget - snappedStart));
        }

        [Test]
        public void TestBulletDragTracksMouseAtCompactScale()
        {
            var start = new Vector2(100, 100);
            var end = new Vector2(220, 180);
            var movement = new Vector2(80, 40);
            var bullet = new DodgeBullet
            {
                Position = start,
                EndPosition = end,
                Duration = 1000,
            };
            DodgePlayfield playfield = null!;
            DodgeEditorViewToolboxGroup viewToolbox = null!;

            AddStep("add bullet", () => EditorBeatmap.Add(bullet));
            AddStep("get editor components", () =>
            {
                playfield = this.ChildrenOfType<DodgePlayfield>().Single();
                viewToolbox = this.ChildrenOfType<DodgeEditorViewToolboxGroup>().Single();
            });
            AddStep("enable smaller playfield", () => viewToolbox.CompactPlayfield = true);
            AddStep("select selection tool", () => InputManager.Key(Key.Number1));
            AddStep("move to bullet start", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(start)));
            AddStep("select bullet", () => InputManager.Click(MouseButton.Left));
            AddAssert("bullet selected", () => EditorBeatmap.SelectedHitObjects.Contains(bullet));

            AddStep("begin drag", () => InputManager.PressButton(MouseButton.Left));
            AddStep("drag halfway", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(start + movement / 2)));
            AddAssert("halfway movement follows cursor", () => Precision.AlmostEquals(bullet.Position, start + movement / 2, 0.1f));
            AddStep("drag to target", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(start + movement)));
            AddStep("finish drag", () => InputManager.ReleaseButton(MouseButton.Left));

            AddAssert("start follows total mouse movement", () => Precision.AlmostEquals(bullet.Position, start + movement, 0.1f));
            AddAssert("end follows total mouse movement", () => Precision.AlmostEquals(bullet.EndPosition, end + movement, 0.1f));
        }

        [Test]
        public void TestArenaChangeCanBePlaced()
        {
            var topLeft = new Vector2(80, 60);
            var bottomRight = new Vector2(420, 300);
            Playfield playfield = null!;
            DodgeArenaToolboxGroup arenaToolbox = null!;

            AddStep("select arena tool", () => InputManager.Key(Key.Number3));
            AddStep("get playfield", () => playfield = this.ChildrenOfType<DodgePlayfield>().Single());
            AddStep("move to arena top-left", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(topLeft)));
            AddStep("fix arena top-left", () => InputManager.Click(MouseButton.Left));
            AddStep("move to arena bottom-right", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(bottomRight)));
            AddStep("fix arena bottom-right", () => InputManager.Click(MouseButton.Left));

            AddAssert("arena change placed", () => EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Count(), () => Is.EqualTo(1));
            AddAssert("arena target position stored", () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Single().TargetPosition, topLeft));
            AddAssert("arena target size stored", () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Single().TargetSize, bottomRight - topLeft));
            AddAssert("arena transition lasts four snap gaps", () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Single().Duration, 500, 1));

            AddStep("get arena toolbox", () => arenaToolbox = this.ChildrenOfType<DodgeArenaToolboxGroup>().Single());
            AddStep("make existing arena long", () =>
            {
                DodgeArenaChange arena = EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Single();
                EditorBeatmap.BeginChange();
                arena.Duration = 2000;
                EditorBeatmap.Update(arena);
                EditorBeatmap.EndChange();
            });
            AddStep("reset arena", () => arenaToolbox.ResetArena());
            AddAssert("reset keeps one arena change", () => EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Count(), () => Is.EqualTo(1));
            AddAssert("reset returns to four snap gaps", () => EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Single().Duration, () => Is.EqualTo(500).Within(1));
            AddAssert("reset position is default", () => EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Single().TargetPosition, () => Is.EqualTo(Vector2.Zero));
            AddAssert("reset size is default", () => EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Single().TargetSize, () => Is.EqualTo(DodgePlayfield.BASE_SIZE));
        }

        [Test]
        public void TestFirstArenaChangeIsPlacedAtZeroFromLaterEditorTime()
        {
            var topLeft = new Vector2(80, 60);
            var bottomRight = new Vector2(420, 300);
            Playfield playfield = null!;

            AddStep("seek after timing origin", () => EditorClock.Seek(1000));
            AddStep("select arena tool", () => InputManager.Key(Key.Number3));
            AddStep("get playfield", () => playfield = this.ChildrenOfType<DodgePlayfield>().Single());
            AddStep("move to arena top-left", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(topLeft)));
            AddStep("fix arena top-left", () => InputManager.Click(MouseButton.Left));
            AddStep("move to arena bottom-right", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(bottomRight)));
            AddStep("fix arena bottom-right", () => InputManager.Click(MouseButton.Left));

            AddAssert("first arena is placed at zero",
                () => EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Single().StartTime,
                () => Is.Zero);
        }

        [Test]
        public void TestInitialArenaCanBeReplacedBeforeFirstTimingPoint()
        {
            var topLeft = new Vector2(96, 72);
            var bottomRight = new Vector2(416, 312);
            DodgePlayfield playfield = null!;
            DodgeHitObjectComposer composer = null!;

            AddStep("set delayed timing and initial arena", () =>
            {
                EditorBeatmap.ControlPointInfo.Clear();
                EditorBeatmap.ControlPointInfo.Add(1000, new TimingControlPoint { BeatLength = 500 });
                EditorBeatmap.Add(new DodgeArenaChange
                {
                    StartTime = 0,
                    TargetPosition = Vector2.Zero,
                    TargetSize = DodgePlayfield.BASE_SIZE,
                });
                EditorClock.Seek(0);
            });
            AddStep("get editor components", () =>
            {
                playfield = this.ChildrenOfType<DodgePlayfield>().Single();
                composer = this.ChildrenOfType<DodgeHitObjectComposer>().Single();
            });
            AddStep("select arena tool", () => InputManager.Key(Key.Number3));
            AddAssert("arena tool selected", () => composer.BlueprintContainer.CurrentTool, () => Is.TypeOf<DodgeArenaChangeCompositionTool>());
            AddStep("move to arena top-left", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(topLeft)));
            AddStep("fix arena top-left", () => InputManager.Click(MouseButton.Left));
            AddStep("move to arena bottom-right", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(bottomRight)));
            AddStep("fix arena bottom-right", () => InputManager.Click(MouseButton.Left));

            AddAssert("initial arena replaced", () => EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Count(), () => Is.EqualTo(1));
            AddAssert("replacement remains at zero", () => EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Single().StartTime, () => Is.Zero);
            AddAssert("replacement position stored",
                () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Single().TargetPosition, topLeft));
            AddAssert("replacement size stored",
                () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Single().TargetSize, bottomRight - topLeft));
        }

        [Test]
        public void TestEmitterPlacementAndCompactPlayfield()
        {
            var source = new Vector2(256, 192);
            var aim = new Vector2(456, 192);
            Playfield playfield = null!;
            DodgeHitObjectComposer composer = null!;
            DodgeBulletToolboxGroup bulletToolbox = null!;
            DodgeEmitterToolboxGroup emitterToolbox = null!;
            DodgeEditorViewToolboxGroup viewToolbox = null!;
            DodgeCoverageOverlay coverageOverlay = null!;

            AddStep("get editor components", () =>
            {
                playfield = this.ChildrenOfType<DodgePlayfield>().Single();
                composer = this.ChildrenOfType<DodgeHitObjectComposer>().Single();
                bulletToolbox = this.ChildrenOfType<DodgeBulletToolboxGroup>().Single();
                emitterToolbox = this.ChildrenOfType<DodgeEmitterToolboxGroup>().Single();
                viewToolbox = this.ChildrenOfType<DodgeEditorViewToolboxGroup>().Single();
                coverageOverlay = this.ChildrenOfType<DodgeCoverageOverlay>().Single();
            });
            AddStep("configure emitter", () =>
            {
                Assert.That(composer.EditorSettings.EmitterTrajectoryGuideStyle.Value, Is.EqualTo(DodgeTrajectoryGuideStyle.Path));
                emitterToolbox.BulletCount.Value = 7;
                emitterToolbox.SpreadAngle.Value = 180;
                emitterToolbox.ContinueUntilExit.Value = true;
            });
            AddStep("select emitter tool", () => InputManager.Key(Key.Number4));
            AddStep("move to emitter source", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(source)));
            AddStep("fix emitter source", () => InputManager.Click(MouseButton.Left));
            AddStep("move emitter aim", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(aim)));
            AddStep("fix emitter aim", () => InputManager.Click(MouseButton.Left));

            AddAssert("one emitter placed", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Count(), () => Is.EqualTo(1));
            AddAssert("emitter tool remains active", () => composer.BlueprintContainer.CurrentTool, () => Is.TypeOf<DodgeEmitterCompositionTool>());
            AddAssert("source stored", () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().Position, source));
            AddAssert("aim stored", () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().AimPosition, aim));
            AddAssert("count stored", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().BulletCount, () => Is.EqualTo(7));
            AddAssert("spread stored", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().SpreadAngle, () => Is.EqualTo(180));
            AddAssert("emitter continues until exit", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().ContinueUntilExit);
            AddAssert("emitter uses path guide by default", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().TrajectoryGuideStyle,
                () => Is.EqualTo(DodgeTrajectoryGuideStyle.Path));
            AddAssert("duration uses four snap gaps", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().Duration, () => Is.EqualTo(500).Within(1));

            AddStep("show bullet coverage", () => viewToolbox.ShowBulletCoverage = true);
            AddAssert("composer receives coverage setting", () => composer.EditorSettings.ShowBulletCoverage.Value);
            AddAssert("coverage overlay is visible", () => coverageOverlay.IsCoverageVisible);
            AddUntilStep("coverage includes safe and crossed cells",
                () => coverageOverlay.HitCounts.Any(count => count == 0) && coverageOverlay.HitCounts.Any(count => count > 0));
            AddStep("hide bullet coverage", () => viewToolbox.ShowBulletCoverage = false);
            AddAssert("coverage overlay is hidden", () => !coverageOverlay.IsCoverageVisible);

            AddStep("enable smaller playfield", () => viewToolbox.CompactPlayfield = true);
            AddAssert("composer receives compact setting", () => composer.EditorSettings.CompactPlayfield.Value);
            AddAssert("all editor playfield layers shrink together",
                () =>
                {
                    var containers = composer.ChildrenOfType<DodgeEditorPlayfieldAdjustmentContainer>().ToArray();
                    return containers.Length > 0
                           && containers.All(container => Precision.AlmostEquals(container.Size.X, DodgeEditorPlayfieldAdjustmentContainer.COMPACT_SCALE, 0.01f));
                });

            var outsideStart = new Vector2(-60, 100);
            var outsideEnd = new Vector2(400, 280);
            AddStep("make bullets continue until exit", () => bulletToolbox.ContinueUntilExit.Value = true);
            AddStep("select bullet tool while compact", () => InputManager.Key(Key.Number2));
            AddStep("move to visible outside start", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(outsideStart)));
            AddStep("fix visible outside start", () => InputManager.Click(MouseButton.Left));
            AddStep("move to visible outside end", () => InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(outsideEnd)));
            AddStep("fix visible outside end", () => InputManager.Click(MouseButton.Left));
            AddAssert("compact view stores outside start X", () => EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single().Position.X,
                () => Is.EqualTo(outsideStart.X).Within(0.01f));
            AddAssert("compact view stores outside start Y", () => EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single().Position.Y,
                () => Is.EqualTo(outsideStart.Y).Within(0.01f));
            AddAssert("compact view stores end", () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single().EndPosition, outsideEnd));
            AddAssert("bullet continues until exit", () => EditorBeatmap.HitObjects.OfType<DodgeBullet>().Single().ContinueUntilExit);

            AddStep("restore normal playfield", () => viewToolbox.CompactPlayfield = false);
            AddAssert("all editor playfield layers restore together",
                () =>
                {
                    var containers = composer.ChildrenOfType<DodgeEditorPlayfieldAdjustmentContainer>().ToArray();
                    return containers.Length > 0
                           && containers.All(container => Precision.AlmostEquals(container.Size.X, DodgePlayfieldAdjustmentContainer.DEFAULT_SCALE, 0.01f));
                });
        }

        [Test]
        public void TestMovingEmitterAppearanceAndAutoplayRoute()
        {
            var source = new Vector2(80, 80);
            var aim = new Vector2(360, 80);
            var movementEnd = new Vector2(80, 300);
            var colour = Colour4.FromHex("#39B8FF");
            var outline = Colour4.FromHex("#08273A");
            Playfield playfield = null!;
            DodgeEmitterToolboxGroup emitterToolbox = null!;
            DodgeEditorViewToolboxGroup viewToolbox = null!;
            DodgeAutoplayRouteOverlay routeOverlay = null!;

            AddStep("get moving emitter components", () =>
            {
                playfield = this.ChildrenOfType<DodgePlayfield>().Single();
                emitterToolbox = this.ChildrenOfType<DodgeEmitterToolboxGroup>().Single();
                viewToolbox = this.ChildrenOfType<DodgeEditorViewToolboxGroup>().Single();
                routeOverlay = this.ChildrenOfType<DodgeAutoplayRouteOverlay>().Single();
            });
            AddStep("configure moving emitter appearance", () =>
            {
                emitterToolbox.Repeating.Value = true;
                emitterToolbox.Moving.Value = true;
                emitterToolbox.BurstCount.Value = 4;
                emitterToolbox.BurstBeatDivisor.Value = DodgeEmitterBeatDivisor.Quarter;
                emitterToolbox.ProjectileColour.Value = colour;
            });
            AddStep("select emitter tool", () => InputManager.Key(Key.Number4));
            AddStep("place source", () =>
            {
                InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(source));
                InputManager.Click(MouseButton.Left);
            });
            AddStep("place aim", () =>
            {
                InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(aim));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("moving emitter waits for third click", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Count(), () => Is.Zero);
            AddStep("place movement end", () =>
            {
                InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(movementEnd));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("moving emitter placed", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Count(), () => Is.EqualTo(1));
            AddAssert("movement end stored",
                () => Precision.AlmostEquals(EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().MovementEndPosition, movementEnd));
            AddAssert("burst count stored", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().BurstCount, () => Is.EqualTo(4));
            AddAssert("burst interval stored", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().BurstInterval, () => Is.EqualTo(125));
            AddAssert("burst BPM divisor stored", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().BurstBeatDivisor, () => Is.EqualTo(4));
            AddAssert("source movement stored", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().MoveSource);
            AddAssert("fill colour stored", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().Colour, () => Is.EqualTo(colour));

            AddStep("show autoplay route", () => viewToolbox.ShowAutoplayRoute = true);
            AddAssert("autoplay route overlay visible", () => routeOverlay.IsRouteVisible);
            AddUntilStep("route snapshot contains emitter", () => routeOverlay.SnapshotObjectCount == 1 || routeOverlay.LastCalculationError != null);
            AddAssert("route calculation has no error", () => routeOverlay.LastCalculationError, () => Is.Null);
            AddUntilStep("background route generated", () => routeOverlay.GeneratedRoutePointCount > 2 || routeOverlay.LastCalculationError != null);
            AddAssert("background route has no error", () => routeOverlay.LastCalculationError, () => Is.Null);
            AddUntilStep("autoplay route calculated", () => routeOverlay.RoutePointCount > 2);
            AddUntilStep("only local autoplay window is visible", () =>
                routeOverlay.VisibleRoutePointCount > 2
                && routeOverlay.VisibleRoutePointCount < routeOverlay.RoutePointCount);
            AddStep("hide autoplay route", () => viewToolbox.ShowAutoplayRoute = false);
            AddAssert("autoplay route overlay hidden", () => !routeOverlay.IsRouteVisible);
        }

        [Test]
        public void TestStationaryRepeatingEmitterUsesTwoClickPlacement()
        {
            var source = new Vector2(100, 120);
            var aim = new Vector2(400, 120);
            Playfield playfield = null!;
            DodgeEmitterToolboxGroup emitterToolbox = null!;

            AddStep("get emitter components", () =>
            {
                playfield = this.ChildrenOfType<DodgePlayfield>().Single();
                emitterToolbox = this.ChildrenOfType<DodgeEmitterToolboxGroup>().Single();
            });
            AddStep("configure stationary repeat", () =>
            {
                emitterToolbox.Repeating.Value = true;
                emitterToolbox.Moving.Value = false;
                emitterToolbox.BurstCount.Value = 3;
                emitterToolbox.BurstBeatDivisor.Value = DodgeEmitterBeatDivisor.Half;
            });
            AddStep("select emitter tool", () => InputManager.Key(Key.Number4));
            AddStep("place source", () =>
            {
                InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(source));
                InputManager.Click(MouseButton.Left);
            });
            AddStep("place aim", () =>
            {
                InputManager.MoveMouseTo(playfield.GamefieldToScreenSpace(aim));
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("stationary repeat placed after second click", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Count(), () => Is.EqualTo(1));
            AddAssert("repeat count stored", () => EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().BurstCount, () => Is.EqualTo(3));
            AddAssert("source movement disabled", () => !EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single().MoveSource);
            AddAssert("all bursts use same source", () =>
            {
                DodgeEmitter emitter = EditorBeatmap.HitObjects.OfType<DodgeEmitter>().Single();
                return Enumerable.Range(0, emitter.EffectiveBurstCount).All(index => emitter.SourcePositionAt(index) == emitter.Position);
            });
        }

        [Test]
        public void TestAutoplayRouteCanRestartAfterCancellationAndArenaChange()
        {
            DodgeEditorViewToolboxGroup viewToolbox = null!;
            DodgeAutoplayRouteOverlay routeOverlay = null!;

            AddStep("get autoplay route components", () =>
            {
                viewToolbox = this.ChildrenOfType<DodgeEditorViewToolboxGroup>().Single();
                routeOverlay = this.ChildrenOfType<DodgeAutoplayRouteOverlay>().Single();
            });
            AddStep("add route workload", () =>
            {
                EditorBeatmap.AddRange(Enumerable.Range(0, 32).Select(index => new DodgeBullet
                {
                    StartTime = index * 125,
                    Duration = 4000,
                    Position = new Vector2(index % 2 == 0 ? 0 : DodgePlayfield.WIDTH, 24 + index * 10),
                    EndPosition = new Vector2(index % 2 == 0 ? DodgePlayfield.WIDTH : 0, 24 + index * 10),
                }));
            });
            AddStep("start route calculation", () => viewToolbox.ShowAutoplayRoute = true);
            AddStep("cancel route calculation", () => viewToolbox.ShowAutoplayRoute = false);
            AddWaitStep("allow cancellation to complete", 5);
            AddStep("restart route calculation", () => viewToolbox.ShowAutoplayRoute = true);
            AddStep("place arena while route is enabled", () => EditorBeatmap.Add(new DodgeArenaChange
            {
                StartTime = 0,
                TargetPosition = new Vector2(64, 48),
                TargetSize = new Vector2(384, 288),
            }));
            AddAssert("route remains visible", () => routeOverlay.IsRouteVisible);
            AddAssert("arena was added", () => EditorBeatmap.HitObjects.OfType<DodgeArenaChange>().Count(), () => Is.EqualTo(1));
            AddStep("stop route calculation", () => viewToolbox.ShowAutoplayRoute = false);
        }
    }
}
