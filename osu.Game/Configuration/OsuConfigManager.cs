// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework;
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Configuration.Tracking;
using osu.Framework.Extensions;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Extensions.TypeExtensions;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Beatmaps.Drawables.Cards;
using osu.Game.Input;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Overlays.Dashboard.Friends;
using osu.Game.Overlays.Mods.Input;
using osu.Game.Rulesets.Scoring;
using osu.Game.Scoring;
using osu.Game.Screens.Edit.Compose.Components;
using osu.Game.Screens.OnlinePlay.Lounge.Components;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Game.Skinning;
using osu.Game.Users;
using osu.Game.Online.API;
using osu.Game.Online;

namespace osu.Game.Configuration
{
    public class OsuConfigManager : IniConfigManager<OsuSetting>, IGameplaySettings
    {
        private readonly Storage storage;
        private readonly object originalGameCredentialsLock = new object();
        private string originalGameUsername = string.Empty;
        private string originalGameToken = string.Empty;
        private string? originalGameVersionLine;

        protected override string Filename => @"game.ini";
        protected virtual string CustomFilename => @"mosu.ini";

        public OsuConfigManager(Storage storage)
            : base(storage)
        {
            this.storage = storage;
            migrateOldConfig(storage);
            migrateReleaseStreamSetting(storage);
            migrateLegacyConnectionProxySetting();
            migrateSongSelectStyleSetting();
            enforceSingleServerRestrictions();
        }

        /// <summary>
        /// Maps the two legacy boolean song-select toggles (v1 screen + skinned legacy carousel)
        /// onto the single <see cref="ForkSongSelectStyle"/> enum.
        /// </summary>
        private void migrateSongSelectStyleSetting()
        {
            bool v1 = Get<bool>(OsuSetting.ForkSongSelectV1Carousel);
            bool skinned = Get<bool>(OsuSetting.ForkSongSelectSkinnedLegacyCarousel);

            if (!v1 && !skinned)
                return;

            // An explicitly saved value from the new dropdown always wins. Without this check,
            // a stale old v1 toggle would change Legacy back to 2024 on every launch.
            var style = GetBindable<ForkSongSelectStyle>(OsuSetting.ForkSongSelectStyle);

            if (style.IsDefault)
                style.Value = skinned ? ForkSongSelectStyle.LegacySkinned : ForkSongSelectStyle.Classic2024;

            // The old values are migration inputs only. Clear them so they cannot override a
            // subsequent choice if the selected new style happens to equal its default value.
            SetValue(OsuSetting.ForkSongSelectV1Carousel, false);
            SetValue(OsuSetting.ForkSongSelectSkinnedLegacyCarousel, false);
        }

        private void migrateLegacyConnectionProxySetting()
        {
            if (!Get<bool>(OsuSetting.ForkUseConnectionProxy))
                return;

            SetValue(OsuSetting.ForkConnectionRoute, MosuConnectionRoute.Proxy2);
            SetValue(OsuSetting.ForkUseConnectionProxy, false);
        }

        private bool isCustomSetting(OsuSetting lookup)
        {
            string name = lookup.ToString();
            return name.StartsWith("Fork", StringComparison.Ordinal) ||
                   lookup == OsuSetting.CustomApiUrl ||
                   lookup == OsuSetting.DisableAutomaticUpdates ||
                   lookup == OsuSetting.ReleaseStream ||
                   lookup == OsuSetting.Username ||
                   lookup == OsuSetting.Token ||
                   lookup == OsuSetting.Version;
        }

        protected override void PerformLoad()
        {
            loadFromFile(Filename);
            loadFromFile(CustomFilename);
        }

        private Storage? getStorage()
        {
            if (storage != null) return storage;

            var field = typeof(IniConfigManager<OsuSetting>).GetField("storage", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return field?.GetValue(this) as Storage;
        }

        private void loadFromFile(string filename)
        {
            var activeStorage = getStorage();
            if (string.IsNullOrEmpty(filename) || activeStorage == null || !activeStorage.Exists(filename)) return;

            using (var stream = activeStorage.GetStream(filename))
            {
                if (stream == null)
                    return;

                using (var reader = new StreamReader(stream))
                {
                    string? line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        int equalsIndex = line.IndexOf('=');

                        if (line.Length == 0 || line[0] == '#' || equalsIndex < 0) continue;

                        string key = line.AsSpan(0, equalsIndex).Trim().ToString();
                        string val = line.AsSpan(equalsIndex + 1).Trim().ToString();

                        if (!Enum.TryParse(key, out OsuSetting lookup))
                            continue;

                        bool isCustom = isCustomSetting(lookup);
                        bool isTargetFileCustom = filename == CustomFilename;

                        if (isCustom != isTargetFileCustom)
                        {
                            if (filename == Filename)
                            {
                                if (lookup == OsuSetting.Username)
                                    originalGameUsername = val;
                                else if (lookup == OsuSetting.Token)
                                    originalGameToken = val;
                                else if (lookup == OsuSetting.Version)
                                    originalGameVersionLine = line;
                            }
                            continue;
                        }

                        if (ConfigStore.TryGetValue(lookup, out var b))
                        {
                            try
                            {
                                if (!(b is osu.Framework.Bindables.IParseable parseable))
                                    throw new InvalidOperationException($"Bindable type {b.GetType().ReadableName()} is not parseable.");

                                parseable.Parse(val, System.Globalization.CultureInfo.InvariantCulture);
                            }
                            catch (Exception e)
                            {
                                osu.Framework.Logging.Logger.Log($@"Unable to parse config key {lookup}: {e}", osu.Framework.Logging.LoggingTarget.Runtime, osu.Framework.Logging.LogLevel.Important);
                            }
                        }
                        else if (AddMissingEntries)
                        {
                            SetDefault(lookup, val);
                        }
                    }
                }
            }
        }

        protected override bool PerformSave()
        {
            lock (originalGameCredentialsLock)
                return saveToFile(Filename, false) && saveToFile(CustomFilename, true);
        }

        /// <summary>
        /// Reads the credentials owned by the official osu! client directly from <c>game.ini</c>.
        /// These are deliberately kept separate from the Mosu credentials stored in <c>mosu.ini</c>.
        /// </summary>
        public (string Username, string Token) ReadOriginalGameCredentials()
        {
            lock (originalGameCredentialsLock)
            {
                var activeStorage = getStorage();

                if (activeStorage == null || !activeStorage.Exists(Filename))
                    return (string.Empty, string.Empty);

                string username = string.Empty;
                string token = string.Empty;

                try
                {
                    using var stream = activeStorage.GetStream(Filename);

                    if (stream == null)
                        return (string.Empty, string.Empty);

                    using var reader = new StreamReader(stream);
                    string? line;

                    while ((line = reader.ReadLine()) != null)
                    {
                        int equalsIndex = line.IndexOf('=');

                        if (equalsIndex < 0)
                            continue;

                        string key = line.AsSpan(0, equalsIndex).Trim().ToString();
                        string value = line.AsSpan(equalsIndex + 1).Trim().ToString();

                        if (key == nameof(OsuSetting.Username))
                            username = value;
                        else if (key == nameof(OsuSetting.Token))
                            token = value;
                    }
                }
                catch (Exception ex)
                {
                    osu.Framework.Logging.Logger.Error(ex, $"Failed to read official osu! credentials from {Filename}");
                    return (string.Empty, string.Empty);
                }

                originalGameUsername = username;
                originalGameToken = token;
                return (username, token);
            }
        }

        /// <summary>
        /// Atomically updates only the official osu! token line while preserving all other
        /// recognised and unrecognised entries in <c>game.ini</c>.
        /// </summary>
        public bool TryWriteOriginalGameToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return false;

            lock (originalGameCredentialsLock)
            {
                var activeStorage = getStorage();

                if (activeStorage == null)
                    return false;

                try
                {
                    var lines = new List<string>();

                    if (activeStorage.Exists(Filename))
                    {
                        using var input = activeStorage.GetStream(Filename);

                        if (input != null)
                        {
                            using var reader = new StreamReader(input);
                            string? line;

                            while ((line = reader.ReadLine()) != null)
                                lines.Add(line);
                        }
                    }

                    bool replaced = false;

                    for (int i = 0; i < lines.Count; i++)
                    {
                        int equalsIndex = lines[i].IndexOf('=');

                        if (equalsIndex < 0 || !string.Equals(lines[i].AsSpan(0, equalsIndex).Trim().ToString(), nameof(OsuSetting.Token), StringComparison.Ordinal))
                            continue;

                        string prefix = lines[i].Substring(0, equalsIndex + 1);
                        string whitespace = new string(lines[i].Skip(equalsIndex + 1).TakeWhile(char.IsWhiteSpace).ToArray());
                        lines[i] = $"{prefix}{whitespace}{token.Replace("\n", "").Replace("\r", "")}";
                        replaced = true;
                        break;
                    }

                    if (!replaced)
                        lines.Add($"{nameof(OsuSetting.Token)} = {token.Replace("\n", "").Replace("\r", "")}");

                    using (var output = activeStorage.CreateFileSafely(Filename))
                    using (var writer = new StreamWriter(output))
                    {
                        foreach (string line in lines)
                            writer.WriteLine(line);
                    }

                    originalGameToken = token;
                    return true;
                }
                catch (Exception ex)
                {
                    osu.Framework.Logging.Logger.Error(ex, $"Failed to update official osu! token in {Filename}");
                    return false;
                }
            }
        }

        private bool saveToFile(string filename, bool saveCustom)
        {
            if (string.IsNullOrEmpty(filename)) return false;

            var activeStorage = getStorage();
            if (activeStorage == null) return false;

            var unrecognizedSettings = new System.Collections.Generic.Dictionary<string, string>();

            try
            {
                if (filename == Filename)
                    originalGameVersionLine = null;

                if (activeStorage.Exists(filename))
                {
                    using (var stream = activeStorage.GetStream(filename))
                    using (var reader = new StreamReader(stream))
                    {
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            int equalsIndex = line.IndexOf('=');
                            if (equalsIndex < 0) continue;

                            string key = line.AsSpan(0, equalsIndex).Trim().ToString();
                            string val = line.AsSpan(equalsIndex + 1).Trim().ToString();

                            if (Enum.TryParse(key, out OsuSetting lookup))
                            {
                                if (filename == Filename)
                                {
                                    if (lookup == OsuSetting.Username && !string.IsNullOrEmpty(val))
                                        originalGameUsername = val;
                                    else if (lookup == OsuSetting.Token && !string.IsNullOrEmpty(val))
                                        originalGameToken = val;
                                    else if (lookup == OsuSetting.Version)
                                        originalGameVersionLine = line;
                                }
                            }
                            else
                            {
                                unrecognizedSettings[key] = val;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                osu.Framework.Logging.Logger.Error(ex, $"Failed to read {filename} before saving");
            }

            try
            {
                using (var stream = activeStorage.CreateFileSafely(filename))
                using (var w = new StreamWriter(stream))
                {
                    foreach (var p in ConfigStore)
                    {
                        if (filename == Filename)
                        {
                            if (p.Key == OsuSetting.Username)
                            {
                                w.WriteLine(@"{0} = {1}", p.Key, originalGameUsername.Replace("\n", "").Replace("\r", ""));
                                continue;
                            }
                            if (p.Key == OsuSetting.Token)
                            {
                                w.WriteLine(@"{0} = {1}", p.Key, originalGameToken.Replace("\n", "").Replace("\r", ""));
                                continue;
                            }
                            if (p.Key == OsuSetting.Version)
                            {
                                if (originalGameVersionLine != null)
                                    w.WriteLine(originalGameVersionLine);

                                continue;
                            }
                        }

                        if (isCustomSetting(p.Key) == saveCustom)
                        {
                            w.WriteLine(@"{0} = {1}", p.Key, p.Value.ToString(System.Globalization.CultureInfo.InvariantCulture).AsNonNull().Replace("\n", "").Replace("\r", ""));
                        }
                    }

                    foreach (var kvp in unrecognizedSettings)
                    {
                        w.WriteLine(@"{0} = {1}", kvp.Key, kvp.Value.Replace("\n", "").Replace("\r", ""));
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private void migrateReleaseStreamSetting(Storage storage)
        {
            if (!storage.Exists(Filename))
                return;

            bool gameContainsReleaseStream = false;
            bool customContainsReleaseStream = false;
            string? legacyValue = null;

            static string? findReleaseStream(Storage storage, string filename)
            {
                if (!storage.Exists(filename))
                    return null;

                using var stream = storage.GetStream(filename);
                if (stream == null)
                    return null;

                using var reader = new StreamReader(stream);
                string? line;

                while ((line = reader.ReadLine()) != null)
                {
                    int equalsIndex = line.IndexOf('=');
                    if (equalsIndex < 0 || !string.Equals(line.AsSpan(0, equalsIndex).Trim().ToString(), nameof(OsuSetting.ReleaseStream), StringComparison.Ordinal))
                        continue;

                    return line.AsSpan(equalsIndex + 1).Trim().ToString();
                }

                return null;
            }

            legacyValue = findReleaseStream(storage, Filename);
            gameContainsReleaseStream = legacyValue != null;
            customContainsReleaseStream = findReleaseStream(storage, CustomFilename) != null;

            if (!gameContainsReleaseStream)
                return;

            if (!customContainsReleaseStream && Enum.TryParse(legacyValue, true, out ReleaseStream releaseStream))
                SetValue(OsuSetting.ReleaseStream, releaseStream);

            // Rewriting both files moves the setting into mosu.ini and removes the stale game.ini entry.
            Save();
        }

        private void migrateOldConfig(Storage storage)
        {
            if (storage.Exists(CustomFilename))
                return;

            if (!storage.Exists(Filename))
                return;

            try
            {
                System.Collections.Generic.List<string> customLines = new System.Collections.Generic.List<string>();

                using (var stream = storage.GetStream(Filename))
                {
                    if (stream != null)
                    {
                        using (var reader = new StreamReader(stream))
                        {
                            string? line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                int equalsIndex = line.IndexOf('=');
                                if (equalsIndex < 0) continue;

                                string key = line.AsSpan(0, equalsIndex).Trim().ToString();
                                if (Enum.TryParse(key, out OsuSetting lookup) &&
                                    lookup != OsuSetting.Version &&
                                    isCustomSetting(lookup))
                                {
                                    customLines.Add(line);
                                }
                            }
                        }
                    }
                }

                if (customLines.Count > 0)
                {
                    using (var stream = storage.CreateFileSafely(CustomFilename))
                    {
                        using (var writer = new StreamWriter(stream))
                        {
                            foreach (var line in customLines)
                            {
                                writer.WriteLine(line);
                            }
                        }
                    }

                    // Reload settings now that custom config has been populated in mosu.ini
                    Load();
                }
            }
            catch (Exception ex)
            {
                osu.Framework.Logging.Logger.Error(ex, $"Failed to migrate custom settings from {Filename} to {CustomFilename}");
            }
        }

        protected override void InitialiseDefaults()
        {
            // UI/selection defaults
            SetDefault(OsuSetting.Ruleset, string.Empty);
            SetDefault(OsuSetting.Skin, SkinInfo.ARGON_SKIN.ToString());
            SetDefault(OsuSetting.ForkSeparateSkinsPerRuleset, false);
            SetDefault(OsuSetting.ForkUseSkinCursorOutsideGameplay, false);
            SetDefault(OsuSetting.ForkOsuSkin, string.Empty);
            SetDefault(OsuSetting.ForkTaikoSkin, string.Empty);
            SetDefault(OsuSetting.ForkCatchSkin, string.Empty);
            SetDefault(OsuSetting.ForkManiaSkin, string.Empty);
            SetDefault(OsuSetting.ForkDodgeSkin, string.Empty);

            SetDefault(OsuSetting.BeatmapDetailTab, BeatmapDetailTab.Local);
            SetDefault(OsuSetting.BeatmapLeaderboardSortMode, LeaderboardSortMode.Score);
            SetDefault(OsuSetting.BeatmapDetailModsFilter, false);

            SetDefault(OsuSetting.ShowConvertedBeatmaps, true);
            SetDefault(OsuSetting.DisplayStarsMinimum, 0.0, 0, 10, 0.1);
            SetDefault(OsuSetting.DisplayStarsMaximum, 10.1, 0, 10.1, 0.1);

            SetDefault(OsuSetting.SongSelectGroupMode, GroupMode.None);
            SetDefault(OsuSetting.SongSelectSortingMode, SortMode.Title);

            SetDefault(OsuSetting.RandomSelectAlgorithm, RandomSelectAlgorithm.RandomPermutation);
            SetDefault(OsuSetting.ModSelectHotkeyStyle, ModSelectHotkeyStyle.Sequential);
            SetDefault(OsuSetting.ModSelectTextSearchStartsActive, true);

            SetDefault(OsuSetting.ChatDisplayHeight, ChatOverlay.DEFAULT_HEIGHT, 0.2f, 1f, 0.01f);

            SetDefault(OsuSetting.BeatmapListingCardSize, BeatmapCardSize.Normal);
            SetDefault(OsuSetting.BeatmapListingFeaturedArtistFilter, true);

            SetDefault(OsuSetting.ProfileCoverExpanded, true);

            SetDefault(OsuSetting.ToolbarClockDisplayMode, ToolbarClockDisplayMode.Full);

            SetDefault(OsuSetting.SongSelectBackgroundBlur, false);

            // Online settings
            SetDefault(OsuSetting.Username, string.Empty);
            SetDefault(OsuSetting.Token, string.Empty);

            SetDefault(OsuSetting.AutomaticallyDownloadMissingBeatmaps, true);

            SetDefault(OsuSetting.SavePassword, true).ValueChanged += enabled =>
            {
                if (enabled.NewValue)
                    SetValue(OsuSetting.SaveUsername, true);
                else
                    GetBindable<string>(OsuSetting.Token).SetDefault();
            };

            SetDefault(OsuSetting.SaveUsername, true).ValueChanged += enabled =>
            {
                if (!enabled.NewValue)
                {
                    GetBindable<string>(OsuSetting.Username).SetDefault();
                    SetValue(OsuSetting.SavePassword, false);
                }
            };

            SetDefault(OsuSetting.CustomApiUrl, string.Empty);

            SetDefault(OsuSetting.ExternalLinkWarning, true);
            SetDefault(OsuSetting.PreferNoVideo, false);
            SetDefault(OsuSetting.BeatmapDownloadMirror, DownloadMirror.Auto);
            SetDefault(OsuSetting.ForkUseOfficialBeatmapService, false);
            SetDefault(OsuSetting.ForkOfficialOsuFailureNotificationKey, string.Empty);

            SetDefault(OsuSetting.ShowOnlineExplicitContent, false);

            SetDefault(OsuSetting.NotifyOnUsernameMentioned, true);
            SetDefault(OsuSetting.NotifyOnPrivateMessage, true);
            SetDefault(OsuSetting.NotifyOnFriendPresenceChange, true);

            // Audio
            SetDefault(OsuSetting.VolumeInactive, 0.25, 0, 1, 0.01);
            SetDefault(OsuSetting.ForkReduceVolumeOutsideGameplay, false);
            SetDefault(OsuSetting.ForkExclusiveAudio, false);
            SetDefault(OsuSetting.ForkExclusiveAudioGameplayOnly, false);

            SetDefault(OsuSetting.MenuVoice, true);
            SetDefault(OsuSetting.MenuMusic, true);
            SetDefault(OsuSetting.MenuTips, true);

            SetDefault(OsuSetting.AudioOffset, 0, -500.0, 500.0, 1);

            SetDefault(OsuSetting.AutomaticallyAdjustBeatmapOffset, false);

            // Input
            SetDefault(OsuSetting.MenuCursorSize, 1.0f, 0.5f, 2f, 0.01f);
            SetDefault(OsuSetting.GameplayCursorSize, 1.0f, 0.1f, 2f, 0.01f);
            SetDefault(OsuSetting.GameplayCursorDuringTouch, false);
            SetDefault(OsuSetting.AutoCursorSize, false);

            SetDefault(OsuSetting.MouseDisableButtons, false);
            SetDefault(OsuSetting.MouseDisableWheel, false);
            SetDefault(OsuSetting.ConfineMouseMode, OsuConfineMouseMode.DuringGameplay);

            SetDefault(OsuSetting.TouchDisableGameplayTaps, false);
            SetDefault(OsuSetting.ForkShowInput, false);
            SetDefault(OsuSetting.ForkShowAimAssistRadius, false);
            SetDefault(OsuSetting.ForkVirtualCursorInputDelay, false);
            SetDefault(OsuSetting.ForkAimAssistEnabled, false);
            SetDefault(OsuSetting.ForkAimAssistStrength, 0.62, 0, 1, 0.01);
            SetDefault(OsuSetting.ForkAimAssistFovRadius, 130f, 0f, 300f, 1f);
            SetDefault(OsuSetting.ForkAimAssistIntentThreshold, -0.05, -1, 1, 0.05);
            SetDefault(OsuSetting.ForkAimAssistDynamicFriction, 0.42, 0, 1, 0.01);
            SetDefault(OsuSetting.ForkAimAssistAntiJitterMs, 35.0, 0, 150, 1);
            SetDefault(OsuSetting.ForkAimAssistOvershootAllowance, 14f, 0f, 100f, 1f);
            SetDefault(OsuSetting.ForkAimAssistCenterBias, 0.58, 0, 1, 0.01);
            SetDefault(OsuSetting.ForkAimAssistShowTargets, false);
            SetDefault(OsuSetting.ForkAimAssistShowFlowDebug, false);
            SetDefault(OsuSetting.ForkCustomUsername, string.Empty);
            SetDefault(OsuSetting.ForkMorasoomaEndTag, false);
            SetDefault(OsuSetting.ForkCustomUIFont, "Default");
            SetDefault(OsuSetting.ForkRussianFontFix, true);
            SetDefault(OsuSetting.ForkRelaxEnabled, false);
            SetDefault(OsuSetting.ForkRelaxBlindTapEnabled, false);
            SetDefault(OsuSetting.ForkRelaxBaseOffset, 22.0, -60, 60, 1);
            SetDefault(OsuSetting.ForkRelaxTimingVariance, 10.0, 0, 40, 1);
            SetDefault(OsuSetting.ForkRelaxDynamicDrift, 0.0, 0, 40, 1);
            SetDefault(OsuSetting.ForkRelaxHoldTime, 42.0, 0, 100, 1);
            SetDefault(OsuSetting.ForkRelaxHoldVariance, 4.0, 0, 40, 1);
            SetDefault(OsuSetting.ForkRelaxSliderTailOffset, 0.0, -80, 80, 1);
            SetDefault(OsuSetting.ForkRelaxSyncRadius, 16f, 0f, 120f, 1f);
            SetDefault(OsuSetting.ForkRelaxMaxSyncDelay, 14.0, 0, 120, 1);
            SetDefault(OsuSetting.ForkRelaxBlindTapThreshold, 160f, 0f, 400f, 1f);
            SetDefault(OsuSetting.ForkRelaxAlternateThreshold, 170.0, 0, 300, 1);
            SetDefault(OsuSetting.ForkRelaxPrimaryFingerReset, 220.0, 0, 600, 1);
            SetDefault(OsuSetting.ForkRelaxMisaltProbability, 0.0, 0, 1, 0.01);
            SetDefault(OsuSetting.ForkRelaxStackVarianceMultiplier, 1.35, 0.1, 4, 0.05);
            SetDefault(OsuSetting.ForkRelaxStreamBlindMode, false);
            SetDefault(OsuSetting.ForkRelaxStableBpm, 200.0, 60.0, 600.0, 1.0);
            SetDefault(OsuSetting.ForkRelaxStableBpmMigrationComplete, false);
            SetDefault(OsuSetting.ForkRelaxPpSystem, ForkRelaxPpSystem.MosuRealistik);
            SetDefault(OsuSetting.ForkDisableRemoteLogging, false);
            SetDefault(OsuSetting.ForkDisableOnlineRecordSending, false);
            SetDefault(OsuSetting.ForkObservedHitObjectGraphEnabled, false);
            SetDefault(OsuSetting.ForkObservedHitObjectGraphDebugVisible, false);
            SetDefault(OsuSetting.ForkObservedHitObjectGraphShiftPixels, 0.5f, 0f, 8f, 0.1f);
            SetDefault(OsuSetting.ForkObservedHitObjectGraphShiftIntervalMs, 1000.0, 100.0, 5000.0, 50.0);
            SetDefault(OsuSetting.ForkCustomRecommendedDifficultyEnabled, false);
            SetDefault(OsuSetting.ForkCustomRecommendedDifficulty, 5.0, 0.0, 15.0, 0.1);
            SetDefault(OsuSetting.ForkUncappedFrameRate, false);
            SetDefault(OsuSetting.ForkLimitMenuFps2x, false);
            SetDefault(OsuSetting.ForkWindowsUltraPerformanceMode, false);
            SetDefault(OsuSetting.ForkSkinPerformanceMode, false);
            SetDefault(OsuSetting.ForkSkinPerformanceFreezeAnimations, true);
            SetDefault(OsuSetting.ForkSkinPerformanceSimplifyEffects, true);
            SetDefault(OsuSetting.ForkSkinPerformanceOptimiseTextures, true);
            SetDefault(OsuSetting.ForkSkinPerformanceSimplifyHud, true);
            SetDefault(OsuSetting.ForkSkinPerformanceSimplifyCounters, true);
            SetDefault(OsuSetting.ForkSkinPerformanceDisableKiaiFlashing, true);
            SetDefault(OsuSetting.ForkSkinPerformanceBlackBackground, true);
            SetDefault(OsuSetting.ForkArgonFollowRing, true);
            SetDefault(OsuSetting.ForkLargeTextureAtlas, false);
            SetDefault(OsuSetting.ForkAtlasRegionAllocator, false);
            SetDefault(OsuSetting.ForkGameplayRenderScale, 1f, 0.1f, 1f, 0.05f);
            SetDefault(OsuSetting.ForkPerformanceLogging, false);
            SetDefault(OsuSetting.ForkAllowTearing, true);
            SetDefault(OsuSetting.ForkUpdateThreadSpinWait, false);
            SetDefault(OsuSetting.ForkFrameLimiterRestoreMode, FrameSync.Limit2x);
            SetDefault(OsuSetting.ForkCustomApproachRateEnabled, false);
            SetDefault(OsuSetting.ForkCustomApproachRate, 10f, 0f, 11f, 0.1f);
            SetDefault(OsuSetting.ForkEnableModNumericInput, false);
            SetDefault(OsuSetting.ForkSongSelectOldCarouselPreviews, false);
            SetDefault(OsuSetting.ForkSongSelectSkinnedLegacyCarousel, false);
            SetDefault(OsuSetting.ForkSongSelectV1Carousel, false);
            SetDefault(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern);
            SetDefault(OsuSetting.ForkSongSelectCarouselBackgroundDim, 0.10, 0, 1, 0.01);
            SetDefault(OsuSetting.ForkSongSelectCarouselPerformanceMode, false);
            SetDefault(OsuSetting.ForkSongSelectCarouselPreviews, true);
            SetDefault(OsuSetting.ForkSongSelectCarouselLazyLoading, true);
            SetDefault(OsuSetting.ForkSongSelectCarouselPreviewResolution, 100, 25, 100);
            SetDefault(OsuSetting.ForkSongSelectCarouselBackgroundDim, 0.10, 0, 1, 0.01);
            SetDefault(OsuSetting.ForkSongSelectStoryboardBackground, false);
            SetDefault(OsuSetting.ForkAutoHideToolbar, false);
            SetDefault(OsuSetting.ForkReplayRenderDebugTraceMode, ReplayRenderDebugTraceMode.Disabled);
            SetDefault(OsuSetting.ForkReplayRenderQualityPreset, ReplayRenderQualityPreset.Balanced);
            SetDefault(OsuSetting.ForkReplayRenderResolution, "1920x1080");
            SetDefault(OsuSetting.ForkReplayRenderFps, 60);
            SetDefault(OsuSetting.ForkReplayRenderBitrateMbps, 12);
            SetDefault(OsuSetting.ForkReplayRenderEncoder, "Auto (Recommended)");
            SetDefault(OsuSetting.ForkReplayRenderShowResultsAfterPeriod, false);
            SetDefault(OsuSetting.ForkExportReplayOnlyClicks, false);
            SetDefault(OsuSetting.ForkVisualOD11, false);
            SetDefault(OsuSetting.ForkHitErrorMeterShowPositionalMisses, false);
            SetDefault(OsuSetting.ForkRussianFontFix, true);
            SetDefault(OsuSetting.ForkRelaxEnabled, false);
            SetDefault(OsuSetting.ForkRelaxBlindTapEnabled, false);
            SetDefault(OsuSetting.ForkRelaxBaseOffset, 22.0, -60, 60, 1);
            SetDefault(OsuSetting.ForkRelaxTimingVariance, 10.0, 0, 40, 1);
            SetDefault(OsuSetting.ForkRelaxDynamicDrift, 0.0, 0, 40, 1);
            SetDefault(OsuSetting.ForkRelaxHoldTime, 42.0, 0, 100, 1);
            SetDefault(OsuSetting.ForkRelaxHoldVariance, 4.0, 0, 40, 1);
            SetDefault(OsuSetting.ForkRelaxSliderTailOffset, 0.0, -80, 80, 1);
            SetDefault(OsuSetting.ForkRelaxSyncRadius, 16f, 0f, 120f, 1f);
            SetDefault(OsuSetting.ForkRelaxMaxSyncDelay, 14.0, 0, 120, 1);
            SetDefault(OsuSetting.ForkRelaxBlindTapThreshold, 160f, 0f, 400f, 1f);
            SetDefault(OsuSetting.ForkRelaxAlternateThreshold, 170.0, 0, 300, 1);
            SetDefault(OsuSetting.ForkRelaxPrimaryFingerReset, 220.0, 0, 600, 1);
            SetDefault(OsuSetting.ForkRelaxMisaltProbability, 0.0, 0, 1, 0.01);
            SetDefault(OsuSetting.ForkRelaxStackVarianceMultiplier, 1.35, 0.1, 4, 0.05);
            SetDefault(OsuSetting.ForkRelaxStreamBlindMode, false);
            SetDefault(OsuSetting.ForkRelaxStableBpm, 200.0, 60.0, 600.0, 1.0);
            SetDefault(OsuSetting.ForkRelaxStableBpmMigrationComplete, false);
            SetDefault(OsuSetting.ForkDisableRemoteLogging, false);
            SetDefault(OsuSetting.ForkDisableOnlineRecordSending, false);
            SetDefault(OsuSetting.ForkObservedHitObjectGraphEnabled, false);
            SetDefault(OsuSetting.ForkObservedHitObjectGraphDebugVisible, false);
            SetDefault(OsuSetting.ForkObservedHitObjectGraphShiftPixels, 0.5f, 0f, 8f, 0.1f);
            SetDefault(OsuSetting.ForkObservedHitObjectGraphShiftIntervalMs, 1000.0, 100.0, 5000.0, 50.0);
            SetDefault(OsuSetting.ForkCustomRecommendedDifficultyEnabled, false);
            SetDefault(OsuSetting.ForkCustomRecommendedDifficulty, 5.0, 0.0, 15.0, 0.1);
            SetDefault(OsuSetting.ForkUncappedFrameRate, false);
            SetDefault(OsuSetting.ForkWindowsUltraPerformanceMode, false);
            SetDefault(OsuSetting.ForkSkinPerformanceMode, false);
            SetDefault(OsuSetting.ForkSkinPerformanceFreezeAnimations, true);
            SetDefault(OsuSetting.ForkSkinPerformanceSimplifyEffects, true);
            SetDefault(OsuSetting.ForkSkinPerformanceOptimiseTextures, true);
            SetDefault(OsuSetting.ForkSkinPerformanceSimplifyHud, true);
            SetDefault(OsuSetting.ForkSkinPerformanceSimplifyCounters, true);
            SetDefault(OsuSetting.ForkSkinPerformanceDisableKiaiFlashing, true);
            SetDefault(OsuSetting.ForkSkinPerformanceBlackBackground, true);
            SetDefault(OsuSetting.ForkArgonFollowRing, true);
            SetDefault(OsuSetting.ForkLargeTextureAtlas, false);
            SetDefault(OsuSetting.ForkDeferredVertexUploadBatching, false);
            SetDefault(OsuSetting.ForkDeferredDirectVertexUpload, false);
            SetDefault(OsuSetting.ForkDeferredDirectUniformUpload, false);
            SetDefault(OsuSetting.ForkVeldridPipelineLookupCache, false);
            SetDefault(OsuSetting.ForkStaticChildLifetimeCache, false);
            SetDefault(OsuSetting.ForkAtlasRegionAllocator, false);
            SetDefault(OsuSetting.ForkGameplayRenderScale, 1f, 0.1f, 1f, 0.05f);
            SetDefault(OsuSetting.ForkPerformanceLogging, false);
            SetDefault(OsuSetting.ForkDebugHudMode, DebugHudMode.Disabled);
            SetDefault(OsuSetting.ForkShowMemoryInToolbar, false);
            SetDefault(OsuSetting.ForkDebugFreezeAlerts, false);
            SetDefault(OsuSetting.ForkAllowTearing, true);
            SetDefault(OsuSetting.ForkUpdateThreadSpinWait, false);
            SetDefault(OsuSetting.ForkFrameLimiterRestoreMode, FrameSync.Limit2x);
            SetDefault(OsuSetting.ForkCustomApproachRateEnabled, false);
            SetDefault(OsuSetting.ForkCustomApproachRate, 10f, 0f, 11f, 0.1f);
            SetDefault(OsuSetting.ForkEnableModNumericInput, false);
            SetDefault(OsuSetting.ForkSongSelectOldCarouselPreviews, false);
            SetDefault(OsuSetting.ForkSongSelectSkinnedLegacyCarousel, false);
            SetDefault(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern);
            SetDefault(OsuSetting.ForkSongSelectCarouselBackgroundDim, 0.10, 0, 1, 0.01);
            SetDefault(OsuSetting.ForkSongSelectStoryboardBackground, false);
            SetDefault(OsuSetting.ForkReplayRenderDebugTraceMode, ReplayRenderDebugTraceMode.Disabled);
            SetDefault(OsuSetting.ForkReplayRenderQualityPreset, ReplayRenderQualityPreset.Balanced);
            SetDefault(OsuSetting.ForkReplayRenderResolution, "1920x1080");
            SetDefault(OsuSetting.ForkReplayRenderFps, 60);
            SetDefault(OsuSetting.ForkReplayRenderBitrateMbps, 12);
            SetDefault(OsuSetting.ForkReplayRenderEncoder, "Auto (Recommended)");
            SetDefault(OsuSetting.ForkReplayRenderShowResultsAfterPeriod, false);
            SetDefault(OsuSetting.ForkExportReplayOnlyClicks, false);
            SetDefault(OsuSetting.ForkVisualOD11, false);
            SetDefault(OsuSetting.ForkHitErrorMeterShowPositionalMisses, false);
#if DEBUG
            SetDefault(OsuSetting.ForkGameplayIntegrityDebugScenario, GameplayIntegrityDebugScenario.None);
#endif
            SetDefault(OsuSetting.ForkUse8kPollingRate, false);
            SetDefault(OsuSetting.ForkThemeMode, ThemeMode.Default);
            SetDefault(OsuSetting.ForkOverlayTransparency, false);
            SetDefault(OsuSetting.ForkOverlayBlurStrength, 0.4, 0.0, 1.0, 0.01);
            SetDefault(OsuSetting.ForkOverlayDimAmount, 0.0, -1.0, 1.0, 0.01);
            SetDefault(OsuSetting.ForkMenuLogo, ForkMenuLogo.Random);
            SetDefault(OsuSetting.ForkMenuLogoGradient, ForkMenuLogoGradient.Random);
            SetDefault(OsuSetting.ForkMenuLogoTriangles, true);
            SetDefault(OsuSetting.ForkDisableInterfaceShear, false);
            SetDefault(OsuSetting.ForkDifficultyAdditionalInfo, true);
            SetDefault(OsuSetting.ForkShowModsInPresetList, true);
            SetDefault(OsuSetting.ForkEnhancedRankingRows, true);
            SetDefault(OsuSetting.ForkActiveProfileId, @"default");
            SetDefault(OsuSetting.ForkUseConnectionProxy, false);
            SetDefault(OsuSetting.ForkConnectionRoute, MosuConnectionRoute.Direct);
            SetDefault(OsuSetting.ForkDisableBeatmapStatusOverwrite, true);
            SetDefault(OsuSetting.ForkUseStableDirectoryDirectly, false);
            SetDefault(OsuSetting.ForkStableDirectoryPath, string.Empty);
            SetDefault(OsuSetting.ForkClassicModDefault, false);
            SetDefault(OsuSetting.ForkHideVisualSliderMisses, false);
            SetDefault(OsuSetting.ForkShowBeatmapsWithMissingAudio, false);



            SetDefault(OsuSetting.MenuParallaxScale, 1.0f, 0.0f, 2.0f, 0.1f);
            SetDefault(OsuSetting.SongSelectCollectionFilter, string.Empty);
            SetDefault(OsuSetting.MultiplayerShowFullFilter, false);
            SetDefault(OsuSetting.PMFriendsOnly, false);

            // Graphics
            SetDefault(OsuSetting.ShowFpsDisplay, false);

            SetDefault(OsuSetting.ShowStoryboard, true);
            SetDefault(OsuSetting.BeatmapSkins, true);
            SetDefault(OsuSetting.BeatmapColours, true);
            SetDefault(OsuSetting.BeatmapHitsounds, true);

            SetDefault(OsuSetting.CursorRotation, true);

            SetDefault(OsuSetting.MenuParallax, true);

            // See https://stackoverflow.com/a/63307411 for default sourcing.
            SetDefault(OsuSetting.Prefer24HourTime, !CultureInfoHelper.SystemCulture.DateTimeFormat.ShortTimePattern.Contains(@"tt"));

            // Gameplay
            SetDefault(OsuSetting.PositionalHitsoundsLevel, 0.2f, 0, 1, 0.01f);
            SetDefault(OsuSetting.DimLevel, 0.7, 0, 1, 0.01);
            SetDefault(OsuSetting.BlurLevel, 0, 0, 1, 0.01);
            SetDefault(OsuSetting.LightenDuringBreaks, true);

            SetDefault(OsuSetting.HitLighting, true);
            SetDefault(OsuSetting.StarFountains, true);

            SetDefault(OsuSetting.HUDVisibilityMode, HUDVisibilityMode.Always);
            SetDefault(OsuSetting.ShowHealthDisplayWhenCantFail, true);
            SetDefault(OsuSetting.FadePlayfieldWhenHealthLow, true);
            SetDefault(OsuSetting.KeyOverlay, false);
            SetDefault(OsuSetting.ReplaySettingsOverlay, true);
            SetDefault(OsuSetting.ReplayPlaybackControlsExpanded, true);
            SetDefault(OsuSetting.GameplayLeaderboard, true);
            SetDefault(OsuSetting.AlwaysPlayFirstComboBreak, true);

            SetDefault(OsuSetting.FloatingComments, false);

            SetDefault(OsuSetting.ScoreDisplayMode, ScoringMode.Standardised);

            SetDefault(OsuSetting.IncreaseFirstObjectVisibility, true);
            SetDefault(OsuSetting.GameplayDisableWinKey, true);

            // Update
            SetDefault(OsuSetting.ReleaseStream, ReleaseStream.Lazer);

            SetDefault(OsuSetting.Version, string.Empty);

            SetDefault(OsuSetting.ShowFirstRunSetup, true);
            SetDefault(OsuSetting.ShowMobileDisclaimer, RuntimeInfo.IsMobile);

            SetDefault(OsuSetting.ScreenshotFormat, ScreenshotFormat.Jpg);
            SetDefault(OsuSetting.ScreenshotCaptureMenuCursor, false);

            SetDefault(OsuSetting.Scaling, ScalingMode.Off);
            SetDefault(OsuSetting.SafeAreaConsiderations, true);
            SetDefault(OsuSetting.ScalingBackgroundDim, 0.9f, 0.5f, 1f, 0.01f);

            SetDefault(OsuSetting.ScalingSizeX, 0.8f, 0.2f, 1f, 0.01f);
            SetDefault(OsuSetting.ScalingSizeY, 0.8f, 0.2f, 1f, 0.01f);

            SetDefault(OsuSetting.ScalingPositionX, 0.5f, 0f, 1f, 0.01f);
            SetDefault(OsuSetting.ScalingPositionY, 0.5f, 0f, 1f, 0.01f);

            if (RuntimeInfo.IsMobile)
                SetDefault(OsuSetting.UIScale, 1f, 0.8f, 1.1f, 0.01f);
            else
                SetDefault(OsuSetting.UIScale, 1f, 0.8f, 1.6f, 0.01f);

            SetDefault(OsuSetting.UIHoldActivationDelay, 200.0, 0.0, 500.0, 50.0);

            SetDefault(OsuSetting.IntroSequence, IntroSequence.Triangles);

            SetDefault(OsuSetting.MenuBackgroundSource, BackgroundSource.Skin);
            SetDefault(OsuSetting.SeasonalBackgroundMode, SeasonalBackgroundMode.Sometimes);

            SetDefault(OsuSetting.DiscordRichPresence, DiscordRichPresenceMode.Full);

            SetDefault(OsuSetting.EditorDim, 0.25f, 0f, 1f, 0.25f);
            SetDefault(OsuSetting.EditorWaveformOpacity, 0.25f, 0f, 1f, 0.25f);
            SetDefault(OsuSetting.EditorShowHitMarkers, true);
            SetDefault(OsuSetting.EditorAutoSeekOnPlacement, true);
            SetDefault(OsuSetting.EditorLimitedDistanceSnap, false);
            SetDefault(OsuSetting.EditorShowSpeedChanges, false);
            SetDefault(OsuSetting.EditorScaleOrigin, EditorOrigin.GridCentre);
            SetDefault(OsuSetting.EditorRotationOrigin, EditorOrigin.GridCentre);
            SetDefault(OsuSetting.EditorAdjustExistingObjectsOnTimingChanges, true);

            SetDefault(OsuSetting.HideCountryFlags, false);

            SetDefault(OsuSetting.MultiplayerRoomFilter, RoomPermissionsFilter.All);
            SetDefault(OsuSetting.MultiplayerShowInProgressFilter, true);

            SetDefault(OsuSetting.LastProcessedMetadataId, -1);

            SetDefault(OsuSetting.ComboColourNormalisationAmount, 0.2f, 0f, 1f, 0.01f);
            SetDefault(OsuSetting.UserOnlineStatus, UserStatus.Online);

            SetDefault(OsuSetting.EditorTimelineShowTimingChanges, true);
            SetDefault(OsuSetting.EditorTimelineShowBreaks, true);
            SetDefault(OsuSetting.EditorTimelineShowTicks, true);

            SetDefault(OsuSetting.EditorContractSidebars, false);

            SetDefault(OsuSetting.AlwaysShowHoldForMenuButton, false);
            SetDefault(OsuSetting.AlwaysRequireHoldingForPause, false);
            SetDefault(OsuSetting.EditorShowStoryboard, true);

            SetDefault(OsuSetting.EditorSubmissionNotifyOnDiscussionReplies, true);
            SetDefault(OsuSetting.EditorSubmissionLoadInBrowserAfterSubmission, true);

            // GU specific settings
            SetDefault(OsuSetting.DisableAutomaticUpdates, false);

            SetDefault(OsuSetting.WasSupporter, false);

            // intentionally uses `DateTime?` and not `DateTimeOffset?` because the latter fails due to `DateTimeOffset` not implementing `IConvertible`
            SetDefault(OsuSetting.LastOnlineTagsPopulation, (DateTime?)null);

            SetDefault(OsuSetting.DashboardSortMode, UserSortCriteria.LastVisit);
            SetDefault(OsuSetting.DashboardDisplayStyle, OverlayPanelDisplayStyle.Card);
        }

        protected override bool CheckLookupContainsPrivateInformation(OsuSetting lookup)
        {
            switch (lookup)
            {
                case OsuSetting.Token:
                    return true;
            }

            return false;
        }

        private void enforceSingleServerRestrictions()
        {
            // Force hidden single-server settings back to safe defaults so local config edits cannot enable them.
            // SetValue(OsuSetting.CustomApiUrl, string.Empty); // Disabled to allow profile custom API URLs
            SetValue(OsuSetting.ForkCustomApproachRateEnabled, false);
            SetValue(OsuSetting.ForkCustomApproachRate, 10f);
            SetValue(OsuSetting.ForkShowInput, false);
            SetValue(OsuSetting.ForkShowAimAssistRadius, false);
            SetValue(OsuSetting.ForkVirtualCursorInputDelay, false);

            SetValue(OsuSetting.ForkAimAssistEnabled, false);
            SetValue(OsuSetting.ForkAimAssistStrength, 0.62);
            SetValue(OsuSetting.ForkAimAssistFovRadius, 130f);
            SetValue(OsuSetting.ForkAimAssistIntentThreshold, -0.05);
            SetValue(OsuSetting.ForkAimAssistDynamicFriction, 0.42);
            SetValue(OsuSetting.ForkAimAssistAntiJitterMs, 35.0);
            SetValue(OsuSetting.ForkAimAssistOvershootAllowance, 14f);
            SetValue(OsuSetting.ForkAimAssistCenterBias, 0.58);
            SetValue(OsuSetting.ForkAimAssistShowTargets, false);
            SetValue(OsuSetting.ForkAimAssistShowFlowDebug, false);

            SetValue(OsuSetting.ForkRelaxEnabled, false);
            SetValue(OsuSetting.ForkRelaxBlindTapEnabled, false);
            SetValue(OsuSetting.ForkRelaxBaseOffset, 22.0);
            SetValue(OsuSetting.ForkRelaxTimingVariance, 10.0);
            SetValue(OsuSetting.ForkRelaxDynamicDrift, 0.0);
            SetValue(OsuSetting.ForkRelaxHoldTime, 42.0);
            SetValue(OsuSetting.ForkRelaxHoldVariance, 4.0);
            SetValue(OsuSetting.ForkRelaxSliderTailOffset, 0.0);
            SetValue(OsuSetting.ForkRelaxSyncRadius, 16f);
            SetValue(OsuSetting.ForkRelaxMaxSyncDelay, 14.0);
            SetValue(OsuSetting.ForkRelaxBlindTapThreshold, 160f);
            SetValue(OsuSetting.ForkRelaxAlternateThreshold, 170.0);
            SetValue(OsuSetting.ForkRelaxPrimaryFingerReset, 220.0);
            SetValue(OsuSetting.ForkRelaxMisaltProbability, 0.0);
            SetValue(OsuSetting.ForkRelaxStackVarianceMultiplier, 1.35);
            SetValue(OsuSetting.ForkRelaxStreamBlindMode, false);
            SetValue(OsuSetting.ForkRelaxStableBpm, 200.0);
            SetValue(OsuSetting.ForkDisableRemoteLogging, false);
        }
        public override TrackedSettings CreateTrackedSettings()
        {
            return new TrackedSettings
            {
                new TrackedSetting<bool>(OsuSetting.ShowFpsDisplay, state => new SettingDescription(
                    rawValue: state,
                    name: GlobalActionKeyBindingStrings.ToggleFPSCounter,
                    value: state ? CommonStrings.Enabled.ToLower() : CommonStrings.Disabled.ToLower(),
                    shortcut: LookupKeyBindings(GlobalAction.ToggleFPSDisplay))
                ),
                new TrackedSetting<bool>(OsuSetting.MouseDisableButtons, disabledState => new SettingDescription(
                    rawValue: !disabledState,
                    name: GlobalActionKeyBindingStrings.ToggleGameplayMouseButtons,
                    value: disabledState ? CommonStrings.Disabled.ToLower() : CommonStrings.Enabled.ToLower(),
                    shortcut: LookupKeyBindings(GlobalAction.ToggleGameplayMouseButtons))
                ),
                new TrackedSetting<bool>(OsuSetting.GameplayLeaderboard, state => new SettingDescription(
                    rawValue: state,
                    name: GlobalActionKeyBindingStrings.ToggleInGameLeaderboard,
                    value: state ? CommonStrings.Enabled.ToLower() : CommonStrings.Disabled.ToLower(),
                    shortcut: LookupKeyBindings(GlobalAction.ToggleInGameLeaderboard))
                ),
                new TrackedSetting<HUDVisibilityMode>(OsuSetting.HUDVisibilityMode, visibilityMode => new SettingDescription(
                    rawValue: visibilityMode,
                    name: GameplaySettingsStrings.HUDVisibilityMode,
                    value: visibilityMode.GetLocalisableDescription(),
                    shortcut: new TranslatableString(@"_", @"{0}: {1} {2}: {3}",
                        GlobalActionKeyBindingStrings.ToggleInGameInterface,
                        LookupKeyBindings(GlobalAction.ToggleInGameInterface),
                        GlobalActionKeyBindingStrings.HoldForHUD,
                        LookupKeyBindings(GlobalAction.HoldForHUD)))
                ),
                new TrackedSetting<ScalingMode>(OsuSetting.Scaling, scalingMode => new SettingDescription(
                        rawValue: scalingMode,
                        name: GraphicsSettingsStrings.ScreenScaling,
                        value: scalingMode.GetLocalisableDescription()
                    )
                ),
                new TrackedSetting<string>(OsuSetting.Skin, skin =>
                {
                    string skinName = string.Empty;

                    if (Guid.TryParse(skin, out var id))
                        skinName = LookupSkinName(id);

                    return new SettingDescription(
                        rawValue: skinName,
                        name: SkinSettingsStrings.SkinSectionHeader,
                        value: skinName,
                        shortcut: new TranslatableString(@"_", @"{0}: {1}",
                            GlobalActionKeyBindingStrings.RandomSkin,
                            LookupKeyBindings(GlobalAction.RandomSkin))
                    );
                }),
                new TrackedSetting<float>(OsuSetting.UIScale, scale => new SettingDescription(
                        rawValue: scale,
                        name: GraphicsSettingsStrings.UIScaling,
                        value: $"{scale:N2}x"
                        // TODO: implement lookup for framework platform key bindings
                    )
                ),
            };
        }

        public Func<Guid, string> LookupSkinName { private get; set; } = _ => @"unknown";
        public Func<GlobalAction, LocalisableString> LookupKeyBindings { private get; set; } = _ => @"unknown";

        IBindable<float> IGameplaySettings.ComboColourNormalisationAmount => GetOriginalBindable<float>(OsuSetting.ComboColourNormalisationAmount);
        IBindable<float> IGameplaySettings.PositionalHitsoundsLevel => GetOriginalBindable<float>(OsuSetting.PositionalHitsoundsLevel);
    }

    // IMPORTANT: These are used in user configuration files.
    // The naming of these keys should not be changed once they are deployed in a release, unless migration logic is also added.
    public enum OsuSetting
    {
        Ruleset,
        Token,
        MenuCursorSize,
        GameplayCursorSize,
        AutoCursorSize,
        GameplayCursorDuringTouch,
        DimLevel,
        BlurLevel,
        EditorDim,
        LightenDuringBreaks,
        ShowStoryboard,
        KeyOverlay,
        GameplayLeaderboard,
        PositionalHitsoundsLevel,
        AlwaysPlayFirstComboBreak,
        FloatingComments,
        HUDVisibilityMode,

        ShowHealthDisplayWhenCantFail,
        FadePlayfieldWhenHealthLow,

        /// <summary>
        /// Disables mouse buttons clicks during gameplay.
        /// </summary>
        MouseDisableButtons,
        MouseDisableWheel,
        ConfineMouseMode,

        /// <summary>
        /// Globally applied audio offset.
        /// This is added to the audio track's current time. Higher values will cause gameplay to occur earlier, relative to the audio track.
        /// </summary>
        AudioOffset,

        VolumeInactive,
        MenuMusic,
        MenuVoice,
        MenuTips,
        CursorRotation,
        MenuParallax,
        MenuParallaxScale,
        Prefer24HourTime,
        BeatmapDetailTab,
        BeatmapLeaderboardSortMode,
        BeatmapDetailModsFilter,
        Username,
        ReleaseStream,
        SavePassword,
        SaveUsername,
        DisplayStarsMinimum,
        DisplayStarsMaximum,
        SongSelectGroupMode,
        SongSelectSortingMode,
        SongSelectCollectionFilter,
        RandomSelectAlgorithm,
        ModSelectHotkeyStyle,
        ShowFpsDisplay,
        ChatDisplayHeight,
        BeatmapListingCardSize,
        ToolbarClockDisplayMode,
        SongSelectBackgroundBlur,
        Version,
        ShowFirstRunSetup,
        ShowConvertedBeatmaps,
        Skin,
        ScreenshotFormat,
        ScreenshotCaptureMenuCursor,
        BeatmapSkins,
        BeatmapColours,
        BeatmapHitsounds,
        IncreaseFirstObjectVisibility,
        ScoreDisplayMode,
        ExternalLinkWarning,
        PreferNoVideo,
        Scaling,
        ScalingPositionX,
        ScalingPositionY,
        ScalingSizeX,
        ScalingSizeY,
        ScalingBackgroundDim,
        UIScale,
        IntroSequence,
        NotifyOnUsernameMentioned,
        NotifyOnPrivateMessage,
        NotifyOnFriendPresenceChange,
        UIHoldActivationDelay,
        HitLighting,
        StarFountains,
        MenuBackgroundSource,
        GameplayDisableWinKey,
        SeasonalBackgroundMode,
        EditorWaveformOpacity,
        EditorShowHitMarkers,
        EditorAutoSeekOnPlacement,
        DiscordRichPresence,

        ShowOnlineExplicitContent,
        LastProcessedMetadataId,
        SafeAreaConsiderations,
        ComboColourNormalisationAmount,
        ProfileCoverExpanded,
        EditorLimitedDistanceSnap,
        ReplaySettingsOverlay,
        ReplayPlaybackControlsExpanded,
        AutomaticallyDownloadMissingBeatmaps,
        EditorShowSpeedChanges,
        TouchDisableGameplayTaps,
        ForkReduceVolumeOutsideGameplay,
        ForkExclusiveAudio,
        ForkExclusiveAudioGameplayOnly,
        ForkShowInput,
        ForkShowAimAssistRadius,
        ForkVirtualCursorInputDelay,
        ForkAimAssistEnabled,
        ForkAimAssistStrength,
        ForkAimAssistFovRadius,
        ForkAimAssistIntentThreshold,
        ForkAimAssistDynamicFriction,
        ForkAimAssistAntiJitterMs,
        ForkAimAssistOvershootAllowance,
        ForkAimAssistCenterBias,
        ForkAimAssistShowTargets,
        ForkAimAssistShowFlowDebug,
        ForkCustomUsername,
        ForkMorasoomaEndTag,
        ForkRelaxEnabled,
        ForkRelaxBlindTapEnabled,
        ForkRelaxBaseOffset,
        ForkRelaxTimingVariance,
        ForkRelaxDynamicDrift,
        ForkRelaxHoldTime,
        ForkRelaxHoldVariance,
        ForkRelaxSliderTailOffset,
        ForkRelaxSyncRadius,
        ForkRelaxMaxSyncDelay,
        ForkRelaxBlindTapThreshold,
        ForkRelaxAlternateThreshold,
        ForkRelaxPrimaryFingerReset,
        ForkRelaxMisaltProbability,
        ForkRelaxStackVarianceMultiplier,
        ForkRelaxStreamBlindMode,
        ForkRelaxStableBpm,
        ForkRelaxStableBpmMigrationComplete,
        ForkRelaxPpSystem,
        ForkDisableRemoteLogging,
        ForkDisableOnlineRecordSending,
        ForkObservedHitObjectGraphEnabled,
        ForkObservedHitObjectGraphDebugVisible,
        ForkObservedHitObjectGraphShiftPixels,
        ForkObservedHitObjectGraphShiftIntervalMs,
        ForkCustomRecommendedDifficultyEnabled,
        ForkCustomRecommendedDifficulty,
        ForkUncappedFrameRate,
        ForkLimitMenuFps2x,
        ForkWindowsUltraPerformanceMode,
        ForkSkinPerformanceMode,
        ForkSkinPerformanceFreezeAnimations,
        ForkSkinPerformanceSimplifyEffects,
        ForkSkinPerformanceOptimiseTextures,
        ForkSkinPerformanceSimplifyHud,
        ForkSkinPerformanceSimplifyCounters,
        ForkSkinPerformanceDisableKiaiFlashing,
        ForkSkinPerformanceBlackBackground,
        ForkArgonFollowRing,
        ForkSeparateSkinsPerRuleset,
        ForkUseSkinCursorOutsideGameplay,
        ForkOsuSkin,
        ForkTaikoSkin,
        ForkCatchSkin,
        ForkManiaSkin,
        ForkDodgeSkin,
        ForkLargeTextureAtlas,
        ForkDeferredVertexUploadBatching,
        ForkDeferredDirectVertexUpload,
        ForkDeferredDirectUniformUpload,
        ForkVeldridPipelineLookupCache,
        ForkStaticChildLifetimeCache,
        ForkAtlasRegionAllocator,
        ForkGameplayRenderScale,
        ForkPerformanceLogging,
        ForkDebugHudMode,
        ForkShowMemoryInToolbar,
        ForkDebugFreezeAlerts,
        ForkAllowTearing,
        ForkUpdateThreadSpinWait,
        ForkFrameLimiterRestoreMode,
        ForkCustomApproachRateEnabled,
        ForkCustomApproachRate,
        ForkEnableModNumericInput,
        ForkSongSelectOldCarouselPreviews,
        ForkSongSelectSkinnedLegacyCarousel,
        ForkSongSelectV1Carousel,
        ForkSongSelectStyle,
        ForkSongSelectCarouselPerformanceMode,
        ForkSongSelectCarouselPreviews,
        ForkSongSelectCarouselLazyLoading,
        ForkSongSelectCarouselPreviewResolution,
        ForkSongSelectCarouselBackgroundDim,
        ForkSongSelectStoryboardBackground,
        ForkAutoHideToolbar,
        ForkReplayRenderDebugTraceMode,
        ForkReplayRenderQualityPreset,
        ForkReplayRenderResolution,
        ForkReplayRenderFps,
        ForkReplayRenderBitrateMbps,
        ForkReplayRenderEncoder,
        ForkReplayRenderShowResultsAfterPeriod,
        ModSelectTextSearchStartsActive,
        BeatmapDownloadMirror,
        ForkUseOfficialBeatmapService,
        ForkOfficialOsuFailureNotificationKey,
        ForkExportReplayOnlyClicks,
        ForkVisualOD11,
        ForkHitErrorMeterShowPositionalMisses,
#if DEBUG
        ForkGameplayIntegrityDebugScenario,
#endif
        ForkUse8kPollingRate,
        ForkThemeMode,
        ForkOverlayTransparency,
        ForkOverlayBlurStrength,
        ForkOverlayDimAmount,
        ForkMenuLogo,
        ForkMenuLogoGradient,
        ForkMenuLogoTriangles,
        ForkDisableInterfaceShear,
        ForkDifficultyAdditionalInfo,
        ForkShowModsInPresetList,
        ForkEnhancedRankingRows,
        ForkActiveProfileId,
        ForkUseConnectionProxy,
        ForkConnectionRoute,
        ForkDisableBeatmapStatusOverwrite,
        ForkUseStableDirectoryDirectly,
        ForkStableDirectoryPath,
        ForkCustomUIFont,
        ForkRussianFontFix,
        ForkClassicModDefault,
        ForkHideVisualSliderMisses,
        ForkShowBeatmapsWithMissingAudio,

        /// <summary>
        /// The status for the current user to broadcast to other players.
        /// </summary>
        UserOnlineStatus,

        MultiplayerRoomFilter,
        HideCountryFlags,
        EditorTimelineShowTimingChanges,
        EditorTimelineShowTicks,
        AlwaysShowHoldForMenuButton,
        EditorContractSidebars,
        EditorScaleOrigin,
        EditorRotationOrigin,
        EditorTimelineShowBreaks,
        EditorAdjustExistingObjectsOnTimingChanges,
        AlwaysRequireHoldingForPause,
        MultiplayerShowInProgressFilter,
        MultiplayerShowFullFilter,
        BeatmapListingFeaturedArtistFilter,
        ShowMobileDisclaimer,
        EditorShowStoryboard,
        EditorSubmissionNotifyOnDiscussionReplies,
        EditorSubmissionLoadInBrowserAfterSubmission,

        /// <summary>
        /// Cached state of whether local user is a supporter.
        /// Used to allow early checks (ie for startup samples) to be in the correct state, even if the API authentication process has not completed.
        /// </summary>
        WasSupporter,

        LastOnlineTagsPopulation,

        AutomaticallyAdjustBeatmapOffset,

        /// <summary>
        /// Custom API endpoint URL.
        /// </summary>
        CustomApiUrl,

        DashboardSortMode,
        DashboardDisplayStyle,

        /// <summary>
        /// Disables automatic updates for the GU version.
        /// </summary>
        DisableAutomaticUpdates,

        /// <summary>
        /// Blocks private messages, room invites, and duel requests from non-friends.
        /// </summary>
        PMFriendsOnly
    }
}
