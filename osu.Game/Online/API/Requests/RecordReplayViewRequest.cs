using System.Net.Http;
using osu.Framework.IO.Network;

namespace osu.Game.Online.API.Requests
{
    public class RecordReplayViewRequest : APIRequest
    {
        private readonly long scoreId;

        public RecordReplayViewRequest(long scoreId) => this.scoreId = scoreId;

        protected override string Target => $"scores/{scoreId}/replay-view";

        protected override WebRequest CreateWebRequest()
        {
            var request = base.CreateWebRequest();
            request.Method = HttpMethod.Post;
            return request;
        }
    }
}
