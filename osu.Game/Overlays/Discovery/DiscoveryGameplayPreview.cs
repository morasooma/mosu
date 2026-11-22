using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Track;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.IO.Stores;
using osu.Framework.Logging;
using osu.Framework.Timing;
using osu.Game.Audio;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;

namespace osu.Game.Overlays.Discovery
{
    /// <summary>One native, input-isolated playfield, driven by the short audio clip's clock.</summary>
    public partial class DiscoveryGameplayPreview : CompositeDrawable, IPreviewTrackOwner
    {
        private readonly IBeatmap map;
        private readonly byte[]? audioBytes;
        private readonly RulesetInfo rulesetInfo;
        private readonly CancellationToken cancellation;
        private ITrackStore? tracks;
        private ClipPreviewTrack? preview;
        private DrawableRuleset? drawable;
        private readonly Container playfield;
        private bool ready;
        private bool playing = true;
        private readonly bool fullAudio;
        private readonly double audioAnchor;
        private double previousPosition;
        private int diagnosticFrames;
        public double Duration => preview?.Length ?? 0;
        public double ClipPosition => preview?.ClipTime ?? 0;
        public bool FullAudio => fullAudio;
        internal bool HasGameplay => fullAudio || map.Metadata.PreviewTime >= 0;
        internal int VisibleHitObjects => drawable?.Playfield.HitObjectContainer.AliveObjects.Count(h => h.IsPresent && h.DrawWidth > 0 && h.DrawHeight > 0) ?? 0;
        [Resolved] private PreviewTrackManager previewManager { get; set; } = null!;

        public bool Playing => playing;
        public double MapTime => audioAnchor + (preview?.ClipTime ?? 0);
        public override bool PropagatePositionalInputSubTree => false;
        public override bool PropagateNonPositionalInputSubTree => false;

        internal DiscoveryGameplayPreview(IBeatmap map, byte[]? audioBytes, RulesetInfo ruleset, CancellationToken cancellation = default, bool fullAudio = false)
        {
            this.map = map;
            this.audioBytes = audioBytes;
            rulesetInfo = ruleset;
            this.cancellation = cancellation;
            this.fullAudio = fullAudio;
            audioAnchor = fullAudio ? 0 : Math.Max(0, map.Metadata.PreviewTime);
            RelativeSizeAxes = Axes.Both;
            InternalChild = playfield = new Container { RelativeSizeAxes = Axes.Both };
        }

        [BackgroundDependencyLoader]
        private void load(AudioManager audio, OsuConfigManager config)
        {
            tracks = audio.GetTrackStore(new ClipStore(audioBytes));
            preview = new ClipPreviewTrack(map.BeatmapInfo.BeatmapSet!, tracks, audioBytes == null);
            previewManager.Register(preview);
            mapClock = new FramedOffsetClock(new ClipClock(preview)) { Offset = audioAnchor };
            playfield.Clock = mapClock;
            if (HasGameplay)
            {
                var ruleset = rulesetInfo.CreateInstance();
                var autoplay = ruleset.GetAutoplayMod() ?? throw new InvalidOperationException("Autoplay is unavailable for this mode.");
                Mod[] mods = { autoplay };
                var playable = new FlatWorkingBeatmap(map).GetPlayableBeatmap(rulesetInfo, mods, cancellation);
                drawable = ruleset.CreateDrawableRulesetWith(playable, mods);
                drawable.FrameStablePlayback = false;
                drawable.Audio.Volume.Value = 0;
                playfield.Child = new RulesetSkinProvidingContainer(ruleset, playable, null)
                {
                    RelativeSizeAxes = Axes.Both,
                    BeatmapSkins = { Current = config.GetBindable<bool>(OsuSetting.BeatmapSkins) },
                    BeatmapColours = { Current = config.GetBindable<bool>(OsuSetting.BeatmapColours) },
                    BeatmapHitsounds = { Value = false },
                    Child = drawable,
                };
                replay = autoplay.CreateScoreFromReplayData(playable, mods);
            }
            // PreviewTrack is explicitly long-running: load it directly and observe any scheduled
            // failure here on the worker, before attaching it to the ordinary gameplay subtree.
            var completion = new osu.Framework.Threading.Scheduler();
            LoadComponentAsync(preview, cancellation: cancellation, scheduler: completion).GetAwaiter().GetResult();
            completion.Update();
            cancellation.ThrowIfCancellationRequested();
            // Add the clip as a sibling so gameplay-volume muting never mutes the music.
            AddInternal(preview);
            cancellation.ThrowIfCancellationRequested();
        }

        private Scoring.Score? replay;
        private FramedOffsetClock? mapClock;
        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (preview == null) return;
            if (drawable != null)
            {
                drawable.SetReplayScore(replay);
                drawable.Playfield.DisplayJudgements.Value = false;
            }
            ready = true;
            if (fullAudio) preview.SeekClip(Math.Max(0, map.Metadata.PreviewTime));
            if (playing) preview.ResumeClip();
        }

        internal void StopPlayback() => preview?.Stop();

        public void Seek(double position)
        {
            if (preview == null) return;
            preview.SeekClip(Math.Clamp(position, 0, Math.Max(0, preview.Length - 1)));
            if (drawable != null) drawable.FrameStablePlayback = false;
        }

        public void TogglePlayback()
        {
            playing = !playing;
            if (!ready || preview == null) return;
            if (playing) preview.ResumeClip();
            else preview.PauseClip();
        }

        protected override void Update()
        {
            base.Update();
            if (ready && drawable != null && mapClock != null)
            {
                bool rewound = ClipPosition < previousPosition - 500;
                if (rewound) drawable.SetReplayScore(replay);
                drawable.FrameStablePlayback = playing && !rewound && Math.Abs(drawable.FrameStableClock.CurrentTime - mapClock.CurrentTime) <= 1000;
                previousPosition = ClipPosition;
                if (++diagnosticFrames == 120)
                {
                    var objects = drawable.Playfield.HitObjectContainer.AliveObjects.Take(4).ToArray();
                    Logger.Log($"Discovery render map={map.BeatmapInfo.OnlineID} anchor={audioAnchor:F0} clip={ClipPosition:F0} "
                               + $"gameplay={drawable.FrameStableClock.CurrentTime:F0} objects={drawable.Objects.Count()} alive={VisibleHitObjects} "
                               + $"size={DrawSize} notes=[{string.Join("; ", objects.Select(h => $"{h.GetType().Name} alpha={h.Alpha:F2} time={h.Clock.CurrentTime:F0} size={h.DrawSize}"))}]",
                        LoggingTarget.Runtime);
                }
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            preview?.Stop();
            preview?.Dispose();
            base.Dispose(isDisposing);
            tracks?.Dispose();
        }

        private sealed class ClipClock(ClipPreviewTrack track) : IClock
        {
            public double CurrentTime => track.ClipTime;
            public double Rate => 1;
            public bool IsRunning => track.IsRunning;
        }

        private partial class ClipPreviewTrack : PreviewTrackManager.TrackManagerPreviewTrack
        {
            private readonly ITrackStore clipTracks;
            private readonly bool silent;
            internal ClipPreviewTrack(IBeatmapSetInfo set, ITrackStore tracks, bool silent) : base(set, tracks)
            {
                clipTracks = tracks;
                this.silent = silent;
            }
            private double pausedAt;
            private bool paused;
            internal double ClipTime => paused ? pausedAt : CurrentTime;
            protected override Track GetTrack() => silent ? clipTracks.GetVirtual(10000) : clipTracks.Get("clip");
            internal void PauseClip()
            {
                pausedAt = CurrentTime;
                paused = true;
                Stop();
            }
            internal void SeekClip(double position)
            {
                pausedAt = position;
                Track?.Seek(position);
            }
            internal void ResumeClip()
            {
                Start();
                Schedule(() => { Track?.Seek(pausedAt); paused = false; });
            }
            protected override void LoadComplete()
            {
                base.LoadComplete();
                Looping = true;
            }
        }

        private sealed class ClipStore(byte[]? bytes) : IResourceStore<byte[]>
        {
            public byte[] Get(string name) => bytes!;
            public Task<byte[]> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult(bytes!);
            public Stream? GetStream(string name) => bytes == null ? null : new MemoryStream(bytes, false);
            public IEnumerable<string> GetAvailableResources() => new[] { "clip" };
            public void Dispose() { }
        }
    }
}
