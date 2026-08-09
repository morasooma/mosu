// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Net.Http;
using osu.Framework.IO.Network;
using osu.Game.Online.API;

namespace osu.Game.Online.Rooms
{
    public class PartUserFromRoomRequest : APIRequest
    {
        private readonly long roomId;
        private readonly long userId;

        public PartUserFromRoomRequest(long roomId, long userId)
        {
            this.roomId = roomId;
            this.userId = userId;
        }

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Delete;
            return request;
        }

        protected override string Target => $@"rooms/{roomId}/users/{userId}";
    }
}
