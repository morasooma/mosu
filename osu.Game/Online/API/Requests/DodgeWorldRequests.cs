// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.IO.Network;

namespace osu.Game.Online.API.Requests
{
    public class GetDodgeWorldRequest : APIRequest<DodgeWorldApiResponse>
    {
        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/private/dodge-world";
        protected override string Target => throw new NotSupportedException();
    }

    public class ReplaceDodgeWorldRequest : APIRequest<DodgeWorldApiResponse>
    {
        private readonly long expectedRevision;
        private readonly JObject world;

        public ReplaceDodgeWorldRequest(long expectedRevision, string worldJson)
        {
            this.expectedRevision = expectedRevision;
            world = JObject.Parse(worldJson);
        }

        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/private/dodge-world";
        protected override string Target => throw new NotSupportedException();

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Put;
            request.ContentType = "application/json";
            request.AddRaw(new JObject
            {
                ["expected_revision"] = expectedRevision,
                ["world"] = world,
            }.ToString(Formatting.None));
            return request;
        }
    }

    public class UploadDodgeWorldAssetRequest : APIRequest<DodgeWorldAssetResponse>
    {
        private readonly byte[] content;
        private readonly string contentType;

        public UploadDodgeWorldAssetRequest(byte[] content, string contentType)
        {
            this.content = content;
            this.contentType = contentType;
        }

        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/private/dodge-world/assets";
        protected override string Target => throw new NotSupportedException();

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Post;
            request.ContentType = contentType;
            request.AddRaw(content);
            return request;
        }
    }

    /// <summary>
    /// Records that the player reached a story point.
    /// </summary>
    /// <remarks>
    /// Neither the flag nor its value is sent. The server reads them from the published world, as it does a
    /// warp's price, so this request can say "I finished with this object" and nothing more.
    /// </remarks>
    public class RaiseDodgeWorldStoryFlagRequest : APIRequest<DodgeWorldStoryApiResponse>
    {
        private readonly string roomId;
        private readonly string entityId;

        public RaiseDodgeWorldStoryFlagRequest(string roomId, string entityId)
        {
            this.roomId = roomId;
            this.entityId = entityId;
        }

        protected override string Uri => $@"{API!.Endpoints.APIUrl}/api/private/dodge-world/story/flag";
        protected override string Target => throw new NotSupportedException();

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Post;
            request.ContentType = "application/json";
            request.AddRaw(new JObject
            {
                ["room_id"] = roomId,
                ["entity_id"] = entityId,
            }.ToString(Formatting.None));
            return request;
        }
    }

    public class DodgeWorldStoryApiResponse
    {
        [JsonProperty("applied")]
        public bool Applied { get; set; }

        [JsonProperty("flags")]
        public Dictionary<string, long>? Flags { get; set; }

        /// <summary>
        /// What reaching this point paid, which is nothing unless <see cref="Applied"/> is true.
        /// </summary>
        [JsonProperty("experience_awarded")]
        public int ExperienceAwarded { get; set; }

        [JsonProperty("coins_awarded")]
        public int CoinsAwarded { get; set; }

        [JsonProperty("progression")]
        public DodgeWorldProgressionResponse? Progression { get; set; }
    }

    /// <summary>
    /// Pays a warp's price: to open it, or to travel to one already open.
    /// </summary>
    /// <remarks>
    /// The price is not sent. The server reads it from the published world, so a client cannot name
    /// its own.
    /// </remarks>
    public class DodgeWorldWarpRequest : APIRequest<DodgeWorldWarpApiResponse>
    {
        private readonly string roomId;
        private readonly string entityId;
        private readonly bool unlocking;

        public DodgeWorldWarpRequest(string roomId, string entityId, bool unlocking)
        {
            this.roomId = roomId;
            this.entityId = entityId;
            this.unlocking = unlocking;
        }

        protected override string Uri =>
            $@"{API!.Endpoints.APIUrl}/api/private/dodge-world/warps/{(unlocking ? "unlock" : "travel")}";

        protected override string Target => throw new NotSupportedException();

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Post;
            request.ContentType = "application/json";
            request.AddRaw(new JObject
            {
                ["room_id"] = roomId,
                ["entity_id"] = entityId,
            }.ToString(Formatting.None));
            return request;
        }
    }

    public class DodgeWorldApiResponse
    {
        [JsonProperty("revision")]
        public long Revision { get; set; }

        [JsonProperty("can_edit")]
        public bool CanEdit { get; set; }

        [JsonProperty("updated_at")]
        public DateTimeOffset UpdatedAt { get; set; }

        [JsonProperty("updated_by")]
        public long? UpdatedBy { get; set; }

        [JsonProperty("world")]
        public JObject World { get; set; } = new JObject();

        [JsonProperty("online_users")]
        public int OnlineUsers { get; set; }

        [JsonProperty("progression")]
        public DodgeWorldProgressionResponse? Progression { get; set; }

        [JsonProperty("unlocked_warps")]
        public DodgeWorldWarpReference[]? UnlockedWarps { get; set; }

        /// <summary>The player's story progress. Absent for a server that does not know about it yet.</summary>
        [JsonProperty("flags")]
        public Dictionary<string, long>? Flags { get; set; }
    }

    /// <summary>
    /// One warp the player has opened. Identified by room and entity id, since entity ids are only
    /// unique within a room.
    /// </summary>
    public class DodgeWorldWarpReference
    {
        [JsonProperty("room_id")]
        public string RoomId { get; set; } = string.Empty;

        [JsonProperty("entity_id")]
        public string EntityId { get; set; } = string.Empty;
    }

    public class DodgeWorldWarpApiResponse
    {
        [JsonProperty("ok")]
        public bool Ok { get; set; }

        [JsonProperty("reason")]
        public string? Reason { get; set; }

        [JsonProperty("coins_spent")]
        public int CoinsSpent { get; set; }

        [JsonProperty("progression")]
        public DodgeWorldProgressionResponse? Progression { get; set; }

        [JsonProperty("unlocked_warps")]
        public DodgeWorldWarpReference[]? UnlockedWarps { get; set; }
    }

    public class DodgeWorldAssetResponse
    {
        [JsonProperty("url")]
        public string Url { get; set; } = string.Empty;

        [JsonProperty("sha256")]
        public string Sha256 { get; set; } = string.Empty;

        [JsonProperty("width")]
        public int Width { get; set; }

        [JsonProperty("height")]
        public int Height { get; set; }

        [JsonProperty("content_type")]
        public string ContentType { get; set; } = string.Empty;
    }

    public class DodgeWorldProgressionResponse
    {
        [JsonProperty("level")]
        public int Level { get; set; }

        [JsonProperty("experience")]
        public long Experience { get; set; }

        [JsonProperty("experience_for_next_level")]
        public long ExperienceForNextLevel { get; set; }

        [JsonProperty("coins")]
        public long Coins { get; set; }
    }
}
