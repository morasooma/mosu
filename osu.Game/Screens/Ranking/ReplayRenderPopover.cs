// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Localisation;
using osu.Game.Overlays.Settings;
using osu.Game.Scoring;
using osu.Game.Scoring.Render;
using osu.Game.Screens;
using osu.Framework.Screens;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.Ranking
{
    public partial class ReplayRenderPopover : OsuPopover
    {
        private const float popover_width = 320;
        private const string ffmpeg_download_url = "https://ffmpeg.org/download.html";

        private readonly ScoreInfo scoreInfo;
        private bool monitoringRender;
        private Process? renderProcess;
        private readonly Queue<string> renderProcessErrorLines = new Queue<string>();

        private double? customStartTime;
        private double? customEndTime;
        private readonly List<OsuSpriteText> themedPrimaryText = new List<OsuSpriteText>();
        private readonly List<OsuSpriteText> themedSecondaryText = new List<OsuSpriteText>();
        private OsuScrollContainer settingsScroll = null!;
        private OverlayColourProvider colourProvider = null!;
        private IBindable<Colour4> themeColour = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private GameHost host { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IPerformFromScreenRunner? performer { get; set; }

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        public ReplayRenderPopover(ScoreInfo scoreInfo)
            : base(false)
        {
            this.scoreInfo = scoreInfo;

            Content.Padding = new MarginPadding(15);
            Body.CornerRadius = 6;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            this.colourProvider = colourProvider;
            bool updatingRecommendedBitrate = false;
            bool bitrateTouchedByUser = false;
            string savedResolution = config.Get<string>(OsuSetting.ForkReplayRenderResolution);
            int savedFps = config.Get<int>(OsuSetting.ForkReplayRenderFps);
            int savedBitrate = config.Get<int>(OsuSetting.ForkReplayRenderBitrateMbps);
            string savedEncoder = config.Get<string>(OsuSetting.ForkReplayRenderEncoder);

            if (!new[] { "1280x720", "1920x1080", "2560x1440", "3840x2160" }.Contains(savedResolution))
                savedResolution = "1920x1080";

            if (!new[] { 30, 60, 120, 240 }.Contains(savedFps))
                savedFps = 60;

            if (!RenderEncodingSelector.GetEncoderOptions().Contains(savedEncoder))
                savedEncoder = RenderEncodingSelector.AutoEncoderLabel;

            var resolutionDropdown = new OsuDropdown<string>
            {
                RelativeSizeAxes = Axes.X,
                Items = new[] { "1280x720", "1920x1080", "2560x1440", "3840x2160" },
                Current = new Bindable<string>(savedResolution),
            };

            var fpsDropdown = new OsuDropdown<int>
            {
                RelativeSizeAxes = Axes.X,
                Items = new[] { 30, 60, 120, 240 },
                Current = new Bindable<int>(savedFps),
            };

            var bitrateSlider = new SettingsSlider<int>
            {
                RelativeSizeAxes = Axes.X,
                TransferValueOnCommit = true,
                Current = new BindableInt(savedBitrate)
                {
                    MinValue = 2,
                    MaxValue = 40,
                },
            };

            var qualityPresetDropdown = new OsuDropdown<ReplayRenderQualityPreset>
            {
                RelativeSizeAxes = Axes.X,
                Items = Enum.GetValues<ReplayRenderQualityPreset>(),
                Current = config.GetBindable<ReplayRenderQualityPreset>(OsuSetting.ForkReplayRenderQualityPreset).GetBoundCopy(),
            };

            var encoderDropdown = new OsuDropdown<string>
            {
                RelativeSizeAxes = Axes.X,
                Items = RenderEncodingSelector.GetEncoderOptions(),
                Current = new Bindable<string>(savedEncoder),
            };

            var showResultsAfterPeriodCheckbox = new SettingsCheckbox
            {
                RelativeSizeAxes = Axes.X,
                LabelText = ReplayRenderStrings.ShowResultsAfterPeriod,
                Current = config.GetBindable<bool>(OsuSetting.ForkReplayRenderShowResultsAfterPeriod).GetBoundCopy(),
            };

            var recommendationText = trackThemedText(new OsuSpriteText
            {
                Text = ReplayRenderStrings.RecommendedDetecting,
                Font = OsuFont.GetFont(size: 12),
            }, secondary: true);

            void updateRecommendedBitrate()
            {
                if (bitrateTouchedByUser)
                    return;

                updatingRecommendedBitrate = true;
                bitrateSlider.Current.Value = RenderEncodingSelector.GetRecommendedBitrateMbps(resolutionDropdown.Current.Value, fpsDropdown.Current.Value, encoderDropdown.Current.Value, qualityPresetDropdown.Current.Value, host);
                updatingRecommendedBitrate = false;
            }

            bitrateSlider.Current.BindValueChanged(_ =>
            {
                if (!updatingRecommendedBitrate)
                    bitrateTouchedByUser = true;
            });

            resolutionDropdown.Current.BindValueChanged(_ => updateRecommendedBitrate());
            fpsDropdown.Current.BindValueChanged(_ => updateRecommendedBitrate());
            qualityPresetDropdown.Current.BindValueChanged(_ =>
            {
                bitrateTouchedByUser = false;
                updateRecommendedBitrate();
            });
            encoderDropdown.Current.BindValueChanged(_ =>
            {
                if (encoderDropdown.Current.Value == RenderEncodingSelector.AutoEncoderLabel)
                    bitrateTouchedByUser = false;

                updateRecommendedBitrate();
            });

            bitrateTouchedByUser = false;
            updateRecommendedBitrate();

            _ = Task.Run(() =>
            {
                string recommendation = RenderEncodingSelector.DescribeRecommendation(host);
                host.UpdateThread.Scheduler.Add(() => recommendationText.Text = $"Recommended: {recommendation}");
            });

            var periodText = trackThemedText(new OsuSpriteText
            {
                Text = ReplayRenderStrings.PeriodFullReplay,
                Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                Margin = new MarginPadding { Top = 10 },
            });

            Child = settingsScroll = new OsuScrollContainer
            {
                Width = popover_width,
                Height = 600,
                ScrollbarOverlapsContent = false,
                Child = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 12),
                    Padding = new MarginPadding { Right = 10, Bottom = 5 },
                    Children = new Drawable[]
                    {
                        trackThemedText(new OsuSpriteText
                        {
                            Text = ReplayRenderStrings.Title,
                            Font = OsuFont.GetFont(size: 22, weight: FontWeight.Bold),
                        }),
                        new Box
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 1,
                            Colour = OsuColour.Gray(0.25f),
                        },
                        createSetting(ReplayRenderStrings.Resolution, resolutionDropdown),
                        createSetting(ReplayRenderStrings.Framerate, fpsDropdown),
                        createSetting(ReplayRenderStrings.QualityPreset, qualityPresetDropdown),
                        createSetting(ReplayRenderStrings.VideoBitrate, bitrateSlider),
                        createSetting(ReplayRenderStrings.Encoder, encoderDropdown),
                        recommendationText,
                        periodText,
                        new RoundedButton
                        {
                            Text = ReplayRenderStrings.SelectPeriod,
                            RelativeSizeAxes = Axes.X,
                            Height = 40,
                            BackgroundColour = OsuColour.Gray(0.2f),
                            Action = () =>
                            {
                                var fullScore = scoreManager.GetScore(scoreInfo);
                                if (fullScore == null) return;

                                // Restore the real mods from the original scoreInfo, as DB fetched ones are APIMods
                                fullScore.ScoreInfo.Mods = scoreInfo.Mods;

                                Hide();
                                performer?.PerformFromScreen(s => osu.Framework.Screens.ScreenExtensions.Push(s, new osu.Game.Screens.Play.ReplayPeriodSelectorLoader(fullScore, (start, end) =>
                                {
                                    customStartTime = start;
                                    customEndTime = end;

                                    TimeSpan sTime = TimeSpan.FromMilliseconds(start);
                                    TimeSpan eTime = TimeSpan.FromMilliseconds(end);
                                    periodText.Text = $"Period: {(int)sTime.TotalMinutes:00}:{sTime.Seconds:00} -> {(int)eTime.TotalMinutes:00}:{eTime.Seconds:00}";

                                    Show();
                                })), new[] { typeof(osu.Game.Screens.Ranking.ResultsScreen) });
                            }
                        },
                        showResultsAfterPeriodCheckbox,
                        new RoundedButton
                        {
                            Text = ReplayRenderStrings.StartRender,
                            RelativeSizeAxes = Axes.X,
                            Height = 40,
                            Action = () => startRender(resolutionDropdown.Current.Value, fpsDropdown.Current.Value, bitrateSlider.Current.Value, encoderDropdown.Current.Value,
                                qualityPresetDropdown.Current.Value, showResultsAfterPeriodCheckbox.Current.Value)
                        }
                    }
                },
            };

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateTextColours(), true);
        }

        private void startRender(string resolution, int fps, int bitrate, string encoder, ReplayRenderQualityPreset qualityPreset, bool showResultsAfterPeriod)
        {
            if (monitoringRender)
                return;

            try
            {
                Guid targetScoreId = scoreInfo.ID;
                lock (renderProcessErrorLines)
                    renderProcessErrorLines.Clear();
                var localScore = scoreManager.Query(s => s.ID == scoreInfo.ID);

                if (localScore == null && scoreInfo.OnlineID > 0)
                {
                    localScore = scoreManager.Query(s => s.OnlineID == scoreInfo.OnlineID);
                    if (localScore != null)
                        targetScoreId = localScore.ID;
                }

                config.SetValue(OsuSetting.ForkReplayRenderResolution, resolution);
                config.SetValue(OsuSetting.ForkReplayRenderFps, fps);
                config.SetValue(OsuSetting.ForkReplayRenderBitrateMbps, bitrate);
                config.SetValue(OsuSetting.ForkReplayRenderEncoder, encoder);
                config.SetValue(OsuSetting.ForkReplayRenderQualityPreset, qualityPreset);
                config.SetValue(OsuSetting.ForkReplayRenderShowResultsAfterPeriod, showResultsAfterPeriod);

                var renderStorage = storage.GetStorageForDirectory("renders");
                string statusFileName = $".replay-render-status-{Guid.NewGuid():N}.json";
                string statusPath = renderStorage.GetFullPath(statusFileName);
                string cancelFileName = $".replay-render-cancel-{Guid.NewGuid():N}.flag";
                string cancelPath = renderStorage.GetFullPath(cancelFileName);

                string[] args =
                {
                    "--render-replay",
                    targetScoreId.ToString(),
                    "--status-file",
                    statusPath,
                    "--cancel-file",
                    cancelPath,
                    "--resolution",
                    resolution,
                    "--fps",
                    fps.ToString(),
                    "--bitrate",
                    bitrate.ToString(),
                    "--encoder",
                    RenderEncodingSelector.ResolveEncoderArgument(encoder, host),
                    "--quality-preset",
                    qualityPreset.ToString(),
                    "--show-results-after-period",
                    showResultsAfterPeriod.ToString(),
                };

                var argsList = args.ToList();
                if (customStartTime.HasValue)
                {
                    argsList.Add("--trim-start");
                    argsList.Add(customStartTime.Value.ToString("0"));
                }
                if (customEndTime.HasValue)
                {
                    argsList.Add("--trim-end");
                    argsList.Add(customEndTime.Value.ToString("0"));
                }

                ProgressNotification notification = new ProgressNotification
                {
                    State = ProgressNotificationState.Queued,
                    Text = ReplayRenderStrings.PreparingRender,
                    CompletionText = ReplayRenderStrings.RenderFinished,
                };

                notification.CancelRequested = () =>
                {
                    // Signal graceful cancellation via the cancel flag file.
                    try
                    {
                        File.WriteAllText(cancelPath, string.Empty);
                    }
                    catch
                    {
                    }

                    // Give the worker time to close FFmpeg and write the Cancelled status before
                    // falling back to a hard kill. Killing immediately races the status reporter
                    // and used to surface as "stopped before completion".
                    _ = forceKillRenderAfterGracePeriodAsync();

                    return true;
                };

                notifications?.Post(notification);

                monitoringRender = true;
                Hide();

                _ = launchRenderAsync(argsList.ToArray(), renderStorage, statusPath, cancelPath, notification);
                returnToMainMenu();
            }
            catch (Exception ex)
            {
                postRenderErrorNotification($"Failed to start replay render: {ex.Message}");
            }
        }

        private void returnToMainMenu()
        {
            performer?.PerformFromScreen(_ => { });
        }

        private async Task forceKillRenderAfterGracePeriodAsync()
        {
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            try
            {
                Process? process = renderProcess;
                if (process != null && !process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
            }
        }

        private async Task launchRenderAsync(string[] args, Storage renderStorage, string statusPath, string cancelPath, ProgressNotification notification)
        {
            try
            {
                Process process = await Task.Run(() =>
                {
                    var startedProcess = new Process
                    {
                        StartInfo = ReplayRenderProcessStartInfoBuilder.Create(args),
                    };

                    startedProcess.ErrorDataReceived += (_, e) => recordRenderProcessError(e.Data);

                    if (!startedProcess.Start())
                        throw new InvalidOperationException("Failed to start the replay renderer process.");

                    startedProcess.BeginErrorReadLine();
                    return startedProcess;
                }).ConfigureAwait(false);

                renderProcess = process;
                ReplayRenderProcessRegistry.Register(process);
                await monitorRenderAsync(process, renderStorage, statusPath, cancelPath, notification).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                host.UpdateThread.Scheduler.Add(() =>
                {
                    monitoringRender = false;
                    notification.State = ProgressNotificationState.Cancelled;
                    postRenderErrorNotification($"Failed to start replay render: {ex.Message}");
                });

                try
                {
                    File.Delete(statusPath);
                }
                catch
                {
                }

                try
                {
                    File.Delete(cancelPath);
                }
                catch
                {
                }
            }
        }

        private async Task monitorRenderAsync(Process process, Storage renderStorage, string statusPath, string cancelPath, ProgressNotification notification)
        {
            ReplayRenderStatus? lastStatus = null;

            try
            {
                while (true)
                {
                    if (ReplayRenderStatus.TryReadFromFile(statusPath, out ReplayRenderStatus? status) && status != null)
                    {
                        if (!statusMatches(status, lastStatus))
                        {
                            ReplayRenderStatus capturedStatus = status;
                            host.UpdateThread.Scheduler.Add(() => updateNotification(capturedStatus, renderStorage, notification));
                            lastStatus = status;
                        }

                        if (status.State is ReplayRenderOperationState.Completed or ReplayRenderOperationState.Failed or ReplayRenderOperationState.Cancelled)
                            return;
                    }

                    if (process.HasExited)
                    {
                        // The worker writes its terminal status immediately before exiting. Allow
                        // a short grace period for the atomic status-file move to become visible.
                        for (int attempt = 0; attempt < 8; attempt++)
                        {
                            if (ReplayRenderStatus.TryReadFromFile(statusPath, out ReplayRenderStatus? finalStatus) && finalStatus != null
                                && finalStatus.State is ReplayRenderOperationState.Completed or ReplayRenderOperationState.Failed or ReplayRenderOperationState.Cancelled)
                            {
                                host.UpdateThread.Scheduler.Add(() => updateNotification(finalStatus, renderStorage, notification));
                                return;
                            }

                            await Task.Delay(125).ConfigureAwait(false);
                        }

                        int? exitCode = null;
                        try { exitCode = process.ExitCode; } catch { }

                        host.UpdateThread.Scheduler.Add(() =>
                        {
                            if (notification.State is ProgressNotificationState.Active or ProgressNotificationState.Queued)
                            {
                                notification.State = ProgressNotificationState.Cancelled;
                                string? processError = getRenderProcessError();
                                string message = exitCode.HasValue
                                    ? $"Replay render stopped before completion (exit code {exitCode.Value})."
                                    : "Replay render stopped before completion.";

                                if (!string.IsNullOrWhiteSpace(processError))
                                    message += $" {processError}";

                                postRenderErrorNotification(message);
                            }
                        });

                        return;
                    }

                    await Task.Delay(250).ConfigureAwait(false);
                }
            }
            finally
            {
                host.UpdateThread.Scheduler.Add(() => monitoringRender = false);
                ReplayRenderProcessRegistry.Unregister(process);
                process.Dispose();

                try
                {
                    File.Delete(statusPath);
                }
                catch
                {
                }

                try
                {
                    File.Delete(cancelPath);
                }
                catch
                {
                }
            }
        }

        private static bool statusMatches(ReplayRenderStatus left, ReplayRenderStatus? right)
            => right != null
               && left.State == right.State
               && left.CurrentFrame == right.CurrentFrame
               && left.TotalFrames == right.TotalFrames
               && left.OutputFps == right.OutputFps
               && left.OutputFileName == right.OutputFileName
               && left.ErrorMessage == right.ErrorMessage
               && Math.Abs(left.EffectiveRenderFps - right.EffectiveRenderFps) < 0.05
               && Math.Abs(left.RealtimeFactor - right.RealtimeFactor) < 0.01;

        private void recordRenderProcessError(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return;

            Logger.Log($"[ReplayRenderWorker] {line}", LoggingTarget.Runtime, LogLevel.Important);

            lock (renderProcessErrorLines)
            {
                renderProcessErrorLines.Enqueue(line.Trim());
                while (renderProcessErrorLines.Count > 64)
                    renderProcessErrorLines.Dequeue();
            }
        }

        private string? getRenderProcessError()
        {
            lock (renderProcessErrorLines)
            {
                if (renderProcessErrorLines.Count == 0)
                    return null;

                string[] lines = renderProcessErrorLines.ToArray();
                return lines.FirstOrDefault(line => line.Contains("Unhandled exception", StringComparison.OrdinalIgnoreCase))
                       ?? lines.FirstOrDefault(line => line.Contains("Exception:", StringComparison.OrdinalIgnoreCase))
                       ?? lines[0];
            }
        }

        private void updateNotification(ReplayRenderStatus status, Storage renderStorage, ProgressNotification notification)
        {
            switch (status.State)
            {
                case ReplayRenderOperationState.Queued:
                    notification.State = ProgressNotificationState.Queued;
                    notification.Text = ReplayRenderStrings.PreparingRender;
                    break;

                case ReplayRenderOperationState.Active:
                    notification.State = ProgressNotificationState.Active;
                    notification.Progress = status.Progress;
                    notification.Text = status.TotalFrames > 0
                        ? $"Rendering replay... {status.CurrentFrame}/{status.TotalFrames} ({status.Progress:P0}) at {status.EffectiveRenderFps:0.#} fps ({status.RealtimeFactor:0.##}x)"
                        : "Rendering replay...";
                    break;

                case ReplayRenderOperationState.DownloadingFFmpeg:
                    notification.State = ProgressNotificationState.Active;
                    notification.Progress = status.DownloadProgress;
                    notification.Text = status.OutputFileName ?? "Downloading FFmpeg...";
                    break;

                case ReplayRenderOperationState.Completed:
                    notification.Progress = 1;
                    notification.CompletionClickAction = !string.IsNullOrEmpty(status.OutputFileName)
                        ? () => renderStorage.PresentFileExternally(status.OutputFileName)
                        : null;
                    notification.CompletionText = !string.IsNullOrEmpty(status.OutputFileName)
                        ? $"Replay render finished: {status.OutputFileName}"
                        : "Replay render finished.";
                    notification.State = ProgressNotificationState.Completed;
                    break;

                case ReplayRenderOperationState.Failed:
                    notification.State = ProgressNotificationState.Cancelled;
                    postRenderErrorNotification($"Replay render failed: {status.ErrorMessage ?? "unknown error"}");
                    break;

                case ReplayRenderOperationState.Cancelled:
                    notification.State = ProgressNotificationState.Cancelled;
                    notification.CompletionText = !string.IsNullOrEmpty(status.OutputFileName)
                        ? $"Replay render cancelled. Partial output: {status.OutputFileName}"
                        : "Replay render cancelled.";
                    break;
            }
        }

        private void postRenderErrorNotification(string message)
        {
            bool ffmpegMissing = message.Contains("ffmpeg was not found", StringComparison.OrdinalIgnoreCase)
                                 || message.Contains("Failed to locate FFmpeg", StringComparison.OrdinalIgnoreCase)
                                 || message.Contains("set FFMPEG_PATH", StringComparison.OrdinalIgnoreCase);

            if (!ffmpegMissing)
            {
                notifications?.Post(new SimpleErrorNotification
                {
                    Text = message
                });
                return;
            }

            notifications?.Post(new SimpleErrorNotification
            {
                Text = ReplayRenderStrings.FFmpegNotFound,
                Activated = () =>
                {
                    host.OpenUrlExternally(ffmpeg_download_url);
                    return true;
                }
            });

            notifications?.Post(new SimpleNotification
            {
                Text = ReplayRenderStrings.FFmpegInstallHint,
                Activated = () =>
                {
                    host.OpenUrlExternally(ffmpeg_download_url);
                    return true;
                }
            });
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();

            if (host.Window == null || settingsScroll.DrawHeight <= 0 || settingsScroll.ScreenSpaceDrawQuad.Height <= 0)
                return;

            // Keep the popover inside the physical window at larger UI scales. Using the
            // screen-space ratio also accounts for any scaling applied by the overlay hierarchy.
            float screenSpaceScale = settingsScroll.ScreenSpaceDrawQuad.Height / settingsScroll.DrawHeight;
            float availableScreenSpaceHeight = Math.Max(300, host.Window.ClientSize.Height - 80);
            float targetHeight = Math.Clamp(availableScreenSpaceHeight / screenSpaceScale, 300, 650);

            if (Math.Abs(settingsScroll.Height - targetHeight) > 0.5f)
                settingsScroll.Height = targetHeight;
        }

        private OsuSpriteText trackThemedText(OsuSpriteText text, bool secondary = false)
        {
            (secondary ? themedSecondaryText : themedPrimaryText).Add(text);
            return text;
        }

        private void updateTextColours()
        {
            Colour4 primary = OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Colour4.White;
            Colour4 secondary = OverlayColourProvider.IsLightTheme ? colourProvider.Content2 : OsuColour.Gray(0.8f);

            foreach (OsuSpriteText text in themedPrimaryText)
                text.Colour = primary;

            foreach (OsuSpriteText text in themedSecondaryText)
                text.Colour = secondary;
        }

        private FillFlowContainer createSetting(LocalisableString label, Drawable control) => new FillFlowContainer
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Direction = FillDirection.Vertical,
            Spacing = new Vector2(0, 6),
            Children = new Drawable[]
            {
                trackThemedText(new OsuSpriteText
                {
                    Text = label,
                    Font = OsuFont.GetFont(size: 15, weight: FontWeight.SemiBold),
                }),
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Child = control,
                },
            }
        };

        protected override void Dispose(bool isDisposing)
        {
            // Note: we intentionally do NOT kill the render process here.
            // The popover is disposed when hidden, but the render continues in the background.
            // Process killing on game exit is handled by ReplayRenderProcessRegistry.
            base.Dispose(isDisposing);
        }
    }

    /// <summary>
    /// Tracks active replay render processes so they can be killed when the game exits,
    /// preventing orphaned render processes from continuing after the user closes the game.
    /// </summary>
    public static class ReplayRenderProcessRegistry
    {
        private static readonly HashSet<Process> processes = new HashSet<Process>();

        public static void Register(Process process)
        {
            lock (processes)
            {
                processes.Add(process);
            }
        }

        public static void Unregister(Process process)
        {
            lock (processes)
            {
                processes.Remove(process);
            }
        }

        /// <summary>
        /// Force-kills all registered render processes. Called on game exit.
        /// </summary>
        public static void KillAll()
        {
            lock (processes)
            {
                foreach (var process in processes)
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch
                    {
                    }

                    try { process.Dispose(); } catch { }
                }

                processes.Clear();
            }
        }
    }
}
