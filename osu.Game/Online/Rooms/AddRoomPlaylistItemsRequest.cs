using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;
using osu.Framework.IO.Network;
using osu.Game.Online.API;

namespace osu.Game.Online.Rooms
{
    public class AddRoomPlaylistItemsRequest : APIRequest<APICreatedRoom>
    {
        private readonly long roomId;
        private readonly IReadOnlyList<PlaylistItem> items;

        public AddRoomPlaylistItemsRequest(long roomId, IReadOnlyList<PlaylistItem> items)
        {
            this.roomId = roomId;
            this.items = items;
        }

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Post;
            request.ContentType = "application/json";
            request.AddRaw(JsonConvert.SerializeObject(new { playlist = items }));
            return request;
        }

        protected override string Target => $"rooms/{roomId}/playlist";
    }
}
