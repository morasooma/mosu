// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable warnings

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Rendering.Dummy;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Lists;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using osu.Game.Beatmaps.Formats;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Beatmaps
{
    public class WorkingBeatmapCache : IBeatmapResourceProvider, IWorkingBeatmapCache
    {
        private readonly WeakList<WorkingBeatmap> workingCache = new WeakList<WorkingBeatmap>();

        /// <summary>
        /// Beatmap files may specify this filename to denote that they don't have an audio track.
        /// </summary>
        private const string virtual_track_filename = @"virtual";

        /// <summary>
        /// A default representation of a WorkingBeatmap to use when no beatmap is available.
        /// </summary>
        public readonly WorkingBeatmap DefaultBeatmap;

        private readonly AudioManager audioManager;
        private readonly IResourceStore<byte[]> resources;
        private readonly LargeTextureStore largeTextureStore;
        private readonly LargeTextureStore beatmapPanelTextureStore;
        private readonly LargeTextureStore beatmapLegacyPreviewTextureStore;
        private readonly CarouselPreviewTextureStore stableBeatmapPanelTextureStore;
        private readonly CarouselPreviewTextureStore stableBeatmapLegacyPreviewTextureStore;
        private readonly ITrackStore trackStore;
        private readonly IResourceStore<byte[]> files;
        private readonly RealmAccess realm;

        [CanBeNull]
        private readonly GameHost? host;

        public WorkingBeatmapCache(ITrackStore trackStore, AudioManager audioManager, IResourceStore<byte[]> resources, IResourceStore<byte[]> files, WorkingBeatmap? defaultBeatmap = null,
                                   GameHost? host = null, RealmAccess? realm = null)
        {
            DefaultBeatmap = defaultBeatmap;

            this.audioManager = audioManager;
            this.resources = resources;
            this.host = host;
            this.files = files;
            largeTextureStore = new LargeTextureStore(host?.Renderer ?? new DummyRenderer(), host?.CreateTextureLoaderStore(files));
            beatmapPanelTextureStore = new CarouselPreviewTextureStore(host?.Renderer ?? new DummyRenderer(), new BeatmapPanelBackgroundTextureLoaderStore(host?.CreateTextureLoaderStore(files)));
            beatmapLegacyPreviewTextureStore = new CarouselPreviewTextureStore(host?.Renderer ?? new DummyRenderer(), new BeatmapLegacyPreviewBackgroundTextureLoaderStore(host?.CreateTextureLoaderStore(files)));
            stableBeatmapPanelTextureStore = new CarouselPreviewTextureStore(host?.Renderer ?? new DummyRenderer(),
                new BeatmapPanelBackgroundTextureLoaderStore(host?.CreateTextureLoaderStore(new AbsolutePathResourceStore())));
            stableBeatmapLegacyPreviewTextureStore = new CarouselPreviewTextureStore(host?.Renderer ?? new DummyRenderer(),
                new BeatmapLegacyPreviewBackgroundTextureLoaderStore(host?.CreateTextureLoaderStore(new AbsolutePathResourceStore())));
            this.trackStore = trackStore;
            this.realm = realm;
        }

        public void Invalidate(BeatmapSetInfo info)
        {
            foreach (var b in info.Beatmaps)
                Invalidate(b);
        }

        public void Invalidate(BeatmapInfo info)
        {
            lock (workingCache)
            {
                var working = workingCache.FirstOrDefault(w => info.Equals(w.BeatmapInfo));

                if (working != null)
                {
                    Logger.Log($"Invalidating working beatmap cache for {info}");
                    workingCache.Remove(working);
                    OnInvalidated?.Invoke(working);
                }
            }
        }

        public event Action<WorkingBeatmap> OnInvalidated;

        public virtual WorkingBeatmap GetWorkingBeatmap(BeatmapInfo? beatmapInfo)
        {
            if (beatmapInfo == null || ReferenceEquals(beatmapInfo, DefaultBeatmap.BeatmapInfo))
                return DefaultBeatmap;

            lock (workingCache)
            {
                var working = workingCache.FirstOrDefault(w => beatmapInfo.Equals(w.BeatmapInfo));

                if (working != null)
                    return working;

                beatmapInfo = beatmapInfo.Detach();

                // If this ever gets hit, a request has arrived with an outdated BeatmapInfo.
                // An outdated BeatmapInfo may contain a reference to a previous version of the beatmap's files on disk.
                Debug.Assert(confirmFileHashIsUpToDate(beatmapInfo), "working beatmap returned with outdated path");

                if (StablePathManager.IsStableBeatmap(beatmapInfo.ID))
                {
                    workingCache.Add(working = new StableWorkingBeatmap(beatmapInfo, audioManager, host,
                        stableBeatmapPanelTextureStore, stableBeatmapLegacyPreviewTextureStore));
                }
                else
                {
                    workingCache.Add(working = new BeatmapManagerWorkingBeatmap(beatmapInfo, this));
                }

                // best effort; may be higher than expected.
                GlobalStatistics.Get<int>("Beatmaps", $"Cached {nameof(WorkingBeatmap)}s").Value = workingCache.Count();

                return working;
            }
        }

        private bool confirmFileHashIsUpToDate(BeatmapInfo beatmapInfo)
        {
            string refetchPath = realm.Run(r => r.Find<BeatmapInfo>(beatmapInfo.ID)?.File?.File.Hash);
            return refetchPath == null || refetchPath == beatmapInfo.File?.File.Hash;
        }

        #region IResourceStorageProvider

        TextureStore IBeatmapResourceProvider.LargeTextureStore => largeTextureStore;
        TextureStore IBeatmapResourceProvider.BeatmapPanelTextureStore => beatmapPanelTextureStore;
        ITrackStore IBeatmapResourceProvider.Tracks => trackStore;
        IRenderer IStorageResourceProvider.Renderer => host?.Renderer ?? new DummyRenderer();
        AudioManager IStorageResourceProvider.AudioManager => audioManager;
        RealmAccess IStorageResourceProvider.RealmAccess => realm;
        IResourceStore<byte[]> IStorageResourceProvider.Files => files;
        IResourceStore<byte[]> IStorageResourceProvider.Resources => resources;
        IResourceStore<TextureUpload> IStorageResourceProvider.CreateTextureLoaderStore(IResourceStore<byte[]> underlyingStore) => host?.CreateTextureLoaderStore(underlyingStore);

        #endregion

        private class BeatmapManagerWorkingBeatmap : WorkingBeatmap
        {
            [NotNull]
            private readonly IBeatmapResourceProvider resources;

            public BeatmapManagerWorkingBeatmap(BeatmapInfo beatmapInfo, [NotNull] IBeatmapResourceProvider resources)
                : base(beatmapInfo, resources.AudioManager)
            {
                this.resources = resources;
            }

            protected override IBeatmap GetBeatmap()
            {
                var customFormat = BeatmapInfo.Ruleset.CreateInstance() as ICustomBeatmapFormat;
                string beatmapFilename = BeatmapInfo.Path;

                // Early custom-format builds stored a combined .osu + sidecar hash in BeatmapInfo.Hash.
                // Such a hash cannot resolve BeatmapInfo.File, so recover the primary file by matching
                // the legacy combined hash. The next editor save will persist the corrected .osu hash.
                if (beatmapFilename == null && customFormat != null)
                    beatmapFilename = findLegacyCustomBeatmapFilename();

                if (beatmapFilename == null)
                    return new Beatmap { BeatmapInfo = BeatmapInfo };

                try
                {
                    string fileStorePath = BeatmapSetInfo.GetPathForFile(beatmapFilename);

                    var stream = GetStream(fileStorePath);

                    if (stream == null)
                    {
                        Logger.Log($"Beatmap failed to load (file {beatmapFilename} not found on disk at expected location {fileStorePath}).", level: LogLevel.Error);
                        return null;
                    }

                    string streamMD5 = stream.ComputeMD5Hash();
                    string streamSHA2 = stream.ComputeSHA2Hash();

                    if (streamMD5 != BeatmapInfo.MD5Hash)
                    {
                        Logger.Log($"Beatmap failed to load (file {BeatmapInfo.Path} does not have the expected hash).", level: LogLevel.Error);
                        return null;
                    }

                    using (var reader = new LineBufferedReader(stream))
                    {
                        IBeatmap beatmap = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);

                        if (customFormat != null)
                        {
                            string sidecarFilename = CustomBeatmapFormat.GetSidecarFilename(beatmapFilename);
                            string sidecarStorePath = BeatmapSetInfo.GetPathForFile(sidecarFilename);

                            if (sidecarStorePath == null)
                            {
                                Logger.Log($"Beatmap failed to load (custom sidecar {sidecarFilename} was not found).", level: LogLevel.Error);
                                return null;
                            }

                            using var sidecarStream = GetStream(sidecarStorePath);

                            if (sidecarStream == null)
                            {
                                Logger.Log($"Beatmap failed to load (custom sidecar {sidecarFilename} could not be read).", level: LogLevel.Error);
                                return null;
                            }

                            beatmap.BeatmapInfo.Ruleset = BeatmapInfo.Ruleset;
                            beatmap = customFormat.DecodeSidecar(beatmap, sidecarStream);
                        }

                        beatmap.BeatmapInfo.MD5Hash = streamMD5;
                        beatmap.BeatmapInfo.Hash = streamSHA2;
                        beatmap.BeatmapInfo.UpdateStatisticsFromBeatmap(beatmap);

                        return beatmap;
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Beatmap failed to load");
                    return null;
                }
            }

            public override Texture GetPanelBackground() => getBackgroundFromStore(resources.BeatmapPanelTextureStore);

            public override Texture GetPanelBackground(int resolutionPercent) =>
                getBackgroundFromStore(resources.BeatmapPanelTextureStore, name => CarouselPreviewTextureRequest.Create(name, resolutionPercent));

            public override Texture GetLegacyPreviewBackground()
            {
                if (resources is WorkingBeatmapCache cache)
                    return getBackgroundFromStore(cache.beatmapLegacyPreviewTextureStore);

                return GetBackground();
            }

            public override Texture GetLegacyPreviewBackground(int resolutionPercent)
            {
                if (resources is WorkingBeatmapCache cache)
                {
                    return getBackgroundFromStore(cache.beatmapLegacyPreviewTextureStore,
                        name => CarouselPreviewTextureRequest.Create(name, resolutionPercent));
                }

                return GetLegacyPreviewBackground();
            }

            public override Texture GetBackground() => getBackgroundFromStore(resources.LargeTextureStore);

            private Texture getBackgroundFromStore(TextureStore store, Func<string, string> resourceNameTransform = null)
            {
                if (StablePathManager.IsStableBeatmap(BeatmapInfo.ID))
                    return getStableBackgroundFromStore(store, resourceNameTransform);

                if (string.IsNullOrEmpty(Metadata?.BackgroundFile))
                    return null;

                try
                {
                    string fileStorePath = BeatmapSetInfo.GetPathForFile(Metadata.BackgroundFile);
                    var texture = store.Get(resourceNameTransform?.Invoke(fileStorePath) ?? fileStorePath);

                    if (texture == null)
                    {
                        Logger.Log($"Beatmap background failed to load (file {Metadata.BackgroundFile} not found on disk at expected location {fileStorePath}).");
                        return null;
                    }

                    return texture;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Background failed to load");
                    return null;
                }
            }

            private string findLegacyCustomBeatmapFilename()
            {
                foreach (var osuFile in BeatmapSetInfo.Files.Where(f => f.Filename.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)))
                {
                    var sidecarFile = BeatmapSetInfo.GetFile(CustomBeatmapFormat.GetSidecarFilename(osuFile.Filename));

                    if (sidecarFile == null)
                        continue;

                    using var osuStream = GetStream(BeatmapSetInfo.GetPathForFile(osuFile.Filename));
                    using var sidecarStream = GetStream(sidecarFile.File.GetStoragePath());

                    if (osuStream == null || sidecarStream == null)
                        continue;

                    using var combinedStream = new MemoryStream();
                    osuStream.CopyTo(combinedStream);
                    sidecarStream.CopyTo(combinedStream);

                    if (combinedStream.ComputeSHA2Hash() == BeatmapInfo.Hash)
                        return osuFile.Filename;
                }

                return null;
            }

            private Texture? getStableBackgroundFromStore(TextureStore store, Func<string, string> resourceNameTransform = null)
            {
                if (!StablePathManager.TryGetPath(BeatmapInfo.ID, out string beatmapPath) || string.IsNullOrEmpty(beatmapPath) || !File.Exists(beatmapPath))
                    return null;

                string? beatmapDirectory = Path.GetDirectoryName(beatmapPath);

                if (string.IsNullOrEmpty(beatmapDirectory))
                    return null;

                string? backgroundFile = Metadata?.BackgroundFile;

                if (string.IsNullOrEmpty(backgroundFile))
                {
                    try
                    {
                        using var stream = File.OpenRead(beatmapPath);
                        using var reader = new LineBufferedReader(stream);
                        backgroundFile = Decoder.GetDecoder<Beatmap>(reader).Decode(reader).Metadata.BackgroundFile;
                    }
                    catch (Exception e)
                    {
                        Logger.Error(e, "Stable beatmap background metadata failed to load");
                        return null;
                    }
                }

                if (string.IsNullOrEmpty(backgroundFile))
                    return null;

                try
                {
                    var dataStore = new StorageBackedResourceStore(new NativeStorage(beatmapDirectory));
                    var textureLoaderStore = resources.CreateTextureLoaderStore(dataStore);

                    TextureStore stableStore;

                    if (ReferenceEquals(store, resources.BeatmapPanelTextureStore))
                        stableStore = new LargeTextureStore(resources.Renderer, new BeatmapPanelBackgroundTextureLoaderStore(textureLoaderStore));
                    else if (resources is WorkingBeatmapCache cache && ReferenceEquals(store, cache.beatmapLegacyPreviewTextureStore))
                        stableStore = new LargeTextureStore(resources.Renderer, new BeatmapLegacyPreviewBackgroundTextureLoaderStore(textureLoaderStore));
                    else
                        stableStore = new LargeTextureStore(resources.Renderer, textureLoaderStore);

                    var texture = stableStore.Get(resourceNameTransform?.Invoke(backgroundFile) ?? backgroundFile);

                    if (texture == null)
                        Logger.Log($"Stable beatmap background failed to load (file {backgroundFile} not found in {beatmapDirectory}).");

                    return texture;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Stable beatmap background failed to load");
                    return null;
                }
            }

            protected override Track GetBeatmapTrack()
            {
                if (string.IsNullOrEmpty(Metadata?.AudioFile))
                    return null;

                if (Metadata.AudioFile == virtual_track_filename)
                    return null;

                try
                {
                    string fileStorePath = BeatmapSetInfo.GetPathForFile(Metadata.AudioFile);
                    var track = resources.Tracks.Get(fileStorePath);

                    if (track == null)
                    {
                        Logger.Log($"Beatmap failed to load (file {Metadata.AudioFile} not found on disk at expected location {fileStorePath}).", level: LogLevel.Error);

                        if (resources is WorkingBeatmapCache cache && cache.host?.Dependencies?.Get(typeof(OsuConfigManager)) is OsuConfigManager config && config.Get<bool>(OsuSetting.ForkShowBeatmapsWithMissingAudio))
                            return new TrackVirtual(BeatmapInfo.Length);

                        return null;
                    }

                    return track;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Track failed to load");
                    return null;
                }
            }

            protected override Waveform GetWaveform()
            {
                if (string.IsNullOrEmpty(Metadata?.AudioFile))
                    return null;

                if (Metadata.AudioFile == virtual_track_filename)
                    return null;

                try
                {
                    string fileStorePath = BeatmapSetInfo.GetPathForFile(Metadata.AudioFile);

                    var trackData = GetStream(fileStorePath);

                    if (trackData == null)
                    {
                        Logger.Log($"Beatmap waveform failed to load (file {Metadata.AudioFile} not found on disk at expected location {fileStorePath}).", level: LogLevel.Error);
                        return null;
                    }

                    return new Waveform(trackData);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Waveform failed to load");
                    return null;
                }
            }

            protected override Storyboard GetStoryboard()
            {
                Storyboard storyboard;

                if (BeatmapInfo.Path == null)
                    return new Storyboard();

                try
                {
                    string fileStorePath = BeatmapSetInfo.GetPathForFile(BeatmapInfo.Path);
                    var beatmapFileStream = GetStream(fileStorePath);

                    if (beatmapFileStream == null)
                    {
                        Logger.Log($"Beatmap failed to load (file {BeatmapInfo.Path} not found on disk at expected location {fileStorePath})", level: LogLevel.Error);
                        return new Storyboard();
                    }

                    using (var reader = new LineBufferedReader(beatmapFileStream))
                    {
                        var decoder = Decoder.GetDecoder<Storyboard>(reader);

                        Stream storyboardFileStream = null;

                        string mainStoryboardFilename = getMainStoryboardFilename(BeatmapSetInfo.Metadata);

                        if (BeatmapSetInfo?.Files.FirstOrDefault(f => f.Filename.Equals(mainStoryboardFilename, StringComparison.OrdinalIgnoreCase))?.Filename is string
                            storyboardFilename)
                        {
                            string storyboardFileStorePath = BeatmapSetInfo?.GetPathForFile(storyboardFilename);
                            storyboardFileStream = GetStream(storyboardFileStorePath);

                            if (storyboardFileStream == null)
                                Logger.Log($"Storyboard failed to load (file {storyboardFilename} not found on disk at expected location {storyboardFileStorePath})", level: LogLevel.Error);
                        }

                        if (storyboardFileStream != null)
                        {
                            // Stand-alone storyboard was found, so parse in addition to the beatmap's local storyboard.
                            using (var secondaryReader = new LineBufferedReader(storyboardFileStream))
                                storyboard = decoder.Decode(reader, secondaryReader);
                        }
                        else
                            storyboard = decoder.Decode(reader);
                    }
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Storyboard failed to load");
                    storyboard = new Storyboard();
                }

                storyboard.BeatmapInfo = BeatmapInfo;

                return storyboard;
            }

            protected internal override ISkin GetSkin()
            {
                try
                {
                    return new LegacyBeatmapSkin(BeatmapInfo, resources);
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Skin failed to load");
                    return null;
                }
            }

            public override Stream GetStream(string storagePath) => resources.Files.GetStream(storagePath);

            private string getMainStoryboardFilename(IBeatmapMetadataInfo metadata)
            {
                // Matches stable implementation, because it's probably simpler than trying to do anything else.
                // This may need to be reconsidered after we begin storing storyboards in the new editor.
                string baseFilename = (metadata.Artist.Length > 0 ? metadata.Artist + @" - " + metadata.Title : Path.GetFileNameWithoutExtension(metadata.AudioFile))
                                      + (metadata.Author.Username.Length > 0 ? @" (" + metadata.Author.Username + @")" : string.Empty)
                                      + @".osb";
                return baseFilename.GetValidFilename();
            }
        }
    }
}
