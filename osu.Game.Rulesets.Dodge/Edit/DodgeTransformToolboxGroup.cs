// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Edit;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Compose.Components;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    /// <summary>
    /// Numeric counterparts to the selection-box movement, rotation and scale handles.
    /// </summary>
    public partial class DodgeTransformToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        public SelectionRotationHandler RotationHandler { get; init; } = null!;

        public SelectionScaleHandler ScaleHandler { get; init; } = null!;

        private readonly BindableFloat moveX = new BindableFloat
        {
            MinValue = -512,
            MaxValue = 512,
            Precision = 1,
        };

        private readonly BindableFloat moveY = new BindableFloat
        {
            MinValue = -384,
            MaxValue = 384,
            Precision = 1,
        };

        private readonly BindableFloat rotation = new BindableFloat(15)
        {
            MinValue = -180,
            MaxValue = 180,
            Precision = 0.1f,
        };

        private readonly BindableFloat scaleX = new BindableFloat(100)
        {
            MinValue = 1,
            MaxValue = 400,
            Precision = 0.1f,
        };

        private readonly BindableFloat scaleY = new BindableFloat(100)
        {
            MinValue = 1,
            MaxValue = 400,
            Precision = 0.1f,
        };

        public DodgeTransformToolboxGroup()
            : base(DodgeEditorStrings.Transform)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new Vector2(5);
            Children = new Drawable[]
            {
                createSlider(moveX, DodgeEditorStrings.RelativeX, "X: 0"),
                createSlider(moveY, DodgeEditorStrings.RelativeY, "Y: 0"),
                createButton(DodgeEditorStrings.ApplyMovement, applyMovement),
                createSlider(rotation, DodgeEditorStrings.Rotation, "R: 15°"),
                createButton(DodgeEditorStrings.ApplyRotation, () => RotationHandler.Rotate(rotation.Value)),
                createSlider(scaleX, DodgeEditorStrings.ScaleX, "X: 100%"),
                createSlider(scaleY, DodgeEditorStrings.ScaleY, "Y: 100%"),
                createButton(DodgeEditorStrings.ApplyScale, () => ScaleHandler.ScaleSelection(new Vector2(scaleX.Value / 100, scaleY.Value / 100))),
            };

            moveX.BindValueChanged(value => ((ExpandableSlider<float>)Children[0]).ContractedLabelText = $"X: {value.NewValue:N0}", true);
            moveY.BindValueChanged(value => ((ExpandableSlider<float>)Children[1]).ContractedLabelText = $"Y: {value.NewValue:N0}", true);
            rotation.BindValueChanged(value => ((ExpandableSlider<float>)Children[3]).ContractedLabelText = $"R: {value.NewValue:N1}°", true);
            scaleX.BindValueChanged(value => ((ExpandableSlider<float>)Children[5]).ContractedLabelText = $"X: {value.NewValue:N1}%", true);
            scaleY.BindValueChanged(value => ((ExpandableSlider<float>)Children[6]).ContractedLabelText = $"Y: {value.NewValue:N1}%", true);
        }

        private static ExpandableSlider<float> createSlider(BindableFloat current, LocalisableString expanded, LocalisableString contracted) => new ExpandableSlider<float>
        {
            Current = current,
            KeyboardStep = 1,
            ExpandedLabelText = expanded,
            ContractedLabelText = contracted,
        };

        private static ExpandableButton createButton(LocalisableString label, System.Action action) => new ExpandableButton
        {
            RelativeSizeAxes = Axes.X,
            ContractedLabelText = label,
            ExpandedLabelText = label,
            Action = action,
        };

        private void applyMovement()
        {
            Vector2 delta = new Vector2(moveX.Value, moveY.Value);

            editorBeatmap.PerformOnSelection(item =>
            {
                if (item is not (Objects.DodgeBullet or Objects.DodgeEmitter or Objects.DodgeArenaChange))
                    return;

                var points = DodgeSelectionTransformUtils.GetPoints(item);
                DodgeSelectionTransformUtils.SetPoints(item, points.Start + delta, points.End + delta);
            });
        }
    }
}
