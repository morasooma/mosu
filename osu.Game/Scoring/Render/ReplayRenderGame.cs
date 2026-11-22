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
using osu.Framework.Bindables;
using osu.Framework.Configuration;
using osu.Framework.Graphics.Containers;
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
using osu.Game.Skinning;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;

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
        private readonly ReplayRenderSnapshot? renderSnapshot;
        private OsuScreenStack screenStack = null!;
        private AudioMixer? renderSampleCaptureMixer;
        private Bindable<bool> disableShear = null!;

        internal Drawable RenderCaptureTarget => screenStack;
        internal AudioMixer? RenderSampleCaptureMixer => renderSampleCaptureMixer;
        internal IRenderGameplaySampleTriggerRecorder GameplaySampleTriggerRecorder => gameplaySampleTriggerRecorder;

        public ReplayRenderGame(string[] args)
        {
            this.args = args;
            string? snapshotPath = getArgument("--render-snapshot");
            if (!string.IsNullOrWhiteSpace(snapshotPath))
            {
                renderSnapshot = ReplayRenderSnapshot.Read(snapshotPath);
                MosuServerEnvironment.UsesStableProtocol = renderSnapshot.UsesStableProtocol;
                MosuServerEnvironment.IsThirdPartyServer = renderSnapshot.IsThirdPartyServer;
            }
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
            defaults[FrameworkSetting.FrameSync] = FrameSync.Unlimited;

            return defaults;
        }

        internal static RendererType GetCaptureRenderer(bool isWindows, bool isLinux)
            => isWindows ? RendererType.Direct3D11 : isLinux ? RendererType.Vulkan : RendererType.Automatic;

        protected override void ConfigureFrameworkConfig(FrameworkConfigManager config)
        {
            base.ConfigureFrameworkConfig(config);

            // Defaults do not override framework.ini, and load()/SetHost() run after renderer
            // creation. Apply the isolated export policy here, before any window/GPU setup.
            config.DisableSaving();
            RendererType renderer = GetCaptureRenderer(OperatingSystem.IsWindows(), OperatingSystem.IsLinux());
            if (renderer != RendererType.Automatic)
                config.SetValue(FrameworkSetting.Renderer, renderer);
            config.SetValue(FrameworkSetting.ExecutionMode, ExecutionMode.SingleThread);
            config.SetValue(FrameworkSetting.FrameSync, FrameSync.Unlimited);
            config.SetValue(FrameworkSetting.WindowMode, WindowMode.Windowed);
            config.SetValue(FrameworkSetting.WindowedSize, renderSize);
            config.SetValue(FrameworkSetting.SizeFullscreen, renderSize);
            config.SetValue(FrameworkSetting.ConfineMouseMode, ConfineMouseMode.Never);
            config.SetValue(FrameworkSetting.MinimiseOnFocusLossInFullscreen, false);
        }

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IRenderGameplaySampleTriggerRecorder>(gameplaySampleTriggerRecorder);
            if (renderSnapshot != null)
                dependencies.Cache(renderSnapshot);
            return dependencies;
        }

        protected override Container CreateScalingContainer() => new ScalingContainer(ScalingMode.Everything);

        public override void SetHost(GameHost host)
        {
            base.SetHost(host);

            // Base SetHost creates LocalConfig before the root scaling containers load.
            // Use current parent-process values, not potentially stale deferred config saves.
            LocalConfig.DisableSaving();
            renderSnapshot?.Layout?.Apply(LocalConfig);
            disableShear = LocalConfig.GetBindable<bool>(OsuSetting.ForkDisableInterfaceShear);
            OsuGame.DisableShear.BindTo(disableShear);

            // Offline capture must not wait for the desktop compositor / DXGI frame-latency
            // object between exported frames. This host is isolated from the interactive client.
            host.AllowTearing.Value = true;

            if (host.Window != null)
            {
                host.Window.ShowOnFirstDraw = false;
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
            LocalConfig.DisableSaving();
            ApplySkinPerformanceSettings(LocalConfig);
            if (renderSnapshot != null)
            {
                MosuServerEnvironment.UsesStableProtocol = renderSnapshot.UsesStableProtocol;
                MosuServerEnvironment.IsThirdPartyServer = renderSnapshot.IsThirdPartyServer;
                // OsuGameBase creates this store in our child dependency scope. Loader
                // parameters resolve against the parent scope, where it is not registered.
                var onlineAssets = Dependencies.Get<OnlineAssetCachingStore>();
                onlineAssets.UseRenderSnapshot(renderSnapshot.Avatars);
                foreach (string avatarUrl in renderSnapshot.Avatars.Keys)
                    onlineAssets.Get(avatarUrl);
                LocalConfig.SetValue(OsuSetting.GameplayLeaderboard, renderSnapshot.ShowLeaderboard);
                LocalConfig.SetValue(OsuSetting.ScoreDisplayMode, renderSnapshot.ScoreDisplayMode);
            }

            renderSampleCaptureMixer = Audio.CreateAudioMixer("ReplayRenderHitsoundCapture");
            Audio.SampleStoreMixerOverride = renderSampleCaptureMixer;

            // Preserve the user's mix levels for post-processing only. This isolated child
            // process must never restore live playback volumes, including during teardown.
            previousMasterVolumeForLog = Audio.Volume.Value;
            previousSampleVolumeForLog = Audio.VolumeSample.Value;
            previousTrackVolume = Audio.VolumeTrack.Value;

            Audio.Volume.Value = 1;
            Audio.VolumeSample.Value = 1;
            // Mute the music track so the user doesn't hear it playing during rendering.
            // Hitsounds are captured from the sample mixer, and music is muxed by ffmpeg later.
            Audio.VolumeTrack.Value = 0;

            // Renderer, deterministic execution and window settings were applied before host startup.

            // Prefer the skin captured by the parent process when the render was started. Config
            // saves are deferred, so a new render process may otherwise observe the previous skin.
            // This also preserves the effective ruleset-specific skin rather than the global one.
            string configuredSkin = getArgument("--skin") ?? LocalConfig.Get<string>(OsuSetting.Skin);
            SkinManager.SetSkinFromConfiguration(configuredSkin);

            Add(new ScalingContainer(ScalingMode.ExcludeOverlays)
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Child = screenStack = new OsuScreenStack
                {
                    RelativeSizeAxes = Axes.Both,
                    Clock = renderSceneClock,
                },
            });
        }

        internal static void ApplySkinPerformanceSettings(OsuConfigManager config)
        {
            // Offline video export must retain full visuals regardless of the user's gameplay
            // performance profile. Disable it in this child process only, without changing config,
            // before any gameplay drawables or skin caches are loaded.
            SkinPerformanceMode.ClearBenchmarkOverride();
            SkinPerformanceMode.Enabled = false;
            SkinPerformanceMode.FreezeAnimations = config.Get<bool>(OsuSetting.ForkSkinPerformanceFreezeAnimations);
            SkinPerformanceMode.SimplifyEffects = config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyEffects);
            SkinPerformanceMode.OptimiseTextures = config.Get<bool>(OsuSetting.ForkSkinPerformanceOptimiseTextures);
            SkinPerformanceMode.SimplifyHud = config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyHud);
            SkinPerformanceMode.SimplifyCounters = config.Get<bool>(OsuSetting.ForkSkinPerformanceSimplifyCounters);
            SkinPerformanceMode.DisableKiaiFlashing = config.Get<bool>(OsuSetting.ForkSkinPerformanceDisableKiaiFlashing);
            SkinPerformanceMode.BlackBackground = config.Get<bool>(OsuSetting.ForkSkinPerformanceBlackBackground);
            SkinPerformanceMode.ArgonFollowRing = config.Get<bool>(OsuSetting.ForkArgonFollowRing);
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
                await Audio.SuspendLiveOutputAsync().ConfigureAwait(true);

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
            string sanitizedTitle = ReplayRenderFileName.SanitizeTitle(rawTitle);
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

            if (renderSnapshot != null)
            {
                score.ScoreInfo.User = renderSnapshot.Player.ToUser();
                score.ScoreInfo.Position = renderSnapshot.PlayerPosition;
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
