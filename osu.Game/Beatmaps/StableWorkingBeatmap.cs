// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Textures;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Formats;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Storyboards;

namespace osu.Game.Beatmaps
{
    public class StableWorkingBeatmap : WorkingBeatmap
    {
        private readonly string? beatmapPath;
        private readonly string? audioPath;
        private readonly AudioManager audioManager;
        private readonly LargeTextureStore? textureStore;
        private readonly LargeTextureStore? panelTextureStore;
        private readonly LargeTextureStore? legacyPreviewTextureStore;
        private readonly bool panelTextureStoreUsesAbsolutePaths;
        private readonly bool legacyPreviewTextureStoreUsesAbsolutePaths;
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

                string? backgroundFile = Metadata?.BackgroundFile;

                if (string.IsNullOrEmpty(backgroundFile))
                    return null;

                string? dir = Path.GetDirectoryName(beatmapPath);
                return string.IsNullOrEmpty(dir) ? null : Path.Combine(dir, backgroundFile);
            }
        }

        public StableWorkingBeatmap(BeatmapInfo beatmapInfo, AudioManager audioManager, osu.Framework.Platform.GameHost host,
                                    LargeTextureStore? sharedPanelTextureStore = null, LargeTextureStore? sharedLegacyPreviewTextureStore = null)
            : base(beatmapInfo, audioManager)
        {
            this.audioManager = audioManager;
            this.host = host;
            panelTextureStore = sharedPanelTextureStore;
            legacyPreviewTextureStore = sharedLegacyPreviewTextureStore;
            panelTextureStoreUsesAbsolutePaths = sharedPanelTextureStore != null;
            legacyPreviewTextureStoreUsesAbsolutePaths = sharedLegacyPreviewTextureStore != null;
            StablePathManager.TryGetPath(beatmapInfo.ID, out beatmapPath);
            StablePathManager.TryGetAudioPath(beatmapInfo.ID, out audioPath);

            if (!string.IsNullOrEmpty(beatmapPath))
            {
                string? dir = Path.GetDirectoryName(beatmapPath);

                if (!string.IsNullOrEmpty(dir))
                {
                    try
                    {
                        resourceStore = new osu.Framework.IO.Stores.StorageBackedResourceStore(new osu.Framework.Platform.NativeStorage(dir));
                        trackStore = audioManager.GetTrackStore(resourceStore);

                        if (host.Renderer != null)
                        {
                            var textureLoaderStore = host.CreateTextureLoaderStore(resourceStore);
                            textureStore = new LargeTextureStore(host.Renderer, textureLoaderStore);
                            if (panelTextureStore == null)
                                panelTextureStore = new LargeTextureStore(host.Renderer, new BeatmapPanelBackgroundTextureLoaderStore(textureLoaderStore));

                            if (legacyPreviewTextureStore == null)
                                legacyPreviewTextureStore = new LargeTextureStore(host.Renderer, new BeatmapLegacyPreviewBackgroundTextureLoaderStore(textureLoaderStore));
                        }
                    }
                    catch (System.Exception exception)
                    {
                        Logger.Error(exception, $"Stable beatmap storage failed to initialise for {dir}");
                    }
                }
            }
        }

        protected override IBeatmap GetBeatmap()
        {
            if (string.IsNullOrEmpty(beatmapPath) || !File.Exists(beatmapPath))
            {
                BeatmapInfo.Status = BeatmapOnlineStatus.LocallyModified;
                Logger.Log($"Stable beatmap file is unavailable: {beatmapPath}", LoggingTarget.Database, LogLevel.Error);
                return new Beatmap();
            }

            try
            {
                using (var stream = File.OpenRead(beatmapPath))
                {
                    string expectedMD5 = BeatmapInfo.MD5Hash;
                    string actualMD5 = stream.ComputeMD5Hash();
                    string actualSHA2 = stream.ComputeSHA2Hash();

                    using var reader = new osu.Game.IO.LineBufferedReader(stream);
                    var decoder = Decoder.GetDecoder<Beatmap>(reader);
                    var beatmap = decoder.Decode(reader);

                    BeatmapInfo.OnlineMD5Hash = expectedMD5;
                    BeatmapInfo.MD5Hash = actualMD5;
                    BeatmapInfo.Hash = actualSHA2;
                    beatmap.BeatmapInfo.OnlineMD5Hash = expectedMD5;
                    beatmap.BeatmapInfo.MD5Hash = actualMD5;
                    beatmap.BeatmapInfo.Hash = actualSHA2;

                    if (string.IsNullOrEmpty(expectedMD5) ||
                        !string.Equals(expectedMD5, actualMD5, System.StringComparison.OrdinalIgnoreCase))
                    {
                        BeatmapInfo.Status = BeatmapOnlineStatus.LocallyModified;
                        beatmap.BeatmapInfo.Status = BeatmapOnlineStatus.LocallyModified;
                        Logger.Log($"Stable beatmap checksum changed on disk: {beatmapPath}", LoggingTarget.Database, LogLevel.Important);
                    }

                    BeatmapInfo.UpdateStatisticsFromBeatmap(beatmap);
                    return beatmap;
                }
            }
            catch (System.Exception exception)
            {
                BeatmapInfo.Status = BeatmapOnlineStatus.LocallyModified;
                Logger.Error(exception, $"Stable beatmap failed to load: {beatmapPath}");
                return new Beatmap();
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

        public override Texture? GetBackground()
            => getBackground(textureStore);

        public override Texture? GetPanelBackground()
            => getBackground(panelTextureStore, useAbsolutePath: panelTextureStoreUsesAbsolutePaths) ?? GetBackground();

        public override Texture? GetPanelBackground(int resolutionPercent)
            => getBackground(panelTextureStore, resolutionPercent, panelTextureStoreUsesAbsolutePaths) ?? GetBackground();

        public override Texture? GetLegacyPreviewBackground()
            => getBackground(legacyPreviewTextureStore, useAbsolutePath: legacyPreviewTextureStoreUsesAbsolutePaths) ?? GetPanelBackground();

        public override Texture? GetLegacyPreviewBackground(int resolutionPercent)
            => getBackground(legacyPreviewTextureStore, resolutionPercent, legacyPreviewTextureStoreUsesAbsolutePaths) ?? GetPanelBackground(resolutionPercent);

        private Texture? getBackground(TextureStore? store, int? resolutionPercent = null, bool useAbsolutePath = false)
        {
            if (store == null || string.IsNullOrEmpty(beatmapPath))
                return null;

            string? bgFile = Metadata?.BackgroundFile;
            if (string.IsNullOrEmpty(bgFile))
                return null;

            try
            {
                string resourceName = useAbsolutePath ? BackgroundFilePath ?? bgFile : bgFile;
                var texture = store.Get(resolutionPercent.HasValue
                    ? CarouselPreviewTextureRequest.Create(resourceName, resolutionPercent.Value)
                    : resourceName);

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

        protected override Track? GetBeatmapTrack()
        {
            osu.Framework.Logging.Logger.Log($"StableWorkingBeatmap getting track for {audioPath}", osu.Framework.Logging.LoggingTarget.Database);

            if (trackStore == null || string.IsNullOrEmpty(audioPath) || !File.Exists(audioPath))
            {
                osu.Framework.Logging.Logger.Log($"Track not found or audioPath empty: {audioPath}", osu.Framework.Logging.LoggingTarget.Database);
                return null;
            }

            try
            {
                string file = Path.GetFileName(audioPath);
                var track = trackStore.Get(file);
                osu.Framework.Logging.Logger.Log($"Track created: {track != null}", osu.Framework.Logging.LoggingTarget.Database);
                return track;
            }
            catch (System.Exception exception)
            {
                Logger.Error(exception, $"Stable beatmap track failed to load: {audioPath}");
                return null;
            }
        }

        public override Stream? GetStream(string storagePath)
        {
            if (string.IsNullOrEmpty(beatmapPath))
                return null;

            string? dir = Path.GetDirectoryName(beatmapPath);
            if (string.IsNullOrEmpty(dir))
                return null;

            string fullPath = Path.Combine(dir, storagePath);

            try
            {
                if (File.Exists(fullPath))
                    return File.OpenRead(fullPath);
            }
            catch (System.Exception exception)
            {
                Logger.Error(exception, $"Stable beatmap resource failed to open: {fullPath}");
            }

            return null;
        }

        protected internal override osu.Game.Skinning.ISkin? GetSkin()
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
            public osu.Game.Database.RealmAccess RealmAccess => null!;
            public osu.Framework.Graphics.Rendering.IRenderer Renderer { get; }
            private readonly osu.Framework.Platform.GameHost host;

            public StableResourceProvider(osu.Framework.Audio.AudioManager audioManager, osu.Framework.IO.Stores.IResourceStore<byte[]> files, osu.Framework.Platform.GameHost host)
            {
                AudioManager = audioManager;
                Files = files;
                Resources = files;
                this.host = host;
                Renderer = host.Renderer;
            }

            public osu.Framework.IO.Stores.IResourceStore<osu.Framework.Graphics.Textures.TextureUpload> CreateTextureLoaderStore(osu.Framework.IO.Stores.IResourceStore<byte[]> underlyingStore)
                => host.CreateTextureLoaderStore(underlyingStore);
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
