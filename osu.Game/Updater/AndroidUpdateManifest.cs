// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;

namespace osu.Game.Updater
{
    internal sealed class AndroidUpdateManifest
    {
        [JsonProperty("schema_version")]
        public int SchemaVersion { get; set; }

        [JsonProperty("channel")]
        public string Channel { get; set; } = string.Empty;

        [JsonProperty("platform")]
        public string Platform { get; set; } = string.Empty;

        [JsonProperty("architecture")]
        public string Architecture { get; set; } = string.Empty;

        [JsonProperty("version")]
        public string Version { get; set; } = string.Empty;

        [JsonProperty("application_version")]
        public int ApplicationVersion { get; set; }

        [JsonProperty("file_name")]
        public string FileName { get; set; } = string.Empty;

        [JsonProperty("sha256")]
        public string Sha256 { get; set; } = string.Empty;

        [JsonProperty("size")]
        public long Size { get; set; }

        [JsonProperty("published_at")]
        public string PublishedAt { get; set; } = string.Empty;
    }
}
