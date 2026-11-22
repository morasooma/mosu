using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Game.Online.Hinamizawa;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;

namespace osu.Game.Beatmaps
{
    /// <summary>Catalogue-only preview. Gameplay must replace it with an archive-backed working beatmap.</summary>
    public sealed class HinamizawaPreviewBeatmap : WorkingBeatmap
    {
        private readonly ITrackStore tracks;
        private readonly LargeTextureStore textures;
        private readonly bool loadMedia;
        public HinamizawaPreviewBeatmap(BeatmapInfo info, ITrackStore tracks, LargeTextureStore textures, AudioManager audio, bool loadMedia = true)
            : base(info, audio)
        {
            this.loadMedia = loadMedia;
            this.tracks = tracks;
            this.textures = textures;
        }
        protected override IBeatmap GetBeatmap() => new Beatmap { BeatmapInfo = BeatmapInfo };
        public override IBeatmap GetPlayableBeatmap(IRulesetInfo ruleset, IReadOnlyList<Mod> mods, CancellationToken token)
            => throw new BeatmapInvalidForRulesetException("Download the selected mirror difficulty before gameplay.");
        public static string GetBackgroundUrl(int difficultyId) => $"{HinamizawaClient.HOST}/v3/osu/beatmaps/b/{difficultyId}/bg";
        public static string GetAudioUrl(int difficultyId) => $"{HinamizawaClient.HOST}/v3/osu/beatmaps/b/{difficultyId}/audio";
        public override Texture? GetBackground() => !loadMedia ? null : textures.Get(GetBackgroundUrl(BeatmapInfo.OnlineID));
        public override Texture? GetPanelBackground() => !loadMedia ? null : textures.Get($"https://assets.ppy.sh/beatmaps/{BeatmapSetInfo.OnlineID}/covers/list.jpg");
        public override Texture? GetLegacyPreviewBackground() => GetPanelBackground();
        private Track? previewTrack;
        private readonly object trackLock = new object();
        protected override Track? GetBeatmapTrack()
        {
            if (!loadMedia) return null;
            lock (trackLock)
                return previewTrack ??= tracks.Get(GetAudioUrl(BeatmapInfo.OnlineID));
        }
        protected internal override ISkin? GetSkin() => null;
        public override Stream? GetStream(string storagePath) => null;
    }
}
