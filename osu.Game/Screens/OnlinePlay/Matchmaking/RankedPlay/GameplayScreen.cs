// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay
{
    public partial class GameplayScreen : RankedPlaySubScreen
    {
        public override LocalisableString StageHeading => global::osu.Game.Localisation.ClientTranslationStrings.J4909;

        [BackgroundDependencyLoader]
        private void load()
        {
            CenterColumn.Children =
            [
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Spacing = new Vector2(20),
                    Children =
                    [
                        new OsuSpriteText
                        {
                            Text = global::osu.Game.Localisation.ClientTranslationStrings.J4910,
                            Anchor = Anchor.TopCentre,
                            Origin = Anchor.TopCentre,
                            Font = OsuFont.GetFont(typeface: Typeface.TorusAlternate, size: 42, weight: FontWeight.Regular),
                        },
                    ]
                },
            ];
        }
    }
}
