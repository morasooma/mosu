// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Audio.Mixing;
using osu.Framework.Configuration;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Timing;
using osu.Framework.Input;
using osu.Game.Configuration;
using osu.Game.Beatmaps;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Scoring;
using osu.Game.Screens;

namespace osu.Game.Scoring.Render
{
    /// <summary>
    /// A minimal game instance that boots silently in the background, loads a specific
    /// replay, and renders it to a video file via <see cref="RenderReplayPlayer"/>.
    /// </summary>
    public partial class ReplayRenderGame : OsuGameBase
    {
        protected override bool EnableOfficialBeatmapIntegration => false;

        private readonly RenderGameplaySampleTriggerRecorder gameplaySampleTriggerRecorder = new RenderGameplaySampleTriggerRecorder();
        private readonly string[] args;
        private readonly Size renderSize;
        private readonly ManualClock renderSceneSourceClock = new ManualClock { IsRunning = true, Rate = 1 };
        private readonly FramedClock renderSceneClock;
        private OsuScreenStack screenStack = null!;
        private AudioMixer? renderSampleCaptureMixer;

        [Resolved]
        private FrameworkConfigManager frameworkConfig { get; set; } = null!;

        internal Drawable RenderCaptureTarget => screenStack;
        internal AudioMixer? RenderSampleCaptureMixer => renderSampleCaptureMixer;
        internal IRenderGameplaySampleTriggerRecorder GameplaySampleTriggerRecorder => gameplaySampleTriggerRecorder;

        public ReplayRenderGame(string[] args)
        {
            this.args = args;
            renderSize = parseResolution(args);
            renderSceneClock = new FramedClock(renderSceneSourceClock, processSource: false);
        }

        protected override IDictionary<FrameworkSetting, object> GetFrameworkConfigDefaults()
        {
            var defaults = base.GetFrameworkConfigDefaults() ?? new Dictionary<FrameworkSetting, object>();

            // The replay renderer runs hidden in the background.
            // Leaving it on the user's default fullscreen path can cause an unnecessary display mode
            // transition and GPU/window focus contention with the interactive client.
            defaults[FrameworkSetting.WindowMode] = WindowMode.Windowed;
            defaults[FrameworkSetting.WindowedSize] = renderSize;
            defaults[FrameworkSetting.ConfineMouseMode] = ConfineMouseMode.Never;
            defaults[FrameworkSetting.MinimiseOnFocusLossInFullscreen] = false;

            return defaults;
        }

        /// <summary>
        /// Prevents the framework config manager from persisting any settings to disk by flipping
        /// its internal hasLoaded flag to false (via reflection). This makes Save() a no-op, which
        /// prevents both the 100ms deferred QueueBackgroundSave and the Dispose()→Save() call from
        /// writing temporary render settings to framework.ini.
        /// </summary>
        private void suppressFrameworkConfigSave()
        {
            try
            {
                var configType = frameworkConfig.GetType().BaseType?.BaseType;
                if (configType == null) return;

                var hasLoadedField = configType.GetField("hasLoaded", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (hasLoadedField != null)
                {
                    hasLoadedField.SetValue(frameworkConfig, false);
                    Logger.Log("[ReplayRenderGame] Suppressed framework config persistence (hasLoaded=false).",
                        LoggingTarget.Runtime,
                        LogLevel.Verbose);
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"[ReplayRenderGame] Failed to suppress framework config save: {ex.Message}",
                    LoggingTarget.Runtime,
                    LogLevel.Important);
            }
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IRenderGameplaySampleTriggerRecorder>(gameplaySampleTriggerRecorder);
            return dependencies;
        }

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);

            if (host.Window != null)
            {
                host.Window.CursorState |= CursorState.Hidden;

                host.Window.Hide();
            }
        }

        internal void SetRenderSceneTime(double sceneTime)
        {
            renderSceneSourceClock.CurrentTime = sceneTime;
        }

        protected override void Update()
        {
            base.Update();
            hideMenuCursor();
        }

        private double previousMasterVolumeForLog;
        private double previousSampleVolumeForLog;
        private double previousTrackVolume;

        [BackgroundDependencyLoader]
        private void load()
        {
            // BDLs execute in base-to-derived order, so Game.Audio is available here. Keep all
            // ReplayRenderGame dependency initialisation in this single loader; osu-framework
            // permits only one BackgroundDependencyLoader method per concrete type.
            // The stock framework has no detached sample-mixer routing hook. Public builds
            // render video and music, but do not export gameplay hitsounds.
            renderSampleCaptureMixer = null;

            // Save original audio settings so we can restore them in Dispose.
            previousMasterVolumeForLog = Audio.Volume.Value;
            previousSampleVolumeForLog = Audio.VolumeSample.Value;
            previousTrackVolume = Audio.VolumeTrack.Value;

            Audio.Volume.Value = 1;
            Audio.VolumeSample.Value = 1;
            // Mute the music track so the user doesn't hear it playing during rendering.
            // Hitsounds are captured from the sample mixer, and music is muxed by ffmpeg later.
            Audio.VolumeTrack.Value = 0;

            // Suppress framework config persistence. Each SetValue() below would otherwise trigger
            // QueueBackgroundSave() (after 100ms) and ConfigManager.Dispose()→Save() on exit, both of
            // which write temporary render settings to framework.ini — overwriting the user's real
            // preferences, especially dangerous if the main client crashed.
            //
            // We flip the internal hasLoaded flag to false via reflection so Save() becomes a no-op
            // (it returns early when !hasLoaded). This is the cleanest way to prevent persistence
            // without modifying the framework, and is safe because this process never needs to save
            // framework config — it's a throwaway render worker.
            suppressFrameworkConfigSave();

            // Force SingleThread mode to ensure 100% deterministic frame synchronization
            // between the Update and Draw loops, eliminating frame swim/tearing/jitter.
            frameworkConfig.SetValue(FrameworkSetting.ExecutionMode, ExecutionMode.SingleThread);

            // Force standard Direct3D11 renderer to ensure background rendering works even when the window is hidden.
            frameworkConfig.SetValue(FrameworkSetting.Renderer, RendererType.Direct3D11);

            // Force the hidden child renderer onto a non-fullscreen path even if the user's persisted
            // framework.ini prefers fullscreen.
            frameworkConfig.SetValue(FrameworkSetting.WindowMode, WindowMode.Windowed);
            frameworkConfig.SetValue(FrameworkSetting.WindowedSize, renderSize);
            frameworkConfig.SetValue(FrameworkSetting.SizeFullscreen, renderSize);
            frameworkConfig.SetValue(FrameworkSetting.MinimiseOnFocusLossInFullscreen, false);

            if (Host.Window != null)
                Host.Window.WindowMode.Value = WindowMode.Windowed;

            // Hitsound export requires the sample mixer to be backed by a decode-capable global mixer.
            frameworkConfig.SetValue(FrameworkSetting.AudioUseExperimentalWasapi, true);

            // Apply the user's custom skin from their osu.cfg configuration.
            SkinManager.SetSkinFromConfiguration(LocalConfig.Get<string>(OsuSetting.Skin));

            Add(screenStack = new OsuScreenStack
            {
                RelativeSizeAxes = Axes.Both,
                Clock = renderSceneClock,
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            // Restore audio volumes in-memory (AudioManager is transient, no config persistence).
            if (Audio != null)
            {
                Audio.Volume.Value = previousMasterVolumeForLog;
                Audio.VolumeSample.Value = previousSampleVolumeForLog;
                Audio.VolumeTrack.Value = previousTrackVolume;
            }

            // frameworkConfig persistence was suppressed in load() via suppressFrameworkConfigSave().
            // The Dispose()→Save() call will be a no-op, so temporary render settings (SingleThread,
            // D3D11, Windowed, Wasapi) will never be written to framework.ini — even if this process
            // crashes or outlives the main client.
            base.Dispose(isDisposing);
        }

        protected override async void LoadComplete()
        {
            base.LoadComplete();
            ReplayRenderStatusReporter? statusReporter = createStatusReporter();

            try
            {
                hideMenuCursor();

            if (Audio != null)
            {
                if (!Audio.UsingGlobalMixer.Value)
                {
                    Audio.Volume.Value = 0;
                    Audio.VolumeSample.Value = 0;
                    Audio.VolumeTrack.Value = 0;
                    Logger.Log("[ReplayRenderGame] Global decode mixer is unavailable. Render will continue, but gameplay hitsounds cannot be captured.",
                        LoggingTarget.Runtime,
                        LogLevel.Verbose);
                }
                else
                {
                    // Volumes were already forced to 1 early in load() BDL (see above) so that hitsounds
                    // are captured even when the client had volume set to 0. The log below records the
                    // values the user actually had.
                    Logger.Log($"[ReplayRenderGame] Normalising render sample volumes for offline hitsound capture (master: {previousMasterVolumeForLog:0.###} -> 1, sample: {previousSampleVolumeForLog:0.###} -> 1, track: {previousTrackVolume:0.###}). Client volume ratio will be applied during post-mix.",
                        LoggingTarget.Runtime,
                        LogLevel.Verbose);
                }
            }

            Host.MaximumUpdateHz = double.MaxValue;
            Host.MaximumDrawHz = double.MaxValue;
            Host.MaximumInactiveHz = double.MaxValue;

            try
            {
                await FFmpegEncoder.EnsureDownloadedAsync(
                    onProgress: (percent, downloadedMB, totalMB) => statusReporter?.ReportDownloadingFFmpeg(percent, downloadedMB, totalMB),
                    token: default).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                statusReporter?.ReportFailed($"Failed to download FFmpeg: {ex.Message}");
                Logger.Error(ex, "[ReplayRenderGame] Failed to ensure FFmpeg is available.");
                Schedule(Exit);
                return;
            }

            string? scoreIdStr = getArgument("--render-replay");

            if (!Guid.TryParse(scoreIdStr, out Guid scoreId))
            {
                statusReporter?.ReportFailed("Invalid or missing --render-replay argument.");
                Logger.Log("[ReplayRenderGame] Invalid or missing --render-replay argument.", LoggingTarget.Runtime, LogLevel.Error);
                Schedule(Exit);
                return;
            }

            ScoreInfo? scoreInfo = ScoreManager.Query(s => s.ID == scoreId);

            if (scoreInfo == null)
            {
                statusReporter?.ReportFailed($"Score {scoreId} not found in database.");
                Logger.Log($"[ReplayRenderGame] Score {scoreId} not found in database.", LoggingTarget.Runtime, LogLevel.Error);

                try
                {
                    var latestScores = ScoreManager.QueryLatest(s => true, 10);
                    Logger.Log("[ReplayRenderGame] --- LATEST 10 SCORES IN DATABASE ---", LoggingTarget.Runtime, LogLevel.Important);
                    foreach (var s in latestScores)
                    {
                        Logger.Log($"  ID: {s.ID} | Player: {s.User.Username} | Beatmap: {s.BeatmapInfo?.Metadata?.Title} | Date: {s.Date}", LoggingTarget.Runtime, LogLevel.Important);
                    }
                    Logger.Log("[ReplayRenderGame] -------------------------------------", LoggingTarget.Runtime, LogLevel.Important);
                }
                catch (Exception ex)
                {
                    Logger.Error(ex, "Failed to list latest scores.");
                }

                Schedule(Exit);
                return;
            }

            int fps = int.TryParse(getArgument("--fps"), out int parsedFps) ? parsedFps : 60;
            int bitrate = int.TryParse(getArgument("--bitrate"), out int parsedBitrate) ? parsedBitrate : 15;
            string encoder = getArgument("--encoder") ?? RenderEncodingSelector.ResolveEncoderArgument(RenderEncodingSelector.AutoEncoderLabel, Host);
            ReplayRenderQualityPreset qualityPreset = Enum.TryParse(getArgument("--quality-preset"), out ReplayRenderQualityPreset parsedQualityPreset)
                ? parsedQualityPreset
                : LocalConfig.Get<ReplayRenderQualityPreset>(OsuSetting.ForkReplayRenderQualityPreset);

            double? trimStart = double.TryParse(getArgument("--trim-start"), out double parsedTrimStart) ? parsedTrimStart : null;
            double? trimEnd = double.TryParse(getArgument("--trim-end"), out double parsedTrimEnd) ? parsedTrimEnd : null;
            bool showResultsAfterPeriod = bool.TryParse(getArgument("--show-results-after-period"), out bool parsedShowResultsAfterPeriod) && parsedShowResultsAfterPeriod;

            string renderDir = Storage.GetStorageForDirectory("renders").GetFullPath(".");
            System.IO.Directory.CreateDirectory(renderDir);

            string rawTitle = scoreInfo.BeatmapInfo?.Metadata?.Title ?? "unknown";
            string sanitizedTitle = string.Concat(rawTitle.Select(c => System.IO.Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            string outputFileName = $"render_{sanitizedTitle}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.mp4";
            string outputPath = System.IO.Path.Combine(renderDir, outputFileName);

            statusReporter?.ReportQueued(outputFileName);

            Score? score;

            try
            {
                score = ScoreManager.GetScore(scoreInfo);
            }
            catch (Exception ex)
            {
                statusReporter?.ReportFailed("Failed to load score replay data.");
                Logger.Error(ex, "[ReplayRenderGame] Failed to load score replay data.");
                Schedule(Exit);
                return;
            }

            if (score?.Replay == null)
            {
                statusReporter?.ReportFailed("Score has no replay data.");
                Logger.Log("[ReplayRenderGame] Score has no replay data.", LoggingTarget.Runtime, LogLevel.Error);
                Schedule(Exit);
                return;
            }

            if (scoreInfo.BeatmapInfo != null)
                Beatmap.Value = BeatmapManager.GetWorkingBeatmap(scoreInfo.BeatmapInfo);

            Ruleset.Value = scoreInfo.Ruleset;
            SelectedMods.Value = scoreInfo.Mods;

            bool flipVertical = false;
            string? audioPath = getAudioPath(scoreInfo.BeatmapInfo);
            string? cancelPath = getArgument("--cancel-file");

            if (!string.IsNullOrWhiteSpace(audioPath))
            {
                Logger.Log("[ReplayRenderGame] Using beatmap audio file as the primary music source. Gameplay hitsounds will be captured separately when the decode sample mixer is available.",
                    LoggingTarget.Runtime,
                    LogLevel.Verbose);
            }

            var renderPlayer = new RenderReplayPlayer(score, renderSize.Width, renderSize.Height, fps, bitrate, encoder, qualityPreset, outputPath, flipVertical, audioPath,
                previousTrackVolume, previousSampleVolumeForLog,
                () => !string.IsNullOrWhiteSpace(cancelPath) && System.IO.File.Exists(cancelPath),
                trimStart, trimEnd, showResultsAfterPeriod);
            renderPlayer.OnProgress += (currentFrame, totalFrames) => statusReporter?.ReportProgress(currentFrame, totalFrames, outputFileName);
            renderPlayer.OnRenderComplete += _ =>
            {
                statusReporter?.ReportCompleted(outputFileName, renderPlayer.TotalFrames);
                Schedule(Exit);
            };
            renderPlayer.OnRenderCancelled += () =>
            {
                statusReporter?.ReportCancelled(outputFileName, renderPlayer.RenderedFrames, renderPlayer.TotalFrames);
                Schedule(Exit);
            };
            renderPlayer.OnRenderError += ex =>
            {
                statusReporter?.ReportFailed(ex.Message);
                Schedule(Exit);
            };

            Logger.Log("[ReplayRenderGame] Pushing render player directly.", LoggingTarget.Runtime, LogLevel.Verbose);
            screenStack.PushSynchronously(renderPlayer);
            }
            catch (Exception ex)
            {
                statusReporter?.ReportFailed(ex.Message);
                Logger.Error(ex, "[ReplayRenderGame] Unhandled error while initialising replay render.");
                Schedule(Exit);
            }
        }

        private void hideMenuCursor()
        {
            if (Host.Window != null)
                Host.Window.CursorState |= CursorState.Hidden;

            if (GlobalCursorDisplay?.MenuCursor == null)
                return;

            GlobalCursorDisplay.MenuCursor.Hide();
            GlobalCursorDisplay.MenuCursor.Alpha = 0;
        }

        private string? getArgument(string key) => args.SkipWhile(a => a != key).Skip(1).FirstOrDefault();

        private static Size parseResolution(string[] args)
        {
            string resolution = args.SkipWhile(a => a != "--resolution").Skip(1).FirstOrDefault() ?? "1920x1080";
            string[] parts = resolution.Split('x', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length == 2
                && int.TryParse(parts[0], out int width)
                && int.TryParse(parts[1], out int height)
                && width > 0
                && height > 0)
            {
                return new Size(width, height);
            }

            return new Size(1920, 1080);
        }

        private ReplayRenderStatusReporter? createStatusReporter()
        {
            string? statusFile = getArgument("--status-file");
            int outputFps = int.TryParse(getArgument("--fps"), out int parsedFps) ? parsedFps : 60;

            return string.IsNullOrWhiteSpace(statusFile)
                ? null
                : new ReplayRenderStatusReporter(statusFile, outputFps);
        }

        private string? getAudioPath(BeatmapInfo? beatmapInfo)
        {
            string? audioStoragePath = beatmapInfo?.BeatmapSet?.GetPathForFile(beatmapInfo.Metadata.AudioFile);

            if (string.IsNullOrWhiteSpace(audioStoragePath))
                return null;

            string fullPath = Storage.GetStorageForDirectory("files").GetFullPath(audioStoragePath);
            return System.IO.File.Exists(fullPath) ? fullPath : null;
        }

        protected override IAPIProvider CreateAPIProvider(EndpointConfiguration endpoints)
        {
            var api = new DummyAPIAccess();
            api.SetState(APIState.Offline);
            return api;
        }
    }
}
