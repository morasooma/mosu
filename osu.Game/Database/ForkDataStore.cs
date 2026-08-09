// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using osu.Framework.Logging;
using osu.Framework.Platform;
using Realms;

namespace osu.Game.Database
{
    public class ForkDataStore : IDisposable
    {
        private const int schema_version = 3;

        private readonly Storage storage;
        private readonly ConcurrentDictionary<Guid, double> ppCache = new ConcurrentDictionary<Guid, double>();
        private readonly ConcurrentDictionary<Guid, RelaxBeatmapData> relaxCache = new ConcurrentDictionary<Guid, RelaxBeatmapData>();
        private readonly ConcurrentDictionary<Guid, bool> ppCalculated = new ConcurrentDictionary<Guid, bool>();
        private readonly ConcurrentDictionary<Guid, bool> relaxCalculated = new ConcurrentDictionary<Guid, bool>();

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
                Schema = new[] { typeof(ForkBeatmapData) },
                FallbackPipePath = tempPathLocation,
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

                    if (data.PerformancePointsCalculated)
                        ppCalculated[data.BeatmapID] = true;

                    if (data.RelaxPerformancePointsCalculated)
                        relaxCalculated[data.BeatmapID] = true;
                }

                Logger.Log($"ForkDataStore: loaded {ppCache.Count} PP entries from fork.realm");
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to load: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        public double GetPP(Guid beatmapId) => ppCache.GetValueOrDefault(beatmapId, -1);

        public double GetRelaxStarRating(Guid beatmapId) => relaxCache.GetValueOrDefault(beatmapId).StarRating;

        public double GetRelaxPP(Guid beatmapId) => relaxCache.GetValueOrDefault(beatmapId).MaxPerformancePoints;

        public bool HasPP(Guid beatmapId) => ppCalculated.ContainsKey(beatmapId);

        public bool HasRelaxData(Guid beatmapId) => relaxCalculated.ContainsKey(beatmapId);

        public void SetPP(Guid beatmapId, double pp)
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
                    }
                    else
                        realm.Add(new ForkBeatmapData { BeatmapID = beatmapId, MaxPerformancePoints = pp, PerformancePointsCalculated = true });
                });

                ppCalculated[beatmapId] = true;
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write PP for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
            }
        }

        public void SetRelaxData(Guid beatmapId, double starRating, double pp)
        {
            relaxCache[beatmapId] = new RelaxBeatmapData(starRating, pp);

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

                    existing.RelaxStarRating = starRating;
                    existing.RelaxMaxPerformancePoints = pp;
                    existing.RelaxPerformancePointsCalculated = true;
                });

                relaxCalculated[beatmapId] = true;
            }
            catch (Exception e)
            {
                Logger.Log($"ForkDataStore: failed to write Relax data for {beatmapId}: {e.Message}", LoggingTarget.Database, LogLevel.Error);
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

        public void Dispose()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
