using osu.Game.Online.API.Requests.Responses;

namespace osu.Game.Online.API.Requests
{
    /// <summary>
    /// Reads the durable score state, including whether its statistics projection has completed.
    /// </summary>
    public class GetScoreRequest : APIRequest<SoloScoreInfo>
    {
        public long ScoreId { get; }

        public GetScoreRequest(long scoreId)
        {
            ScoreId = scoreId;
        }

        protected override string Target => $"scores/{ScoreId}";
    }
}
