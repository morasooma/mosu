// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Database;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Configuration;

namespace osu.Game.Beatmaps
{
    public class BeatmapModelDownloader : ModelDownloader<BeatmapSetInfo, IBeatmapSetInfo>
    {
        private readonly OsuConfigManager config;
        private readonly IBeatmapApiProvider? beatmapApi;

        protected override ArchiveDownloadRequest<IBeatmapSetInfo> CreateDownloadRequest(IBeatmapSetInfo set, bool minimiseDownloadSize)
        {
            bool serverExclusive = set.OnlineID >= BeatmapApiProvider.SERVER_EXCLUSIVE_ID_THRESHOLD
                                   || set.IsServerExclusive();
            bool useOfficialService = beatmapApi?.OfficialApi?.IsAvailable == true
                                      && !serverExclusive;

            var request = new DownloadBeatmapSetRequest(set, minimiseDownloadSize)
            {
                // "Server" means whichever API the beatmap router selected. For normal maps this
                // is osu.ppy.sh; server-exclusive maps continue to honour the Mosu mirror setting.
                Mirror = useOfficialService || serverExclusive
                    ? DownloadMirror.Server
                    : config?.Get<DownloadMirror>(OsuSetting.BeatmapDownloadMirror) ?? DownloadMirror.Auto
            };

            return request;
        }

        public override ArchiveDownloadRequest<IBeatmapSetInfo>? GetExistingDownload(IBeatmapSetInfo model)
            => model == null ? null : CurrentDownloads.Find(r => r.Model?.OnlineID == model.OnlineID);

        public BeatmapModelDownloader(IModelImporter<BeatmapSetInfo> beatmapImporter, IAPIProvider api, OsuConfigManager config = null)
            : base(beatmapImporter, api)
        {
            this.config = config;
            beatmapApi = api as IBeatmapApiProvider;
        }
    }
}
