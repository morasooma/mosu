// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Screens.Play.PlayerSettings
{
    public partial class DiscussionSettings : PlayerSettingsGroup
    {
        public DiscussionSettings()
            : base("discussions")
        {
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            Children = new Drawable[]
            {
                new PlayerCheckbox
                {
                    LabelText = global::osu.Game.Localisation.ClientTranslationStrings.J5096,
                    Current = config.GetBindable<bool>(OsuSetting.FloatingComments)
                },
                new FocusedTextBox
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 30,
                    PlaceholderText = global::osu.Game.Localisation.ClientTranslationStrings.J5097,
                    HoldFocus = false,
                },
            };
        }
    }
}
