// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;

namespace osu.Game.Screens.Edit
{
    public partial class TableHeaderText : OsuSpriteText
    {
        private IBindable<Colour4>? themeColour;

        public TableHeaderText(LocalisableString text)
        {
            Text = text.ToUpper();
            Font = OsuFont.GetFont(size: 12, weight: FontWeight.Bold);
        }

        [BackgroundDependencyLoader(true)]
        private void load(OverlayColourProvider? colourProvider)
        {
            if (colourProvider != null)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content2);
                themeColour.BindValueChanged(c => Colour = c.NewValue, true);
            }
        }
    }
}
