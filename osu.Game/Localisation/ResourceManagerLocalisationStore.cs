// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public class ResourceManagerLocalisationStore : ILocalisationStore
    {
        private readonly Dictionary<string, string?> lookupCache = new Dictionary<string, string?>();
        private readonly Dictionary<string, ResourceManager> resourceManagers = new Dictionary<string, ResourceManager>();

        private static readonly HashSet<string> morasooma_client_identity_keys = new HashSet<string>(StringComparer.Ordinal)
        {
            "osu.Game.Resources.Localisation.ButtonSystem:mobile_disclaimer_body",
            "osu.Game.Resources.Localisation.Dialog:confirm_exit_header_text",
            "osu.Game.Resources.Localisation.FirstRunSetupBeatmapScreen:description",
            "osu.Game.Resources.Localisation.FirstRunSetupOverlay:first_run_setup_description",
            "osu.Game.Resources.Localisation.FirstRunSetupOverlay:welcome_description",
            "osu.Game.Resources.Localisation.FirstRunSetupOverlay:ui_scale_description",
            "osu.Game.Resources.Localisation.GeneralSettings:open_osu_folder",
            "osu.Game.Resources.Localisation.GraphicsSettings:minimise_on_focus_loss",
            "osu.Game.Resources.Localisation.LayoutSettings:osu_is_running_exclusive_fullscreen",
            "osu.Game.Resources.Localisation.Leaderboard:please_invest_in_an_osu_supporter_tag_to_view_this_leaderboard",
            "osu.Game.Resources.Localisation.NamedOverlayComponent:changelog_description",
            "osu.Game.Resources.Localisation.Notifications:audio_playback_issue",
            "osu.Game.Resources.Localisation.Notifications:game_version_after_update",
            "osu.Game.Resources.Localisation.Notifications:api_connection_interrupted",
            "osu.Game.Resources.Localisation.Notifications:update_available",
            "osu.Game.Resources.Localisation.Notifications:update_available_package_managed",
            "osu.Game.Resources.Localisation.Notifications:elevated_privileges",
            "osu.Game.Resources.Localisation.Notifications:macos_app_location",
            "osu.Game.Resources.Localisation.OnlinePlay:supporter_only_duration_notice",
            "osu.Game.Resources.Localisation.Settings:header_description",
            "osu.Game.Resources.Localisation.StorageErrorDialog:storage_error",
            "osu.Game.Resources.Localisation.StorageErrorDialog:location_is_not_accessible",
            "osu.Game.Resources.Localisation.StorageErrorDialog:location_is_empty",
            "osu.Game.Resources.Localisation.SupporterDisplay:thank_you_for_supporting",
            "osu.Game.Resources.Localisation.SupporterDisplay:consider_becoming_a_supporter",
            "osu.Game.Resources.Localisation.UserInterface:not_supporter_note",
            "osu.Game.Resources.Localisation.UserInterface:osu_music_theme",
            "osu.Game.Resources.Localisation.WindowsAssociationManager:osu_beatmap",
            "osu.Game.Resources.Localisation.WindowsAssociationManager:osu_replay",
            "osu.Game.Resources.Localisation.WindowsAssociationManager:osu_skin",
            "osu.Game.Resources.Localisation.WindowsAssociationManager:osu_protocol",
            "osu.Game.Resources.Localisation.WindowsAssociationManager:osu_multiplayer",
        };

        public ResourceManagerLocalisationStore(string cultureCode)
        {
            EffectiveCulture = new CultureInfo(cultureCode);
        }

        public void Dispose()
        {
        }

        public string? Get(string lookup)
        {
            lock (lookupCache)
            {
                if (lookupCache.TryGetValue(lookup, out string? cached))
                    return cached;
            }

            string? result = getInternal(lookup);

            lock (lookupCache)
            {
                // It's important to cache both lookup successes and failures here.
                // The strings cannot really change under the game, so there's no risk of having a lookup fail now but succeed at some point later,
                // and lookup failures are *more* costly than successes as they incur a scan of *all* strings in resources.
                lookupCache[lookup] = result;
            }

            return result;
        }

        private string? getInternal(string lookup)
        {
            string[] split = lookup.Split(':');

            if (split.Length < 2)
                return null;

            string ns = split[0];
            string key = split[1];

            lock (resourceManagers)
            {
                if (!resourceManagers.TryGetValue(ns, out var manager))
                {
                    var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

                    // Traverse backwards through periods in the namespace to find a matching assembly.
                    string assemblyName = ns;

                    while (!string.IsNullOrEmpty(assemblyName))
                    {
                        var matchingAssembly = loadedAssemblies.FirstOrDefault(asm => asm.GetName().Name == assemblyName);

                        if (matchingAssembly != null)
                        {
                            resourceManagers[ns] = manager = new ResourceManager(ns, matchingAssembly);
                            break;
                        }

                        int lastIndex = Math.Max(0, assemblyName.LastIndexOf('.'));
                        assemblyName = assemblyName.Substring(0, lastIndex);
                    }
                }

                if (manager == null)
                    return null;

                // When using the English culture, prefer the fallbacks rather than osu-resources baked strings.
                // They are guaranteed to be up-to-date, and is also what a developer expects to see when making changes to `xxxStrings.cs` files.
                if (EffectiveCulture.Name == @"en")
                    return null;

                try
                {
                    string? value = manager.GetString(key, EffectiveCulture);

                    return value == null || !morasooma_client_identity_keys.Contains(lookup)
                        ? value
                        : replaceClientBranding(value);
                }
                catch (MissingManifestResourceException)
                {
                    // in the case the manifest is missing, it is likely that the user is adding code-first implementations of new localisation namespaces.
                    // it's fine to ignore this as localisation will fallback to default values.
                    return null;
                }
            }
        }

        public Task<string?> GetAsync(string lookup, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Get(lookup));
        }

        public Stream GetStream(string name)
        {
            throw new NotImplementedException();
        }

        public IEnumerable<string> GetAvailableResources()
        {
            throw new NotImplementedException();
        }

        public CultureInfo EffectiveCulture { get; }

        private static string replaceClientBranding(string value) => value
            .Replace("osu!supporter", "Morasooma Supporter", StringComparison.OrdinalIgnoreCase)
            .Replace("osu!", "Morasooma", StringComparison.OrdinalIgnoreCase)
            .Replace("Mosu", "Morasooma", StringComparison.Ordinal);
    }
}
