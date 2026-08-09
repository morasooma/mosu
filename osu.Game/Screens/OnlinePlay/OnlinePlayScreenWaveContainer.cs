// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;

namespace osu.Game.Screens.OnlinePlay
{
    public partial class OnlinePlayScreenWaveContainer : WaveContainer
    {
        protected override bool StartHidden => true;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            FirstWaveColour = colourProvider.Light4;
            SecondWaveColour = colourProvider.Light3;
            ThirdWaveColour = colourProvider.Dark4;
            FourthWaveColour = colourProvider.Dark3;
        }
    }
}
