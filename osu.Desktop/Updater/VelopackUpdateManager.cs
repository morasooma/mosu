// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Screens.Play;
using Velopack;
using Velopack.Sources;
using UpdateManager = osu.Game.Updater.UpdateManager;

namespace osu.Desktop.Updater
{
    public partial class VelopackUpdateManager : UpdateManager
    {
        [Resolved]
        private INotificationOverlay notificationOverlay { get; set; } = null!;

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private ILocalUserPlayInfo? localUserInfo { get; set; }

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private bool isInGameplay => localUserInfo?.PlayingState.Value != LocalUserPlayingState.NotPlaying;

        private ScheduledDelegate? scheduledBackgroundCheck;

        private void scheduleNextUpdateCheck(double delay = 60000 * 30)
        {
            scheduledBackgroundCheck?.Cancel();
            scheduledBackgroundCheck = Scheduler.AddDelayed(() =>
            {
                log("Running scheduled background update check...");
                CheckForUpdate();
            }, delay);
        }

        protected override async Task<bool> PerformUpdateCheck(CancellationToken cancellationToken)
        {
            scheduledBackgroundCheck?.Cancel();

            // Check if automatic updates are disabled in GU settings
            if (config.Get<bool>(OsuSetting.DisableAutomaticUpdates))
            {
                log("Automatic updates are disabled in settings");
                return false;
            }

            if (isInGameplay)
            {
                log("Update check cancelled - user is in gameplay");
                scheduleNextUpdateCheck();
                return false;
            }

            try
            {
                bool isDevBuild = ReleaseStream.Value == Game.Configuration.ReleaseStream.DevBuild;

                if (isDevBuild && !api.IsLoggedIn)
                {
                    log("Waiting for login before checking Dev Build updates.");
                    scheduleNextUpdateCheck(60000);
                    return false;
                }

                if (isDevBuild && api.LocalUser.Value is not { Active: true, IsSupporter: true })
                {
                    log("Dev Build updates require an active supporter account.");
                    scheduleNextUpdateCheck();
                    return false;
                }

                string updateUrl = game.CreateEndpoints().UpdateUrl.TrimEnd('/');
                string updateChannel = getUpdateChannel(ReleaseStream.Value, OperatingSystem.IsLinux());

                if (string.IsNullOrWhiteSpace(updateUrl))
                {
                    log("Update check skipped because no update URL is configured.");
                    scheduleNextUpdateCheck();
                    return false;
                }

                IUpdateSource updateSource;
                if (isDevBuild)
                {
                    updateUrl = getDevUpdateUrl(updateUrl);
                    updateSource = new SimpleWebSource(updateUrl, new AuthenticatedFileDownloader(api, updateUrl));
                }
                else
                {
                    updateSource = new SimpleWebSource(updateUrl);
                }

                log($"Checking {updateUrl} on channel {updateChannel}...");

                Velopack.UpdateManager updateManager = new Velopack.UpdateManager(updateSource, new UpdateOptions
                {
                    AllowVersionDowngrade = true,
                    ExplicitChannel = updateChannel,
                });

                UpdateInfo? update = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);

                if (cancellationToken.IsCancellationRequested)
                {
                    log("Update check cancelled");
                    scheduleNextUpdateCheck();
                    return true;
                }

                if (update == null)
                {
                    // No update is available.
                    log("No update found");
                    scheduleNextUpdateCheck();
                    return false;
                }

                // Download update in the background while notifying awaiters of the update being available.
                log($"New update available: {update.TargetFullRelease.Version}");
                downloadUpdate(updateManager, update, cancellationToken);
                return true;
            }
            catch (Exception e)
            {
                log($"Update check failed with error ({e.Message})");

                // An error is not an available update. Surface it to the manual check UI.
                scheduleNextUpdateCheck();
                throw;
            }
        }

        private void downloadUpdate(Velopack.UpdateManager updateManager, UpdateInfo update, CancellationToken cancellationToken) => Task.Run(async () =>
        {
            log($"Beginning download of update {update.TargetFullRelease.Version}...");

            UpdateDownloadProgressNotification progressNotification = new UpdateDownloadProgressNotification(cancellationToken)
            {
                CompletionClickAction = () =>
                {
                    restartToApplyUpdate(updateManager, update);
                    return true;
                }
            };

            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(progressNotification.CancellationToken, cancellationToken))
                {
                    progressNotification.StartDownload();
                    runOutsideOfGameplay(() => notificationOverlay.Post(progressNotification), cts.Token);

                    await updateManager.DownloadUpdatesAsync(update, p => progressNotification.Progress = p / 100f, cts.Token).ConfigureAwait(false);
                    runOutsideOfGameplay(() => progressNotification.State = ProgressNotificationState.Completed, cts.Token);
                }
            }
            catch (OperationCanceledException)
            {
                progressNotification.FailDownload();
                log(@"Update cancelled");
            }
            catch (Exception e)
            {
                // In the case of an error, a separate notification will be displayed.
                progressNotification.FailDownload();
                Logger.Error(e, @"Update failed!");
            }

            return true;
        }, cancellationToken);

        private void runOutsideOfGameplay(Action action, CancellationToken cancellationToken)
        {
            if (cancellationToken.IsCancellationRequested)
                return;

            if (isInGameplay)
            {
                Scheduler.AddDelayed(() => runOutsideOfGameplay(action, cancellationToken), 1000);
                return;
            }

            action();
        }

        private void restartToApplyUpdate(Velopack.UpdateManager updateManager, UpdateInfo update)
        {
            game.RestartOnExitAction = () => updateManager.WaitExitThenApplyUpdates(update.TargetFullRelease);
            game.AttemptExit();
        }

        private static string getUpdateChannel(Game.Configuration.ReleaseStream releaseStream, bool isLinux)
            => releaseStream == Game.Configuration.ReleaseStream.DevBuild
                ? isLinux ? "dev-linux" : "dev"
                : "stable";

        private static string getDevUpdateUrl(string stableUpdateUrl)
        {
            if (stableUpdateUrl.EndsWith(MosuServerEnvironment.UpdateFeedPath, StringComparison.OrdinalIgnoreCase))
                return stableUpdateUrl[..^MosuServerEnvironment.UpdateFeedPath.Length] + MosuServerEnvironment.DevUpdateFeedPath;

            if (!Uri.TryCreate(stableUpdateUrl, UriKind.Absolute, out Uri? stableUri))
                throw new InvalidOperationException($"Invalid update URL: {stableUpdateUrl}");

            return stableUri.GetLeftPart(UriPartial.Authority) + MosuServerEnvironment.DevUpdateFeedPath;
        }

        private sealed class AuthenticatedFileDownloader : IFileDownloader
        {
            private readonly IAPIProvider api;
            private readonly Uri feedUri;
            private readonly HttpClientFileDownloader downloader = new HttpClientFileDownloader();

            public AuthenticatedFileDownloader(IAPIProvider api, string feedUrl)
            {
                this.api = api;
                feedUri = new Uri(feedUrl, UriKind.Absolute);
            }

            public Task DownloadFile(string url, string targetFile, Action<int> progress, IDictionary<string, string>? headers = null,
                                     double timeout = 30, CancellationToken cancelToken = default)
                => downloader.DownloadFile(url, targetFile, progress, authenticatedHeaders(url, headers), timeout, cancelToken);

            public Task<byte[]> DownloadBytes(string url, IDictionary<string, string>? headers = null, double timeout = 30)
                => downloader.DownloadBytes(url, authenticatedHeaders(url, headers), timeout);

            public Task<string> DownloadString(string url, IDictionary<string, string>? headers = null, double timeout = 30)
                => downloader.DownloadString(url, authenticatedHeaders(url, headers), timeout);

            private IDictionary<string, string> authenticatedHeaders(string url, IDictionary<string, string>? source)
            {
                if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? requestUri) ||
                    !string.Equals(requestUri.Scheme, feedUri.Scheme, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(requestUri.IdnHost, feedUri.IdnHost, StringComparison.OrdinalIgnoreCase) ||
                    requestUri.Port != feedUri.Port)
                {
                    throw new InvalidOperationException("Refusing to send Dev Build credentials to a different update origin.");
                }

                var headers = source == null
                    ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(source, StringComparer.OrdinalIgnoreCase);
                string accessToken = api.AccessToken;
                if (!string.IsNullOrWhiteSpace(accessToken))
                    headers["Authorization"] = $"Bearer {accessToken}";
                headers["x-api-version"] = api.APIVersion.ToString(CultureInfo.InvariantCulture);
                return headers;
            }
        }

        private static void log(string text) => Logger.Log($"VelopackUpdateManager: {text}");
    }
}
