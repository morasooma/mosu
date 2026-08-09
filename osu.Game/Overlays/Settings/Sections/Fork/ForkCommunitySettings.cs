// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkCommunitySettings : SettingsSubsection
    {
        protected override LocalisableString Header => ForkSettingsStrings.CommunityHeader;

        [BackgroundDependencyLoader]
        private void load(OsuGame? game)
        {
            Add(new SettingsButtonV2
            {
                Text = ForkSettingsStrings.CommunityTgChannel,
                TooltipText = @"https://t.me/osufork",
                Action = () => game?.OpenUrlExternally(@"https://t.me/osufork")
            });

            Add(new SettingsButtonV2
            {
                Text = ForkSettingsStrings.CommunityDiscordServer,
                TooltipText = @"https://discord.gg/bRSDssrgUE",
                Action = () => game?.OpenUrlExternally(@"https://discord.gg/bRSDssrgUE")
            });
        }
    }
}
