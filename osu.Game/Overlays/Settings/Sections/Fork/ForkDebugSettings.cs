// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Screens;
using osu.Game.Screens;
using osu.Game.Screens.Select.Hinamizawa;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Scoring;
using osu.Game.Performance.Debug;
using osu.Game.Performance.Diagnostics;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkDebugSettings : SettingsSubsection
    {
        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private PerformanceDebugService? performanceDebugService { get; set; }

        [Resolved(canBeNull: true)]
        private IPerformanceDiagnosticsManager? diagnosticsManager { get; set; }

        private int memoryDumpCaptureInProgress;
        private bool logExportInProgress;

        protected override LocalisableString Header => ForkSettingsStrings.DebugHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, IPerformFromScreenRunner? performer, OsuGame game)
        {
            var children = new List<Drawable>
            {
                new SettingsButtonV2
                {
                    Text = global::osu.Game.Localisation.ClientTranslationStrings.J4040,
                    TooltipText = global::osu.Game.Localisation.ClientTranslationStrings.J4041,
                    Action = () => performer?.PerformFromScreen(screen => screen.Push(new HinamizawaSongSelect())),
                },
                new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.ForkDebugBenchmarkDifficultyCaption,
                    TooltipText = ForkSettingsStrings.ForkDebugBenchmarkDifficultyHint,
                    Action = BackgroundDataStoreProcessor.QueueTestRecalculation,
                },
                new DangerousSettingsButtonV2
                {
                    Text = ForkSettingsStrings.QuickExportLogsBtn,
                    Action = exportQuickLogs
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.PerfLoggingCaption,
                    HintText = diagnosticsManager != null
                        ? global::osu.Game.Localisation.ClientTranslationStrings.J4042
                        : ForkSettingsStrings.PerfLoggingHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkPerformanceLogging)
                })
                {
                    FavouriteId = "OsuSetting.ForkPerformanceLogging",
                    Keywords = new[] { @"performance", @"logging", @"fps", @"frametime", @"cpu", @"metrics" },
                },
                new SettingsItemV2(new FormEnumDropdown<DebugHudMode>
                {
                    Caption = ForkSettingsStrings.ForkDebugHudCaption,
                    HintText = ForkSettingsStrings.ForkDebugHudHint,
                    Current = config.GetBindable<DebugHudMode>(OsuSetting.ForkDebugHudMode)
                })
                {
                    FavouriteId = "OsuSetting.ForkDebugHudMode",
                    Keywords = new[] { @"debug", @"hud", @"freeze", @"stutter", @"ram", @"gc" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ForkDebugToolbarMemoryCaption,
                    HintText = ForkSettingsStrings.ForkDebugToolbarMemoryHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkShowMemoryInToolbar)
                })
                {
                    FavouriteId = "OsuSetting.ForkShowMemoryInToolbar",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ForkDebugFreezeNotificationsCaption,
                    HintText = ForkSettingsStrings.ForkDebugFreezeNotificationsHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkDebugFreezeAlerts)
                })
                {
                    FavouriteId = "OsuSetting.ForkDebugFreezeAlerts",
                },
                new SettingsItemV2(new FormEnumDropdown<ReplayRenderQualityPreset>
                {
                    Caption = ForkSettingsStrings.ReplayQualityPresetCaption,
                    HintText = ForkSettingsStrings.ReplayQualityPresetHint,
                    Current = config.GetBindable<ReplayRenderQualityPreset>(OsuSetting.ForkReplayRenderQualityPreset)
                })
                {
                    FavouriteId = "OsuSetting.ForkReplayRenderQualityPreset",
                },
                new SettingsItemV2(new FormEnumDropdown<ReplayRenderDebugTraceMode>
                {
                    Caption = ForkSettingsStrings.ReplayTraceModeCaption,
                    HintText = ForkSettingsStrings.ReplayTraceModeHint,
                    Current = config.GetBindable<ReplayRenderDebugTraceMode>(OsuSetting.ForkReplayRenderDebugTraceMode)
                })
                {
                    FavouriteId = "OsuSetting.ForkReplayRenderDebugTraceMode",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ExportClicksOnlyCaption,
                    HintText = ForkSettingsStrings.ExportClicksOnlyHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkExportReplayOnlyClicks)
                })
                {
                    FavouriteId = "OsuSetting.ForkExportReplayOnlyClicks",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ShowBeatmapsWithMissingAudioCaption,
                    HintText = ForkSettingsStrings.ShowBeatmapsWithMissingAudioHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkShowBeatmapsWithMissingAudio)
                })
                {
                    FavouriteId = "OsuSetting.ForkShowBeatmapsWithMissingAudio",
                },
            };

            children.Insert(0, new DangerousSettingsButtonV2
            {
                Text = CrashReportStrings.TriggerCrash,
                TooltipText = CrashReportStrings.TriggerCrashHint,
                Action = () => dialogOverlay?.Push(new ConfirmDialog(CrashReportStrings.ConfirmCrash, () =>
                {
                    game.TriggerCrashReportTest();
                })),
            });
            children.Insert(0, new SettingsButtonV2
            {
                Text = global::osu.Game.Localisation.ClientTranslationStrings.J4043,
                TooltipText = global::osu.Game.Localisation.ClientTranslationStrings.J4044,
                Action = verifyPpCalculation,
            });
            if (diagnosticsManager != null)
            {
                children.InsertRange(2, new Drawable[]
                {
                    new DangerousSettingsButtonV2
                    {
                        Text = ForkSettingsStrings.ForkDebugCreateMemoryDumpCaption,
                        TooltipText = ForkSettingsStrings.ForkDebugCreateMemoryDumpHint,
                        Action = confirmMemoryDumpCapture,
                    },
                    new SettingsButtonV2
                    {
                        Text = ForkSettingsStrings.ForkDebugOpenMemoryDumpsFolderCaption,
                        Action = diagnosticsManager.OpenMemoryDumpFolder,
                    },
                });
            }

            Children = children;
        }

        [Resolved]
        private osu.Framework.Bindables.IBindable<osu.Game.Beatmaps.WorkingBeatmap> selectedBeatmap { get; set; } = null!;

        [Resolved]
        private GameHost gameHost { get; set; } = null!;

        private int ppVerificationInProgress;

        private void verifyPpCalculation()
        {
            if (Interlocked.Exchange(ref ppVerificationInProgress, 1) != 0)
                return;

            // Capture the selected map now, not after a background selection change.
            var working = selectedBeatmap.Value;
            osu.Game.Rulesets.IPpCalculationVerification? verifier;
            try
            {
                verifier = working.BeatmapInfo.Ruleset.CreateInstance() as osu.Game.Rulesets.IPpCalculationVerification;
                if (verifier == null)
                    throw new InvalidOperationException("Выбери Standard-карту перед проверкой ПП.");
            }
            catch (Exception e)
            {
                Interlocked.Exchange(ref ppVerificationInProgress, 0);
                notificationOverlay?.Post(new SimpleErrorNotification { Text = e.Message });
                return;
            }

            var notification = new ProgressNotification { Text = global::osu.Game.Localisation.ClientTranslationStrings.J4043, State = ProgressNotificationState.Active };
            notificationOverlay?.Post(notification);
            Task.Run(() =>
            {
                string stage = "запуск";
                using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                try
                {
                    using var linked = CancellationTokenSource.CreateLinkedTokenSource(notification.CancellationToken, timeout.Token);
                    string result = verifier.VerifyPpCalculation(working, (text, progress) =>
                    {
                        stage = text;
                        gameHost.UpdateThread.Scheduler.Add(() =>
                        {
                            notification.Text = "Проверка ПП: " + text;
                            notification.Progress = progress;
                        });
                    }, linked.Token);
                    Logger.Log(result);
                    gameHost.UpdateThread.Scheduler.Add(() =>
                    {
                        notification.CompletionText = global::osu.Game.Localisation.ClientTranslationStrings.J4046;
                        notification.State = ProgressNotificationState.Completed;
                        dialogOverlay?.Push(new PpVerificationResultDialog(result));
                    });
                }
                catch (OperationCanceledException)
                {
                    string reason = timeout.IsCancellationRequested ? "Превышен лимит 5 минут" : "Отменено пользователем";
                    Logger.Log($"Проверка ПП: {reason}; этап: {stage}");
                    gameHost.UpdateThread.Scheduler.Add(() =>
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        notificationOverlay?.Post(new SimpleNotification { Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4047(reason, stage) });
                    });
                }
                catch (Exception e)
                {
                    Logger.Error(e, "PP calculation verification failed at " + stage);
                    gameHost.UpdateThread.Scheduler.Add(() =>
                    {
                        notification.State = ProgressNotificationState.Cancelled;
                        notificationOverlay?.Post(new SimpleErrorNotification { Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4048(stage, e.Message) });
                    });
                }
                finally
                {
                    Interlocked.Exchange(ref ppVerificationInProgress, 0);
                }
            });
        }

        private partial class PpVerificationResultDialog : PopupDialog
        {
            public override float PreferredWidth => 760;

            public PpVerificationResultDialog(string result)
            {
                HeaderText = global::osu.Game.Localisation.ClientTranslationStrings.J4043;
                BodyText = "Старый / новый, медианы трёх проходов. Все числовые атрибуты и ПП совпали.\n\n"
                           + string.Join("\n", System.Linq.Enumerable.Where(result.Split('\n'), line => line.Contains("объектов;")))
                           + "\n\nПолный результат — в runtime.log. Проверяется агрегация обычного standard, не RX.";
                Buttons = new PopupDialogButton[] { new PopupDialogOkButton { Text = global::osu.Game.Localisation.ClientTranslationStrings.J4052 } };
            }
        }

        private void confirmMemoryDumpCapture()
        {
            dialogOverlay?.Push(new ConfirmDialog(
                ForkSettingsStrings.ForkDebugMemoryDumpConfirm,
                captureMemoryDump));
        }

        private void captureMemoryDump()
        {
            if (diagnosticsManager == null || Interlocked.Exchange(ref memoryDumpCaptureInProgress, 1) != 0)
                return;

            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = ForkSettingsStrings.ForkDebugMemoryDumpCreating,
            };

            notificationOverlay?.Post(notification);

            Task.Run(async () =>
            {
                try
                {
                    string path = await diagnosticsManager.CaptureMemoryDumpAsync(notification.CancellationToken).ConfigureAwait(false);
                    notification.CompletionText = ForkSettingsStrings.ForkDebugMemoryDumpSaved(Path.GetFileName(Path.GetDirectoryName(path)) ?? string.Empty);
                    notification.CompletionClickAction = () =>
                    {
                        diagnosticsManager.OpenMemoryDumpFolder();
                        return true;
                    };
                    notification.State = ProgressNotificationState.Completed;
                }
                catch (OperationCanceledException)
                {
                    notification.State = ProgressNotificationState.Cancelled;
                }
                catch (Exception e)
                {
                    Logger.Error(e, "Memory dump capture failed");
                    notification.State = ProgressNotificationState.Cancelled;
                    notificationOverlay?.Post(new SimpleErrorNotification
                    {
                        Text = ForkSettingsStrings.ForkDebugMemoryDumpError(e.Message),
                    });
                }
                finally
                {
                    Interlocked.Exchange(ref memoryDumpCaptureInProgress, 0);
                }
            });
        }

        private async void exportQuickLogs()
        {
            if (logExportInProgress)
                return;
            logExportInProgress = true;
            string? traceArchive = null;
            string? traceError = null;
            try
            {
                if (diagnosticsManager != null)
                    traceArchive = await diagnosticsManager.FinishPerformanceRecordingAsync();
            }
            catch (Exception e)
            {
                traceError = e.Message;
                Logger.Error(e, "Performance trace could not be finalised for log export");
            }

            try
            {
                string? freezeReport = performanceDebugService?.CreateCsvReport();
                string? zipPath = await Task.Run(() => createQuickLogsArchive(traceArchive, traceError, freezeReport));
                Schedule(() =>
                {
                    if (zipPath == null)
                    {
                        notificationOverlay?.Post(new SimpleNotification { Text = ForkSettingsStrings.ForkDebugNoRecentLogs });
                        return;
                    }
                    var exports = storage.GetStorageForDirectory("exports");
                    string name = Path.GetFileName(zipPath);
                    notificationOverlay?.Post(new SimpleNotification
                    {
                        Text = ForkSettingsStrings.QuickExportLogsSuccess(name),
                        Activated = () => exports.PresentFileExternally(name),
                    });
                    exports.PresentFileExternally(name);
                });
            }
            catch (Exception e)
            {
                Schedule(() => notificationOverlay?.Post(new SimpleErrorNotification { Text = ForkSettingsStrings.QuickExportLogsFailed(e.Message) }));
            }
            finally
            {
                Schedule(() => logExportInProgress = false);
            }
        }

        private string? createQuickLogsArchive(string? traceArchive, string? traceError, string? freezeReport)
        {
            var now = DateTimeOffset.UtcNow;
            var tenMinutesAgo = now.AddMinutes(-10);
            var filesToExport = new List<string>();

            var logsDir = storage.GetStorageForDirectory("logs").GetFullPath(string.Empty);
            var perfDir = storage.GetStorageForDirectory("performance").GetFullPath(string.Empty);
            var rendersDir = storage.GetStorageForDirectory("renders").GetFullPath(string.Empty);

            if (Directory.Exists(logsDir))
            {
                foreach (var file in Directory.GetFiles(logsDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(file) >= tenMinutesAgo)
                            filesToExport.Add(file);
                    }
                    catch { }
                }
            }

            if (Directory.Exists(perfDir))
            {
                foreach (var file in Directory.GetFiles(perfDir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(file) >= tenMinutesAgo)
                            filesToExport.Add(file);
                    }
                    catch { }
                }
            }

            if (Directory.Exists(rendersDir))
            {
                foreach (var file in Directory.GetFiles(rendersDir, "*.json", SearchOption.AllDirectories))
                {
                    try
                    {
                        if (File.GetLastWriteTimeUtc(file) >= tenMinutesAgo)
                            filesToExport.Add(file);
                    }
                    catch { }
                }
            }

            if (traceArchive != null && File.Exists(traceArchive))
                filesToExport.Add(traceArchive);

            if (filesToExport.Count == 0 && traceError == null && string.IsNullOrEmpty(freezeReport))
                return null;

            var exportStorage = storage.GetStorageForDirectory("exports");
            string zipFileName = $"logs_export_{DateTime.Now:yyyyMMdd_HHmmss_fff}.zip";

            try
            {
                using (var zipStream = exportStorage.GetStream(zipFileName, FileAccess.Write, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    if (traceError != null)
                    {
                        using var failure = new StreamWriter(archive.CreateEntry("performance/trace-error.txt").Open());
                        failure.Write(traceError);
                    }

                    if (freezeReport != null)
                    {
                        var freezeEntry = archive.CreateEntry("performance/freeze-events.csv");
                        using var freezeWriter = new StreamWriter(freezeEntry.Open());
                        freezeWriter.Write(freezeReport);
                    }

                    foreach (var filePath in filesToExport)
                    {
                        try
                        {
                            string entryName = Path.GetFileName(filePath);
                            string? parentDir = Path.GetFileName(Path.GetDirectoryName(filePath));
                            if (!string.IsNullOrEmpty(parentDir))
                                entryName = parentDir + "/" + entryName;

                            var entry = archive.CreateEntry(entryName);
                            using (var entryStream = entry.Open())
                            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                fileStream.CopyTo(entryStream);
                            }
                        }
                        catch (Exception ex)
                        {
                            // The trace is the point of this export: never silently report success without it.
                            if (filePath == traceArchive)
                                throw;
                            osu.Framework.Logging.Logger.Log($"Failed to add file {filePath} to quick export zip: {ex.Message}");
                        }
                    }
                }

                return exportStorage.GetFullPath(zipFileName);
            }
            catch
            {
                exportStorage.Delete(zipFileName);
                throw;
            }
        }
    }
}
