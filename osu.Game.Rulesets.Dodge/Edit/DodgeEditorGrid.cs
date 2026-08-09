// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Screens.Edit.Compose.Components;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeEditorGrid : RectangularPositionSnapGrid
    {
        public bool IsGridVisible => Alpha > 0;

        public DodgeEditorGrid()
        {
            Size = DodgePlayfield.BASE_SIZE;
            Alpha = 0;
        }

        [BackgroundDependencyLoader]
        private void load(DodgeEditorSettings settings, DodgeGridToolboxGroup gridToolbox)
        {
            Spacing.BindTo(gridToolbox.SpacingVector);
            StartPosition.BindTo(gridToolbox.StartPosition);
            GridLineRotation.BindTo(gridToolbox.GridLinesRotation);
            settings.GridSnapEnabled.BindValueChanged(enabled => Alpha = enabled.NewValue ? 1 : 0, true);
        }
    }
}
