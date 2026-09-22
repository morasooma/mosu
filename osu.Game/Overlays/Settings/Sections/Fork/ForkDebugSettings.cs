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

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private PerformanceDebugService? performanceDebugService { get; set; }

        [Resolved(canBeNull: true)]
        private IPerformanceDiagnosticsManager? diagnosticsManager { get; set; }

        private SettingsButtonV2 forceDatabaseConversionButton = null!;
        private int memoryDumpCaptureInProgress;

        protected override LocalisableString Header => ForkSettingsStrings.DebugHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            var children = new List<Drawable>
            {
                (forceDatabaseConversionButton = new DangerousSettingsButtonV2
                {
                    Text = "Принудительно конвертировать newer-version БД",
                    TooltipText = "Находит последний client*_newer_version.realm, конвертирует его и принудительно устанавливает после перезапуска. Текущая client.realm останется в backup.",
                    Action = () => _ = forceDatabaseConversionAsync()
                }),
                new DangerousSettingsButtonV2
                {
                    Text = ForkSettingsStrings.QuickExportLogsBtn,
                    Action = exportQuickLogs
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.PerfLoggingCaption,
                    HintText = ForkSettingsStrings.PerfLoggingHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkPerformanceLogging)
                })
                {
                    Keywords = new[] { @"performance", @"logging", @"fps", @"frametime", @"cpu", @"metrics" },
                },
                new SettingsItemV2(new FormEnumDropdown<DebugHudMode>
                {
                    Caption = "Диагностический HUD",
                    HintText = "Показывает frametime потоков, память, GC и вероятную причину фризов. Подробный режим выводит дополнительные счётчики GC.",
                    Current = config.GetBindable<DebugHudMode>(OsuSetting.ForkDebugHudMode)
                })
                {
                    Keywords = new[] { @"debug", @"hud", @"freeze", @"stutter", @"ram", @"gc" },
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Показывать память в верхней панели",
                    HintText = "Добавляет компактный индикатор RAM и размера .NET GC heap в toolbar.",
                    Current = config.GetBindable<bool>(OsuSetting.ForkShowMemoryInToolbar)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = "Показывать уведомления о фризах",
                    HintText = "На несколько секунд показывает длительность и вероятную причину кадра дольше 20 мс.",
                    Current = config.GetBindable<bool>(OsuSetting.ForkDebugFreezeAlerts)
                }),
                new SettingsItemV2(new FormEnumDropdown<ReplayRenderQualityPreset>
                {
                    Caption = ForkSettingsStrings.ReplayQualityPresetCaption,
                    HintText = ForkSettingsStrings.ReplayQualityPresetHint,
                    Current = config.GetBindable<ReplayRenderQualityPreset>(OsuSetting.ForkReplayRenderQualityPreset)
                }),
                new SettingsItemV2(new FormEnumDropdown<ReplayRenderDebugTraceMode>
                {
                    Caption = ForkSettingsStrings.ReplayTraceModeCaption,
                    HintText = ForkSettingsStrings.ReplayTraceModeHint,
                    Current = config.GetBindable<ReplayRenderDebugTraceMode>(OsuSetting.ForkReplayRenderDebugTraceMode)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ExportClicksOnlyCaption,
                    HintText = ForkSettingsStrings.ExportClicksOnlyHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkExportReplayOnlyClicks)
                }),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ShowBeatmapsWithMissingAudioCaption,
                    HintText = ForkSettingsStrings.ShowBeatmapsWithMissingAudioHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkShowBeatmapsWithMissingAudio)
                }),
            };

#if DEBUG
            children.Insert(0, new SettingsItemV2(new FormEnumDropdown<GameplayIntegrityDebugScenario>
            {
                Caption = "Античит: тестовый сигнал (только DEBUG)",
                HintText = "Одноразово подменяет только итоговый integrity-отчёт следующей игры и автоматически вернётся в None. Не влияет на gameplay.",
                Current = config.GetBindable<GameplayIntegrityDebugScenario>(OsuSetting.ForkGameplayIntegrityDebugScenario),
            }));
#endif

            if (diagnosticsManager != null)
            {
                children.InsertRange(2, new Drawable[]
                {
                    new DangerousSettingsButtonV2
                    {
                        Text = "Создать дамп памяти",
                        TooltipText = "Дамп может содержать токены, личные данные и другие секреты из памяти. Никому не отправляйте его целиком без доверия к получателю.",
                        Action = confirmMemoryDumpCapture,
                    },
                    new SettingsButtonV2
                    {
                        Text = "Открыть папку дампов памяти",
                        Action = diagnosticsManager.OpenMemoryDumpFolder,
                    },
                });
            }

            Children = children;
        }

        private void confirmMemoryDumpCapture()
        {
            dialogOverlay?.Push(new ConfirmDialog(
                "Дамп памяти может занимать несколько гигабайт и содержать токены авторизации, личные данные, сообщения, пути к файлам и другие секреты из памяти процесса. Не публикуйте и не отправляйте файл недоверенным людям. Продолжить?",
                captureMemoryDump));
        }

        private void captureMemoryDump()
        {
            if (diagnosticsManager == null || Interlocked.Exchange(ref memoryDumpCaptureInProgress, 1) != 0)
                return;

            var notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = "Создаётся дамп памяти. Клиент может временно зависнуть…",
            };

            notificationOverlay?.Post(notification);

            Task.Run(async () =>
            {
                try
                {
                    string path = await diagnosticsManager.CaptureMemoryDumpAsync(notification.CancellationToken).ConfigureAwait(false);
                    notification.CompletionText = $"Дамп памяти сохранён: {Path.GetFileName(Path.GetDirectoryName(path))}";
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
                        Text = $"Не удалось создать дамп памяти: {e.Message}",
                    });
                }
                finally
                {
                    Interlocked.Exchange(ref memoryDumpCaptureInProgress, 0);
                }
            });
        }

        private async Task forceDatabaseConversionAsync()
        {
            forceDatabaseConversionButton.Enabled.Value = false;

            try
            {
                bool succeeded = await Task.Run(realm.TryForceConvertNewerVersionedDatabase).ConfigureAwait(true);

                notificationOverlay?.Post(new SimpleNotification
                {
                    Text = succeeded
                        ? "База успешно сконвертирована. Перезапустите клиент для принудительной установки; текущая client.realm будет сохранена в backup."
                        : "Подходящий newer-version backup не найден или конвертация завершилась ошибкой. Оригинальные файлы не изменены."
                });
            }
            catch (Exception e)
            {
                Logger.Error(e, "Forced newer-version database conversion failed.");
                notificationOverlay?.Post(new SimpleErrorNotification
                {
                    Text = "Принудительная конвертация базы завершилась ошибкой. Оригинальные файлы не изменены."
                });
            }
            finally
            {
                forceDatabaseConversionButton.Enabled.Value = true;
            }
        }

        private void exportQuickLogs()
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

            if (filesToExport.Count == 0 && (performanceDebugService == null || performanceDebugService.FreezeEvents.Count == 0))
            {
                notificationOverlay?.Post(new SimpleNotification
                {
                    Text = "Не найдено логов или рендеров за последние 10 минут."
                });
                return;
            }

            var exportStorage = storage.GetStorageForDirectory("exports");
            string zipFileName = $"logs_export_{DateTime.Now:yyyyMMdd_HHmmss}.zip";

            try
            {
                using (var zipStream = exportStorage.GetStream(zipFileName, FileAccess.Write, FileMode.Create))
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
                {
                    if (performanceDebugService != null)
                    {
                        var freezeEntry = archive.CreateEntry("performance/freeze-events.csv");
                        using var freezeWriter = new StreamWriter(freezeEntry.Open());
                        freezeWriter.Write(performanceDebugService.CreateCsvReport());
                    }

                    foreach (var filePath in filesToExport)
                    {
                        try
                        {
                            string entryName = Path.GetFileName(filePath);
                            string? parentDir = Path.GetFileName(Path.GetDirectoryName(filePath));
                            if (!string.IsNullOrEmpty(parentDir))
                                entryName = Path.Combine(parentDir, entryName);

                            var entry = archive.CreateEntry(entryName);
                            using (var entryStream = entry.Open())
                            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                            {
                                fileStream.CopyTo(entryStream);
                            }
                        }
                        catch (Exception ex)
                        {
                            osu.Framework.Logging.Logger.Log($"Failed to add file {filePath} to quick export zip: {ex.Message}");
                        }
                    }
                }

                notificationOverlay?.Post(new SimpleNotification
                {
                    Text = $"Логи успешно экспортированы в {zipFileName}!",
                    Activated = () =>
                    {
                        return exportStorage.PresentFileExternally(zipFileName);
                    }
                });

                exportStorage.PresentFileExternally(zipFileName);
            }
            catch (Exception ex)
            {
                notificationOverlay?.Post(new SimpleNotification
                {
                    Text = $"Ошибка при экспорте логов: {ex.Message}"
                });
            }
        }
    }
}
