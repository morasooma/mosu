using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using osu.Framework.Bindables;
using osu.Framework.Platform;
using osu.Game.Configuration;

namespace osu.Game.Online
{
    public class ServerProfileManager
    {
        private const string PROFILES_FILE = "server_profiles.json";

        public const string DEFAULT_STABLE_CLIENT_VERSION = "b20260412";

        private static readonly (string Domain, string Name)[] forbiddenServers =
        {
            ("akatsuki.gg", "Akatsuki"),
            ("ussr.pl", "ussr.pl"),
            ("osuokayu.pw", "osuokayu.pw"),
            ("osudesu.su", "osudesu.su"),
            ("mellowosu.ru", "mellowosu.ru"),
        };

        private Storage? storage;
        private OsuConfigManager? config;
        private bool isInitialized;

        public List<ServerProfile> Profiles { get; private set; } = new List<ServerProfile>();

        public ServerProfile ActiveProfile { get; private set; } = null!;

        public ServerProfileManager(Storage? storage, OsuConfigManager? config = null)
        {
            if (storage != null)
            {
                Initialize(storage, config);
            }
            else
            {
                LoadProfiles();
                ActiveProfile = Profiles.First(p => p.Id == "default");
            }
        }

        public void Initialize(Storage newStorage, OsuConfigManager? newConfig)
        {
            if (isInitialized || newStorage == null) return;

            storage = newStorage;
            config = newConfig;
            isInitialized = true;

            LoadProfiles();

            var activeId = config?.Get<string>(OsuSetting.ForkActiveProfileId) ?? "default";
            ActiveProfile = Profiles.FirstOrDefault(p => p.Id == activeId) ?? Profiles.First(p => p.Id == "default");

            if (ActiveProfile.Id != activeId && config != null)
            {
                config.SetValue(OsuSetting.ForkActiveProfileId, ActiveProfile.Id);
                config.SetValue(OsuSetting.CustomApiUrl, string.Empty);
                config.Save();
            }

            if (config != null)
            {
                config.GetBindable<string>(OsuSetting.Username).BindValueChanged(e =>
                {
                    if (ActiveProfile != null && ActiveProfile.Username != e.NewValue)
                    {
                        ActiveProfile.Username = e.NewValue;
                        SaveProfiles();
                    }
                }, true);

                config.GetBindable<string>(OsuSetting.Token).BindValueChanged(e =>
                {
                    if (ActiveProfile != null && ActiveProfile.Token != e.NewValue)
                    {
                        ActiveProfile.Token = e.NewValue;
                        SaveProfiles();
                    }
                }, true);
            }
        }

        public void LoadProfiles()
        {
            Profiles.Clear();

            try
            {
                if (storage != null && storage.Exists(PROFILES_FILE))
                {
                    using (var stream = storage.GetStream(PROFILES_FILE, FileAccess.Read))
                    using (var reader = new StreamReader(stream))
                    {
                        string json = reader.ReadToEnd();
                        var loaded = JsonConvert.DeserializeObject<List<ServerProfile>>(json);
                        if (loaded != null)
                            Profiles.AddRange(loaded);
                    }
                }
            }
            catch (Exception ex)
            {
                osu.Framework.Logging.Logger.Log($"Failed to load server profiles: {ex}", osu.Framework.Logging.LoggingTarget.Runtime);
            }

            // Do not retain forbidden profiles from older client versions or manually edited files.
            bool removedForbiddenProfiles = Profiles.RemoveAll(profile => profile.Id != "default" && TryGetForbiddenServer(profile, out _)) > 0;

            // Ensure default profiles exist
            var defaultMosu = Profiles.FirstOrDefault(p => p.Id == "default");
            if (defaultMosu == null)
            {
                defaultMosu = new ServerProfile
                {
                    Id = "default",
                    Name = "Morasooma Server",
                    ApiUrl = "",
                    WebsiteUrl = "",
                    ClientVersion = "Protected",
                    VersionHash = "Protected",
                    ClientId = "Protected",
                    ClientSecret = "Protected",
                    SupportsSpecialRulesets = true,
                    IsDefault = true
                };
                Profiles.Insert(0, defaultMosu);
            }
            else
            {
                defaultMosu.Name = "Morasooma Server";
                defaultMosu.IsDefault = true;
                defaultMosu.ClientVersion = "Protected";
                defaultMosu.VersionHash = "Protected";
                defaultMosu.ClientId = "Protected";
                defaultMosu.ClientSecret = "Protected";
                defaultMosu.SupportsSpecialRulesets = true;
            }

            foreach (var profile in Profiles.Where(p => p.UseStableProtocol))
                EnsureStableDefaults(profile);

            if (removedForbiddenProfiles)
                SaveProfiles();
        }

        public void SaveProfiles()
        {
            if (storage == null) return;

            try
            {
                foreach (var profile in Profiles.Where(p => p.UseStableProtocol))
                    EnsureStableDefaults(profile);

                var defaultMosu = Profiles.FirstOrDefault(p => p.Id == "default");
                if (defaultMosu != null)
                {
                    defaultMosu.Name = "Morasooma Server";
                    defaultMosu.ClientVersion = "Protected";
                    defaultMosu.VersionHash = "Protected";
                    defaultMosu.ClientId = "Protected";
                    defaultMosu.ClientSecret = "Protected";
                    defaultMosu.SupportsSpecialRulesets = true;
                }

                string json = JsonConvert.SerializeObject(Profiles, Formatting.Indented);
                using (var stream = storage.GetStream(PROFILES_FILE, FileAccess.Write, FileMode.Create))
                using (var writer = new StreamWriter(stream))
                {
                    writer.Write(json);
                }
            }
            catch (Exception ex)
            {
                osu.Framework.Logging.Logger.Log($"Failed to save server profiles: {ex}", osu.Framework.Logging.LoggingTarget.Runtime);
            }
        }

        public bool SelectProfile(string id)
        {
            var profile = Profiles.FirstOrDefault(p => p.Id == id);
            if (profile == null || TryGetForbiddenServer(profile, out _)) return false;

            ActiveProfile = profile;

            if (config != null)
            {
                config.SetValue(OsuSetting.ForkActiveProfileId, id);
                config.SetValue(OsuSetting.CustomApiUrl, profile.Id == "default" ? string.Empty : profile.ApiUrl);
                config.SetValue(OsuSetting.Username, profile.Username ?? string.Empty);
                config.SetValue(OsuSetting.Token, profile.Token ?? string.Empty);
                config.Save();
            }

            return true;
        }

        public void AddProfile(ServerProfile profile)
        {
            if (profile.UseStableProtocol)
                EnsureStableDefaults(profile);
            else
                EnsureStableIdentity(profile);

            Profiles.Add(profile);
            SaveProfiles();
        }

        public static void EnsureStableDefaults(ServerProfile profile)
        {
            EnsureStableIdentity(profile);

            if (!IsValidStableClientVersion(profile.ClientVersion))
                profile.ClientVersion = DEFAULT_STABLE_CLIENT_VERSION;
        }

        public static bool IsValidStableClientVersion(string? version)
            => !string.IsNullOrWhiteSpace(version) && Regex.IsMatch(version, @"(?:^b)?\d{8}", RegexOptions.CultureInvariant);

        public static bool TryGetForbiddenServer(ServerProfile profile, out string serverName)
        {
            foreach (string url in new[] { profile.ApiUrl, profile.WebsiteUrl, profile.BanchoUrl })
            {
                if (tryGetForbiddenServer(url, out serverName))
                    return true;
            }

            serverName = string.Empty;
            return false;
        }

        private static bool tryGetForbiddenServer(string? url, out string serverName)
        {
            serverName = string.Empty;

            if (string.IsNullOrWhiteSpace(url))
                return false;

            string candidate = url.Trim();
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out Uri? uri) || string.IsNullOrEmpty(uri.Host))
                Uri.TryCreate($"https://{candidate.TrimStart('/')}", UriKind.Absolute, out uri);

            if (uri == null || string.IsNullOrEmpty(uri.Host))
                return false;

            string host = uri.IdnHost.TrimEnd('.');

            foreach (var forbidden in forbiddenServers)
            {
                if (host.Equals(forbidden.Domain, StringComparison.OrdinalIgnoreCase)
                    || host.EndsWith($".{forbidden.Domain}", StringComparison.OrdinalIgnoreCase))
                {
                    serverName = forbidden.Name;
                    return true;
                }
            }

            return false;
        }

        public static void EnsureStableIdentity(ServerProfile profile)
        {
            if (string.IsNullOrEmpty(profile.StableOsuPathHash))
                profile.StableOsuPathHash = createStableIdentityValue();

            if (string.IsNullOrEmpty(profile.StableAdapterHash))
                profile.StableAdapterHash = createStableIdentityValue();

            if (string.IsNullOrEmpty(profile.StableUninstallId))
                profile.StableUninstallId = createStableIdentityValue();

            if (string.IsNullOrEmpty(profile.StableDiskId))
                profile.StableDiskId = createStableIdentityValue();
        }

        private static string createStableIdentityValue() => Guid.NewGuid().ToString("N");

        public void DeleteProfile(string id)
        {
            if (id == "default") return;

            var profile = Profiles.FirstOrDefault(p => p.Id == id);
            if (profile != null)
            {
                Profiles.Remove(profile);
                if (ActiveProfile.Id == id)
                {
                    SelectProfile("default");
                }
                else
                {
                    SaveProfiles();
                }
            }
        }
    }
}
