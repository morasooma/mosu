// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using osu.Framework.IO.Network;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Online.Hinamizawa;

namespace osu.Game.Online
{
    public sealed class TrustedDomainOnlineStore : OnlineStore
    {
        private static readonly string[] trustedDomainRoots =
        {
            "ppy.sh",
            "morasooma.net",
        };

        private static readonly string[] trustedHosts = new[]
        {
            new ProductionEndpointConfiguration().APIUrl,
            new ProductionEndpointConfiguration().WebsiteUrl,
            new DevelopmentEndpointConfiguration().APIUrl,
            new DevelopmentEndpointConfiguration().WebsiteUrl,
        }.Select(extractHost)
         .Where(host => !string.IsNullOrEmpty(host))
         .Distinct(StringComparer.OrdinalIgnoreCase)
         .ToArray()!;

        private readonly OsuConfigManager? configManager;

        public TrustedDomainOnlineStore(OsuConfigManager? configManager = null)
        {
            this.configManager = configManager;
        }

        // TextureLoaderStore uses GetStream -> Get, including for asynchronous texture loads.
        // Keep the original resource name as the texture cache key; only change its transport.
        public override byte[] Get(string url)
        {
            if (!string.IsNullOrWhiteSpace(configManager?.Get<string>(OsuSetting.CustomApiUrl)))
                return base.Get(url);

            return GetWithCoverFallback(url, downloadCover, original => base.Get(original))!;
        }

        internal static byte[]? GetWithCoverFallback(string url, Func<string, byte[]?> getMirror, Func<string, byte[]?> getOriginal)
        {
            string? mirrorUrl = GetMirrorCoverUrl(url);
            if (mirrorUrl != null)
            {
                byte[]? data = getMirror(mirrorUrl);
                if (data?.Length > 0)
                    return data;
            }

            return getOriginal(url);
        }

        internal static string? GetMirrorCoverUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || uri.UserInfo.Length != 0 ||
                !string.Equals(uri.Host, "assets.ppy.sh", StringComparison.OrdinalIgnoreCase))
                return null;

            string[] parts = uri.AbsolutePath.Split('/');
            if (parts.Length != 5 || parts[1] != "beatmaps" || parts[3] != "covers" ||
                !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out int setId) || setId <= 0)
                return null;

            if (parts[4] is not ("cover.jpg" or "cover@2x.jpg" or "card.jpg" or "card@2x.jpg" or
                "list.jpg" or "list@2x.jpg" or "slimcover.jpg" or "slimcover@2x.jpg"))
                return null;

            // The documented render endpoint preserves the variant and revision query,
            // caches hot images, and can reconstruct missing covers from archived artwork.
            return $"{HinamizawaClient.HOST}/v3/osu/beatmaps/proxy-image?url={Uri.EscapeDataString(url)}";
        }

        private static byte[]? downloadCover(string url)
        {
            try
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                using var request = new CoverWebRequest(url)
                {
                    Timeout = 3000,
                    AllowRetryOnTimeout = false,
                };
                request.PerformAsync(deadline.Token).GetAwaiter().GetResult();
                return request.GetResponseData();
            }
            catch (Exception ex)
            {
                Logger.Log($"Beatmap cover mirror failed; falling back to the original URL: {ex.Message}", LoggingTarget.Network);
                return null;
            }
        }

        private sealed class CoverWebRequest(string url) : WebRequest(url)
        {
            protected override string UserAgent => $"Morasooma/{typeof(TrustedDomainOnlineStore).Assembly.GetName().Version} (+https://github.com/GooGuTeam/osu)";
        }

        protected override string GetLookupUrl(string url)
        {
            string? customApiUrl = configManager?.Get<string>(OsuSetting.CustomApiUrl);
            if (!string.IsNullOrWhiteSpace(customApiUrl))
                return url;

            return ResolveLookupUrl(url);
        }

        internal static string ResolveLookupUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (trustedDomainRoots.Any(root => isSameOrSubdomain(uri.Host, root)) ||
                 trustedHosts.Any(host => string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase)) ||
                 (uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && uri.UserInfo.Length == 0 &&
                  string.Equals(uri.Host, "mirror.hinamizawa.ai", StringComparison.OrdinalIgnoreCase))))
            {
                return url;
            }

            // Older API records may contain an absolute URL from a retired server.
            // Server-owned files are identified by their path and can be safely
            // resolved against the current endpoint without contacting the old host.
            if (uri != null && uri.AbsolutePath.StartsWith("/file/", StringComparison.OrdinalIgnoreCase))
            {
                var currentEndpoint = new Uri(MosuServerEnvironment.ServerUrl, UriKind.Absolute);
                var migratedUri = new UriBuilder(uri)
                {
                    Scheme = currentEndpoint.Scheme,
                    Host = currentEndpoint.Host,
                    Port = currentEndpoint.IsDefaultPort ? -1 : currentEndpoint.Port,
                    UserName = string.Empty,
                    Password = string.Empty,
                }.Uri.AbsoluteUri;

                Logger.Log(
                    $"[TrustedDomainOnlineStore] Redirected stored server resource to the current endpoint: {uri.PathAndQuery}",
                    LoggingTarget.Network,
                    LogLevel.Verbose
                );

                return migratedUri;
            }

            Logger.Log(
                $"[TrustedDomainOnlineStore] Blocked external resource lookup: {url}",
                LoggingTarget.Network,
                LogLevel.Important
            );

            return string.Empty;
        }

        private static bool isSameOrSubdomain(string host, string root) =>
            string.Equals(host, root, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith($".{root}", StringComparison.OrdinalIgnoreCase);

        private static string? extractHost(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return uri.Host;

            return null;
        }
    }
}
