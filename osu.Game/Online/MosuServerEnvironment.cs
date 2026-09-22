using System;

namespace osu.Game.Online
{
    public static class MosuServerEnvironment
    {
        public const string PublicServerUrl = @"https://morasooma.net";
        public const string ToriiServerUrl = @"https://lazer-api.shikkesora.com";

#if DEBUG
        public const string ServerUrl = @"http://127.0.0.1:18082";
#else
        public const string ServerUrl = PublicServerUrl;
#endif
        public const string PrimaryProxyServerUrl = @"https://proxy.morasooma.net";
        public const string SecondaryProxyServerUrl = @"https://proxy2.morasooma.net";

        // The original proxy is currently unavailable. Keep both endpoints named so
        // the preferred route can be changed without losing the infrastructure map.
        public const string ProxyServerUrl = SecondaryProxyServerUrl;
        public const string UpdateFeedPath = @"/client/updates";
        public const string DevUpdateFeedPath = @"/api/v2/client-updates/dev";
        public const string InstallerPath = UpdateFeedPath + @"/mosu-stable-Setup.exe";
        public const string UpdateUrl = ServerUrl + UpdateFeedPath;
        public const string InstallerUrl = ServerUrl + InstallerPath;

        public static string GetServerUrl(MosuConnectionRoute route) => route switch
        {
            MosuConnectionRoute.Direct => ServerUrl,
            MosuConnectionRoute.Proxy1 => PrimaryProxyServerUrl,
            MosuConnectionRoute.Proxy2 => SecondaryProxyServerUrl,
            _ => ServerUrl,
        };

        public static string GetServerUrl(bool useConnectionProxy) =>
            GetServerUrl(useConnectionProxy ? MosuConnectionRoute.Proxy2 : MosuConnectionRoute.Direct);

        public static bool IsThirdPartyServer { get; set; }
        public static bool IsToriiServer { get; set; }
        public static bool UsesStableProtocol { get; set; }
        public static bool SupportsSpecialRulesets { get; set; } = false;

        /// <summary>
        /// Whether the active server uses the lazer protocol required by break skipping.
        /// Stable servers do not expose the required gameplay capabilities.
        /// </summary>
        public static bool SupportsBreakSkipping => !UsesStableProtocol;

        /// <summary>
        /// Whether only osu!relax is supported as a special ruleset (true for the Mosu server).
        /// When true, autopilot and relax variants of other rulesets are not displayed.
        /// </summary>
        public static bool OnlyOsuRelax => !IsThirdPartyServer;
        public static string ActiveVersion { get; set; } = string.Empty;
        public static string ActiveVersionHash { get; set; } = string.Empty;
        public static bool DisableBeatmapStatusOverwrite { get; set; }

        public static bool IsToriiServerUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            string candidate = url.Trim();

            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri))
                Uri.TryCreate($"https://{candidate.TrimStart('/')}", UriKind.Absolute, out uri);

            return uri?.IdnHost.Equals("lazer-api.shikkesora.com", StringComparison.OrdinalIgnoreCase) == true;
        }
    }
}
