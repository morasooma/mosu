// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics.Textures;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Storyboards;

namespace osu.Game.Beatmaps
{
    public class StableWorkingBeatmap : WorkingBeatmap
    {
        private readonly string beatmapPath;
        private readonly string audioPath;
        private readonly AudioManager audioManager;
        private readonly LargeTextureStore? textureStore;
        private readonly LargeTextureStore? panelTextureStore;
        private readonly LargeTextureStore? legacyPreviewTextureStore;
        private readonly osu.Framework.Audio.Track.ITrackStore? trackStore;
        private readonly osu.Framework.IO.Stores.IResourceStore<byte[]>? resourceStore;
        private readonly osu.Framework.Platform.GameHost host;

        // Exposed for cross-instance comparison in TryTransferTrack
        internal string? AudioFilePath => audioPath;
        internal string? BackgroundFilePath
        {
            get
            {
                if (string.IsNullOrEmpty(beatmapPath))
                    return null;

                string? backgroundFile = Beatmap?.Metadata?.BackgroundFile;

                if (string.IsNullOrEmpty(backgroundFile))
                    return null;

                string? dir = Path.GetDirectoryName(beatmapPath);
                return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, backgroundFile);
            }
        }

        public StableWorkingBeatmap(BeatmapInfo beatmapInfo, AudioManager audioManager, osu.Framework.Platform.GameHost host)
            : base(beatmapInfo, audioManager)
        {
            this.audioManager = audioManager;
            this.host = host;
            StablePathManager.TryGetPath(beatmapInfo.ID, out beatmapPath);
            StablePathManager.TryGetAudioPath(beatmapInfo.ID, out audioPath);

            if (!string.IsNullOrEmpty(beatmapPath))
            {
                string? dir = Path.GetDirectoryName(beatmapPath);

                if (!string.IsNullOrEmpty(dir))
                {
                    resourceStore = new osu.Framework.IO.Stores.StorageBackedResourceStore(new osu.Framework.Platform.NativeStorage(dir));
                    trackStore = audioManager.GetTrackStore(resourceStore);

                    if (host?.Renderer != null)
                    {
                        var textureLoaderStore = host.CreateTextureLoaderStore(resourceStore);
                        textureStore = new LargeTextureStore(host.Renderer, textureLoaderStore);
                        panelTextureStore = new LargeTextureStore(host.Renderer, new BeatmapPanelBackgroundTextureLoaderStore(textureLoaderStore));
                        legacyPreviewTextureStore = new LargeTextureStore(host.Renderer, new BeatmapLegacyPreviewBackgroundTextureLoaderStore(textureLoaderStore));
                    }
                }
            }
        }

        protected override IBeatmap GetBeatmap()
        {
            if (string.IsNullOrEmpty(beatmapPath) || !File.Exists(beatmapPath))
                return new Beatmap();

            using (var stream = File.OpenRead(beatmapPath))
            using (var reader = new osu.Game.IO.LineBufferedReader(stream))
            {
                var decoder = Decoder.GetDecoder<Beatmap>(reader);
                var beatmap = decoder.Decode(reader);
                BeatmapInfo.UpdateStatisticsFromBeatmap(beatmap);
                return beatmap;
            }
        }

        protected override Storyboard GetStoryboard()
        {
            if (string.IsNullOrEmpty(beatmapPath) || !File.Exists(beatmapPath))
                return createEmptyStoryboard();

            try
            {
                using var beatmapStream = File.OpenRead(beatmapPath);
                using var beatmapReader = new osu.Game.IO.LineBufferedReader(beatmapStream);
                var decoder = Decoder.GetDecoder<Storyboard>(beatmapReader);

                string? storyboardPath = getMainStoryboardPath();
                Storyboard storyboard;

                if (storyboardPath != null)
                {
                    using var storyboardStream = File.OpenRead(storyboardPath);
                    using var storyboardReader = new osu.Game.IO.LineBufferedReader(storyboardStream);
                    storyboard = decoder.Decode(beatmapReader, storyboardReader);
                }
                else
                    storyboard = decoder.Decode(beatmapReader);

                storyboard.BeatmapInfo = BeatmapInfo;
                storyboard.ResourceStore = resourceStore;
                return storyboard;
            }
            catch (System.Exception e)
            {
                osu.Framework.Logging.Logger.Error(e, "Stable beatmap storyboard failed to load");
                return createEmptyStoryboard();
            }
        }

        private Storyboard createEmptyStoryboard() => new Storyboard
        {
            BeatmapInfo = BeatmapInfo,
            Beatmap = Beatmap,
            ResourceStore = resourceStore,
        };

        private string? getMainStoryboardPath()
        {
            string? directory = Path.GetDirectoryName(beatmapPath);

            if (string.IsNullOrEmpty(directory))
                return null;

            string filename = (Metadata.Artist.Length > 0 ? Metadata.Artist + @" - " + Metadata.Title : Path.GetFileNameWithoutExtension(Metadata.AudioFile))
                              + (Metadata.Author.Username.Length > 0 ? @" (" + Metadata.Author.Username + @")" : string.Empty)
                              + @".osb";
            filename = filename.GetValidFilename();

            return Directory.EnumerateFiles(directory)
                            .FirstOrDefault(path => string.Equals(Path.GetFileName(path), filename, System.StringComparison.OrdinalIgnoreCase));
        }

        public override Texture GetBackground()
            => getBackground(textureStore);

        public override Texture GetPanelBackground()
            => getBackground(panelTextureStore) ?? GetBackground();

        public override Texture GetLegacyPreviewBackground()
            => getBackground(legacyPreviewTextureStore) ?? GetPanelBackground();

        private Texture? getBackground(TextureStore? store)
        {
            if (store == null || string.IsNullOrEmpty(beatmapPath))
                return null;

            string? bgFile = Beatmap?.Metadata?.BackgroundFile;
            if (string.IsNullOrEmpty(bgFile))
                return null;

            try
            {
                var texture = store.Get(bgFile);

                if (texture == null)
                    osu.Framework.Logging.Logger.Log($"Stable beatmap background failed to load: {bgFile} from {BackgroundFilePath}", osu.Framework.Logging.LoggingTarget.Database);

                return texture;
            }
            catch (System.Exception e)
            {
                osu.Framework.Logging.Logger.Error(e, $"Stable beatmap background failed to load: {bgFile} from {BackgroundFilePath}");
                return null;
            }
        }

        protected override Track GetBeatmapTrack()
        {
            osu.Framework.Logging.Logger.Log($"StableWorkingBeatmap getting track for {audioPath}", osu.Framework.Logging.LoggingTarget.Database);

            if (trackStore == null || string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath))
            {
                osu.Framework.Logging.Logger.Log($"Track not found or audioPath empty: {audioPath}", osu.Framework.Logging.LoggingTarget.Database);
                return null;
            }

            string file = Path.GetFileName(audioPath);
            var track = trackStore.Get(file);
            osu.Framework.Logging.Logger.Log($"Track created: {track != null}", osu.Framework.Logging.LoggingTarget.Database);
            return track;
        }

        public override Stream GetStream(string storagePath)
        {
            if (string.IsNullOrEmpty(beatmapPath))
                return null;

            string? dir = Path.GetDirectoryName(beatmapPath);
            if (string.IsNullOrEmpty(dir))
                return null;

            string fullPath = Path.Combine(dir, storagePath);

            if (File.Exists(fullPath))
                return File.OpenRead(fullPath);

            return null;
        }

        protected internal override osu.Game.Skinning.ISkin GetSkin()
        {
            if (resourceStore == null) return null;
            var provider = new StableResourceProvider(audioManager, resourceStore, host);
            return new StableBeatmapSkin(BeatmapInfo, resourceStore, provider);
        }

        private class StableResourceProvider : osu.Game.IO.IStorageResourceProvider
        {
            public osu.Framework.Audio.AudioManager AudioManager { get; }
            public osu.Framework.IO.Stores.IResourceStore<byte[]> Files { get; }
            public osu.Framework.IO.Stores.IResourceStore<byte[]> Resources { get; }
            public osu.Game.Database.RealmAccess RealmAccess => null;
            public osu.Framework.Graphics.Rendering.IRenderer Renderer { get; }
            private readonly osu.Framework.Platform.GameHost host;

            public StableResourceProvider(osu.Framework.Audio.AudioManager audioManager, osu.Framework.IO.Stores.IResourceStore<byte[]> files, osu.Framework.Platform.GameHost host)
            {
                AudioManager = audioManager;
                Files = files;
                Resources = files;
                this.host = host;
                Renderer = host?.Renderer;
            }

            public osu.Framework.IO.Stores.IResourceStore<osu.Framework.Graphics.Textures.TextureUpload> CreateTextureLoaderStore(osu.Framework.IO.Stores.IResourceStore<byte[]> underlyingStore)
                => host?.CreateTextureLoaderStore(underlyingStore);
        }

        private class StableBeatmapSkin : osu.Game.Skinning.LegacySkin
        {
            public StableBeatmapSkin(BeatmapInfo beatmapInfo, osu.Framework.IO.Stores.IResourceStore<byte[]> store, StableResourceProvider provider)
                : base(new osu.Game.Skinning.SkinInfo
                {
                    Name = beatmapInfo.ToString(),
                    Creator = beatmapInfo.Metadata.Author.Username
                }, provider, store, beatmapInfo.Path ?? string.Empty)
            {
                Configuration.AllowDefaultComboColoursFallback = false;
            }

            protected override bool AllowManiaConfigLookups => false;
            protected override bool UseCustomSampleBanks => true;
            protected override bool AllowHighResolutionSprites => false;

            public override osu.Framework.Audio.Sample.ISample? GetSample(osu.Game.Audio.ISampleInfo sampleInfo)
            {
                if (sampleInfo is osu.Game.Audio.HitSampleInfo hitSampleInfo && !hitSampleInfo.UseBeatmapSamples)
                    return null;

                return base.GetSample(sampleInfo);
            }
        }

        /// <summary>
        /// For stable beatmaps, AudioEquals always returns true because BeatmapSet files are empty (null==null).
        /// Override TryTransferTrack to compare by actual audio file path instead.
        /// This prevents the same track being reused when switching between different stable songs.
        /// </summary>
        public override bool TryTransferTrack(WorkingBeatmap target)
        {
            if (target is StableWorkingBeatmap stableTarget)
            {
                // Only transfer if they share the same audio file (same song folder + audio file name)
                if (audioPath != stableTarget.AudioFilePath)
                    return false;
                // Same audio file path — safe to transfer (base would also succeed but we confirm here)
                return !Track.IsDummyDevice && (stableTarget.track = Track) != null;
            }

            // Not a stable beatmap target — fall back to default behaviour
            return base.TryTransferTrack(target);
        }
    }
}
