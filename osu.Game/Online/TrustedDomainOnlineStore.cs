// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Game.Configuration;

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

        protected override string GetLookupUrl(string url)
        {
            string? customApiUrl = configManager?.Get<string>(OsuSetting.CustomApiUrl);
            if (!string.IsNullOrWhiteSpace(customApiUrl))
                return url;

            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (trustedDomainRoots.Any(root => isSameOrSubdomain(uri.Host, root)) ||
                 trustedHosts.Any(host => string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase))))
            {
                return url;
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
