// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;

namespace osu.Game.Screens.Ranking.Contracted
{
    public partial class ContractedPanelTopContent : CompositeDrawable
    {
        public readonly Bindable<int?> ScorePosition = new Bindable<int?>();

        private OsuSpriteText text = null!;
        private IBindable<Colour4>? themeColour;

        public ContractedPanelTopContent()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(OverlayColourProvider? colourProvider = null)
        {
            InternalChild = text = new OsuSpriteText
            {
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Y = 6,
                Font = OsuFont.GetFont(size: 18, weight: FontWeight.Bold)
            };

            if (colourProvider != null)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ => text.Colour = colourProvider.Content1, true);
            }
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            ScorePosition.BindValueChanged(pos => text.Text = pos.NewValue != null ? $"#{pos.NewValue}" : string.Empty, true);
        }
    }
}
