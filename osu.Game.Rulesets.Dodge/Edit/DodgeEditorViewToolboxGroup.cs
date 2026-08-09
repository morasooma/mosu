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
