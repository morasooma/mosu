using System;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using osu.Framework.IO.Network;
using osu.Game.Overlays.CustomTheme;

namespace osu.Game.Online.API.Requests
{
    public class CommunityThemeItem
    {
        [JsonProperty("id")] public long Id;
        [JsonProperty("owner_id")] public long OwnerId;
        [JsonProperty("author")] public string Author = string.Empty;
        [JsonProperty("name")] public string Name = string.Empty;
        [JsonProperty("revision")] public int Revision;
        [JsonProperty("preview")] public string Preview = string.Empty;
    }

    public class CommunityThemePage
    {
        [JsonProperty("items")] public CommunityThemeItem[] Items = Array.Empty<CommunityThemeItem>();
        [JsonProperty("total")] public int Total;
    }

    public class CommunityThemeMutation
    {
        [JsonProperty("id")] public long Id;
        [JsonProperty("revision")] public int Revision;
    }

    public class GetCommunityThemesRequest : APIRequest<CommunityThemePage>
    {
        private readonly string search;
        private readonly long? ownerId;
        private readonly int offset;
        public GetCommunityThemesRequest(string search, long? ownerId, int offset)
        {
            this.search = search;
            this.ownerId = ownerId;
            this.offset = offset;
        }
        protected override string Target => $"themes?search={System.Uri.EscapeDataString(search)}&offset={offset}&limit=12" + (ownerId.HasValue ? $"&ownerId={ownerId}" : "");
    }

    public class DownloadCommunityThemeRequest : APIRequest<JObject>
    {
        private readonly long id;
        public DownloadCommunityThemeRequest(long id) => this.id = id;
        protected override string Target => $"themes/{id}/download";
    }

    public class PublishCommunityThemeRequest : APIRequest<CommunityThemeMutation>
    {
        private readonly long? id;
        private readonly object payload;
        public PublishCommunityThemeRequest(string name, string requestKey, CustomThemeDefinition theme, byte[] preview, long? id = null, int revision = 0)
        {
            this.id = id;
            var portable = theme.DeepClone();
            portable.Extensions = null;
            if (portable.Geometry != null) portable.Geometry.Extensions = null;
            payload = new
            {
                request_key = requestKey, revision,
                package = new PortableThemePackage { Name = name, Theme = portable },
                preview = Convert.ToBase64String(preview),
            };
        }
        protected override string Target => id.HasValue ? $"themes/{id}" : "themes";
        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = id.HasValue ? HttpMethod.Put : HttpMethod.Post;
            request.ContentType = "application/json";
            request.AddRaw(JsonConvert.SerializeObject(payload));
            return request;
        }
    }

    public class DeleteCommunityThemeRequest : APIRequest<CommunityThemeMutation>
    {
        private readonly long id;
        private readonly int revision;
        public DeleteCommunityThemeRequest(long id, int revision) { this.id = id; this.revision = revision; }
        protected override string Target => $"themes/{id}?revision={revision}";
        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Delete;
            return request;
        }
    }
}
