// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using osu.Framework.Logging;
using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.BeatmapListing;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Performs online metadata lookups using the osu-web API.
    /// </summary>
    public class APIBeatmapMetadataSource : IOnlineBeatmapMetadataSource
    {
        private readonly IAPIProvider api;

        public APIBeatmapMetadataSource(IAPIProvider api)
        {
            this.api = api;
        }

        public bool Available => api.State.Value == APIState.Online;

        public bool TryLookup(BeatmapInfo beatmapInfo, out OnlineBeatmapMetadata? onlineMetadata)
        {
            if (!Available)
            {
                onlineMetadata = null;
                return false;
            }

            var beatmapSet = beatmapInfo.BeatmapSet;
            Debug.Assert(beatmapSet != null);

            if (beatmapSet == null)
            {
                onlineMetadata = null;
                return false;
            }

            try
            {
                Logger.Log($@"{nameof(APIBeatmapMetadataSource)} lookup start: beatmap={beatmapInfo} onlineId={beatmapInfo.OnlineID} setOnlineId={beatmapSet.OnlineID} md5={beatmapInfo.MD5Hash} path={beatmapInfo.Path}", LoggingTarget.Network);

                // intentionally blocking to limit web request concurrency
                var directMatch = performBeatmapLookupByOnlineId(beatmapInfo)
                                  ?? performBeatmapLookup(beatmapInfo);

                if (directMatch != null)
                {
                    logForModel(beatmapSet, $@"Online retrieval mapped {beatmapInfo} to {directMatch.OnlineBeatmapSetID} / {directMatch.OnlineID}.");
                    onlineMetadata = createMetadata(directMatch);
                    return true;
                }

                var fallbackMatch = performBeatmapSetLookup(beatmapInfo);

                if (fallbackMatch != null)
                {
                    logForModel(beatmapSet, $@"Online retrieval matched {beatmapInfo} via beatmap set lookup to {fallbackMatch.OnlineBeatmapSetID} / {fallbackMatch.OnlineID}.");
                    onlineMetadata = createMetadata(fallbackMatch);
                    return true;
                }

                var metadataSearchMatch = performMetadataSearchLookup(beatmapInfo);

                if (metadataSearchMatch != null)
                {
                    logForModel(beatmapSet, $@"Online retrieval matched {beatmapInfo} via metadata search to {metadataSearchMatch.OnlineBeatmapSetID} / {metadataSearchMatch.OnlineID}.");
                    onlineMetadata = createMetadata(metadataSearchMatch);
                    return true;
                }

                logForModel(beatmapSet, $@"Online retrieval failed for {beatmapInfo}");
                onlineMetadata = null;
                return true;
            }
            catch (Exception e)
            {
                logForModel(beatmapSet, $@"Online retrieval failed for {beatmapInfo} ({e.Message})");
                onlineMetadata = null;
                return false;
            }
        }

        private APIBeatmap? performBeatmapLookupByOnlineId(BeatmapInfo beatmapInfo)
        {
            if (beatmapInfo.OnlineID <= 0)
                return null;

            Logger.Log($@"{nameof(APIBeatmapMetadataSource)} trying direct online-id lookup for beatmap id {beatmapInfo.OnlineID}", LoggingTarget.Network);
            var req = new GetBeatmapRequest(beatmapInfo);
            api.Perform(req);

            if (req.CompletionState == APIRequestCompletionState.Failed)
                return null;

            var response = req.Response;

            if (response == null)
                return null;

            if (!onlineIdLookupMatchesBeatmap(beatmapInfo, response))
            {
                Logger.Log(
                    $@"{nameof(APIBeatmapMetadataSource)} rejected direct online-id lookup for beatmap id {beatmapInfo.OnlineID}: local checksum {beatmapInfo.MD5Hash} does not match online checksum {response.MD5Hash}. Falling back to checksum lookup.",
                    LoggingTarget.Network);
                return null;
            }

            return response;
        }

        /// <summary>
        /// Verifies that an online-ID lookup still describes the local difficulty.
        /// Server-exclusive archives created by older servers may contain IDs which were assigned
        /// in a different order from the corresponding database rows.
        /// </summary>
        internal static bool onlineIdLookupMatchesBeatmap(IBeatmapInfo localBeatmap, APIBeatmap onlineBeatmap)
            => string.IsNullOrEmpty(localBeatmap.MD5Hash)
               || string.IsNullOrEmpty(onlineBeatmap.MD5Hash)
               || string.Equals(localBeatmap.MD5Hash, onlineBeatmap.MD5Hash, StringComparison.OrdinalIgnoreCase);

        private APIBeatmap? performBeatmapLookup(BeatmapInfo beatmapInfo)
        {
            Logger.Log($@"{nameof(APIBeatmapMetadataSource)} trying checksum/filename lookup for md5={beatmapInfo.MD5Hash} path={beatmapInfo.Path}", LoggingTarget.Network);
            var req = new GetBeatmapRequest(beatmapInfo);
            api.Perform(req);

            if (req.CompletionState == APIRequestCompletionState.Failed)
                return null;

            return req.Response;
        }

        private APIBeatmap? performBeatmapSetLookup(BeatmapInfo beatmapInfo)
        {
            var beatmapSet = beatmapInfo.BeatmapSet;

            if (beatmapSet?.OnlineID <= 0)
                return null;

            Logger.Log($@"{nameof(APIBeatmapMetadataSource)} trying beatmap set lookup for set id {beatmapSet.OnlineID}", LoggingTarget.Network);
            var req = new GetBeatmapSetRequest(beatmapSet.OnlineID);
            api.Perform(req);

            if (req.CompletionState == APIRequestCompletionState.Failed || req.Response == null)
                return null;

            var response = req.Response;

            foreach (var beatmap in response.Beatmaps)
                beatmap.BeatmapSet = response;

            return matchBeatmapFromSet(beatmapInfo, response.Beatmaps);
        }

        private APIBeatmap? performMetadataSearchLookup(BeatmapInfo beatmapInfo)
        {
            var metadata = beatmapInfo.Metadata;

            string fullQuery = buildQuery(new[]
            {
                metadata.ArtistUnicode,
                metadata.Artist,
                metadata.TitleUnicode,
                metadata.Title,
                metadata.Author.Username,
            });

            string fallbackQuery = buildQuery(new[]
            {
                metadata.ArtistUnicode,
                metadata.Artist,
                metadata.TitleUnicode,
                metadata.Title,
            });

            if (string.IsNullOrWhiteSpace(fullQuery) && string.IsNullOrWhiteSpace(fallbackQuery))
                return null;

            foreach (string query in new[] { fullQuery, fallbackQuery }.Where(q => !string.IsNullOrWhiteSpace(q)).Distinct())
            {
                Logger.Log($"{nameof(APIBeatmapMetadataSource)} trying metadata search lookup with query \"{query}\"", LoggingTarget.Network);

                var req = new SearchBeatmapSetsRequest(
                    query,
                    beatmapInfo.Ruleset,
                    searchCategory: SearchCategory.Any,
                    sortCriteria: SortCriteria.Relevance,
                    sortDirection: SortDirection.Descending,
                    extra: Online.MosuServerEnvironment.IsThirdPartyServer
                           || beatmapInfo.Metadata.IsServerExclusive()
                           || beatmapInfo.BeatmapSet.IsServerExclusive()
                        ? new[] { SearchExtra.ServerExclusive }
                        : null,
                    explicitContent: SearchExplicit.Show);

                api.Perform(req);

                if (req.CompletionState == APIRequestCompletionState.Failed || req.Response == null)
                    continue;

                Logger.Log($@"{nameof(APIBeatmapMetadataSource)} metadata search returned {req.Response.BeatmapSets.Count()} set candidates", LoggingTarget.Network);

                var matchingSet = req.Response.BeatmapSets
                                     .Where(s => matchesSetMetadata(s, beatmapInfo))
                                     .OrderByDescending(s => setMetadataScore(s, beatmapInfo))
                                     .FirstOrDefault();

                if (matchingSet == null)
                    continue;

                foreach (var beatmap in matchingSet.Beatmaps)
                    beatmap.BeatmapSet = matchingSet;

                return matchBeatmapFromSet(beatmapInfo, matchingSet.Beatmaps, requireStrongStructuralMatch: true);
            }

            return null;
        }

        private static bool matchesSetMetadata(APIBeatmapSet set, BeatmapInfo localBeatmap)
        {
            var localMetadata = localBeatmap.Metadata;

            bool titleMatches = stringEqualsLoose(set.Title, localMetadata.Title)
                                || stringEqualsLoose(set.TitleUnicode, localMetadata.TitleUnicode)
                                || stringEqualsLoose(set.Title, localMetadata.TitleUnicode)
                                || stringEqualsLoose(set.TitleUnicode, localMetadata.Title);

            bool artistMatches = stringEqualsLoose(set.Artist, localMetadata.Artist)
                                 || stringEqualsLoose(set.ArtistUnicode, localMetadata.ArtistUnicode)
                                 || stringEqualsLoose(set.Artist, localMetadata.ArtistUnicode)
                                 || stringEqualsLoose(set.ArtistUnicode, localMetadata.Artist);

            return titleMatches && artistMatches;
        }

        private static int setMetadataScore(APIBeatmapSet set, BeatmapInfo localBeatmap)
        {
            int score = 0;
            var metadata = localBeatmap.Metadata;

            if (stringEqualsLoose(set.Title, metadata.Title) || stringEqualsLoose(set.TitleUnicode, metadata.TitleUnicode))
                score += 2;

            if (stringEqualsLoose(set.Artist, metadata.Artist) || stringEqualsLoose(set.ArtistUnicode, metadata.ArtistUnicode))
                score += 2;

            if (stringEqualsLoose(set.Author.Username, metadata.Author.Username))
                score += 1;

            return score;
        }

        private static bool stringEqualsLoose(string? left, string? right)
            => !string.IsNullOrWhiteSpace(left)
               && !string.IsNullOrWhiteSpace(right)
               && string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);

        private static string buildQuery(IEnumerable<string?> parts)
            => string.Join(' ', parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase));

        private static OnlineBeatmapMetadata createMetadata(APIBeatmap beatmap) => new OnlineBeatmapMetadata
        {
            BeatmapID = beatmap.OnlineID,
            BeatmapSetID = beatmap.OnlineBeatmapSetID,
            AuthorID = beatmap.AuthorID,
            BeatmapStatus = beatmap.Status,
            BeatmapSetStatus = beatmap.BeatmapSet?.Status,
            DateRanked = beatmap.BeatmapSet?.Ranked,
            DateSubmitted = beatmap.BeatmapSet?.Submitted,
            MD5Hash = beatmap.MD5Hash,
            LastUpdated = beatmap.LastUpdated,
        };

        private static APIBeatmap? matchBeatmapFromSet(BeatmapInfo localBeatmap, IEnumerable<APIBeatmap> onlineBeatmaps, bool requireStrongStructuralMatch = false)
        {
            List<APIBeatmap> candidates = onlineBeatmaps.Where(b => b.RulesetID == localBeatmap.Ruleset.OnlineID).ToList();

            if (candidates.Count == 0)
                return null;

            if (requireStrongStructuralMatch)
                return matchBeatmapByStructure(localBeatmap, candidates);

            candidates = narrowCandidates(candidates, b => string.Equals(b.MD5Hash, localBeatmap.MD5Hash, StringComparison.OrdinalIgnoreCase));
            candidates = narrowCandidates(candidates, b => string.Equals(b.DifficultyName, localBeatmap.DifficultyName, StringComparison.OrdinalIgnoreCase));
            candidates = narrowCandidates(candidates, b => Math.Abs(b.Length - localBeatmap.Length) < 1);
            candidates = narrowCandidates(candidates, b => Math.Abs(b.BPM - localBeatmap.BPM) < 0.01);
            candidates = narrowCandidates(candidates, b => Math.Abs(b.DrainRate - localBeatmap.Difficulty.DrainRate) < 0.01f);
            candidates = narrowCandidates(candidates, b => Math.Abs(b.CircleSize - localBeatmap.Difficulty.CircleSize) < 0.01f);
            candidates = narrowCandidates(candidates, b => Math.Abs(b.ApproachRate - localBeatmap.Difficulty.ApproachRate) < 0.01f);
            candidates = narrowCandidates(candidates, b => Math.Abs(b.OverallDifficulty - localBeatmap.Difficulty.OverallDifficulty) < 0.01f);
            candidates = narrowCandidates(candidates, b => b.TotalObjectCount == localBeatmap.TotalObjectCount);
            candidates = narrowCandidates(candidates, b => b.EndTimeObjectCount == localBeatmap.EndTimeObjectCount);

            return candidates.OrderBy(b => b.OnlineID).FirstOrDefault();
        }

        /// <summary>
        /// Matches a beatmap discovered via a metadata search.
        /// Metadata identifies a set of candidates, but is not sufficient to establish beatmap identity by itself.
        /// </summary>
        internal static APIBeatmap? matchBeatmapByStructure(BeatmapInfo localBeatmap, IEnumerable<APIBeatmap> onlineBeatmaps)
        {
            List<APIBeatmap> candidates = onlineBeatmaps.Where(b => b.RulesetID == localBeatmap.Ruleset.OnlineID).ToList();

            if (!string.IsNullOrEmpty(localBeatmap.MD5Hash))
            {
                var checksumMatches = candidates.Where(b => string.Equals(b.MD5Hash, localBeatmap.MD5Hash, StringComparison.OrdinalIgnoreCase)).ToList();

                if (checksumMatches.Count > 0)
                    return checksumMatches.OrderBy(b => b.OnlineID).First();
            }

            // Online lengths are rounded to seconds, while local lengths retain millisecond precision.
            candidates = candidates.Where(b => localBeatmap.Length > 0
                                                 && b.Length > 0
                                                 && Math.Abs(b.Length - localBeatmap.Length) <= 2000).ToList();

            // Object counts form the stable structural fingerprint which allows a renamed mapper to be found
            // without treating every map of the same song as an update of the local one.
            candidates = candidates.Where(b => localBeatmap.TotalObjectCount >= 0
                                                 && b.TotalObjectCount > 0
                                                 && b.TotalObjectCount == localBeatmap.TotalObjectCount
                                                 && localBeatmap.EndTimeObjectCount >= 0
                                                 && b.EndTimeObjectCount == localBeatmap.EndTimeObjectCount
                                                 && corroboratingMatchCount(localBeatmap, b) >= 2).ToList();

            if (candidates.Count == 1)
                return candidates[0];

            // Prefer a uniquely named difficulty when two structurally identical difficulties exist.
            var difficultyNameMatches = candidates.Where(b => string.Equals(b.DifficultyName, localBeatmap.DifficultyName, StringComparison.OrdinalIgnoreCase)).ToList();
            return difficultyNameMatches.Count == 1 ? difficultyNameMatches[0] : null;
        }

        private static int corroboratingMatchCount(BeatmapInfo localBeatmap, APIBeatmap onlineBeatmap)
        {
            int matches = 0;

            if (string.Equals(onlineBeatmap.DifficultyName, localBeatmap.DifficultyName, StringComparison.OrdinalIgnoreCase))
                matches++;
            if (localBeatmap.BPM > 0 && onlineBeatmap.BPM > 0 && Math.Abs(onlineBeatmap.BPM - localBeatmap.BPM) < 0.01)
                matches++;
            if (Math.Abs(onlineBeatmap.DrainRate - localBeatmap.Difficulty.DrainRate) < 0.01f)
                matches++;
            if (Math.Abs(onlineBeatmap.CircleSize - localBeatmap.Difficulty.CircleSize) < 0.01f)
                matches++;
            if (Math.Abs(onlineBeatmap.ApproachRate - localBeatmap.Difficulty.ApproachRate) < 0.01f)
                matches++;
            if (Math.Abs(onlineBeatmap.OverallDifficulty - localBeatmap.Difficulty.OverallDifficulty) < 0.01f)
                matches++;

            return matches;
        }

        private static List<T> narrowCandidates<T>(List<T> candidates, Func<T, bool> predicate)
        {
            var narrowed = candidates.Where(predicate).ToList();
            return narrowed.Count > 0 ? narrowed : candidates;
        }

        private void logForModel(BeatmapSetInfo set, string message) =>
            RealmArchiveModelImporter<BeatmapSetInfo>.LogForModel(set, $@"[{nameof(APIBeatmapMetadataSource)}] {message}");

        public void Dispose()
        {
        }
    }
}
