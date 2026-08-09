// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Localisation;
using osu.Game.Online.API;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class OfficialOsuAccountStatusButton : SettingsButtonV2
    {
        private readonly OfficialOsuBeatmapApi? officialApi;
        private OsuColour colours = null!;

        public OfficialOsuAccountStatusButton(IBeatmapApiProvider? beatmapApi)
        {
            officialApi = beatmapApi?.OfficialApi;
            Action = () => officialApi?.RefreshCredentials();
            TooltipText = ForkSettingsStrings.OfficialOsuRetryButton;
            Keywords = new[] { "osu", "account", "token", "beatmap", "game.ini" };
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            this.colours = colours;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (officialApi == null)
            {
                updateStatus();
                return;
            }

            officialApi.ConnectionState.BindValueChanged(_ => updateStatus(), true);
            officialApi.AccountUsername.BindValueChanged(_ => updateStatus(), true);
        }

        private void updateStatus()
        {
            OfficialOsuBeatmapApiState state = officialApi?.ConnectionState.Value ?? OfficialOsuBeatmapApiState.Disabled;
            LocalisableString status;

            switch (state)
            {
                case OfficialOsuBeatmapApiState.TokenMissing:
                    status = ForkSettingsStrings.OfficialOsuStatusTokenMissing;
                    BackgroundColour = colours.YellowDark;
                    break;

                case OfficialOsuBeatmapApiState.Connecting:
                    status = ForkSettingsStrings.OfficialOsuStatusConnecting;
                    BackgroundColour = colours.BlueDark;
                    break;

                case OfficialOsuBeatmapApiState.Connected:
                    status = ForkSettingsStrings.OfficialOsuStatusConnected(officialApi?.AccountUsername.Value ?? string.Empty);
                    BackgroundColour = colours.GreenDark;
                    break;

                case OfficialOsuBeatmapApiState.AuthenticationFailed:
                    status = ForkSettingsStrings.OfficialOsuStatusAuthenticationFailed;
                    BackgroundColour = colours.RedDark;
                    break;

                case OfficialOsuBeatmapApiState.NetworkUnavailable:
                    status = ForkSettingsStrings.OfficialOsuStatusNetworkUnavailable;
                    BackgroundColour = colours.YellowDark;
                    break;

                default:
                    status = ForkSettingsStrings.OfficialOsuStatusDisabled;
                    BackgroundColour = colours.Gray4;
                    break;
            }

            // Settings buttons use a single centred line and do not wrap. The checkbox directly
            // above already names the feature, so keep this row to the compact status only.
            Text = status;
        }
    }
}
