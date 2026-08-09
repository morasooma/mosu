// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Overlays;

namespace osu.Game.Screens.Select
{
    internal sealed partial class WedgeBackground : InputBlockingContainer
    {
        public float StartAlpha { get; init; } = 0.9f;

        public float FinalAlpha { get; init; } = 0.6f;

        public float WidthForGradient { get; init; } = 0.3f;

        private OverlayColourProvider colourProvider = null!;
        private IBindable<Colour4> themeColour = null!;
        private Box additiveBackground = null!;
        private Box mainBackground = null!;
        private Box gradientBackground = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                additiveBackground = new Box
                {
                    Blending = BlendingParameters.Additive,
                    RelativeSizeAxes = Axes.Both,
                    Width = 0.6f,
                    Alpha = 0.5f,
                    Colour = ColourInfo.GradientHorizontal(colourProvider.Background2, colourProvider.Background2.Opacity(0)),
                },
                mainBackground = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Width = 1 - WidthForGradient,
                    Colour = colourProvider.Background5.Opacity(StartAlpha),
                },
                gradientBackground = new Box
                {
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    RelativeSizeAxes = Axes.Both,
                    Width = WidthForGradient,
                    Colour = ColourInfo.GradientHorizontal(colourProvider.Background5.Opacity(StartAlpha), colourProvider.Background5.Opacity(FinalAlpha)),
                },
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Background5);
            themeColour.BindValueChanged(_ => updateColours(), true);
        }

        private void updateColours()
        {
            additiveBackground.Colour = ColourInfo.GradientHorizontal(colourProvider.Background2, colourProvider.Background2.Opacity(0));
            mainBackground.Colour = colourProvider.Background5.Opacity(StartAlpha);
            gradientBackground.Colour = ColourInfo.GradientHorizontal(colourProvider.Background5.Opacity(StartAlpha), colourProvider.Background5.Opacity(FinalAlpha));
        }
    }
}
