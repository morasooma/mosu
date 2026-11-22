// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Rulesets.Difficulty;
using Realms;
using osu.Game.Graphics.Backgrounds.AI;

namespace osu.Game.Database
{
    public class ForkDataStore : IDisposable
    {
        private const int schema_version = 14; // 14: versioned background classification cache; original osu! Realm is unchanged.

        /// <summary>
        /// Calculator revision of the Mosu/Realistik relax PP system
        /// (fields <c>Relax*</c> in <see cref="ForkBeatmapData"/>).
        /// </summary>
        public const int RELAX_PERFORMANCE_CALCULATION_VERSION = 2026081302;

        /// <summary>
        /// Calculator revision of the vanilla lazer relax PP system
        /// (fields <c>RelaxVanilla*</c> in <see cref="ForkBeatmapData"/>).
        /// </summary>
        public const int RELAX_VANILLA_PERFORMANCE_CALCULATION_VERSION = 2026082201;

        /// <summary>
        /// Calculator revision of the MosuPp relax PP system (fields <c>RelaxMosuPp*</c> in <see cref="ForkBeatmapData"/>).
        /// Bump whenever a MosuPp rule changes (see MOSUPP_CHANGELOG.md).
        /// </summary>
        public const int RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION = 40;

        public const int RELAX_TAZIK_PP_PERFORMANCE_CALCULATION_VERSION = 44;
        public const int RELAX_PRERELEASE_PERFORMANCE_CALCULATION_VERSION = 46;

        // The prerelease uses the existing versioned JSON cache in fork.realm; no schema migration.
        private const string prerelease_cache_ruleset = "library-relax-pp";
        private const string prerelease_cache_mods = nameof(ForkRelaxPpSystem.MosuPpPrerelease);

        private readonly Storage storage;
        private readonly ConcurrentDictionary<Guid, double> ppCache = new ConcurrentDictionary<Guid, double>();
        private readonly ConcurrentDictionary<Guid, RelaxBeatmapData> relaxCache = new ConcurrentDictionary<Guid, RelaxBeatmapData>();
        private readonly ConcurrentDictionary<Guid, RelaxBeatmapData> relaxVanillaCache = new ConcurrentDictionary<Guid, RelaxBeatmapData>();
        private readonly ConcurrentDictionary<Guid, RelaxBeatmapData> relaxMosuPpCache = new ConcurrentDictionary<Guid, RelaxBeatmapData>();
        private readonly ConcurrentDictionary<Guid, int> ppVersions = new ConcurrentDictionary<Guid, int>();
        private readonly ConcurrentDictionary<Guid, int> relaxVersions = new ConcurrentDictionary<Guid, int>();
        private readonly ConcurrentDictionary<Guid, int> relaxVanillaVersions = new ConcurrentDictionary<Guid, int>();
        private readonly ConcurrentDictionary<Guid, int> relaxMosuPpVersions = new ConcurrentDictionary<Guid, int>();
        private readonly ConcurrentDictionary<Guid, RelaxBeatmapData> relaxTazikPpCache = new();
        private readonly ConcurrentDictionary<Guid, int> relaxTazikPpVersions = new();
        private readonly ConcurrentDictionary<Guid, RelaxBeatmapData> relaxPrereleaseCache = new();
        private readonly ConcurrentDictionary<Guid, int> relaxPrereleaseVersions = new();
        private readonly ConcurrentDictionary<Guid, DodgeDifficultyData> dodgeDifficultyCache = new ConcurrentDictionary<Guid, DodgeDifficultyData>();
        private readonly ConcurrentDictionary<Guid, long> mosuScores = new ConcurrentDictionary<Guid, long>();
        private readonly TagCoopReplayCache tagCoopReplays = new TagCoopReplayCache();
        private readonly ConcurrentDictionary<Guid, byte> tagCoopReplayIds = new ConcurrentDictionary<Guid, byte>();
        private readonly ConcurrentDictionary<(Guid BeatmapId, string Ruleset), AdditionalInfoData> additionalInfoCache = new ConcurrentDictionary<(Guid, string), AdditionalInfoData>();

        public static int GetRelaxCalculationVersion(ForkRelaxPpSystem system)
            => system switch
            {
                ForkRelaxPpSystem.LazerVanilla => RELAX_VANILLA_PERFORMANCE_CALCULATION_VERSION,
                ForkRelaxPpSystem.MosuPp => RELAX_MOSU_PP_PERFORMANCE_CALCULATION_VERSION,
                ForkRelaxPpSystem.TazikPp => RELAX_TAZIK_PP_PERFORMANCE_CALCULATION_VERSION,
                ForkRelaxPpSystem.MosuPpPrerelease => RELAX_PRERELEASE_PERFORMANCE_CALCULATION_VERSION,
                _ => RELAX_PERFORMANCE_CALCULATION_VERSION,
            };

        public static ForkDataStore? Instance { get; private set; }

        public ForkDataStore(Storage storage)
        {
            this.storage = storage;
            Instance = this;
            loadFromRealm();
        }

        private RealmConfiguration getConfiguration()
        {
            string tempPathLocation = Path.Combine(Path.GetTempPath(), @"lazer");

            if (!Directory.Exists(tempPathLocation))
                Directory.CreateDirectory(tempPathLocation);

            return new RealmConfiguration(storage.GetFullPath("fork.realm", true))
            {
                SchemaVersion = schema_version,
                Schema = new[] { typeof(ForkBeatmapData), typeof(ForkScoreData), typeof(ForkSeasonalBackgroundData), typeof(ForkBackgroundClassificationData) },
                FallbackPipePath = tempPathLocation,
                MigrationCallback = (migration, oldVersion) =>
                {
                    if (oldVersion < 14)
                    {
                        string backup = storage.GetFullPath("fork.before-ai-v14.realm", true);
                        if (!File.Exists(backup))
                            migration.OldRealm.WriteCopy(new RealmConfiguration(backup));
                    }
                },
            };
        }

        private void loadFromRealm()
        {
            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                foreach (var data in realm.All<ForkBeatmapData>())
                {
                    ppCache[data.BeatmapID] = data.MaxPerformancePoints;
                    relaxCache[data.BeatmapID] = new RelaxBeatmapData(data.RelaxStarRating, data.RelaxMaxPerformancePoints);
                    relaxVanillaCache[data.BeatmapID] = new RelaxBeatmapData(data.RelaxVanillaStarRating, data.RelaxVanillaMaxPerformancePoints);
                    relaxMosuPpCache[data.BeatmapID] = new RelaxBeatmapData(data.RelaxMosuPpStarRating, data.RelaxMosuPpMaxPerformancePoints);
                    relaxTazikPpCache[data.BeatmapID] = new RelaxBeatmapData(data.RelaxTazikPpStarRating, data.RelaxTazikPpMaxPerformancePoints);

                    if (data.DodgeDifficultyVersion > 0 && data.DodgeStarRating >= 0)
                    {
                        dodgeDifficultyCache[data.BeatmapID] = new DodgeDifficultyData(
                            data.DodgeStarRating,
                            data.DodgeDifficultyVersion,
                            data.DodgeBeatmapChecksum ?? string.Empty,
                            data.DodgeDifficultyAttributesJson ?? string.Empty);
                    }

                    if (!string.IsNullOrEmpty(data.AdditionalInfoJson))
                    {
                        try
                        {
                            var entries = JsonConvert.DeserializeObject<Dictionary<string, AdditionalInfoData>>(data.AdditionalInfoJson);
                            if (entries != null)
                            {
                                foreach (var entry in entries)
                                {
                                    // Schema 9 initially stored nomod entries under the bare ruleset name.
                                    string key = entry.Key.Contains('|') ? entry.Key : createAdditionalInfoKey(entry.Key, "nomod");
                                    additionalInfoCache[(data.BeatmapID, key)] = entry.Value;
                                    if (key == createAdditionalInfoKey(prerelease_cache_ruleset, prerelease_cache_mods)
                                        && entry.Value.Version == RELAX_PRERELEASE_PERFORMANCE_CALCULATION_VERSION
                                        && entry.Value.PerformancePoints is double pp && double.IsFinite(pp) && pp >= 0
                                        && double.IsFinite(entry.Value.Stars) && entry.Value.Stars >= 0)
                                    {
                                        relaxPrereleaseCache[data.BeatmapID] = new RelaxBeatmapData(entry.Value.Stars, pp);
                                        relaxPrereleaseVersions[data.BeatmapID] = entry.Value.Version;
                                    }
                                    else if (key == createAdditionalInfoKey("library-relax", prerelease_cache_mods)
                                             && entry.Value.Version == RELAX_PRERELEASE_PERFORMANCE_CALCULATION_VERSION
                                             && double.IsFinite(entry.Value.Stars) && entry.Value.Stars >= 0)
                                    {
                                        relaxPrereleaseCache.TryAdd(data.BeatmapID, new RelaxBeatmapData(entry.Value.Stars, 0));
                                    }
                                }
                            }
                        }
                        catch (JsonException)
                        {
                            // A damaged optional cache entry must not prevent loading the rest of fork.realm.
                        }
                    }

                    if (data.PerformancePointsCalculated)
                        ppVersions[data.BeatmapID] = data.PerformancePointsVersion;

                    if (data.RelaxPerformancePointsCalculated)
                        relaxVersions[data.BeatmapID] = data.RelaxPerformancePointsVersion;

                    if (data.RelaxVanillaPerformancePointsCalculated)
                        relaxVanillaVersions[data.BeatmapID] = data.RelaxVanillaPerformancePointsVersion;

                    if (data.RelaxMosuPpPerformancePointsCalculated)
                        relaxMosuPpVersions[data.BeatmapID] = data.RelaxMosuPpPerformancePointsVersion;

                    if (data.RelaxTazikPpPerformancePointsCalculated)
                        relaxTazikPpVersions[data.BeatmapID] = data.RelaxTazikPpPerformancePointsVersion;
                }

                foreach (var data in realm.All<ForkScoreData>())
                {
                    // Replay JSON can be several megabytes per score. It is only needed
                    // for playback/export, never for startup or normal score listing.
                    if (data.MosuTotalScore is long total) mosuScores[data.ScoreID] = total;
                }

                // Filter inside Realm so ordinary scores need no payload allocation or
                // disk lookup when ScoreInfo checks whether a replay extension exists.
                foreach (var data in realm.All<ForkScoreData>().Where(s => s.TagCoopReplayJson != ""))
                    tagCoopReplayIds[data.ScoreID] = 0;

                Logger.Log($"ForkDataStore: loaded {ppCache.Count} PP entries; {tagCoopReplayIds.Count} Tag Co-op replays load on demand");
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to load: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        // Called only by the AI worker. Never preload the artwork cache at client startup.
        public BackgroundClassification? GetBackgroundClassification(string hash, string modelVersion)
        {
            using var realm = Realm.GetInstance(getConfiguration());
            var data = realm.Find<ForkBackgroundClassificationData>($"{modelVersion}:{hash}");
            if (data == null)
                return null;

            var result = new BackgroundClassification(data.Drawings, data.Hentai, data.Neutral, data.Porn, data.Sexy);
            return result.IsValid ? result : null;
        }

        public void SetBackgroundClassification(string hash, string modelVersion, BackgroundClassification result)
        {
            if (!isSha256(hash) || !result.IsValid)
                throw new ArgumentException("Invalid background classification.");

            using var realm = Realm.GetInstance(getConfiguration());
            realm.Write(() => realm.Add(new ForkBackgroundClassificationData
            {
                Key = $"{modelVersion}:{hash}",
                Drawings = result.Drawings,
                Hentai = result.Hentai,
                Neutral = result.Neutral,
                Porn = result.Porn,
                Sexy = result.Sexy,
            }, update: true));
        }

        public double GetPP(Guid beatmapId) => ppCache.GetValueOrDefault(beatmapId, -1);

        public byte[]? GetSeasonalBackground(string hash)
        {
            if (!isSha256(hash))
                return null;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());
                return realm.Find<ForkSeasonalBackgroundData>(hash.ToLowerInvariant())?.Data.ToArray();
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to read seasonal background {hash}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
                return null;
            }
        }

        public void SetSeasonalBackground(string hash, byte[] data)
        {
            if (!isSha256(hash) || data.Length == 0)
                return;

            string normalisedHash = hash.ToLowerInvariant();

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());
                realm.Write(() => realm.Add(new ForkSeasonalBackgroundData
                {
                    Hash = normalisedHash,
                    Data = data,
                    LastAccessed = DateTimeOffset.UtcNow,
                }, update: true));
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write seasonal background {hash}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        private static bool isSha256(string hash)
            => hash.Length == 64 && hash.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

        public static string CreateModsKey(IEnumerable<Mod>? mods)
        {
            var apiMods = mods?.OrderBy(mod => mod.Acronym)
                               .Select(mod => new APIMod(mod))
                               .Select(mod => new APIMod
                               {
                                   Acronym = mod.Acronym,
                                   Settings = mod.Settings.OrderBy(setting => setting.Key, StringComparer.Ordinal)
                                                         .ToDictionary(setting => setting.Key, setting => setting.Value)
                               })
                               .ToArray() ?? Array.Empty<APIMod>();
            return apiMods.Length == 0 ? "nomod" : JsonConvert.SerializeObject(apiMods, Formatting.None);
        }

        /// <summary>
        /// Distinguishes persisted RX display text for each client PP formula. MRX uses the
        /// normal cache except when the prerelease is selected.
        /// </summary>
        public static string CreateAdditionalInfoModsKey(string ruleset, IEnumerable<Mod>? mods)
        {
            Mod[] selectedMods = mods?.ToArray() ?? Array.Empty<Mod>();
            string modsKey = CreateModsKey(selectedMods);
            ForkRelaxPpSystem system = RelaxPpSystemSelection.Current;
            if (ruleset != "osu" || !selectedMods.Any(mod => mod is ModRelax
                || (system == ForkRelaxPpSystem.MosuPpPrerelease && mod.Acronym == "MRX")))
                return modsKey;
            return $"{modsKey}|relax-pp:{system}:{GetRelaxCalculationVersion(system)}";
        }

        private static string createAdditionalInfoKey(string ruleset, string modsKey)
            => $"{ruleset}|{modsKey}";

        public bool TryGetAdditionalInfo(Guid beatmapId, string ruleset, string modsKey, string? checksum, int version, out AdditionalInfoData data)
        {
            if (additionalInfoCache.TryGetValue((beatmapId, createAdditionalInfoKey(ruleset, modsKey)), out data)
                && data.Version == version
                && !string.IsNullOrEmpty(checksum)
                && string.Equals(data.Checksum, checksum, StringComparison.OrdinalIgnoreCase)
                && double.IsFinite(data.Stars)
                && data.MaxCombo >= 0
                && !string.IsNullOrEmpty(data.Text))
                return true;

            data = default;
            return false;
        }

        public void SetAdditionalInfo(Guid beatmapId, string ruleset, string modsKey, AdditionalInfoData data)
        {
            if (beatmapId == Guid.Empty || string.IsNullOrEmpty(ruleset) || string.IsNullOrEmpty(modsKey) || string.IsNullOrEmpty(data.Checksum)
                || data.Version <= 0 || !double.IsFinite(data.Stars) || data.MaxCombo < 0 || string.IsNullOrEmpty(data.Text))
                return;

            string cacheKey = createAdditionalInfoKey(ruleset, modsKey);
            if (additionalInfoCache.TryGetValue((beatmapId, cacheKey), out var existingData) && existingData == data)
                return;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());
                realm.Write(() =>
                {
                    var existing = realm.Find<ForkBeatmapData>(beatmapId);
                    if (existing == null)
                    {
                        existing = new ForkBeatmapData { BeatmapID = beatmapId };
                        realm.Add(existing);
                    }

                    var entries = string.IsNullOrEmpty(existing.AdditionalInfoJson)
                        ? new Dictionary<string, AdditionalInfoData>()
                        : JsonConvert.DeserializeObject<Dictionary<string, AdditionalInfoData>>(existing.AdditionalInfoJson)
                          ?? new Dictionary<string, AdditionalInfoData>();
                    entries[cacheKey] = data;
                    existing.AdditionalInfoJson = JsonConvert.SerializeObject(entries);
                });
                additionalInfoCache[(beatmapId, cacheKey)] = data;
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write additional info for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        public readonly record struct AdditionalInfoData(string Checksum, int Version, double Stars, int MaxCombo, string Text)
        {
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public double? PerformancePoints { get; init; }
        }

        public double GetRelaxStarRating(Guid beatmapId) => getRelaxCache().GetValueOrDefault(beatmapId).StarRating;

        public double GetRelaxPP(Guid beatmapId) => getRelaxCache().GetValueOrDefault(beatmapId).MaxPerformancePoints;

        public bool HasPP(Guid beatmapId) => ppVersions.ContainsKey(beatmapId);

        public bool HasRelaxData(Guid beatmapId)
            => getRelaxVersions().TryGetValue(beatmapId, out int storedVersion)
               && storedVersion == GetRelaxCalculationVersion(RelaxPpSystemSelection.Current);

        private ConcurrentDictionary<Guid, RelaxBeatmapData> getRelaxCache()
            => getRelaxCache(RelaxPpSystemSelection.Current);

        private ConcurrentDictionary<Guid, RelaxBeatmapData> getRelaxCache(ForkRelaxPpSystem system)
            => system switch
            {
                ForkRelaxPpSystem.LazerVanilla => relaxVanillaCache,
                ForkRelaxPpSystem.MosuPp => relaxMosuPpCache,
                ForkRelaxPpSystem.TazikPp => relaxTazikPpCache,
                ForkRelaxPpSystem.MosuPpPrerelease => relaxPrereleaseCache,
                _ => relaxCache,
            };

        private ConcurrentDictionary<Guid, int> getRelaxVersions()
            => getRelaxVersions(RelaxPpSystemSelection.Current);

        private ConcurrentDictionary<Guid, int> getRelaxVersions(ForkRelaxPpSystem system)
            => system switch
            {
                ForkRelaxPpSystem.LazerVanilla => relaxVanillaVersions,
                ForkRelaxPpSystem.MosuPp => relaxMosuPpVersions,
                ForkRelaxPpSystem.TazikPp => relaxTazikPpVersions,
                ForkRelaxPpSystem.MosuPpPrerelease => relaxPrereleaseVersions,
                _ => relaxVersions,
            };

        public DodgeDifficultyData GetDodgeDifficulty(Guid beatmapId)
            => dodgeDifficultyCache.GetValueOrDefault(beatmapId, new DodgeDifficultyData());

        public bool HasDodgeDifficulty(Guid beatmapId, int difficultyVersion)
            => dodgeDifficultyCache.TryGetValue(beatmapId, out DodgeDifficultyData data)
               && data.DifficultyVersion == difficultyVersion;

        public bool HasDodgeDifficultyAttributes(Guid beatmapId, int difficultyVersion, string? beatmapChecksum)
            => dodgeDifficultyCache.TryGetValue(beatmapId, out DodgeDifficultyData data)
               && data.DifficultyVersion == difficultyVersion
               && data.HasFullAttributes
               && !string.IsNullOrEmpty(beatmapChecksum)
               && string.Equals(data.BeatmapChecksum, beatmapChecksum, StringComparison.OrdinalIgnoreCase);

        public long? GetMosuTotalScore(Guid scoreId) => mosuScores.TryGetValue(scoreId, out long total) ? total : null;

        public void SetMosuTotalScore(Guid scoreId, long total)
        {
            if (scoreId == Guid.Empty || total < 0) return;
            using var realm = Realm.GetInstance(getConfiguration());
            realm.Write(() =>
            {
                var entry = realm.Find<ForkScoreData>(scoreId) ?? realm.Add(new ForkScoreData { ScoreID = scoreId });
                entry.MosuTotalScore = total;
            });
            mosuScores[scoreId] = total;
        }

        public string GetTagCoopReplay(Guid scoreId)
        {
            if (scoreId == Guid.Empty || !tagCoopReplayIds.ContainsKey(scoreId))
                return string.Empty;

            try
            {
                return tagCoopReplays.GetOrLoad(scoreId, () =>
                {
                    using var realm = Realm.GetInstance(getConfiguration());
                    return realm.Find<ForkScoreData>(scoreId)?.TagCoopReplayJson ?? string.Empty;
                });
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to read Tag Co-op replay for {scoreId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
                return string.Empty;
            }
        }

        internal long CachedTagCoopReplayBytes => tagCoopReplays.CachedBytes;

        public void SetTagCoopReplay(Guid scoreId, string json)
        {
            if (scoreId == Guid.Empty)
                return;

            if (string.IsNullOrEmpty(json))
                tagCoopReplayIds.TryRemove(scoreId, out _);
            else
                tagCoopReplayIds[scoreId] = 0;
            tagCoopReplays.Set(scoreId, json);

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    var existing = realm.Find<ForkScoreData>(scoreId);

                    if (string.IsNullOrEmpty(json))
                    {
                        if (existing != null)
                        {
                            if (existing.MosuTotalScore != null) existing.TagCoopReplayJson = string.Empty;
                            else realm.Remove(existing);
                        }

                        return;
                    }

                    if (existing != null)
                        existing.TagCoopReplayJson = json;
                    else
                        realm.Add(new ForkScoreData { ScoreID = scoreId, TagCoopReplayJson = json });
                });
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write Tag Co-op replay for {scoreId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        internal void ImportTagCoopReplays(IEnumerable<KeyValuePair<Guid, string>> replays)
        {
            KeyValuePair<Guid, string>[] validReplays = replays.Where(r => r.Key != Guid.Empty && !string.IsNullOrEmpty(r.Value)).ToArray();

            if (validReplays.Length == 0)
                return;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    foreach ((Guid scoreId, string json) in validReplays)
                    {
                        var existing = realm.Find<ForkScoreData>(scoreId);

                        if (existing != null)
                            existing.TagCoopReplayJson = json;
                        else
                            realm.Add(new ForkScoreData { ScoreID = scoreId, TagCoopReplayJson = json });
                    }
                });
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to import Tag Co-op replays: {e.Message}", LoggingTarget.Database, LogLevel.Error);
                throw;
            }

            // Never acquire the read-cache lock while holding a Realm write transaction:
            // a concurrent cache miss can be opening Realm under that same cache lock.
            foreach ((Guid scoreId, string json) in validReplays)
            {
                tagCoopReplayIds[scoreId] = 0;
                tagCoopReplays.Set(scoreId, json);
            }
        }

        internal void ImportPerformancePoints(IEnumerable<KeyValuePair<Guid, double>> performancePoints)
        {
            KeyValuePair<Guid, double>[] validEntries = performancePoints.Where(p => p.Key != Guid.Empty && p.Value >= 0).ToArray();

            if (validEntries.Length == 0)
                return;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    foreach ((Guid beatmapId, double pp) in validEntries)
                    {
                        ppCache[beatmapId] = pp;
                        ppVersions[beatmapId] = 0;

                        var existing = realm.Find<ForkBeatmapData>(beatmapId);

                        if (existing != null)
                        {
                            existing.MaxPerformancePoints = pp;
                            existing.PerformancePointsCalculated = true;
                            existing.PerformancePointsVersion = 0;
                        }
                        else
                            realm.Add(new ForkBeatmapData { BeatmapID = beatmapId, MaxPerformancePoints = pp, PerformancePointsCalculated = true });
                    }
                });
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to import beatmap PP values: {e.Message}", LoggingTarget.Database, LogLevel.Error);
                throw;
            }
        }

        public void SetPP(Guid beatmapId, double pp, int calculationVersion)
        {
            ppCache[beatmapId] = pp;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    var existing = realm.Find<ForkBeatmapData>(beatmapId);

                    if (existing != null)
                    {
                        existing.MaxPerformancePoints = pp;
                        existing.PerformancePointsCalculated = true;
                        existing.PerformancePointsVersion = calculationVersion;
                    }
                    else
                        realm.Add(new ForkBeatmapData
                        {
                            BeatmapID = beatmapId,
                            MaxPerformancePoints = pp,
                            PerformancePointsCalculated = true,
                            PerformancePointsVersion = calculationVersion,
                        });
                });

                ppVersions[beatmapId] = calculationVersion;
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write PP for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        public bool HasRelaxDifficulty(Guid beatmapId, string checksum, int version)
            => HasRelaxData(beatmapId) || TryGetAdditionalInfo(beatmapId, "library-relax", RelaxPpSystemSelection.Current.ToString(), checksum, version, out _);

        public void SetRelaxDifficulty(Guid beatmapId, string checksum, int version, double stars)
        {
            SetRelaxData(beatmapId, stars, GetRelaxPP(beatmapId), false);
            SetAdditionalInfo(beatmapId, "library-relax", RelaxPpSystemSelection.Current.ToString(),
                new AdditionalInfoData(checksum, version, stars, 0, "relax-difficulty"));
        }

        public void SetRelaxData(Guid beatmapId, double starRating, double pp, bool ppCalculated = true)
        {
            ForkRelaxPpSystem system = RelaxPpSystemSelection.Current;
            int calculationVersion = GetRelaxCalculationVersion(system);

            getRelaxCache(system)[beatmapId] = new RelaxBeatmapData(starRating, pp);

            if (system == ForkRelaxPpSystem.MosuPpPrerelease)
            {
                // Library Relax PP, like the older fields, is keyed by the local beatmap ID.
                if (ppCalculated)
                {
                    SetAdditionalInfo(beatmapId, prerelease_cache_ruleset, prerelease_cache_mods,
                        new AdditionalInfoData(beatmapId.ToString("N"), calculationVersion, starRating, 0, "relax-performance") { PerformancePoints = pp });
                    relaxPrereleaseVersions[beatmapId] = calculationVersion;
                }
                return;
            }

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    var existing = realm.Find<ForkBeatmapData>(beatmapId);

                    if (existing == null)
                    {
                        existing = new ForkBeatmapData { BeatmapID = beatmapId };
                        realm.Add(existing);
                    }

                    if (system == ForkRelaxPpSystem.TazikPp)
                    {
                        existing.RelaxTazikPpStarRating = starRating;
                        existing.RelaxTazikPpMaxPerformancePoints = pp;
                        if (ppCalculated)
                        {
                            existing.RelaxTazikPpPerformancePointsCalculated = true;
                            existing.RelaxTazikPpPerformancePointsVersion = calculationVersion;
                        }
                    }
                    else if (system == ForkRelaxPpSystem.MosuPp)
                    {
                        existing.RelaxMosuPpStarRating = starRating;
                        existing.RelaxMosuPpMaxPerformancePoints = pp;
                        if (ppCalculated)
                        {
                            existing.RelaxMosuPpPerformancePointsCalculated = true;
                            existing.RelaxMosuPpPerformancePointsVersion = calculationVersion;
                        }
                    }
                    else if (system == ForkRelaxPpSystem.LazerVanilla)
                    {
                        existing.RelaxVanillaStarRating = starRating;
                        existing.RelaxVanillaMaxPerformancePoints = pp;
                        if (ppCalculated)
                        {
                            existing.RelaxVanillaPerformancePointsCalculated = true;
                            existing.RelaxVanillaPerformancePointsVersion = calculationVersion;
                        }
                    }
                    else
                    {
                        existing.RelaxStarRating = starRating;
                        existing.RelaxMaxPerformancePoints = pp;
                        if (ppCalculated)
                        {
                            existing.RelaxPerformancePointsCalculated = true;
                            existing.RelaxPerformancePointsVersion = calculationVersion;
                        }
                    }
                });

                if (ppCalculated)
                    getRelaxVersions(system)[beatmapId] = calculationVersion;
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write Relax data for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        public void SetDodgeDifficulty(Guid beatmapId, double starRating, int difficultyVersion)
        {
            if (beatmapId == Guid.Empty || !double.IsFinite(starRating) || starRating < 0 || difficultyVersion <= 0)
                return;

            writeDodgeDifficulty(beatmapId, new DodgeDifficultyData(starRating, difficultyVersion, string.Empty, string.Empty));
        }

        public void SetDodgeDifficulty(Guid beatmapId, string? beatmapChecksum, DifficultyAttributes attributes, int difficultyVersion)
        {
            if (beatmapId == Guid.Empty
                || string.IsNullOrEmpty(beatmapChecksum)
                || attributes == null
                || !double.IsFinite(attributes.StarRating)
                || attributes.StarRating < 0
                || difficultyVersion <= 0)
                return;

            string attributesJson;

            try
            {
                attributesJson = JsonConvert.SerializeObject(attributes);
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to serialise Dodge difficulty for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
                return;
            }

            writeDodgeDifficulty(beatmapId, new DodgeDifficultyData(
                attributes.StarRating,
                difficultyVersion,
                beatmapChecksum,
                attributesJson));
        }

        private void writeDodgeDifficulty(Guid beatmapId, DodgeDifficultyData data)
        {
            if (dodgeDifficultyCache.TryGetValue(beatmapId, out DodgeDifficultyData existingData)
                && existingData == data)
                return;

            dodgeDifficultyCache[beatmapId] = data;

            try
            {
                using var realm = Realm.GetInstance(getConfiguration());

                realm.Write(() =>
                {
                    var existing = realm.Find<ForkBeatmapData>(beatmapId);

                    if (existing == null)
                    {
                        existing = new ForkBeatmapData { BeatmapID = beatmapId };
                        realm.Add(existing);
                    }

                    existing.DodgeStarRating = data.StarRating;
                    existing.DodgeDifficultyVersion = data.DifficultyVersion;
                    existing.DodgeBeatmapChecksum = data.BeatmapChecksum;
                    existing.DodgeDifficultyAttributesJson = data.AttributesJson;
                });
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write Dodge difficulty for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        public HashSet<Guid> GetBeatmapIdsWithoutPP(IEnumerable<Guid> allIds)
        {
            return allIds.Where(id => GetPP(id) <= 0).ToHashSet();
        }

        public readonly record struct RelaxBeatmapData(double StarRating, double MaxPerformancePoints)
        {
            public RelaxBeatmapData()
                : this(-1, -1)
            {
            }
        }

        public readonly record struct DodgeDifficultyData(
            double StarRating,
            int DifficultyVersion,
            string BeatmapChecksum,
            string AttributesJson)
        {
            public bool HasFullAttributes => !string.IsNullOrWhiteSpace(AttributesJson);

            public DodgeDifficultyData()
                : this(-1, 0, string.Empty, string.Empty)
            {
            }
        }

        public void Dispose()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
