using System.Net.Http;
using osu.Framework.IO.Network;
using osu.Game.Online.API;

namespace osu.Game.Online.Rooms
{
    public sealed class SetPlaylistPinnedRequest : APIRequest
    {
        public long RoomId { get; }
        public bool Pinned { get; }

        public SetPlaylistPinnedRequest(long roomId, bool pinned)
        {
            RoomId = roomId;
            Pinned = pinned;
        }

        protected override string Target => $"rooms/{RoomId}/pin";

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = Pinned ? HttpMethod.Put : HttpMethod.Delete;
            return request;
        }
    }
}
