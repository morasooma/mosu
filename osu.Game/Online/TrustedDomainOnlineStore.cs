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

            return ResolveLookupUrl(url);
        }

        internal static string ResolveLookupUrl(string url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) &&
                (trustedDomainRoots.Any(root => isSameOrSubdomain(uri.Host, root)) ||
                 trustedHosts.Any(host => string.Equals(uri.Host, host, StringComparison.OrdinalIgnoreCase))))
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
