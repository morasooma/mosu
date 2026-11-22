// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Rulesets.Dodge.UI;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeEditorPlayfieldAdjustmentContainer : DodgePlayfieldAdjustmentContainer
    {
        public const float COMPACT_SCALE = 0.72f;

        [Resolved]
        private DodgeEditorSettings settings { get; set; } = null!;

        protected override float ViewZoom => settings?.EditorZoom.Value ?? 1;
        protected override Vector2 ViewCentre => settings == null ? DodgePlayfield.BASE_SIZE / 2
            : new Vector2(settings.EditorCentreX.Value, settings.EditorCentreY.Value);

        [BackgroundDependencyLoader]
        private void load()
        {
            settings.CompactPlayfield.BindValueChanged(compact => setCompactPlayfield(compact.NewValue), true);
        }

        private void setCompactPlayfield(bool compact)
        {
            Size = new Vector2(compact ? COMPACT_SCALE : DEFAULT_SCALE);
        }
    }
}
