using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeAppearanceToolboxGroup : EditorToolboxGroup
    {
        [Resolved]
        private DodgeHitObjectComposer composer { get; set; } = null!;

        private ExpandableButton copy = null!;
        private ExpandableButton paste = null!;

        public DodgeAppearanceToolboxGroup()
            : base(DodgeEditorStrings.Appearance)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new Vector2(5);
            Children = new Drawable[]
            {
                copy = new ExpandableButton
                {
                    RelativeSizeAxes = Axes.X,
                    ContractedLabelText = DodgeEditorStrings.CopyAppearance,
                    ExpandedLabelText = DodgeEditorStrings.CopyAppearance,
                    Action = composer.CopyAppearance,
                },
                paste = new ExpandableButton
                {
                    RelativeSizeAxes = Axes.X,
                    ContractedLabelText = DodgeEditorStrings.PasteAppearance,
                    ExpandedLabelText = DodgeEditorStrings.PasteAppearance,
                    Action = composer.PasteAppearance,
                },
            };
        }

        protected override void Update()
        {
            base.Update();
            copy.Enabled.Value = composer.CanCopyAppearance;
            paste.Enabled.Value = composer.CanPasteAppearance;
        }
    }
}
