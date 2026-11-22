// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.Multiplayer;
using osu.Game.Overlays.Notifications;
using osu.Game.Updater;

namespace osu.Game.Overlays.Settings.Sections.General
{
    public partial class CheckForUpdatesButton : SettingsButtonV2
    {
        [Resolved]
        private UpdateManager? updateManager { get; set; }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private OsuGame? game { get; set; }

        public CheckForUpdatesButton()
        {
            Text = GeneralSettingsStrings.CheckUpdate;
            Action = () => checkForUpdates().FireAndForget();
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Enabled.Value = updateManager?.CanCheckForUpdate == true;
        }

        private async Task checkForUpdates()
        {
            if (updateManager == null || game == null)
                return;

            Enabled.Value = false;

            var checkingNotification = new ProgressNotification
            {
                Text = GeneralSettingsStrings.CheckingForUpdates,
            };
            notifications?.Post(checkingNotification);

            try
            {
                bool foundUpdate = await updateManager.CheckForUpdateAsync(checkingNotification.CancellationToken).ConfigureAwait(true);

                if (!foundUpdate)
                {
                    notifications?.Post(new SimpleNotification
                    {
                        Text = GeneralSettingsStrings.RunningLatestRelease(game.Version),
                        Icon = FontAwesome.Solid.CheckCircle,
                    });
                }
            }
            catch
            {
            }
            finally
            {
                checkingNotification.CompleteSilently();
                Enabled.Value = true;
            }
        }
    }
}
