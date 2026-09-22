// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Online.API;

namespace osu.Game.Updater
{
    /// <summary>
    /// An update manager that shows notifications if a newer release is detected for mobile platforms.
    /// Installation is left up to the user.
    /// </summary>
    public partial class MobileUpdateNotifier : UpdateManager
    {
        public override ReleaseStream? FixedReleaseStream => stream;

        private string version = null!;
        private ReleaseStream stream;
        private OsuGameBase game = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(OsuGameBase game)
        {
            this.game = game;
            version = game.Version.TrimStart('v').Split('-').First();
            stream = Enum.TryParse(game.Version.Split('-').Last(), true, out ReleaseStream s) ? s : Configuration.ReleaseStream.Lazer;
        }

        protected override async Task<bool> PerformUpdateCheck(CancellationToken cancellationToken)
        {
            try
            {
                if (RuntimeInfo.OS == RuntimeInfo.Platform.Android)
                    return await checkAndroidUpdate(cancellationToken).ConfigureAwait(false);

                // Dev Build is Windows-only and is never advertised through mobile releases.
                bool includePrerelease = false;

                OsuJsonWebRequest<GitHubRelease[]> releasesRequest = new OsuJsonWebRequest<GitHubRelease[]>("https://api.github.com/repos/GooGuTeam/osu/releases?per_page=10&page=1");
                await releasesRequest.PerformAsync(cancellationToken).ConfigureAwait(false);

                GitHubRelease[] releases = releasesRequest.ResponseObject;
                GitHubRelease? latest = releases.OrderByDescending(r => r.PublishedAt).FirstOrDefault(r => includePrerelease || !r.Prerelease);

                if (latest == null)
                    return false;

                string latestTagName = latest.TagName.TrimStart('v').Split('-').First();

                if (latestTagName != version && tryGetBestUrl(latest, out string? url))
                {
                    Notifications.Post(new UpdateAvailableNotification(cancellationToken)
                    {
                        Text = LocalisableString.Interpolate($"{NotificationsStrings.UpdateAvailable(version, latestTagName)}\n\n{NotificationsStrings.UpdateAvailableManualInstall}"),
                        Icon = FontAwesome.Solid.Download,
                        Activated = () =>
                        {
                            host.OpenUrlExternally(url);
                            return true;
                        }
                    });

                    return true;
                }
            }
            catch (Exception e)
            {
                // we shouldn't crash on a web failure. or any failure for the matter.
                Logger.Error(e, "Mobile update check failed");
                return true;
            }

            return false;
        }

        private async Task<bool> checkAndroidUpdate(CancellationToken cancellationToken)
        {
            string updateUrl = game.CreateEndpoints().UpdateUrl.TrimEnd('/');
            if (string.IsNullOrWhiteSpace(updateUrl))
                return false;

            string channel = getUpdateChannel(stream);
            var manifestRequest = new OsuJsonWebRequest<AndroidUpdateManifest>($"{updateUrl}/android.{channel}.json");
            await manifestRequest.PerformAsync(cancellationToken).ConfigureAwait(false);

            AndroidUpdateManifest manifest = manifestRequest.ResponseObject;
            if (!isNewerVersion(version, manifest.Version))
                return false;

            if (!tryGetAndroidDownloadUrl(updateUrl, channel, manifest, out string? downloadUrl))
            {
                Logger.Log($"MobileUpdateNotifier: Ignoring invalid Android update manifest for {manifest.Version}.");
                return false;
            }

            Notifications.Post(new UpdateAvailableNotification(cancellationToken)
            {
                Text = LocalisableString.Interpolate($"{NotificationsStrings.UpdateAvailable(version, manifest.Version)}\n\n{NotificationsStrings.UpdateAvailableManualInstall}"),
                Icon = FontAwesome.Solid.Download,
                Activated = () =>
                {
                    host.OpenUrlExternally(downloadUrl);
                    return true;
                }
            });

            return true;
        }

        internal static bool isNewerVersion(string currentVersion, string candidateVersion)
        {
            return Version.TryParse(currentVersion, out Version? current)
                   && Version.TryParse(candidateVersion, out Version? candidate)
                   && candidate > current;
        }

        internal static bool tryGetAndroidDownloadUrl(string updateUrl, string expectedChannel, AndroidUpdateManifest manifest, [NotNullWhen(true)] out string? url)
        {
            url = null;

            if (manifest.SchemaVersion != 1
                || !string.Equals(manifest.Channel, expectedChannel, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(manifest.Platform, "android", StringComparison.OrdinalIgnoreCase)
                || !string.Equals(manifest.Architecture, "arm64-v8a", StringComparison.OrdinalIgnoreCase)
                || !Version.TryParse(manifest.Version, out _)
                || manifest.ApplicationVersion <= 0
                || manifest.Size <= 0
                || string.IsNullOrWhiteSpace(manifest.Sha256)
                || manifest.Sha256.Length != 64
                || manifest.Sha256.Any(c => !Uri.IsHexDigit(c)))
            {
                return false;
            }

            string expectedFileName = $"mosu-{manifest.Version}-android-arm64.apk";
            if (!string.Equals(manifest.FileName, expectedFileName, StringComparison.Ordinal))
                return false;

            if (!Uri.TryCreate(updateUrl.TrimEnd('/') + '/', UriKind.Absolute, out Uri? baseUri)
                || (baseUri.Scheme != Uri.UriSchemeHttps && baseUri.Scheme != Uri.UriSchemeHttp))
            {
                return false;
            }

            url = new Uri(baseUri, Uri.EscapeDataString(manifest.FileName)).AbsoluteUri;
            return true;
        }

        private static string getUpdateChannel(ReleaseStream releaseStream)
            => "stable";

        private bool tryGetBestUrl(GitHubRelease release, [NotNullWhen(true)] out string? url)
        {
            url = null;
            GitHubAsset? bestAsset = null;

            switch (RuntimeInfo.OS)
            {
                case RuntimeInfo.Platform.iOS:
                    if (release.Assets?.Exists(f => f.Name.EndsWith(".ipa", StringComparison.Ordinal)) == true)
                        // iOS releases are available via testflight. this link seems to work well enough for now.
                        // see https://stackoverflow.com/a/32960501
                        url = "itms-beta://beta.itunes.apple.com/v1/app/1447765923";

                    break;

                case RuntimeInfo.Platform.Android:
                    if (release.Assets?.Exists(f => f.Name.EndsWith(".apk", StringComparison.Ordinal)) == true)
                        // on our testing device using the .apk URL causes the download to magically disappear.
                        url = release.HtmlUrl;

                    break;
            }

            url ??= bestAsset?.BrowserDownloadUrl;
            return url != null;
        }
    }
}
