namespace osu.Game.Online
{
    public static class MosuServerEnvironment
    {
        public const string PublicServerUrl = @"https://morasooma.net";
#if DEBUG
        public const string ServerUrl = @"http://127.0.0.1:18082";
#else
        public const string ServerUrl = PublicServerUrl;
#endif
        public const string ProxyServerUrl = @"https://proxy.morasooma.net";
        public const string UpdateFeedPath = @"/client/updates";
        public const string InstallerPath = UpdateFeedPath + @"/mosu-stable-Setup.exe";
        public const string UpdateUrl = ServerUrl + UpdateFeedPath;
        public const string InstallerUrl = ServerUrl + InstallerPath;

        public static string GetServerUrl(bool useConnectionProxy) => useConnectionProxy ? ProxyServerUrl : ServerUrl;

        public static bool IsThirdPartyServer { get; set; }
        public static bool SupportsSpecialRulesets { get; set; } = false;

        /// <summary>
        /// Whether only osu!relax is supported as a special ruleset (true for the Mosu server).
        /// When true, autopilot and relax variants of other rulesets are not displayed.
        /// </summary>
        public static bool OnlyOsuRelax => !IsThirdPartyServer;
        public static string ActiveVersion { get; set; } = string.Empty;
        public static string ActiveVersionHash { get; set; } = string.Empty;
        public static bool DisableBeatmapStatusOverwrite { get; set; }
    }
}
