// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking.Expanded
{
    public partial class PlayedOnText : OsuSpriteText
    {
        private readonly DateTimeOffset time;
        private readonly bool withPrefix;
        private readonly Bindable<bool> prefer24HourTime = new Bindable<bool>();
        private IBindable<Colour4> themeColour = null!;

        public PlayedOnText(DateTimeOffset time, bool withPrefix)
        {
            this.time = time;
            this.withPrefix = withPrefix;

            Anchor = Anchor.BottomCentre;
            Origin = Anchor.BottomCentre;
            Font = OsuFont.GetFont(size: 10, weight: FontWeight.SemiBold);
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager configManager, OverlayColourProvider colourProvider)
        {
            configManager.BindWith(OsuSetting.Prefer24HourTime, prefer24HourTime);

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content2);
            themeColour.BindValueChanged(colour => Colour = OverlayColourProvider.IsLightTheme ? colour.NewValue : Colour4.White, true);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            prefer24HourTime.BindValueChanged(_ => updateDisplay(), true);
        }

        private void updateDisplay()
        {
            var timeText = time.ToLocalTime().ToLocalisableString(prefer24HourTime.Value ? @"d MMMM yyyy HH:mm" : @"d MMMM yyyy h:mm tt");

            if (withPrefix)
                Text = LocalisableString.Format("Played on {0}", timeText);
            else
                Text = timeText;
        }
    }
}
