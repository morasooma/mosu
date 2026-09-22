// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Bindables;
using osu.Game.Graphics.UserInterface;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Edit.Tools;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.UI;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    [Cached]
    public partial class DodgeHitObjectComposer : HitObjectComposer<DodgeHitObject, DodgeAction>, IEditorTimelineLayoutProvider
    {
        public override Bindable<TernaryState>? SelectionNewComboState => null;

        [Cached]
        public readonly DodgeArenaToolboxGroup ArenaToolbox = new DodgeArenaToolboxGroup();

        [Cached]
        public readonly DodgeEditorSettings EditorSettings = new DodgeEditorSettings();

        [Cached]
        public readonly DodgeGridToolboxGroup GridToolbox = new DodgeGridToolboxGroup();

        private DodgeBulletToolboxGroup bulletToolbox = null!;
        private DodgeEmitterToolboxGroup emitterToolbox = null!;
        private DodgeBeamToolboxGroup beamToolbox = null!;
        private DodgeTimingToolboxGroup timingToolbox = null!;
        private DodgeTransformToolboxGroup transformToolbox = null!;
        private DodgePatternToolboxGroup patternToolbox = null!;
        private DodgeEditorViewToolboxGroup viewToolbox = null!;
        private DodgeCameraToolboxGroup cameraToolbox = null!;
        private DodgeTriggerToolboxGroup triggerToolbox = null!;

        private DodgeEditorGrid positionSnapGrid = null!;
        private ToolboxContext lastToolboxContext;

        public override bool CursorInPlacementArea
            => BlueprintContainer.ReceivePositionalInputAt(InputManager.CurrentState.Mouse.Position);

        public DodgeHitObjectComposer(Ruleset ruleset)
            : base(ruleset)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RightToolbox.Add(new DodgeEditorContextToolboxGroup());
            RightToolbox.Add(cameraToolbox = new DodgeCameraToolboxGroup());
            RightToolbox.Add(triggerToolbox = new DodgeTriggerToolboxGroup());
            RightToolbox.Add(timingToolbox = new DodgeTimingToolboxGroup
            {
                Expanded = { Value = false },
            });
            RightToolbox.Add(bulletToolbox = new DodgeBulletToolboxGroup());
            RightToolbox.Add(emitterToolbox = new DodgeEmitterToolboxGroup());
            RightToolbox.Add(beamToolbox = new DodgeBeamToolboxGroup());
            RightToolbox.Add(transformToolbox = new DodgeTransformToolboxGroup
            {
                RotationHandler = BlueprintContainer.SelectionHandler.RotationHandler,
                ScaleHandler = BlueprintContainer.SelectionHandler.ScaleHandler,
                Expanded = { Value = false },
            });
            RightToolbox.Add(patternToolbox = new DodgePatternToolboxGroup
            {
                Expanded = { Value = false },
            });
            RightToolbox.Add(ArenaToolbox);
            RightToolbox.Add(GridToolbox);
            RightToolbox.Add(viewToolbox = new DodgeEditorViewToolboxGroup
            {
                Expanded = { Value = false },
            });

            GridToolbox.Expanded.Value = false;
        }

        protected override DrawableRuleset<DodgeHitObject> CreateDrawableRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods)
        {
            positionSnapGrid = new DodgeEditorGrid();
            return new DrawableDodgeEditorRuleset(ruleset, beatmap, mods, positionSnapGrid);
        }

        public Vector2 SnapToPositionGrid(Vector2 position)
            => EditorSettings.GridSnapEnabled.Value ? positionSnapGrid.GetSnappedPosition(position) : position;

        protected override IReadOnlyList<CompositionTool<DodgeAction>> CompositionTools => new CompositionTool<DodgeAction>[]
        {
            new DodgeBulletCompositionTool(),
            new DodgeArenaChangeCompositionTool(),
            new DodgeEmitterCompositionTool(),
            new DodgeBeamCompositionTool(),
            // Camera is appended last: tool hotkeys are assigned by array order,
            // and existing maps/tests rely on the established numbering.
            new DodgeCameraChangeCompositionTool(),
            new DodgeTriggerCompositionTool(),
        };

        protected override ComposeBlueprintContainer CreateBlueprintContainer() => new DodgeBlueprintContainer(this);

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateToolboxContext(true);
        }

        protected override void Update()
        {
            base.Update();
            updateToolboxContext(false);
        }

        private void updateToolboxContext(bool force)
        {
            int selectionMask = 0;

            foreach (var selected in EditorBeatmap.SelectedHitObjects)
            {
                selectionMask |= selected switch
                {
                    DodgeBullet => 1,
                    DodgeEmitter => 2,
                    DodgeArenaChange => 4,
                    DodgeBeam => 8,
                    DodgeCameraChange => 16,
                    DodgeTrigger => 32,
                    _ => 0,
                };
            }

            var context = new ToolboxContext(BlueprintContainer.CurrentTool?.GetType(), selectionMask);

            if (!force && context == lastToolboxContext)
                return;

            lastToolboxContext = context;

            bool placingBullet = BlueprintContainer.CurrentTool is DodgeBulletCompositionTool;
            bool placingEmitter = BlueprintContainer.CurrentTool is DodgeEmitterCompositionTool;
            bool placingArena = BlueprintContainer.CurrentTool is DodgeArenaChangeCompositionTool;
            bool placingBeam = BlueprintContainer.CurrentTool is DodgeBeamCompositionTool;
            bool placingCamera = BlueprintContainer.CurrentTool is DodgeCameraChangeCompositionTool;
            bool placingTrigger = BlueprintContainer.CurrentTool is DodgeTriggerCompositionTool;
            bool selecting = !placingBullet && !placingEmitter && !placingArena && !placingBeam && !placingCamera && !placingTrigger;
            bool hasSelection = selectionMask != 0;

            setVisible(bulletToolbox, placingBullet || selecting && (selectionMask & 1) != 0);
            setVisible(emitterToolbox, placingEmitter || selecting && (selectionMask & 2) != 0);
            setVisible(ArenaToolbox, placingArena || selecting && (selectionMask & 4) != 0);
            setVisible(beamToolbox, placingBeam || selecting && (selectionMask & 8) != 0);
            setVisible(cameraToolbox, placingCamera || selecting && (selectionMask & 16) != 0);
            setVisible(triggerToolbox, placingTrigger || selecting && (selectionMask & 32) != 0);
            setVisible(timingToolbox, selecting && hasSelection);
            setVisible(transformToolbox, selecting && hasSelection);
            setVisible(patternToolbox, !placingArena && (!hasSelection || (selectionMask & 3) != 0));
            setVisible(GridToolbox, true);
            setVisible(viewToolbox, true);

            if (placingBullet || selectionMask == 1)
                bulletToolbox.Expanded.Value = true;

            if (placingEmitter || selectionMask == 2)
                emitterToolbox.Expanded.Value = true;

            if (placingArena || selectionMask == 4)
                ArenaToolbox.Expanded.Value = true;

            if (placingBeam || selectionMask == 8)
                beamToolbox.Expanded.Value = true;

            if (placingTrigger || selectionMask == 32)
                triggerToolbox.Expanded.Value = true;

            timingToolbox.Expanded.Value = selecting && hasSelection;
        }

        private static void setVisible(Drawable drawable, bool visible) => drawable.Alpha = visible ? 1 : 0;

        private readonly record struct ToolboxContext(System.Type? ToolType, int SelectionMask);

        public int TimelineLaneCount => 6;

        public EditorTimelineLane GetTimelineLane(DodgeHitObject hitObject) => hitObject switch
        {
            DodgeBullet => GetTimelineLaneByIndex(0),
            DodgeEmitter => GetTimelineLaneByIndex(1),
            DodgeArenaChange => GetTimelineLaneByIndex(2),
            DodgeBeam => GetTimelineLaneByIndex(3),
            DodgeCameraChange => GetTimelineLaneByIndex(4),
            DodgeTrigger => GetTimelineLaneByIndex(5),
            _ => GetTimelineLaneByIndex(0),
        };

        EditorTimelineLane IEditorTimelineLayoutProvider.GetTimelineLane(osu.Game.Rulesets.Objects.HitObject hitObject)
            => GetTimelineLane((DodgeHitObject)hitObject);

        public EditorTimelineLane GetTimelineLaneByIndex(int index) => index switch
        {
            0 => new EditorTimelineLane(0, Localisation.DodgeEditorStrings.Bullet, Colour4.FromHex("#62D9FF"), "B"),
            1 => new EditorTimelineLane(1, Localisation.DodgeEditorStrings.Emitter, Colour4.FromHex("#FFB45E"), "E"),
            2 => new EditorTimelineLane(2, Localisation.DodgeEditorStrings.Arena, Colour4.FromHex("#C59CFF"), "A"),
            3 => new EditorTimelineLane(3, Localisation.DodgeEditorStrings.Beam, Colour4.FromHex("#FF6B6B"), "L"),
            4 => new EditorTimelineLane(4, Localisation.DodgeEditorStrings.Camera, Colour4.FromHex("#FFDF6B"), "C"),
            5 => new EditorTimelineLane(5, Localisation.DodgeEditorStrings.Trigger, Colour4.FromHex("#FF69B4"), "T"),
            _ => throw new System.ArgumentOutOfRangeException(nameof(index)),
        };

        public object? GetTimelineGroupKey(osu.Game.Rulesets.Objects.HitObject hitObject)
            => hitObject is DodgeBullet or DodgeEmitter ? new TimelineStartTimeGroup(System.Math.Round(hitObject.StartTime, 3)) : null;

        public double GetTimelineDisplayEndTime(osu.Game.Rulesets.Objects.HitObject hitObject)
        {
            switch (hitObject)
            {
                case DodgeBullet bullet:
                    return bullet.MovementEndTime;

                case DodgeEmitter emitter:
                    return emitter.ContinueUntilExit ? emitter.MovementEndTime : emitter.GetEndTime();

                case DodgeArenaChange arena:
                    return arena.EndTime;

                case DodgeBeam beam:
                    return beam.EndTime;

                case DodgeCameraChange camera:
                    return camera.EndTime;

                case DodgeTrigger trigger:
                    return trigger.IsTimedEffect ? trigger.EndTime : trigger.StartTime;

                default:
                    return hitObject.GetEndTime();
            }
        }

        private readonly record struct TimelineStartTimeGroup(double StartTime);
    }
}
