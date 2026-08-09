// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Dodge.Edit.Blueprints;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit;

namespace osu.Game.Rulesets.Dodge.Edit
{
    /// <summary>
    /// Keeps the current editor action understandable without requiring the user
    /// to infer a multi-click placement state from the cursor alone.
    /// </summary>
    public partial class DodgeEditorContextToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        private OsuSpriteText modeText = null!;
        private OsuTextFlowContainer instructionText = null!;
        private LocalisableString lastMode;
        private LocalisableString lastInstruction;

        public LocalisableString CurrentInstruction => lastInstruction;

        public DodgeEditorContextToolboxGroup()
            : base(DodgeEditorStrings.Mapping)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new osuTK.Vector2(5);
            Children = new Drawable[]
            {
                modeText = new OsuSpriteText
                {
                    Font = OsuFont.GetFont(size: 15, weight: FontWeight.Bold),
                },
                instructionText = new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: 13))
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Alpha = 0.75f,
                },
                new OsuTextFlowContainer(sprite => sprite.Font = OsuFont.GetFont(size: 11))
                {
                    Text = DodgeEditorStrings.ToolShortcuts,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Alpha = 0.5f,
                },
            };
        }

        protected override void Update()
        {
            base.Update();

            LocalisableString mode;
            LocalisableString instruction;

            if (composer.BlueprintContainer.CurrentTool is DodgeBulletCompositionTool)
            {
                mode = DodgeEditorStrings.BulletMode;
                instruction = composer.BlueprintContainer.CurrentPlacement?.PlacementActive == PlacementBlueprint.PlacementState.Active
                    ? DodgeEditorStrings.BulletPlacementDirection
                    : DodgeEditorStrings.BulletPlacementStart;
            }
            else if (composer.BlueprintContainer.CurrentTool is DodgeEmitterCompositionTool)
            {
                mode = DodgeEditorStrings.EmitterMode;

                if (composer.BlueprintContainer.CurrentPlacement is DodgeEmitterPlacementBlueprint { IsPlacingMovementEnd: true })
                    instruction = DodgeEditorStrings.EmitterPlacementMovementEnd;
                else
                    instruction = composer.BlueprintContainer.CurrentPlacement?.PlacementActive == PlacementBlueprint.PlacementState.Active
                        ? DodgeEditorStrings.EmitterPlacementAim
                        : DodgeEditorStrings.EmitterPlacementSource;
            }
            else if (composer.BlueprintContainer.CurrentTool is DodgeArenaChangeCompositionTool)
            {
                mode = DodgeEditorStrings.ArenaMode;
                instruction = composer.BlueprintContainer.CurrentPlacement?.PlacementActive == PlacementBlueprint.PlacementState.Active
                    ? DodgeEditorStrings.ArenaPlacementEnd
                    : DodgeEditorStrings.ArenaPlacementStart;
            }
            else
            {
                int bulletCount = editorBeatmap.SelectedHitObjects.OfType<DodgeBullet>().Count();
                int emitterCount = editorBeatmap.SelectedHitObjects.OfType<DodgeEmitter>().Count();
                int arenaCount = editorBeatmap.SelectedHitObjects.OfType<DodgeArenaChange>().Count();
                int total = bulletCount + emitterCount + arenaCount;

                mode = total > 0 ? DodgeEditorStrings.SelectionCount(total) : DodgeEditorStrings.SelectMode;
                instruction = total > 0
                    ? DodgeEditorStrings.SelectionHint(bulletCount, emitterCount, arenaCount)
                    : DodgeEditorStrings.ReadyToMap;
            }

            if (mode != lastMode)
            {
                lastMode = mode;
                modeText.Text = mode;
            }

            if (instruction != lastInstruction)
            {
                lastInstruction = instruction;
                instructionText.Text = instruction;
            }
        }
    }
}
