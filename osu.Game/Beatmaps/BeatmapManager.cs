// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.IO.Archives;
using osu.Game.Localisation;
using osu.Game.Models;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Skinning;
using osu.Game.Storyboards;
using osu.Game.Utils;
using Realms;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Handles general operations related to global beatmap management.
    /// </summary>
    public class BeatmapManager : ModelManager<BeatmapSetInfo>, IModelImporter<BeatmapSetInfo>, IWorkingBeatmapCache
    {
        public ITrackStore BeatmapTrackStore { get; }

        private readonly BeatmapImporter beatmapImporter;

        private readonly WorkingBeatmapCache workingBeatmapCache;

        private readonly BeatmapExporter beatmapExporter;

        private readonly LegacyBeatmapExporter legacyBeatmapExporter;

        private readonly RawBeatmapExporter rawBeatmapExporter;

        private readonly Storage storage;

        private readonly Storage userFileStorage;

        private readonly IAPIProvider? api;

        public ProcessBeatmapDelegate? ProcessBeatmap { private get; set; }

        public override bool PauseImports
        {
            get => base.PauseImports;
            set
            {
                base.PauseImports = value;
                beatmapImporter.PauseImports = value;
            }
        }

        public BeatmapManager(Storage storage, RealmAccess realm, IAPIProvider? api, AudioManager audioManager, IResourceStore<byte[]> gameResources, GameHost? host = null,
                              WorkingBeatmap? defaultBeatmap = null, BeatmapDifficultyCache? difficultyCache = null, bool performOnlineLookups = false)
            : base(storage, realm)
        {
            if (performOnlineLookups)
            {
                if (api == null)
                    throw new ArgumentNullException(nameof(api), "API must be provided if online lookups are required.");

                if (difficultyCache == null)
                    throw new ArgumentNullException(nameof(difficultyCache), "Difficulty cache must be provided if online lookups are required.");
            }

            var realmFileStore = new RealmFileStore(realm, storage);
            var userResources = realmFileStore.Store;
            userFileStorage = realmFileStore.Storage;

            BeatmapTrackStore = audioManager.GetTrackStore(userResources);

            beatmapImporter = CreateBeatmapImporter(storage, realm);
            beatmapImporter.ProcessBeatmap = (beatmapSet, scope) => ProcessBeatmap?.Invoke(beatmapSet, scope);
            beatmapImporter.PostNotification = obj => PostNotification?.Invoke(obj);

            workingBeatmapCache = CreateWorkingBeatmapCache(audioManager, gameResources, userResources, defaultBeatmap, host);

            beatmapExporter = new BeatmapExporter(storage)
            {
                PostNotification = obj => PostNotification?.Invoke(obj)
            };

            legacyBeatmapExporter = new LegacyBeatmapExporter(storage)
            {
                PostNotification = obj => PostNotification?.Invoke(obj)
            };

            rawBeatmapExporter = new RawBeatmapExporter(storage)
            {
                PostNotification = obj => PostNotification?.Invoke(obj)
            };

            this.storage = storage;
            this.api = api;
        }

        protected virtual WorkingBeatmapCache CreateWorkingBeatmapCache(AudioManager audioManager, IResourceStore<byte[]> resources, IResourceStore<byte[]> storage, WorkingBeatmap? defaultBeatmap,
                                                                        GameHost? host)
        {
            return new WorkingBeatmapCache(BeatmapTrackStore, audioManager, resources, storage, defaultBeatmap, host, Realm);
        }

        protected virtual BeatmapImporter CreateBeatmapImporter(Storage storage, RealmAccess realm) => new BeatmapImporter(storage, realm);

        /// <summary>
        /// Create a new beatmap set, backed by a <see cref="BeatmapSetInfo"/> model,
        /// with a single difficulty which is backed by a <see cref="BeatmapInfo"/> model
        /// and represented by the returned usable <see cref="WorkingBeatmap"/>.
        /// </summary>
        public WorkingBeatmap CreateNew(RulesetInfo ruleset, APIUser user)
        {
            var metadata = new BeatmapMetadata
            {
                Author = new RealmUser
                {
                    OnlineID = user.OnlineID,
                    Username = user.Username,
                }
            };

            var beatmapSet = new BeatmapSetInfo
            {
                DateAdded = DateTimeOffset.UtcNow,
                Beatmaps =
                {
                    new BeatmapInfo(ruleset, new BeatmapDifficulty(), metadata)
                }
            };

            foreach (BeatmapInfo b in beatmapSet.Beatmaps)
                b.BeatmapSet = beatmapSet;

            var imported = beatmapImporter.ImportModel(beatmapSet);

            if (imported == null)
                throw new InvalidOperationException("Failed to import new beatmap");

            return imported.PerformRead(s => GetWorkingBeatmap(s.Beatmaps.First()));
        }

        /// <summary>
        /// Add a new difficulty to the provided <paramref name="targetBeatmapSet"/> based on the provided <paramref name="referenceWorkingBeatmap"/>.
        /// The new difficulty will be backed by a <see cref="BeatmapInfo"/> model
        /// and represented by the returned <see cref="WorkingBeatmap"/>.
        /// </summary>
        /// <remarks>
        /// Contrary to <see cref="CopyExistingDifficulty"/>, this method does not preserve hitobjects and beatmap-level settings from <paramref name="referenceWorkingBeatmap"/>.
        /// The created beatmap will have zero hitobjects and will have default settings (including difficulty settings), but will preserve metadata and existing timing points.
        /// </remarks>
        /// <param name="targetBeatmapSet">The <see cref="BeatmapSetInfo"/> to add the new difficulty to.</param>
        /// <param name="referenceWorkingBeatmap">The <see cref="WorkingBeatmap"/> to use as a baseline reference when creating the new difficulty.</param>
        /// <param name="rulesetInfo">The ruleset with which the new difficulty should be created.</param>
        public virtual WorkingBeatmap CreateNewDifficulty(BeatmapSetInfo targetBeatmapSet, WorkingBeatmap referenceWorkingBeatmap, RulesetInfo rulesetInfo)
        {
            Beatmap newBeatmap = createBlankDifficulty(
                targetBeatmapSet,
                referenceWorkingBeatmap,
                rulesetInfo,
                preserveAllControlPoints: false);

            return addDifficultyToSet(targetBeatmapSet, newBeatmap, referenceWorkingBeatmap.Skin);
        }

        /// <summary>
        /// Creates a blank difficulty in a local set dedicated to <paramref name="rulesetInfo"/>.
        /// If the source set has already been separated for this ruleset, the existing
        /// local set is reused.
        /// </summary>
        public virtual WorkingBeatmap CreateNewDifficultyInSeparateRulesetSet(
            BeatmapSetInfo sourceBeatmapSet,
            WorkingBeatmap referenceWorkingBeatmap,
            RulesetInfo rulesetInfo,
            APIUser mapper)
        {
            BeatmapSetInfo? existing = FindSeparateRulesetBeatmapSet(sourceBeatmapSet, rulesetInfo, mapper);

            if (existing != null)
            {
                Beatmap existingSetBeatmap = createBlankDifficulty(
                    existing,
                    referenceWorkingBeatmap,
                    rulesetInfo,
                    preserveAllControlPoints: false,
                    mapper);

                return addDifficultyToSet(existing, existingSetBeatmap, referenceWorkingBeatmap.Skin);
            }

            BeatmapSetInfo separatedSet = createSeparatedRulesetSet(sourceBeatmapSet, referenceWorkingBeatmap, rulesetInfo, mapper);

            try
            {
                Beatmap newBeatmap = createBlankDifficulty(
                    separatedSet,
                    referenceWorkingBeatmap,
                    rulesetInfo,
                    preserveAllControlPoints: true,
                    mapper);

                return addDifficultyToSet(
                    separatedSet,
                    newBeatmap,
                    referenceWorkingBeatmap.Skin,
                    referenceWorkingBeatmap.Storyboard);
            }
            catch
            {
                Delete(separatedSet);
                throw;
            }
        }

        /// <summary>
        /// Finds a previously separated local ruleset set for the supplied source.
        /// </summary>
        public virtual BeatmapSetInfo? FindSeparateRulesetBeatmapSet(
            BeatmapSetInfo sourceBeatmapSet,
            RulesetInfo rulesetInfo,
            APIUser? mapper = null)
        {
            BeatmapSetInfo? result = Realm.Run(realm => realm.All<BeatmapSetInfo>()
                                                               .Where(set => !set.DeletePending)
                                                               .AsEnumerable()
                                                               .FirstOrDefault(set =>
                                                                   set.ID != sourceBeatmapSet.ID
                                                                   && set.Beatmaps.Any(beatmap => beatmap.Ruleset.OnlineID == rulesetInfo.OnlineID)
                                                                   && set.Files.Any(file => RulesetBeatmapSetOrigin.MatchesSource(
                                                                       file.Filename,
                                                                       sourceBeatmapSet.ID,
                                                                       sourceBeatmapSet.OnlineID,
                                                                       rulesetInfo.OnlineID)))
                                                               ?.Detach());

            if (result == null || mapper == null)
                return result;

            RulesetBeatmapSetOrigin? origin = readOrigin(result);

            if (origin == null
                || origin.Version >= 2
                || result.Beatmaps.Count == 0
                || result.Beatmaps.Any(beatmap =>
                    beatmap.Metadata.Author.OnlineID != origin.OriginalAuthorOnlineID
                    || !string.Equals(beatmap.Metadata.Author.Username, origin.OriginalAuthorUsername, StringComparison.Ordinal)))
                return result;

            return repairSeparatedSetAuthorship(result, mapper);
        }

        /// <summary>
        /// Retrieves the source attribution stored for a locally separated ruleset beatmap set.
        /// </summary>
        public virtual RulesetBeatmapSetOrigin? GetRulesetBeatmapSetOrigin(BeatmapSetInfo separatedSet)
            => readOrigin(separatedSet);

        /// <summary>
        /// Add a copy of the provided <paramref name="referenceWorkingBeatmap"/> to the provided <paramref name="targetBeatmapSet"/>.
        /// The new difficulty will be backed by a <see cref="BeatmapInfo"/> model
        /// and represented by the returned <see cref="WorkingBeatmap"/>.
        /// </summary>
        /// <remarks>
        /// Contrary to <see cref="CreateNewDifficulty"/>, this method creates a nearly-exact copy of <paramref name="referenceWorkingBeatmap"/>
        /// (with the exception of a few key properties that cannot be copied under any circumstance, like difficulty name, beatmap hash, or online status).
        /// </remarks>
        /// <param name="targetBeatmapSet">The <see cref="BeatmapSetInfo"/> to add the copy to.</param>
        /// <param name="referenceWorkingBeatmap">The <see cref="WorkingBeatmap"/> to be copied.</param>
        public virtual WorkingBeatmap CopyExistingDifficulty(BeatmapSetInfo targetBeatmapSet, WorkingBeatmap referenceWorkingBeatmap)
            => copyExistingDifficulty(targetBeatmapSet, referenceWorkingBeatmap, preserveDifficultyName: false, preserveStoryboard: false);

        /// <summary>
        /// Moves every difficulty for <paramref name="rulesetInfo"/> out of a mixed
        /// source set and into its linked local ruleset set.
        /// </summary>
        public virtual WorkingBeatmap ExtractRulesetDifficultiesToSeparateSet(BeatmapSetInfo sourceBeatmapSet, RulesetInfo rulesetInfo, APIUser mapper)
        {
            BeatmapInfo[] sourceDifficulties = sourceBeatmapSet.Beatmaps
                                                               .Where(beatmap => beatmap.Ruleset.OnlineID == rulesetInfo.OnlineID)
                                                               .ToArray();

            if (sourceDifficulties.Length == 0)
                throw new InvalidOperationException($"The source set has no {rulesetInfo.Name} difficulties to extract.");

            WorkingBeatmap firstReference = GetWorkingBeatmap(sourceDifficulties[0]);
            BeatmapSetInfo? existing = FindSeparateRulesetBeatmapSet(sourceBeatmapSet, rulesetInfo, mapper);
            bool createdSet = existing == null;
            BeatmapSetInfo targetSet = existing ?? createSeparatedRulesetSet(sourceBeatmapSet, firstReference, rulesetInfo, mapper);
            var createdDifficulties = new List<BeatmapInfo>();

            try
            {
                WorkingBeatmap? firstCreated = null;

                foreach (BeatmapInfo sourceDifficulty in sourceDifficulties)
                {
                    WorkingBeatmap reference = GetWorkingBeatmap(sourceDifficulty);
                    WorkingBeatmap created = copyExistingDifficulty(
                        targetSet,
                        reference,
                        preserveDifficultyName: true,
                        preserveStoryboard: true,
                        mapper);

                    createdDifficulties.Add(created.BeatmapInfo);
                    firstCreated ??= created;
                }

                // Only remove the source difficulties after every copy has been
                // persisted successfully.
                foreach (BeatmapInfo sourceDifficulty in sourceDifficulties)
                    DeleteDifficultyImmediately(sourceDifficulty);

                workingBeatmapCache.Invalidate(sourceBeatmapSet);
                return firstCreated!;
            }
            catch
            {
                if (createdSet)
                    Delete(targetSet);
                else
                {
                    foreach (BeatmapInfo createdDifficulty in createdDifficulties)
                        DeleteDifficultyImmediately(createdDifficulty);
                }

                throw;
            }
        }

        private WorkingBeatmap copyExistingDifficulty(
            BeatmapSetInfo targetBeatmapSet,
            WorkingBeatmap referenceWorkingBeatmap,
            bool preserveDifficultyName,
            bool preserveStoryboard,
            APIUser? mapper = null)
        {
            var newBeatmap = referenceWorkingBeatmap.GetPlayableBeatmap(referenceWorkingBeatmap.BeatmapInfo.Ruleset).Clone();
            BeatmapInfo newBeatmapInfo;

            newBeatmap.BeatmapInfo = newBeatmapInfo = referenceWorkingBeatmap.BeatmapInfo.Clone();
            // assign a new ID to the clone.
            newBeatmapInfo.ID = Guid.NewGuid();
            // add "(copy)" suffix to difficulty name, and additionally ensure that it doesn't conflict with any other potentially pre-existing copies.
            newBeatmapInfo.DifficultyName = NamingUtils.GetNextBestName(
                targetBeatmapSet.Beatmaps.Select(b => b.DifficultyName),
                preserveDifficultyName ? newBeatmapInfo.DifficultyName : $"{newBeatmapInfo.DifficultyName} (copy)");
            // clear the hash, as that's what is used to match .osu files with their corresponding realm beatmaps.
            newBeatmapInfo.Hash = string.Empty;
            // clear online properties.
            newBeatmapInfo.ResetOnlineInfo();

            if (mapper != null)
                assignMapper(newBeatmapInfo, mapper);

            return addDifficultyToSet(
                targetBeatmapSet,
                newBeatmap,
                referenceWorkingBeatmap.Skin,
                preserveStoryboard ? referenceWorkingBeatmap.Storyboard : null);
        }

        private WorkingBeatmap addDifficultyToSet(BeatmapSetInfo targetBeatmapSet, IBeatmap newBeatmap, ISkin beatmapSkin, Storyboard? storyboard = null)
        {
            // populate circular beatmap set info <-> beatmap info references manually.
            // several places like `Save()` or `GetWorkingBeatmap()`
            // rely on them being freely traversable in both directions for correct operation.
            targetBeatmapSet.Beatmaps.Add(newBeatmap.BeatmapInfo);
            newBeatmap.BeatmapInfo.BeatmapSet = targetBeatmapSet;

            save(newBeatmap.BeatmapInfo, newBeatmap, beatmapSkin, storyboard ?? new Storyboard(), transferCollections: false);

            workingBeatmapCache.Invalidate(targetBeatmapSet);
            return GetWorkingBeatmap(newBeatmap.BeatmapInfo);
        }

        private static Beatmap createBlankDifficulty(
            BeatmapSetInfo targetBeatmapSet,
            WorkingBeatmap referenceWorkingBeatmap,
            RulesetInfo rulesetInfo,
            bool preserveAllControlPoints,
            APIUser? mapper = null)
        {
            var newBeatmapInfo = new BeatmapInfo(rulesetInfo, new BeatmapDifficulty(), referenceWorkingBeatmap.Metadata.DeepClone())
            {
                DifficultyName = NamingUtils.GetNextBestName(targetBeatmapSet.Beatmaps.Select(b => b.DifficultyName), "New Difficulty")
            };
            var newBeatmap = new Beatmap
            {
                BeatmapInfo = newBeatmapInfo,
                Bookmarks = referenceWorkingBeatmap.Beatmap.Bookmarks.ToArray(),
                AudioLeadIn = referenceWorkingBeatmap.Beatmap.AudioLeadIn,
                WidescreenStoryboard = referenceWorkingBeatmap.Beatmap.WidescreenStoryboard,
                EpilepsyWarning = referenceWorkingBeatmap.Beatmap.EpilepsyWarning,
                SamplesMatchPlaybackRate = referenceWorkingBeatmap.Beatmap.SamplesMatchPlaybackRate,
            };

            if (preserveAllControlPoints)
                newBeatmap.ControlPointInfo = referenceWorkingBeatmap.Beatmap.ControlPointInfo.DeepClone();
            else
            {
                foreach (var timingPoint in referenceWorkingBeatmap.Beatmap.ControlPointInfo.TimingPoints)
                    newBeatmap.ControlPointInfo.Add(timingPoint.Time, timingPoint.DeepClone());

                foreach (var effectPoint in referenceWorkingBeatmap.Beatmap.ControlPointInfo.EffectPoints)
                    newBeatmap.ControlPointInfo.Add(effectPoint.Time, effectPoint.DeepClone());
            }

            if (!rulesetInfo.Equals(referenceWorkingBeatmap.BeatmapInfo.Ruleset))
            {
                foreach (EffectControlPoint effectPoint in newBeatmap.ControlPointInfo.EffectPoints)
                    effectPoint.ScrollSpeedBindable.SetDefault();
            }

            if (mapper != null)
                assignMapper(newBeatmapInfo, mapper);

            return newBeatmap;
        }

        private static void assignMapper(BeatmapInfo beatmapInfo, APIUser mapper)
        {
            beatmapInfo.Metadata.Author.OnlineID = mapper.OnlineID;
            beatmapInfo.Metadata.Author.Username = mapper.Username;
        }

        private BeatmapSetInfo repairSeparatedSetAuthorship(BeatmapSetInfo separatedSet, APIUser mapper)
        {
            bool requiresRepair = separatedSet.Beatmaps.Any(beatmap =>
                beatmap.Metadata.Author.OnlineID != mapper.OnlineID
                || !string.Equals(beatmap.Metadata.Author.Username, mapper.Username, StringComparison.Ordinal));

            if (!requiresRepair)
                return separatedSet;

            BeatmapSetInfo repaired = Realm.Write(realm =>
            {
                BeatmapSetInfo managedSet = realm.Find<BeatmapSetInfo>(separatedSet.ID)
                                                ?? throw new InvalidOperationException("The separated beatmap set no longer exists.");

                foreach (BeatmapInfo beatmap in managedSet.Beatmaps)
                    assignMapper(beatmap, mapper);

                return managedSet.Detach();
            });

            workingBeatmapCache.Invalidate(repaired);
            return repaired;
        }

        private RulesetBeatmapSetOrigin? readOrigin(BeatmapSetInfo separatedSet)
        {
            string? originStoragePath = Realm.Run(realm =>
            {
                BeatmapSetInfo? managedSet = realm.Find<BeatmapSetInfo>(separatedSet.ID);
                RealmNamedFileUsage? originFile = managedSet?.Files.FirstOrDefault(file => RulesetBeatmapSetOrigin.IsOriginFilename(file.Filename));
                return originFile?.File.GetStoragePath();
            });

            if (originStoragePath == null)
                return null;

            try
            {
                using Stream stream = userFileStorage.GetStream(originStoragePath, FileAccess.Read, FileMode.Open);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                return JsonConvert.DeserializeObject<RulesetBeatmapSetOrigin>(reader.ReadToEnd());
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }

        private BeatmapSetInfo createSeparatedRulesetSet(
            BeatmapSetInfo sourceBeatmapSet,
            WorkingBeatmap referenceWorkingBeatmap,
            RulesetInfo rulesetInfo,
            APIUser mapper)
        {
            var separatedSet = new BeatmapSetInfo
            {
                OnlineID = -1,
                DateAdded = DateTimeOffset.UtcNow,
                Status = BeatmapOnlineStatus.None,
            };

            separatedSet = Realm.Write(realm =>
            {
                realm.Add(separatedSet);
                return separatedSet.Detach();
            });

            RealmNamedFileUsage[] sourceFiles = Realm.Run(realm =>
            {
                BeatmapSetInfo? managedSource = realm.Find<BeatmapSetInfo>(sourceBeatmapSet.ID);
                return managedSource?.Files.Detach().ToArray() ?? sourceBeatmapSet.Files.ToArray();
            });

            foreach (RealmNamedFileUsage file in sourceFiles)
            {
                if (!RulesetBeatmapSetOrigin.IsTransferableResourceFilename(file.Filename))
                    continue;

                using Stream? stream = referenceWorkingBeatmap.GetStream(file.File.GetStoragePath());

                if (stream != null)
                    AddFile(separatedSet, stream, file.Filename);
            }

            var origin = new RulesetBeatmapSetOrigin
            {
                TargetRulesetOnlineID = rulesetInfo.OnlineID,
                SourceBeatmapSetLocalID = sourceBeatmapSet.ID,
                SourceBeatmapSetOnlineID = sourceBeatmapSet.OnlineID,
                SourceBeatmapOnlineID = referenceWorkingBeatmap.BeatmapInfo.OnlineID,
                SourceRulesetOnlineID = referenceWorkingBeatmap.BeatmapInfo.Ruleset.OnlineID,
                SourceBeatmapSetUrl = api == null ? null : sourceBeatmapSet.GetOnlineURL(api),
                OriginalAuthorOnlineID = referenceWorkingBeatmap.Metadata.Author.OnlineID,
                OriginalAuthorUsername = referenceWorkingBeatmap.Metadata.Author.Username,
                MapperOnlineID = mapper.OnlineID,
                MapperUsername = mapper.Username,
            };
            byte[] originData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(origin, Formatting.Indented));

            using (var stream = new MemoryStream(originData, false))
            {
                AddFile(
                    separatedSet,
                    stream,
                    RulesetBeatmapSetOrigin.GetFilename(sourceBeatmapSet.ID, sourceBeatmapSet.OnlineID, rulesetInfo.OnlineID));
            }

            return separatedSet;
        }

        /// <summary>
        /// Hide a beatmap difficulty.
        /// Will fail if all difficulties are about to be hidden.
        /// </summary>
        /// <param name="beatmapInfo">The beatmap difficulty to hide.</param>
        public bool Hide(BeatmapInfo beatmapInfo)
        {
            return Realm.Run(r =>
            {
                using (var transaction = r.BeginWrite())
                {
                    if (!beatmapInfo.IsManaged)
                        beatmapInfo = r.Find<BeatmapInfo>(beatmapInfo.ID)!;

                    if (!CanHide(beatmapInfo))
                        return false;

                    beatmapInfo.Hidden = true;
                    transaction.Commit();
                    return true;
                }
            });
        }

        public bool CanHide(BeatmapInfo beatmapInfo) => Realm.Run(r =>
        {
            if (StablePathManager.IsStableBeatmap(beatmapInfo.ID))
                return false;

            if (!beatmapInfo.IsManaged)
            {
                beatmapInfo = r.Find<BeatmapInfo>(beatmapInfo.ID)!;

                if (beatmapInfo == null)
                    return false;
            }

            if (beatmapInfo?.BeatmapSet == null)
                return false;

            return beatmapInfo.BeatmapSet.Beatmaps.Count(b => !b.Hidden) > 1;
        });

        /// <summary>
        /// Restore a beatmap difficulty.
        /// </summary>
        /// <param name="beatmapInfo">The beatmap difficulty to restore.</param>
        public void Restore(BeatmapInfo beatmapInfo)
        {
            Realm.Run(r =>
            {
                using (var transaction = r.BeginWrite())
                {
                    if (!beatmapInfo.IsManaged)
                        beatmapInfo = r.Find<BeatmapInfo>(beatmapInfo.ID)!;

                    beatmapInfo.Hidden = false;
                    transaction.Commit();
                }
            });
        }

        public void RestoreAll()
        {
            Realm.Run(r =>
            {
                using (var transaction = r.BeginWrite())
                {
                    foreach (var beatmap in r.All<BeatmapInfo>().Where(b => b.Hidden))
                        beatmap.Hidden = false;

                    transaction.Commit();
                }
            });
        }

        /// <summary>
        /// Returns a list of all usable <see cref="BeatmapSetInfo"/>s.
        /// IMPORTANT: This should not be used outside of tests. Consider using <see cref="RealmDetachedBeatmapStore"/> instead.
        /// </summary>
        /// <returns>A list of available <see cref="BeatmapSetInfo"/>.</returns>
        public List<BeatmapSetInfo> GetAllUsableBeatmapSets()
        {
            return Realm.Run(r =>
            {
                r.Refresh();
                return r.All<BeatmapSetInfo>().Where(b => !b.DeletePending).AsEnumerable().Detach();
            });
        }

        /// <summary>
        /// Perform a lookup query on available <see cref="BeatmapSetInfo"/>s.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>The first result for the provided query, or null if no results were found.</returns>
        public Live<BeatmapSetInfo>? QueryBeatmapSet(Expression<Func<BeatmapSetInfo, bool>> query)
        {
            return Realm.Run(r => r.All<BeatmapSetInfo>().FirstOrDefault(query)?.ToLive(Realm));
        }

        /// <summary>
        /// Perform a lookup query on available <see cref="BeatmapInfo"/>s.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <returns>The first result for the provided query, or null if no results were found.</returns>
        public BeatmapInfo? QueryBeatmap(Expression<Func<BeatmapInfo, bool>> query) => Realm.Run(r =>
            r.All<BeatmapInfo>().Filter($@"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.DeletePending)} == false").FirstOrDefault(query)?.Detach());

        /// <summary>
        /// Perform a lookup query on available <see cref="BeatmapInfo"/>s.
        /// Use this overload instead of <see cref="QueryBeatmap(System.Linq.Expressions.Expression{System.Func{osu.Game.Beatmaps.BeatmapInfo,bool}})"/>
        /// when Realm is unable to transform an expression to the internal Realm query syntax.
        /// </summary>
        /// <param name="query">The query.</param>
        /// <param name="arguments">The arguments for the query.</param>
        /// <returns>The first result for the provided query, or null if no results were found.</returns>
        public BeatmapInfo? QueryBeatmap(string query, params QueryArgument[] arguments) => Realm.Run(r =>
            r.All<BeatmapInfo>()
             .Filter($@"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.DeletePending)} == false")
             .Filter(query, arguments)
             .FirstOrDefault()?.Detach());

        /// <summary>
        /// Perform a lookup query on available <see cref="BeatmapInfo"/>s for a specific online ID.
        /// </summary>
        /// <returns>A matching local beatmap info if existing and in a valid state.</returns>
        public BeatmapInfo? QueryOnlineBeatmapId(int id) => Realm.Run(r =>
            r.All<BeatmapInfo>()
             .ForOnlineId(id)
             // See https://github.com/ppy/osu/issues/36234 for why this isn't a SingleOrDefault().
             .FirstOrDefault()
             ?.Detach()
        );

        /// <summary>
        /// A default representation of a WorkingBeatmap to use when no beatmap is available.
        /// </summary>
        public IWorkingBeatmap DefaultBeatmap => workingBeatmapCache.DefaultBeatmap;

        /// <summary>
        /// Saves an existing <see cref="IBeatmap"/> file against a given <see cref="BeatmapInfo"/>.
        /// </summary>
        /// <remarks>
        /// This method will also update any user beatmap collection hash references to the new post-saved hash.
        /// </remarks>
        /// <param name="beatmapInfo">The <see cref="BeatmapInfo"/> to save the content against. The file referenced by <see cref="BeatmapInfo.Path"/> will be replaced.</param>
        /// <param name="beatmapContent">The <see cref="IBeatmap"/> content to write.</param>
        /// <param name="beatmapSkin">The beatmap <see cref="ISkin"/> content to write, null if to be omitted.</param>
        /// <param name="storyboard">The storyboard content to write, null if to be omitted.</param>
        public virtual void Save(BeatmapInfo beatmapInfo, IBeatmap beatmapContent, ISkin? beatmapSkin = null, Storyboard? storyboard = null) =>
            save(beatmapInfo, beatmapContent, beatmapSkin, storyboard, transferCollections: true);

        public void DeleteAllVideos()
        {
            Realm.Write(r =>
            {
                var items = r.All<BeatmapSetInfo>().Where(s => !s.DeletePending && !s.Protected);
                DeleteVideos(items.ToList());
            });
        }

        public void ResetAllOffsets()
        {
            Realm.Write(r =>
            {
                var items = r.All<BeatmapInfo>();

                foreach (var beatmap in items)
                {
                    if (beatmap.UserSettings.Offset != 0)
                        beatmap.UserSettings.Offset = 0;
                }

                PostNotification?.Invoke(new ProgressCompletionNotification { Text = MaintenanceSettingsStrings.AllOffsetsReset });
            });
        }

        public void Delete(Expression<Func<BeatmapSetInfo, bool>>? filter = null, bool silent = false)
        {
            Realm.Run(r =>
            {
                var items = r.All<BeatmapSetInfo>().Where(s => !s.DeletePending && !s.Protected);

                if (filter != null)
                    items = items.Where(filter);

                Delete(items.ToList(), silent);
            });
        }

        /// <summary>
        /// Delete a beatmap difficulty immediately.
        /// </summary>
        /// <remarks>
        /// There's no undoing this operation, as we don't have a soft-deletion flag on <see cref="BeatmapInfo"/>.
        /// This may be a future consideration if there's a user requirement for undeleting support.
        /// </remarks>
        public void DeleteDifficultyImmediately(BeatmapInfo beatmapInfo)
        {
            Realm.Write(r =>
            {
                if (!beatmapInfo.IsManaged)
                    beatmapInfo = r.Find<BeatmapInfo>(beatmapInfo.ID)!;

                Debug.Assert(beatmapInfo.BeatmapSet != null);
                Debug.Assert(beatmapInfo.File != null);

                var setInfo = beatmapInfo.BeatmapSet;

                if (beatmapInfo.Path != null)
                {
                    RealmNamedFileUsage? sidecar = setInfo.GetFile(CustomBeatmapFormat.GetSidecarFilename(beatmapInfo.Path));

                    if (sidecar != null)
                        DeleteFile(setInfo, sidecar);
                }

                DeleteFile(setInfo, beatmapInfo.File);
                setInfo.Beatmaps.Remove(beatmapInfo);
                r.Remove(beatmapInfo.Metadata);
                r.Remove(beatmapInfo);

                updateHashAndMarkDirty(setInfo);
                workingBeatmapCache.Invalidate(setInfo);
            });
        }

        /// <summary>
        /// Delete videos from a list of beatmaps.
        /// This will post notifications tracking progress.
        /// </summary>
        public void DeleteVideos(List<BeatmapSetInfo> items, bool silent = false)
        {
            if (items.Count == 0)
            {
                if (!silent)
                    PostNotification?.Invoke(new ProgressCompletionNotification { Text = MaintenanceSettingsStrings.NoVideosFoundToDelete });
                return;
            }

            var notification = new ProgressNotification
            {
                Progress = 0,
                Text = $"Preparing to delete all {HumanisedModelName} videos...",
                CompletionText = MaintenanceSettingsStrings.NoVideosFoundToDelete,
                State = ProgressNotificationState.Active,
            };

            if (!silent)
                PostNotification?.Invoke(notification);

            int i = 0;
            int deleted = 0;

            foreach (var b in items)
            {
                if (notification.State == ProgressNotificationState.Cancelled)
                    // user requested abort
                    return;

                var video = b.Files.FirstOrDefault(f => SupportedExtensions.VIDEO_EXTENSIONS.Any(ex => f.Filename.EndsWith(ex, StringComparison.OrdinalIgnoreCase)));

                if (video != null)
                {
                    DeleteFile(b, video);
                    deleted++;
                    notification.CompletionText = $"Deleted {deleted} {HumanisedModelName} video(s)!";
                }

                notification.Text = $"Deleting videos from {HumanisedModelName}s ({deleted} deleted)";

                notification.Progress = (float)++i / items.Count;
            }

            notification.State = ProgressNotificationState.Completed;
        }

        public void UndeleteAll()
        {
            Realm.Run(r => Undelete(r.All<BeatmapSetInfo>().Where(s => s.DeletePending).ToList()));
        }

        public Task<Live<BeatmapSetInfo>?> ImportAsUpdate(ProgressNotification notification, ImportTask importTask, BeatmapSetInfo original) =>
            beatmapImporter.ImportAsUpdate(notification, importTask, original);

        public Task<ExternalEditOperation<BeatmapSetInfo>> BeginExternalEditing(BeatmapSetInfo model) =>
            beatmapImporter.BeginExternalEditing(model);

        public Task Export(BeatmapSetInfo beatmapSet) => beatmapExporter.ExportAsync(beatmapSet.ToLive(Realm));

        public Task ExportLegacy(BeatmapSetInfo beatmapSet) => legacyBeatmapExporter.ExportAsync(beatmapSet.ToLive(Realm));

        public Task ExportLegacy(BeatmapInfo beatmap) => legacyBeatmapExporter.ExportAsync(beatmap.ToLive(Realm));

        public void ExportMultiple(IEnumerable<Guid> beatmapSetIds)
        {
            var idsList = beatmapSetIds.ToList();
            if (idsList.Count == 0)
                return;

            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = $"Preparing to export {idsList.Count} beatmap sets...",
                Progress = 0,
            };

            PostNotification?.Invoke(notification);

            Task.Run(() =>
            {
                int exportedCount = 0;
                int totalCount = idsList.Count;

                string folderName = $"exported_beatmaps_{DateTimeOffset.Now:yyyyMMdd_HHmmss}";
                var baseExportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");
                var exportStorage = baseExportStorage.GetStorageForDirectory(folderName);
                string folderPath = exportStorage.GetFullPath(string.Empty, true);

                foreach (var id in idsList)
                {
                    if (notification.State == ProgressNotificationState.Cancelled)
                        break;

                    try
                    {
                        Realm.Run(r =>
                        {
                            var beatmapSet = r.Find<BeatmapSetInfo>(id);
                            if (beatmapSet == null) return;

                            string itemFilename = beatmapSet.GetDisplayString().GetValidFilename();
                            if (itemFilename.Length > LegacyExporter<BeatmapSetInfo>.MAX_FILENAME_LENGTH - 4) // 4 for .osz
                                itemFilename = itemFilename.Remove(LegacyExporter<BeatmapSetInfo>.MAX_FILENAME_LENGTH - 4);

                            string filename = NamingUtils.GetNextBestFilename(Directory.GetFiles(folderPath, "*.osz"), $"{itemFilename}.osz");
                            string fullPath = Path.Combine(folderPath, filename);

                            using (var stream = File.Create(fullPath))
                            {
                                rawBeatmapExporter.ExportToStream(beatmapSet, stream, null);
                            }

                            exportedCount++;
                        });
                    }
                    catch (Exception ex)
                    {
                        osu.Framework.Logging.Logger.Log($"Failed to export beatmap set {id}: {ex}", level: osu.Framework.Logging.LogLevel.Error);
                    }

                    notification.Progress = (float)exportedCount / totalCount;
                    notification.Text = $"Exporting beatmaps: {exportedCount} of {totalCount}...";
                }

                if (notification.State != ProgressNotificationState.Cancelled)
                {
                    notification.CompletionText = $"Successfully exported {exportedCount} beatmap sets!";
                    notification.CompletionClickAction = () =>
                    {
                        try
                        {
                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                            {
                                FileName = folderPath,
                                UseShellExecute = true
                            });
                        }
                        catch (Exception ex)
                        {
                            osu.Framework.Logging.Logger.Log($"Failed to open directory: {ex}", level: osu.Framework.Logging.LogLevel.Error);
                        }
                        return true;
                    };
                    notification.State = ProgressNotificationState.Completed;
                }
            });
        }

        private void updateHashAndMarkDirty(BeatmapSetInfo setInfo)
        {
            setInfo.Hash = beatmapImporter.ComputeHash(setInfo);
            setInfo.Status = BeatmapOnlineStatus.LocallyModified;
        }

        private void save(BeatmapInfo beatmapInfo, IBeatmap beatmapContent, ISkin? beatmapSkin, Storyboard? storyboard, bool transferCollections)
        {
            var setInfo = beatmapInfo.BeatmapSet;
            Debug.Assert(setInfo != null);
            var customFormat = beatmapInfo.Ruleset.CreateInstance() as ICustomBeatmapFormat;
            BeatmapDifficulty originalDifficulty = beatmapContent.Difficulty;

            // Difficulty settings must be copied first due to the clone in `Beatmap<>.BeatmapInfo_Set`.
            // This should hopefully be temporary, assuming said clone is eventually removed.

            // Warning: The directionality here is important. Changes have to be copied *from* beatmapContent (which comes from editor and is being saved)
            // *to* the beatmapInfo (which is a database model and needs to receive values without the taiko slider velocity multiplier for correct operation).
            // CopyTo() will undo such adjustments, while CopyFrom() will not.
            beatmapContent.Difficulty.CopyTo(beatmapInfo.Difficulty);

            // All changes to metadata are made in the provided beatmapInfo, so this should be copied to the `IBeatmap` before encoding.
            beatmapContent.BeatmapInfo = beatmapInfo;
            customFormat?.CopyCustomDifficultySettings(originalDifficulty, beatmapContent.Difficulty);

            // Since now this is a locally-modified beatmap, we also set all relevant flags to indicate this.
            beatmapInfo.LastLocalUpdate = DateTimeOffset.Now;
            beatmapInfo.Status = BeatmapOnlineStatus.LocallyModified;

            Realm.Write(r =>
            {
                using var stream = new MemoryStream();
                using var sidecarStream = new MemoryStream();

                if (customFormat != null)
                {
                    customFormat.EncodeCompatibilityBeatmap(beatmapContent, beatmapSkin, storyboard, stream);
                    customFormat.EncodeSidecar(beatmapContent, beatmapSkin, storyboard, sidecarStream);
                }
                else
                {
                    using (var sw = new StreamWriter(stream, Encoding.UTF8, 1024, true))
                        new LegacyBeatmapEncoder(beatmapContent, beatmapSkin, storyboard).Encode(sw);
                }

                stream.Seek(0, SeekOrigin.Begin);
                sidecarStream.Seek(0, SeekOrigin.Begin);

                // AddFile generally handles updating/replacing files, but this is a case where the filename may have also changed so let's delete for simplicity.
                var existingFileInfo = beatmapInfo.Path != null ? setInfo.GetFile(beatmapInfo.Path) : null;
                var existingSidecarFileInfo = customFormat != null && beatmapInfo.Path != null
                    ? setInfo.GetFile(CustomBeatmapFormat.GetSidecarFilename(beatmapInfo.Path))
                    : null;
                string targetFilename = createBeatmapFilenameFromMetadata(beatmapInfo);
                string targetSidecarFilename = CustomBeatmapFormat.GetSidecarFilename(targetFilename);

                // ensure that two difficulties from the set don't point at the same beatmap file.
                if (setInfo.Beatmaps.Any(b => b.ID != beatmapInfo.ID && string.Equals(b.Path, targetFilename, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"{setInfo.GetDisplayString()} already has a difficulty with the name of '{beatmapInfo.DifficultyName}'.");

                if (existingFileInfo != null)
                    DeleteFile(setInfo, existingFileInfo);

                if (existingSidecarFileInfo != null)
                    DeleteFile(setInfo, existingSidecarFileInfo);

                string oldMd5Hash = beatmapInfo.MD5Hash;

                beatmapInfo.MD5Hash = stream.ComputeMD5Hash();
                // BeatmapInfo.Hash is also used to resolve BeatmapInfo.File from the set's files.
                // It must therefore remain the hash of the primary .osu file, not a combined
                // hash of the compatibility file and its custom sidecar.
                beatmapInfo.Hash = stream.ComputeSHA2Hash();

                stream.Seek(0, SeekOrigin.Begin);
                AddFile(setInfo, stream, createBeatmapFilenameFromMetadata(beatmapInfo));

                if (customFormat != null)
                {
                    sidecarStream.Seek(0, SeekOrigin.Begin);
                    AddFile(setInfo, sidecarStream, targetSidecarFilename);
                }

                updateHashAndMarkDirty(setInfo);

                var liveBeatmapSet = r.Find<BeatmapSetInfo>(setInfo.ID)!;

                setInfo.CopyChangesToRealm(liveBeatmapSet);

                if (transferCollections)
                    beatmapInfo.TransferCollectionReferences(r, oldMd5Hash);

                liveBeatmapSet.Beatmaps.Single(b => b.ID == beatmapInfo.ID)
                              .UpdateLocalScores(r);

                // do not look up metadata.
                // this is a locally-modified set now, so looking up metadata is busy work at best and harmful at worst.
                ProcessBeatmap?.Invoke(liveBeatmapSet, MetadataLookupScope.None);
            });

            Debug.Assert(beatmapInfo.BeatmapSet != null);

            static string createBeatmapFilenameFromMetadata(BeatmapInfo beatmapInfo)
            {
                var metadata = beatmapInfo.Metadata;
                return $"{metadata.Artist} - {metadata.Title} ({metadata.Author.Username}) [{beatmapInfo.DifficultyName}].osu".GetValidFilename();
            }
        }

        public void MarkPlayed(BeatmapInfo beatmapSetInfo) => Realm.Run(r =>
        {
            using var transaction = r.BeginWrite();

            var beatmap = r.Find<BeatmapInfo>(beatmapSetInfo.ID)!;
            beatmap.LastPlayed = DateTimeOffset.Now;

            transaction.Commit();
        });

        public void MarkNotPlayed(BeatmapInfo beatmapSetInfo) => Realm.Run(r =>
        {
            using var transaction = r.BeginWrite();

            var beatmap = r.Find<BeatmapInfo>(beatmapSetInfo.ID)!;
            beatmap.LastPlayed = null;

            transaction.Commit();
        });

        #region Implementation of ICanAcceptFiles

        public Task Import(params string[] paths) => beatmapImporter.Import(paths);

        public Task Import(ImportTask[] tasks, ImportParameters parameters = default) => beatmapImporter.Import(tasks, parameters);

        public Task<IEnumerable<Live<BeatmapSetInfo>>> Import(ProgressNotification notification, ImportTask[] tasks, ImportParameters parameters = default) =>
            beatmapImporter.Import(notification, tasks, parameters);

        public Task<Live<BeatmapSetInfo>?> Import(ImportTask task, ImportParameters parameters = default, CancellationToken cancellationToken = default) =>
            beatmapImporter.Import(task, parameters, cancellationToken);

        public Live<BeatmapSetInfo>? Import(BeatmapSetInfo item, ArchiveReader? archive = null, CancellationToken cancellationToken = default) =>
            beatmapImporter.ImportModel(item, archive, default, cancellationToken);

        public IEnumerable<string> HandledExtensions => beatmapImporter.HandledExtensions;

        #endregion

        #region Implementation of IWorkingBeatmapCache

        /// <summary>
        /// Retrieve a <see cref="WorkingBeatmap"/> instance for the provided <see cref="BeatmapInfo"/>
        /// </summary>
        /// <param name="beatmapInfo">The beatmap to lookup.</param>
        /// <param name="refetch">Whether to force a refetch from the database to ensure <see cref="BeatmapInfo"/> is up-to-date.</param>
        /// <returns>A <see cref="WorkingBeatmap"/> instance correlating to the provided <see cref="BeatmapInfo"/>.</returns>
        public WorkingBeatmap GetWorkingBeatmap(BeatmapInfo? beatmapInfo, bool refetch = false)
        {
            if (beatmapInfo != null)
            {
                if (refetch)
                    workingBeatmapCache.Invalidate(beatmapInfo);

                // Detached beatmapsets don't come with files as an optimisation (see `RealmObjectExtensions.beatmap_set_mapper`).
                // If we seem to be missing files, now is a good time to re-fetch.
                bool missingFiles = beatmapInfo.BeatmapSet?.Files.Count == 0;

                if (beatmapInfo.IsManaged)
                {
                    beatmapInfo = beatmapInfo.Detach();
                }
                else if (refetch || missingFiles)
                {
                    Guid id = beatmapInfo.ID;
                    beatmapInfo = Realm.Run(r => r.Find<BeatmapInfo>(id)?.Detach()) ?? beatmapInfo;
                }

                Debug.Assert(beatmapInfo.IsManaged != true);
            }

            return workingBeatmapCache.GetWorkingBeatmap(beatmapInfo);
        }

        WorkingBeatmap IWorkingBeatmapCache.GetWorkingBeatmap(BeatmapInfo beatmapInfo) => GetWorkingBeatmap(beatmapInfo);
        void IWorkingBeatmapCache.Invalidate(BeatmapSetInfo beatmapSetInfo) => workingBeatmapCache.Invalidate(beatmapSetInfo);
        void IWorkingBeatmapCache.Invalidate(BeatmapInfo beatmapInfo) => workingBeatmapCache.Invalidate(beatmapInfo);

        public event Action<WorkingBeatmap>? OnInvalidated
        {
            add => workingBeatmapCache.OnInvalidated += value;
            remove => workingBeatmapCache.OnInvalidated -= value;
        }

        public override bool IsAvailableLocally(BeatmapSetInfo model)
        {
            throw new InvalidOperationException($"Use overload with {nameof(IBeatmapInfo)} parameter instead.");
        }

        public bool IsAvailableLocally(IBeatmapInfo model)
        {
            return StablePathManager.IsAvailableLocally(model.OnlineID, model.MD5Hash)
                   || Realm.Run(r => r.All<BeatmapInfo>()
                                      .Filter($@"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.DeletePending)} == false")
                                      .Filter($@"{nameof(BeatmapInfo.OnlineID)} == $0 AND {nameof(BeatmapInfo.MD5Hash)} == {nameof(BeatmapInfo.OnlineMD5Hash)}", model.OnlineID)
                                      .Any());
        }

        #endregion

        #region Implementation of IPostImports<out BeatmapSetInfo>

        public Action<IEnumerable<Live<BeatmapSetInfo>>>? PresentImport
        {
            set => beatmapImporter.PresentImport = value;
        }

        #endregion

        public override string HumanisedModelName => "beatmap";
    }

    /// <summary>
    /// Delegate type for beatmap processing callbacks.
    /// </summary>
    /// <param name="beatmapSet">The beatmap set to be processed.</param>
    /// <param name="lookupScope">The scope to use when looking up metadata.</param>
    public delegate void ProcessBeatmapDelegate(BeatmapSetInfo beatmapSet, MetadataLookupScope lookupScope);
}
