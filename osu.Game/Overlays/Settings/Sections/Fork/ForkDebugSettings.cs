// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkDebugSettings : SettingsSubsection
    {
        [Resolved]
        private Storage storage { get; set; } = null!;

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        protected override LocalisableString Header => ForkSettingsStrings.DebugHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            var children = new List<Drawable>
            {
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

            Children = children;
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

            if (filesToExport.Count == 0)
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
