using System;
using System.IO;
using System.Linq;
using osu.Framework.Audio;
using osu.Framework.Audio.Sample;
using osu.Framework.Audio.Track;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Platform;
using osu.Game.Audio;
using osu.Game.Beatmaps.Formats;
using osu.Game.IO;
using osu.Game.Extensions;
using osu.Game.Models;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Hinamizawa;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Skinning;
using osu.Game.Storyboards;

namespace osu.Game.Beatmaps
{
    /// <summary>Playable extracted mirror archive, independent of BeatmapStore and the normal working-beatmap cache.</summary>
    public sealed class HinamizawaWorkingBeatmap : WorkingBeatmap, IDisposable
    {
        private readonly Beatmap decoded;
        private readonly string directory;
        private readonly string filename;
        private readonly StorageBackedResourceStore files;
        private readonly ITrackStore tracks;
        private readonly LargeTextureStore textures;
        private readonly ResourceProvider provider;

        public static HinamizawaWorkingBeatmap Create(string directory, APIBeatmap selected, RulesetInfo ruleset, AudioManager audio, GameHost host)
        {
            foreach (string path in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Where(p => p.EndsWith(".osu", StringComparison.OrdinalIgnoreCase)))
            {
                using var stream = File.OpenRead(path);
                string md5 = stream.ComputeMD5Hash();
                string sha = stream.ComputeSHA2Hash();
                using var reader = new LineBufferedReader(stream);
                var beatmap = Decoder.GetDecoder<Beatmap>(reader).Decode(reader);
                if (beatmap.BeatmapInfo.OnlineID != selected.OnlineID) continue;
                if (beatmap.BeatmapInfo.Ruleset.OnlineID != selected.RulesetID)
                    throw new InvalidDataException("The downloaded difficulty has a different ruleset than the selected catalogue entry.");
                if (beatmap.BeatmapInfo.BeatmapSet?.OnlineID is int setId && setId > 0 && selected.OnlineBeatmapSetID > 0 && setId != selected.OnlineBeatmapSetID)
                    throw new InvalidDataException("The downloaded difficulty belongs to a different beatmap set.");
                if (!string.IsNullOrEmpty(selected.Checksum) && !string.Equals(selected.Checksum, md5, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The downloaded difficulty differs from the mirror catalogue. Refresh the search before playing.");
                var info = beatmap.BeatmapInfo;
                string resourceDirectory = Path.GetDirectoryName(path)!;
                if (!string.IsNullOrEmpty(info.Metadata.AudioFile)) HinamizawaClient.ResolveArchivePath(resourceDirectory, info.Metadata.AudioFile);
                if (!string.IsNullOrEmpty(info.Metadata.BackgroundFile)) HinamizawaClient.ResolveArchivePath(resourceDirectory, info.Metadata.BackgroundFile);
                info.Hash = sha;
                info.MD5Hash = info.OnlineMD5Hash = md5;
                info.Ruleset = ruleset;
                info.Status = selected.Status;
                info.StarRating = selected.StarRating;
                info.LastOnlineUpdate = selected.LastUpdated;
                info.UpdateStatisticsFromBeatmap(beatmap);
                var set = new BeatmapSetInfo(new[] { info }) { OnlineID = selected.OnlineBeatmapSetID, Status = selected.Status };
                info.BeatmapSet = set;
                string localFilename = Path.GetFileName(path);
                set.Files.Add(new RealmNamedFileUsage(new RealmFile { Hash = sha }, localFilename));
                return new HinamizawaWorkingBeatmap(beatmap, Path.GetDirectoryName(path)!, localFilename, audio, host);
            }
            throw new InvalidDataException("The selected difficulty is missing from the downloaded set.");
        }

        private HinamizawaWorkingBeatmap(Beatmap beatmap, string directory, string filename, AudioManager audio, GameHost host)
            : base(beatmap.BeatmapInfo, audio)
        {
            decoded = beatmap;
            this.directory = directory;
            this.filename = filename;
            files = new StorageBackedResourceStore(new ArchiveStorage(directory));
            tracks = audio.GetTrackStore(files);
            textures = new LargeTextureStore(host.Renderer, host.CreateTextureLoaderStore(files));
            provider = new ResourceProvider(audio, files, host);
        }

        protected override IBeatmap GetBeatmap() => decoded;
        public override Texture? GetBackground() => string.IsNullOrEmpty(Metadata.BackgroundFile) ? null : textures.Get(Metadata.BackgroundFile);
        protected override Track? GetBeatmapTrack() => tracks.Get(Metadata.AudioFile);
        public override Stream? GetStream(string storagePath)
        {
            // Some consumers request a file-store hash path; map it back to its archive filename.
            string name = BeatmapSetInfo.Files.FirstOrDefault(f => BeatmapSetInfo.GetPathForFile(f.Filename) == storagePath)?.Filename ?? storagePath;
            string path = HinamizawaClient.ResolveArchivePath(directory, name);
            return File.Exists(path) ? File.OpenRead(path) : null;
        }

        protected override Storyboard GetStoryboard()
        {
            using var map = File.OpenRead(Path.Combine(directory, filename));
            using var reader = new LineBufferedReader(map);
            var decoder = Decoder.GetDecoder<Storyboard>(reader);
            string expected = (Metadata.Artist + " - " + Metadata.Title + " (" + Metadata.Author.Username + ").osb").GetValidFilename();
            string? osb = Directory.EnumerateFiles(directory).FirstOrDefault(p => string.Equals(Path.GetFileName(p), expected, StringComparison.OrdinalIgnoreCase));
            Storyboard result;
            if (osb != null)
            {
                using var storyboardStream = File.OpenRead(osb);
                using var storyboardReader = new LineBufferedReader(storyboardStream);
                result = decoder.Decode(reader, storyboardReader);
            }
            else result = decoder.Decode(reader);
            result.BeatmapInfo = BeatmapInfo;
            result.ResourceStore = files;
            return result;
        }

        protected internal override ISkin GetSkin() => new ArchiveSkin(BeatmapInfo, files, provider);

        private bool disposed;
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            CancelAsyncLoad();
            track?.Stop();
            track?.Dispose();
            textures.Dispose();
            tracks.Dispose();
            files.Dispose();
        }

        private sealed class ArchiveStorage(string root) : NativeStorage(root)
        {
            public override string GetFullPath(string path, bool createIfNotExisting = false)
            {
                if (string.IsNullOrEmpty(path)) return base.GetFullPath(path, createIfNotExisting);
                string safe = HinamizawaClient.ResolveArchivePath(root, path);
                return base.GetFullPath(Path.GetRelativePath(root, safe), createIfNotExisting);
            }
        }

        private sealed class ResourceProvider : IStorageResourceProvider
        {
            private readonly GameHost host;
            public ResourceProvider(AudioManager audio, IResourceStore<byte[]> files, GameHost host)
            {
                AudioManager = audio; Files = Resources = files; this.host = host;
            }
            public AudioManager AudioManager { get; }
            public IResourceStore<byte[]> Files { get; }
            public IResourceStore<byte[]> Resources { get; }
            public Database.RealmAccess RealmAccess => null!;
            public IRenderer Renderer => host.Renderer;
            public IResourceStore<TextureUpload> CreateTextureLoaderStore(IResourceStore<byte[]> store) => host.CreateTextureLoaderStore(store);
        }

        private sealed class ArchiveSkin : LegacySkin
        {
            public ArchiveSkin(BeatmapInfo info, IResourceStore<byte[]> files, IStorageResourceProvider provider)
                : base(new SkinInfo { Name = info.ToString(), Creator = info.Metadata.Author.Username }, provider, files, info.Path ?? "")
            {
                Configuration.AllowDefaultComboColoursFallback = false;
            }
            protected override bool AllowManiaConfigLookups => false;
            protected override bool UseCustomSampleBanks => true;
            protected override bool AllowHighResolutionSprites => false;
            public override ISample? GetSample(ISampleInfo info) => info is HitSampleInfo { UseBeatmapSamples: false } ? null : base.GetSample(info);
        }
    }
}
