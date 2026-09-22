using System;

namespace osu.Game.Online
{
    public class ServerProfile
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string ApiUrl { get; set; } = string.Empty;
        public string WebsiteUrl { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string ClientVersion { get; set; } = string.Empty;
        public string VersionHash { get; set; } = string.Empty;
        public string ClientId { get; set; } = "5";
        public string ClientSecret { get; set; } = string.Empty;
        public bool UseStableProtocol { get; set; }
        public string BanchoUrl { get; set; } = string.Empty;
        public string StablePasswordHash { get; set; } = string.Empty;
        public string StableOsuPathHash { get; set; } = string.Empty;
        public string StableAdapterHash { get; set; } = string.Empty;
        public string StableUninstallId { get; set; } = string.Empty;
        public string StableDiskId { get; set; } = string.Empty;
        public bool SupportsSpecialRulesets { get; set; }
        public bool IsDefault { get; set; }
    }
}
