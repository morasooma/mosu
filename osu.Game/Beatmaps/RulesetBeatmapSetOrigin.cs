// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;

namespace osu.Game.Beatmaps
{
    /// <summary>
    /// Provenance stored alongside a locally separated ruleset beatmap set.
    /// The marker allows another difficulty created from the same source set to
    /// be routed back into the already separated set.
    /// </summary>
    public sealed class RulesetBeatmapSetOrigin
    {
        public const string FORMAT = "mosu-ruleset-beatmap-origin";
        public const int VERSION = 2;

        private const string filename_prefix = ".mosu-ruleset-origin";

        [JsonProperty("format")]
        public string Format { get; set; } = FORMAT;

        [JsonProperty("version")]
        public int Version { get; set; } = VERSION;

        [JsonProperty("target_ruleset_id")]
        public int TargetRulesetOnlineID { get; set; }

        [JsonProperty("source_beatmapset_local_id")]
        public Guid SourceBeatmapSetLocalID { get; set; }

        [JsonProperty("source_beatmapset_id")]
        public int SourceBeatmapSetOnlineID { get; set; } = -1;

        [JsonProperty("source_beatmap_id")]
        public int SourceBeatmapOnlineID { get; set; } = -1;

        [JsonProperty("source_ruleset_id")]
        public int SourceRulesetOnlineID { get; set; } = -1;

        [JsonProperty("source_beatmapset_url")]
        public string? SourceBeatmapSetUrl { get; set; }

        [JsonProperty("original_author_id")]
        public int OriginalAuthorOnlineID { get; set; }

        [JsonProperty("original_author")]
        public string OriginalAuthorUsername { get; set; } = string.Empty;

        [JsonProperty("mapper_id")]
        public int MapperOnlineID { get; set; }

        [JsonProperty("mapper")]
        public string MapperUsername { get; set; } = string.Empty;

        [JsonProperty("created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        public static string GetFilename(Guid sourceBeatmapSetLocalID, int sourceBeatmapSetOnlineID, int targetRulesetOnlineID)
            => $"{filename_prefix}-{targetRulesetOnlineID}-{sourceBeatmapSetLocalID:N}-{sourceBeatmapSetOnlineID}.json";

        public static bool IsOriginFilename(string filename)
            => filename.StartsWith($"{filename_prefix}-", StringComparison.OrdinalIgnoreCase)
               && filename.EndsWith(".json", StringComparison.OrdinalIgnoreCase);

        public static bool IsTransferableResourceFilename(string filename)
            => !filename.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)
               && !filename.EndsWith(Formats.CustomBeatmapFormat.SIDECAR_EXTENSION, StringComparison.OrdinalIgnoreCase)
               && !IsOriginFilename(filename);

        public static bool MatchesSource(
            string filename,
            Guid sourceBeatmapSetLocalID,
            int sourceBeatmapSetOnlineID,
            int targetRulesetOnlineID)
        {
            if (!IsOriginFilename(filename)
                || !filename.StartsWith($"{filename_prefix}-{targetRulesetOnlineID}-", StringComparison.OrdinalIgnoreCase))
                return false;

            string localID = sourceBeatmapSetLocalID.ToString("N");

            if (filename.Contains($"-{localID}-", StringComparison.OrdinalIgnoreCase))
                return true;

            return sourceBeatmapSetOnlineID > 0
                   && filename.EndsWith($"-{sourceBeatmapSetOnlineID}.json", StringComparison.OrdinalIgnoreCase);
        }
    }
}
