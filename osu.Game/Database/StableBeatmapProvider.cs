// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using osu.Framework.Logging;
using osu.Framework.Extensions;
using osu.Game.Beatmaps;
using osu.Game.IO;
using osu.Game.Rulesets;
using osu.Game.Models;

namespace osu.Game.Database
{
    public class StableBeatmapProvider
    {
        private readonly RulesetStore rulesetStore;
        
        public StableBeatmapProvider(RulesetStore rulesetStore)
        {
            this.rulesetStore = rulesetStore;
        }

        public StableBeatmapLoadResult GetBeatmaps(StableStorage stableStorage, CancellationToken cancellationToken = default)
        {
            var sets = new List<BeatmapSetInfo>();
            var beatmapPaths = new Dictionary<Guid, string>();
            var audioPaths = new Dictionary<Guid, string>();

            if (stableStorage == null)
                return new StableBeatmapLoadResult(sets, beatmapPaths, audioPaths);

            string dbPath = stableStorage.GetFullPath("osu!.db");
            if (!File.Exists(dbPath))
            {
                Logger.Log($@"osu!.db not found at {dbPath}", LoggingTarget.Database, LogLevel.Important);
                return new StableBeatmapLoadResult(sets, beatmapPaths, audioPaths);
            }

            try
            {
                var songStorage = stableStorage.GetSongStorage();

                using (var stream = File.OpenRead(dbPath))
                using (var reader = new BinaryReader(stream))
                {
                    int version = reader.ReadInt32();
                    int folderCount = reader.ReadInt32();
                    bool accountUnlocked = reader.ReadBoolean();
                    reader.ReadInt64(); // dateUnlock
                    readOsuString(reader); // playerName
                    int beatmapCount = reader.ReadInt32();

                    var setsMap = new Dictionary<string, BeatmapSetInfo>();

                    for (int i = 0; i < beatmapCount; i++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (version < 20191106)
                            reader.ReadInt32(); // size

                        string artist = readOsuString(reader);
                        string artistUnicode = readOsuString(reader);
                        string title = readOsuString(reader);
                        string titleUnicode = readOsuString(reader);
                        string creator = readOsuString(reader);
                        string difficulty = readOsuString(reader);
                        string audioFile = readOsuString(reader);
                        string md5 = readOsuString(reader);
                        string osuFile = readOsuString(reader);
                        
                        byte rankedStatus = reader.ReadByte();
                        short hitcircleCount = reader.ReadInt16();
                        short sliderCount = reader.ReadInt16();
                        short spinnerCount = reader.ReadInt16();
                        reader.ReadInt64(); // modificationTime
                        
                        float ar = version >= 20140609 ? reader.ReadSingle() : reader.ReadByte();
                        float cs = version >= 20140609 ? reader.ReadSingle() : reader.ReadByte();
                        float hp = version >= 20140609 ? reader.ReadSingle() : reader.ReadByte();
                        float od = version >= 20140609 ? reader.ReadSingle() : reader.ReadByte();
                        
                        double sv = reader.ReadDouble();
                        
                        // SRs
                        double srStd = readDoubleIntPairs(reader); // standard
                        double srTaiko = readDoubleIntPairs(reader); // taiko
                        double srCtb = readDoubleIntPairs(reader); // ctb
                        double srMania = readDoubleIntPairs(reader); // mania
                        
                        int drainTime = reader.ReadInt32();
                        int totalTime = reader.ReadInt32();
                        int previewTime = reader.ReadInt32();
                        
                        // Timing points
                        int timingPointsCount = reader.ReadInt32();
                        double firstBpm = 0;
                        for (int j = 0; j < timingPointsCount; j++)
                        {
                            reader.ReadDouble(); // offset
                            double beatLength = reader.ReadDouble();
                            bool uninherited = reader.ReadBoolean();

                            if (uninherited && beatLength > 0 && firstBpm == 0)
                                firstBpm = 60000 / beatLength;
                        }
                        
                        int beatmapId = reader.ReadInt32();
                        int beatmapSetId = reader.ReadInt32();
                        int threadId = reader.ReadInt32();
                        
                        reader.ReadByte(); // std grade
                        reader.ReadByte(); // taiko grade
                        reader.ReadByte(); // ctb grade
                        reader.ReadByte(); // mania grade
                        
                        short localOffset = reader.ReadInt16();
                        float stackLeniency = reader.ReadSingle();
                        byte mode = reader.ReadByte();
                        
                        string source = readOsuString(reader);
                        string tags = readOsuString(reader);
                        
                        short onlineOffset = reader.ReadInt16();
                        string titleFont = readOsuString(reader);
                        bool isUnplayed = reader.ReadBoolean();
                        reader.ReadInt64(); // lastPlayed
                        bool isOsz2 = reader.ReadBoolean();
                        
                        string folderName = readOsuString(reader);
                        
                        reader.ReadInt64(); // lastChecked
                        bool ignoreSound = reader.ReadBoolean();
                        bool ignoreSkin = reader.ReadBoolean();
                        bool disableStoryboard = reader.ReadBoolean();
                        bool disableVideo = reader.ReadBoolean();
                        bool visualOverride = reader.ReadBoolean();
                        
                        if (version < 20140609)
                            reader.ReadInt16(); // unknown
                        
                        reader.ReadInt32(); // lastModTime
                        byte maniaScrollSpeed = reader.ReadByte();

                        if (!setsMap.TryGetValue(folderName, out var setInfo))
                        {
                            setInfo = new BeatmapSetInfo
                            {
                                ID = CreateDeterministicGuid(folderName),
                                OnlineID = beatmapSetId,
                                DateAdded = DateTimeOffset.UtcNow
                            };
                            setsMap[folderName] = setInfo;
                        }

                        var metadata = new BeatmapMetadata
                        {
                            Artist = artist,
                            ArtistUnicode = artistUnicode,
                            Title = title,
                            TitleUnicode = titleUnicode,
                            Author = new RealmUser { Username = creator },
                            Source = source,
                            Tags = tags,
                            PreviewTime = previewTime,
                            AudioFile = audioFile
                        };

                        RulesetInfo ruleset = rulesetStore.GetRuleset(mode)
                                              ?? rulesetStore.GetRuleset(0)
                                              ?? throw new InvalidDataException("No rulesets are available while importing the stable beatmap database.");

                        double starRating = mode == 0 ? srStd :
                                            mode == 1 ? srTaiko :
                                            mode == 2 ? srCtb :
                                            srMania;

                        var bInfo = new BeatmapInfo
                        {
                            ID = CreateDeterministicGuid(md5),
                            BeatmapSet = setInfo,
                            Ruleset = ruleset,
                            DifficultyName = difficulty,
                            OnlineID = beatmapId,
                            Length = totalTime,
                            BPM = firstBpm, // Approximate
                            Hash = md5,
                            MD5Hash = md5,
                            Metadata = metadata,
                            Status = convertStableRankedStatus(rankedStatus),
                            StarRating = starRating,
                            Difficulty = new BeatmapDifficulty
                            {
                                ApproachRate = ar,
                                CircleSize = cs,
                                DrainRate = hp,
                                OverallDifficulty = od,
                                SliderMultiplier = sv,
                                SliderTickRate = 1
                            }
                        };

                        // osu!.db stores the online status per difficulty. Preserve it instead of
                        // letting stable-imported maps appear as unknown/unranked in lazer.
                        if (setInfo.Status == BeatmapOnlineStatus.None || bInfo.Status > setInfo.Status)
                            setInfo.Status = bInfo.Status;
                        
                        beatmapPaths[bInfo.ID] = songStorage.GetFullPath(Path.Combine(folderName, osuFile));
                        audioPaths[bInfo.ID] = songStorage.GetFullPath(Path.Combine(folderName, audioFile));

                        setInfo.Beatmaps.Add(bInfo);
                    }

                    sets = setsMap.Values.ToList();
                    Logger.Log($"Loaded {sets.Count} stable beatmap sets directly from osu!.db.", LoggingTarget.Database);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.Log($@"Error parsing osu!.db: {ex}", LoggingTarget.Database, LogLevel.Error);
            }

            return new StableBeatmapLoadResult(sets, beatmapPaths, audioPaths);
        }

        private string readOsuString(BinaryReader reader)
        {
            byte flag = reader.ReadByte();
            if (flag == 0) return string.Empty;
            if (flag == 11) return reader.ReadString();
            return string.Empty; // Should not happen
        }

        private double readDoubleIntPairs(BinaryReader reader)
        {
            double noModSR = 0;
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                reader.ReadByte(); // flag
                int mods = reader.ReadInt32(); // int
                byte flag = reader.ReadByte(); // flag
                double sr = 0;
                if (flag == 12)
                    sr = reader.ReadSingle(); // single
                else if (flag == 13)
                    sr = reader.ReadDouble(); // double

                if (mods == 0)
                    noModSR = sr;
            }
            return noModSR;
        }

        private static BeatmapOnlineStatus convertStableRankedStatus(byte status) => status switch
        {
            // Stable's osu!.db enum: 0 unknown, 1 unsubmitted, 2 pending,
            // 3 ranked, 4 approved, 5 qualified, 6 loved.
            2 => BeatmapOnlineStatus.Pending,
            3 => BeatmapOnlineStatus.Ranked,
            4 => BeatmapOnlineStatus.Approved,
            5 => BeatmapOnlineStatus.Qualified,
            6 => BeatmapOnlineStatus.Loved,
            _ => BeatmapOnlineStatus.None,
        };
        
        private static Guid CreateDeterministicGuid(string input)
        {
            byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
            return new Guid(hash);
        }
    }

    public sealed class StableBeatmapLoadResult
    {
        public IReadOnlyList<BeatmapSetInfo> BeatmapSets { get; }
        public IReadOnlyDictionary<Guid, string> BeatmapPaths { get; }
        public IReadOnlyDictionary<Guid, string> AudioPaths { get; }

        public StableBeatmapLoadResult(IReadOnlyList<BeatmapSetInfo> beatmapSets, IReadOnlyDictionary<Guid, string> beatmapPaths, IReadOnlyDictionary<Guid, string> audioPaths)
        {
            BeatmapSets = beatmapSets;
            BeatmapPaths = beatmapPaths;
            AudioPaths = audioPaths;
        }
    }

    public static class StablePathManager
    {
        private static readonly object syncRoot = new object();
        private static readonly ConcurrentDictionary<string, StableFileChecksum> checksumCache = new ConcurrentDictionary<string, StableFileChecksum>(StringComparer.OrdinalIgnoreCase);
        private static PathSnapshot current = new PathSnapshot(
            new Dictionary<Guid, string>(),
            new Dictionary<Guid, string>(),
            new Dictionary<int, HashSet<string>>(),
            new Dictionary<int, string>(),
            new Dictionary<int, BeatmapInfo>(),
            new Dictionary<int, string[]>());

        public static event Action? AvailabilityChanged;

        public static void Register(Guid id, string path)
        {
            lock (syncRoot)
            {
                var beatmapPaths = new Dictionary<Guid, string>(current.BeatmapPaths) { [id] = path };
                Volatile.Write(ref current, new PathSnapshot(
                    beatmapPaths,
                    current.AudioPaths,
                    current.ChecksumsByOnlineId,
                    current.PathsByOnlineId,
                    current.BeatmapsByOnlineId,
                    current.PathsByBeatmapSetOnlineId));
            }
        }

        public static void RegisterAudio(Guid id, string path)
        {
            lock (syncRoot)
            {
                var audioPaths = new Dictionary<Guid, string>(current.AudioPaths) { [id] = path };
                Volatile.Write(ref current, new PathSnapshot(
                    current.BeatmapPaths,
                    audioPaths,
                    current.ChecksumsByOnlineId,
                    current.PathsByOnlineId,
                    current.BeatmapsByOnlineId,
                    current.PathsByBeatmapSetOnlineId));
            }
        }

        public static void Replace(IReadOnlyDictionary<Guid, string> newBeatmapPaths, IReadOnlyDictionary<Guid, string> newAudioPaths, IEnumerable<BeatmapSetInfo>? beatmapSets = null)
        {
            var checksumsByOnlineId = new Dictionary<int, HashSet<string>>();
            var pathsByOnlineId = new Dictionary<int, string>();
            var beatmapsByOnlineId = new Dictionary<int, BeatmapInfo>();
            var pathsByBeatmapSetOnlineId = new Dictionary<int, string[]>();

            if (beatmapSets != null)
            {
                foreach (var set in beatmapSets.Where(set => set.OnlineID > 0))
                {
                    pathsByBeatmapSetOnlineId[set.OnlineID] = set.Beatmaps
                        .Select(beatmap => newBeatmapPaths.TryGetValue(beatmap.ID, out string? path) ? path : null)
                        .Where(path => !string.IsNullOrEmpty(path))
                        .Cast<string>()
                        .ToArray();
                }

                foreach (var beatmap in beatmapSets.SelectMany(set => set.Beatmaps).Where(beatmap => beatmap.OnlineID > 0))
                {
                    if (!newBeatmapPaths.TryGetValue(beatmap.ID, out string? path) || string.IsNullOrEmpty(path))
                        continue;

                    pathsByOnlineId[beatmap.OnlineID] = path;
                    beatmapsByOnlineId[beatmap.OnlineID] = beatmap;
                    if (!checksumsByOnlineId.TryGetValue(beatmap.OnlineID, out var checksums))
                        checksumsByOnlineId[beatmap.OnlineID] = checksums = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    if (!string.IsNullOrEmpty(beatmap.MD5Hash))
                        checksums.Add(beatmap.MD5Hash);
                }
            }

            var replacement = new PathSnapshot(
                new Dictionary<Guid, string>(newBeatmapPaths),
                new Dictionary<Guid, string>(newAudioPaths),
                checksumsByOnlineId,
                pathsByOnlineId,
                beatmapsByOnlineId,
                pathsByBeatmapSetOnlineId);
            checksumCache.Clear();
            Volatile.Write(ref current, replacement);
            AvailabilityChanged?.Invoke();
        }

        public static bool TryGetPath(Guid id, [NotNullWhen(true)] out string? path)
            => Volatile.Read(ref current).BeatmapPaths.TryGetValue(id, out path);

        public static bool TryGetAudioPath(Guid id, [NotNullWhen(true)] out string? path)
            => Volatile.Read(ref current).AudioPaths.TryGetValue(id, out path);
        
        public static bool IsStableBeatmap(Guid id)
            => Volatile.Read(ref current).BeatmapPaths.ContainsKey(id);

        public static bool IsAvailableLocally(int onlineId, string? md5Hash = null)
        {
            var snapshot = Volatile.Read(ref current);
            return isAvailableLocally(snapshot, onlineId, md5Hash);
        }

        /// <summary>
        /// Returns metadata for a directly-accessed stable beatmap when its backing file exists
        /// and, when supplied, matches the expected online checksum.
        /// </summary>
        public static BeatmapInfo? GetBeatmapInfo(int onlineId, string? md5Hash = null)
        {
            var snapshot = Volatile.Read(ref current);

            if (!isAvailableLocally(snapshot, onlineId, md5Hash) ||
                !snapshot.BeatmapsByOnlineId.TryGetValue(onlineId, out BeatmapInfo? beatmapInfo))
                return null;

            // Consumers may mutate status and hashes while loading. Never expose the snapshot's
            // shared instance, otherwise one failed load could poison future lookups.
            return beatmapInfo.Clone();
        }

        public static bool IsBeatmapSetAvailableLocally(int onlineId)
            => onlineId > 0 &&
               Volatile.Read(ref current).PathsByBeatmapSetOnlineId.TryGetValue(onlineId, out string[]? paths) &&
               paths.Any(File.Exists);

        private static bool isAvailableLocally(PathSnapshot snapshot, int onlineId, string? md5Hash)
        {
            if (onlineId <= 0 ||
                !snapshot.PathsByOnlineId.TryGetValue(onlineId, out string? path) ||
                !File.Exists(path) ||
                !snapshot.ChecksumsByOnlineId.TryGetValue(onlineId, out var checksums))
                return false;

            if (string.IsNullOrEmpty(md5Hash))
                return true;

            return checksums.Contains(md5Hash) && fileMatchesChecksum(path, md5Hash);
        }

        private static bool fileMatchesChecksum(string path, string expectedMD5)
        {
            try
            {
                var file = new System.IO.FileInfo(path);
                if (!file.Exists)
                    return false;

                long length = file.Length;
                DateTime lastWriteTimeUtc = file.LastWriteTimeUtc;
                if (checksumCache.TryGetValue(path, out var cached) &&
                    cached.Length == length &&
                    cached.LastWriteTimeUtc == lastWriteTimeUtc)
                    return string.Equals(cached.MD5, expectedMD5, StringComparison.OrdinalIgnoreCase);

                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                string actualMD5 = stream.ComputeMD5Hash();

                file.Refresh();
                if (!file.Exists || file.Length != length || file.LastWriteTimeUtc != lastWriteTimeUtc)
                    return false;

                checksumCache[path] = new StableFileChecksum(length, lastWriteTimeUtc, actualMD5);
                return string.Equals(actualMD5, expectedMD5, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private sealed record StableFileChecksum(long Length, DateTime LastWriteTimeUtc, string MD5);

        private sealed class PathSnapshot
        {
            public IReadOnlyDictionary<Guid, string> BeatmapPaths { get; }
            public IReadOnlyDictionary<Guid, string> AudioPaths { get; }
            public IReadOnlyDictionary<int, HashSet<string>> ChecksumsByOnlineId { get; }
            public IReadOnlyDictionary<int, string> PathsByOnlineId { get; }
            public IReadOnlyDictionary<int, BeatmapInfo> BeatmapsByOnlineId { get; }
            public IReadOnlyDictionary<int, string[]> PathsByBeatmapSetOnlineId { get; }

            public PathSnapshot(IReadOnlyDictionary<Guid, string> beatmapPaths, IReadOnlyDictionary<Guid, string> audioPaths,
                                IReadOnlyDictionary<int, HashSet<string>> checksumsByOnlineId,
                                IReadOnlyDictionary<int, string> pathsByOnlineId,
                                IReadOnlyDictionary<int, BeatmapInfo> beatmapsByOnlineId,
                                IReadOnlyDictionary<int, string[]> pathsByBeatmapSetOnlineId)
            {
                BeatmapPaths = beatmapPaths;
                AudioPaths = audioPaths;
                ChecksumsByOnlineId = checksumsByOnlineId;
                PathsByOnlineId = pathsByOnlineId;
                BeatmapsByOnlineId = beatmapsByOnlineId;
                PathsByBeatmapSetOnlineId = pathsByBeatmapSetOnlineId;
            }
        }
    }
}
