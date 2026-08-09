// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Graphics.UserInterface;
using osu.Game.Input.Bindings;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    /// <summary>
    /// Square position grid using the same controls and editor grid size as osu!standard.
    /// </summary>
    public partial class DodgeGridToolboxGroup : EditorToolboxGroup, IKeyBindingHandler<GlobalAction>
    {
        private const float max_automatic_spacing = 64;

        [Resolved]
        private EditorBeatmap editorBeatmap { get; set; } = null!;

        public BindableFloat StartPositionX { get; } = new BindableFloat(DodgePlayfield.WIDTH / 2)
        {
            MinValue = 0,
            MaxValue = DodgePlayfield.WIDTH,
            Precision = 0.1f,
        };

        public BindableFloat StartPositionY { get; } = new BindableFloat(DodgePlayfield.HEIGHT / 2)
        {
            MinValue = 0,
            MaxValue = DodgePlayfield.HEIGHT,
            Precision = 0.1f,
        };

        public BindableFloat GridLineSpacing { get; } = new BindableFloat(4)
        {
            MinValue = 4,
            MaxValue = 256,
            Precision = 0.1f,
        };

        public BindableFloat GridLinesRotation { get; } = new BindableFloat
        {
            MinValue = -45,
            MaxValue = 45,
            Precision = 0.1f,
        };

        public Bindable<Vector2> StartPosition { get; } = new Bindable<Vector2>(DodgePlayfield.BASE_SIZE / 2);

        public Bindable<Vector2> SpacingVector { get; } = new Bindable<Vector2>(new Vector2(4));

        private ExpandableSlider<float> startPositionXSlider = null!;
        private ExpandableSlider<float> startPositionYSlider = null!;
        private ExpandableSlider<float> spacingSlider = null!;
        private ExpandableSlider<float> rotationSlider = null!;

        public DodgeGridToolboxGroup()
            : base(DodgeEditorStrings.Grid)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                startPositionXSlider = new ExpandableSlider<float>
                {
                    Current = new BindableFloat
                    {
                        MinValue = -DodgePlayfield.WIDTH / 2,
                        MaxValue = DodgePlayfield.WIDTH / 2,
                        Precision = 0.1f,
                    },
                    KeyboardStep = 1,
                    ExpandedLabelText = DodgeEditorStrings.XOffset,
                },
                startPositionYSlider = new ExpandableSlider<float>
                {
                    Current = new BindableFloat
                    {
                        MinValue = -DodgePlayfield.HEIGHT / 2,
                        MaxValue = DodgePlayfield.HEIGHT / 2,
                        Precision = 0.1f,
                    },
                    KeyboardStep = 1,
                    ExpandedLabelText = DodgeEditorStrings.YOffset,
                },
                spacingSlider = new ExpandableSlider<float>
                {
                    Current = GridLineSpacing,
                    KeyboardStep = 1,
                    ExpandedLabelText = DodgeEditorStrings.Spacing,
                },
                rotationSlider = new ExpandableSlider<float>
                {
                    Current = GridLinesRotation,
                    KeyboardStep = 1,
                    ExpandedLabelText = DodgeEditorStrings.Rotation,
                },
            };

            GridLineSpacing.Value = editorBeatmap.GridSize;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            StartPositionX.BindValueChanged(x =>
            {
                startPositionXSlider.ContractedLabelText = $"X: {x.NewValue:#,0.##}";
                startPositionXSlider.Current.Value = x.NewValue - DodgePlayfield.WIDTH / 2;
                StartPosition.Value = new Vector2(x.NewValue, StartPosition.Value.Y);
            }, true);

            StartPositionY.BindValueChanged(y =>
            {
                startPositionYSlider.ContractedLabelText = $"Y: {y.NewValue:#,0.##}";
                startPositionYSlider.Current.Value = y.NewValue - DodgePlayfield.HEIGHT / 2;
                StartPosition.Value = new Vector2(StartPosition.Value.X, y.NewValue);
            }, true);

            startPositionXSlider.Current.BindValueChanged(x => StartPositionX.Value = x.NewValue + DodgePlayfield.WIDTH / 2);
            startPositionYSlider.Current.BindValueChanged(y => StartPositionY.Value = y.NewValue + DodgePlayfield.HEIGHT / 2);

            StartPosition.BindValueChanged(position =>
            {
                StartPositionX.Value = position.NewValue.X;
                StartPositionY.Value = position.NewValue.Y;
            });

            GridLineSpacing.BindValueChanged(spacing =>
            {
                spacingSlider.ContractedLabelText = $"S: {spacing.NewValue:#,0.##}";
                SpacingVector.Value = new Vector2(spacing.NewValue);
                editorBeatmap.GridSize = (int)spacing.NewValue;
            }, true);

            GridLinesRotation.BindValueChanged(rotation =>
            {
                rotationSlider.ContractedLabelText = $"R: {rotation.NewValue:#,0.##}";
            }, true);
        }

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (e.Action != GlobalAction.EditorCycleGridSpacing)
                return false;

            GridLineSpacing.Value = GridLineSpacing.Value * 2 >= max_automatic_spacing
                ? GridLineSpacing.Value / 8
                : GridLineSpacing.Value * 2;
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e)
        {
        }
    }
}
