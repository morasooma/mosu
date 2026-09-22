// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Overlays.Dialog;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ConfigurationBackupSettings : SettingsSubsection
    {
        protected override LocalisableString Header => ForkSettingsStrings.ConfigBackupHeader;

        private readonly Bindable<string> savedAt = new Bindable<string>(ForkSettingsStrings.ConfigBackupNotSaved.ToString());

        private SettingsButtonV2 saveButton = null!;
        private SettingsButtonV2 loadButton = null!;
        private bool busy;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private OsuConfigManager localConfig { get; set; } = null!;

        [Resolved]
        private ServerProfileManager profileManager { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [BackgroundDependencyLoader]
        private void load()
        {
            Children = new Drawable[]
            {
                saveButton = new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.ConfigBackupSave,
                    TooltipText = ForkSettingsStrings.ConfigBackupSaveHint,
                    Action = () => _ = saveAsync(),
                },
                new SettingsItemV2(new FormTextBox
                {
                    Caption = ForkSettingsStrings.ConfigBackupDate,
                    Current = savedAt,
                    ReadOnly = true,
                })
                {
                    ShowRevertToDefaultButton = false,
                },
                loadButton = new DangerousSettingsButtonV2
                {
                    Text = RuntimeInfo.IsMobile ? ForkSettingsStrings.ConfigBackupLoadMobile : ForkSettingsStrings.ConfigBackupLoad,
                    TooltipText = ForkSettingsStrings.ConfigBackupLoadHint,
                    Action = () => _ = loadAsync(),
                },
            };

            api.State.BindValueChanged(_ => refreshForConnection(), true);
        }

        private void refreshForConnection() => Schedule(refreshForConnectionOnUpdateThread);

        private void refreshForConnectionOnUpdateThread()
        {
            if (!api.IsLoggedIn)
            {
                savedAt.Value = ForkSettingsStrings.ConfigBackupLoginRequired.ToString();
                setButtons(false, false);
                return;
            }

            _ = refreshStatusAsync();
        }

        private async Task refreshStatusAsync()
        {
            if (busy)
                return;

            busy = true;
            setButtons(false, false);

            try
            {
                ConfigBackupStatusResponse status = await performRequestAsync(new GetConfigBackupStatusRequest()).ConfigureAwait(false);

                Schedule(() =>
                {
                    updateStatus(status);
                    busy = false;
                });
            }
            catch (Exception exception)
            {
                Schedule(() =>
                {
                    savedAt.Value = ForkSettingsStrings.ConfigBackupError(exception.Message).ToString();
                    setButtons(api.IsLoggedIn, false);
                    busy = false;
                });
            }
        }

        private async Task saveAsync()
        {
            if (busy || !api.IsLoggedIn)
                return;

            busy = true;
            setButtons(false, false);
            savedAt.Value = ForkSettingsStrings.ConfigBackupSaving.ToString();

            try
            {
                if (!localConfig.Save())
                    throw new InvalidOperationException(ForkSettingsStrings.ConfigBackupLocalSaveFailed.ToString());

                profileManager.SaveProfiles();
                // InputConfigManager saves with a 100 ms debounce and is framework-owned.
                // Let an input setting changed immediately before this click reach input.json.
                await Task.Delay(200).ConfigureAwait(false);
                ConfigBackupUpload upload = ConfigurationBackupManager.CreateUpload(storage);
                ConfigBackupStatusResponse status = await performRequestAsync(new PutConfigBackupRequest(upload)).ConfigureAwait(false);

                Schedule(() =>
                {
                    updateStatus(status);
                    busy = false;
                });
            }
            catch (Exception exception)
            {
                Schedule(() =>
                {
                    savedAt.Value = ForkSettingsStrings.ConfigBackupError(exception.Message).ToString();
                    setButtons(api.IsLoggedIn, false);
                    busy = false;
                });
            }
        }

        private async Task loadAsync()
        {
            if (busy || !api.IsLoggedIn)
                return;

            busy = true;
            setButtons(false, false);
            savedAt.Value = ForkSettingsStrings.ConfigBackupDownloading.ToString();

            try
            {
                ConfigBackupResponse backup = await performRequestAsync(new GetConfigBackupRequest()).ConfigureAwait(false);

                Schedule(() =>
                {
                    savedAt.Value = formatDate(backup.UpdatedAt);
                    setButtons(true, true);

                    dialogOverlay?.Push(new ConfirmDialog(
                        RuntimeInfo.IsMobile ? ForkSettingsStrings.ConfigBackupLoadConfirmationMobile : ForkSettingsStrings.ConfigBackupLoadConfirmation,
                        () => applyAndRestart(backup)));
                    busy = false;
                });
            }
            catch (Exception exception)
            {
                Schedule(() =>
                {
                    savedAt.Value = ForkSettingsStrings.ConfigBackupError(exception.Message).ToString();
                    setButtons(api.IsLoggedIn, false);
                    busy = false;
                });
            }
        }

        private void applyAndRestart(ConfigBackupResponse backup)
        {
            try
            {
                if (!RuntimeInfo.IsMobile && (game == null || !game.RestartAppWhenExited()))
                    throw new InvalidOperationException(ForkSettingsStrings.ConfigBackupRestartUnavailable.ToString());

                ConfigurationBackupManager.StageRestore(storage, backup);

                if (RuntimeInfo.IsMobile)
                {
                    savedAt.Value = ForkSettingsStrings.ConfigBackupRestartManually.ToString();
                    setButtons(false, false);
                    return;
                }

                savedAt.Value = ForkSettingsStrings.ConfigBackupRestarting.ToString();
                setButtons(false, false);

                game!.AttemptExit();
            }
            catch (Exception exception)
            {
                savedAt.Value = ForkSettingsStrings.ConfigBackupError(exception.Message).ToString();
                setButtons(api.IsLoggedIn, true);
            }
        }

        private void updateStatus(ConfigBackupStatusResponse status)
        {
            savedAt.Value = status.Exists && status.UpdatedAt.HasValue
                ? formatDate(status.UpdatedAt.Value)
                : ForkSettingsStrings.ConfigBackupNotSaved.ToString();
            setButtons(api.IsLoggedIn, status.Exists);
        }

        private static string formatDate(DateTimeOffset value) =>
            value.ToLocalTime().ToString("G", CultureInfo.CurrentCulture);

        private void setButtons(bool canSave, bool canLoad)
        {
            saveButton.Enabled.Value = canSave;
            loadButton.Enabled.Value = canLoad;
        }

        private Task<T> performRequestAsync<T>(APIRequest<T> request)
            where T : class
        {
            var completion = new TaskCompletionSource<T>();
            request.Success += response => completion.TrySetResult(response);
            request.Failure += exception => completion.TrySetException(exception);
            _ = api.PerformAsync(request);
            return completion.Task;
        }
    }
}
