// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;

namespace osu.Game.Online.API.Requests
{
    public class GetConfigBackupStatusRequest : APIRequest<ConfigBackupStatusResponse>
    {
        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/private/user/config-backup/status";
        protected override string Target => throw new NotSupportedException();
    }

    public class GetConfigBackupRequest : APIRequest<ConfigBackupResponse>
    {
        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/private/user/config-backup";
        protected override string Target => throw new NotSupportedException();
    }

    public class PutConfigBackupRequest : APIRequest<ConfigBackupStatusResponse>
    {
        private readonly ConfigBackupUpload upload;

        public PutConfigBackupRequest(ConfigBackupUpload upload)
        {
            this.upload = upload;
        }

        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/private/user/config-backup";
        protected override string Target => throw new NotSupportedException();

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Put;
            request.ContentType = "application/json";
            request.AddRaw(JsonConvert.SerializeObject(upload));
            return request;
        }
    }

    public class ConfigBackupUpload
    {
        [JsonProperty("format_version")]
        public int FormatVersion { get; set; }

        [JsonProperty("files")]
        public ConfigBackupFiles Files { get; set; } = new ConfigBackupFiles();
    }

    public class ConfigBackupFiles
    {
        [JsonProperty("input")]
        public string? Input { get; set; }

        [JsonProperty("game")]
        public string? Game { get; set; }

        [JsonProperty("mosu")]
        public string? Mosu { get; set; }

        [JsonProperty("server_profiles")]
        public string? ServerProfiles { get; set; }
    }

    public class ConfigBackupStatusResponse
    {
        [JsonProperty("exists")]
        public bool Exists { get; set; }

        [JsonProperty("updated_at")]
        public DateTimeOffset? UpdatedAt { get; set; }

        [JsonProperty("content_hash")]
        public string? ContentHash { get; set; }
    }

    public class ConfigBackupResponse
    {
        [JsonProperty("format_version")]
        public int FormatVersion { get; set; }

        [JsonProperty("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        [JsonProperty("content_hash")]
        public string ContentHash { get; set; } = string.Empty;

        [JsonProperty("files")]
        public ConfigBackupFiles Files { get; set; } = new ConfigBackupFiles();
    }
}
