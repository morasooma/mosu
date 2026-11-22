// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Edit;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeEditorViewToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        [Resolved]
        private osu.Game.Screens.Edit.EditorBeatmap editorBeatmap { get; set; } = null!;

        public bool CompactPlayfield
        {
            get => settings.CompactPlayfield.Value;
            set => settings.CompactPlayfield.Value = value;
        }

        public bool ShowBulletCoverage
        {
            get => settings.ShowBulletCoverage.Value;
            set => settings.ShowBulletCoverage.Value = value;
        }

        public bool GridSnapEnabled
        {
            get => settings.GridSnapEnabled.Value;
            set => settings.GridSnapEnabled.Value = value;
        }

        public bool ShowAutoplayRoute
        {
            get => settings.ShowAutoplayRoute.Value;
            set => settings.ShowAutoplayRoute.Value = value;
        }

        public DodgeEditorViewToolboxGroup()
            : base(DodgeEditorStrings.View)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new osuTK.Vector2(5);
            Children = new Drawable[]
            {
                new ExpandableButton
                {
                    ContractedLabelText = DodgeEditorStrings.FitWorld, ExpandedLabelText = DodgeEditorStrings.FitWorld,
                    RelativeSizeAxes = Axes.X,
                    Action = () =>
                    {
                        var bounds = Objects.DodgeWorldGeometry.Bounds(System.Linq.Enumerable.OfType<Objects.DodgeHitObject>(editorBeatmap.HitObjects));
                        settings.FitEditorView(bounds?.Centre ?? UI.DodgePlayfield.BASE_SIZE / 2,
                            bounds.HasValue ? new osuTK.Vector2(bounds.Value.Width, bounds.Value.Height) : UI.DodgePlayfield.BASE_SIZE);
                    },
                },
                new ExpandableButton
                {
                    ContractedLabelText = DodgeEditorStrings.ResetView, ExpandedLabelText = DodgeEditorStrings.ResetView,
                    RelativeSizeAxes = Axes.X,
                    Action = () => { settings.EditorZoom.Value = 1; settings.EditorCentreX.Value = 256; settings.EditorCentreY.Value = 192; },
                },
                new OsuSpriteText { Text = DodgeEditorStrings.EditorNavigationHint, Font = osu.Game.Graphics.OsuFont.Default.With(size: 11) },
                new ExpandableSlider<float> { ExpandedLabelText = DodgeEditorStrings.EditorZoom, Current = settings.EditorZoom, KeyboardStep = 0.05f },
                new ExpandableSlider<float> { ExpandedLabelText = DodgeEditorStrings.EditorCentreX, Current = settings.EditorCentreX, KeyboardStep = 16 },
                new ExpandableSlider<float> { ExpandedLabelText = DodgeEditorStrings.EditorCentreY, Current = settings.EditorCentreY, KeyboardStep = 16 },
                new OsuCheckbox
                {
                    RelativeSizeAxes = Axes.X,
                    LabelText = DodgeEditorStrings.SmallerPlayfield,
                    Current = settings.CompactPlayfield,
                },
                new OsuCheckbox
                {
                    RelativeSizeAxes = Axes.X,
                    LabelText = DodgeEditorStrings.ShowBulletCoverage,
                    Current = settings.ShowBulletCoverage,
                },
                new OsuCheckbox
                {
                    RelativeSizeAxes = Axes.X,
                    LabelText = DodgeEditorStrings.ShowAutoplayRoute,
                    Current = settings.ShowAutoplayRoute,
                },
                new OsuSpriteText
                {
                    Text = DodgeEditorStrings.AutoplayRouteLegend,
                    Alpha = 0.65f,
                },
                new OsuCheckbox
                {
                    RelativeSizeAxes = Axes.X,
                    LabelText = DodgeEditorStrings.GridSnap,
                    Current = settings.GridSnapEnabled,
                },
            };
        }
    }
}
