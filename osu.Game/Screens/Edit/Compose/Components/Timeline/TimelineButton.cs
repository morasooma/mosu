// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Screens.Edit.Timing;
using osuTK.Graphics;

namespace osu.Game.Screens.Edit.Compose.Components.Timeline
{
    public partial class TimelineButton : IconButton
    {
        private IBindable<Colour4>? themeColour;

        internal Color4 CurrentIconColour => IconColour;
        internal Color4 CurrentIconHoverColour => IconHoverColour;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            updateColours(colourProvider);

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateColours(colourProvider));

            Add(new RepeatingButtonBehaviour(this));
        }

        private void updateColours(OverlayColourProvider colourProvider)
        {
            if (OverlayColourProvider.IsLightTheme)
            {
                IconColour = colourProvider.Light3;
                IconHoverColour = colourProvider.Content1;
                HoverColour = colourProvider.Background3;
                FlashColour = colourProvider.Highlight1;
            }
            else
            {
                IconColour = colourProvider.Light3;
                IconHoverColour = colourProvider.Content1;
                HoverColour = colourProvider.Background1;
                FlashColour = colourProvider.Content2;
            }
        }

        protected override HoverSounds CreateHoverSounds(HoverSampleSet sampleSet) => new HoverSounds(sampleSet);
    }
}
