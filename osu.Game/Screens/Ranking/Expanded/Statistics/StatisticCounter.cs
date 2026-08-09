// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Screens.Ranking.Expanded.Accuracy;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Expanded.Statistics
{
    public partial class StatisticCounter : RollingCounter<int>
    {
        protected override double RollingDuration => AccuracyCircle.ACCURACY_TRANSFORM_DURATION;

        protected override Easing RollingEasing => AccuracyCircle.ACCURACY_TRANSFORM_EASING;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;
        private IBindable<Colour4> themeColour = null!;

        protected override OsuSpriteText CreateSpriteText() => base.CreateSpriteText().With(s =>
        {
            s.Font = OsuFont.Torus.With(size: 20, fixedWidth: true);
            s.Spacing = new Vector2(-2, 0);
        });

        protected override void LoadComplete()
        {
            base.LoadComplete();
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(colour => DrawableCount.Colour = OverlayColourProvider.IsLightTheme ? colour.NewValue : Color4.White, true);
        }
    }
}
