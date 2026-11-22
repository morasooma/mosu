// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using System.Threading.Tasks;
using osu.Game.Performance.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Statistics;
using osu.Game.Graphics;
using osu.Game.IO;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Utils;
using SharpCompress.Archives.Zip;
using SharpCompress.Common;
using SharpCompress.Writers.Zip;

namespace osu.Game.Overlays.Settings.Sections.General
{
    public partial class QuickActionSettings : SettingsSubsection
    {
        [Resolved(CanBeNull = true)]
        private FirstRunSetupOverlay? firstRunSetupOverlay { get; set; }

        [Resolved(CanBeNull = true)]
        private OsuGame? game { get; set; }

        protected override LocalisableString Header => GeneralSettingsStrings.QuickActionsHeader;

        [BackgroundDependencyLoader]
        private void load(OsuColour colours, Storage storage)
        {
            AddRange(new Drawable[]
            {
                new SettingsButtonV2
                {
                    Text = GeneralSettingsStrings.RunSetupWizard,
                    Keywords = new[] { @"first run", @"initial", @"getting started", @"import", @"tutorial", @"recommended beatmaps" },
                    TooltipText = FirstRunSetupOverlayStrings.FirstRunSetupDescription,
                    Action = () => firstRunSetupOverlay?.Show(),
                },
                new SettingsButtonV2
                {
                    Text = GeneralSettingsStrings.LearnMoreAboutLazer,
                    TooltipText = GeneralSettingsStrings.LearnMoreAboutLazerTooltip,
                    BackgroundColour = colours.YellowDark,
                    Action = () => game?.OpenUrlExternally(@"/")
                },
            });

            Add(new SettingsButtonV2
            {
                Text = GeneralSettingsStrings.ExportLogs,
                BackgroundColour = colours.YellowDarker.Darken(0.5f),
                Keywords = new[] { @"bug", "report", "logs", "files" },
                Action = exportLogs,
            });

            exportStorage = (storage as OsuStorage)?.GetExportStorage() ?? storage.GetStorageForDirectory(@"exports");
        }

        [Resolved]
        private INotificationOverlay? notifications { get; set; }

        [Resolved(canBeNull: true)]
        private IPerformanceDiagnosticsManager? diagnosticsManager { get; set; }

        private Storage exportStorage = null!;
        private bool exporting;

        private async void exportLogs()
        {
            if (exporting)
                return;
            exporting = true;
            try
            {
                string? trace = diagnosticsManager == null ? null : await diagnosticsManager.FinishPerformanceRecordingAsync();
                await Task.Run(() => exportLogsWithTrace(trace));
            }
            catch (Exception e)
            {
                Logger.Error(e, "Log export failed");
                Schedule(() => notifications?.Post(new SimpleErrorNotification { Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4061(e.Message) }));
            }
            finally
            {
                Schedule(() => exporting = false);
            }
        }

        private void exportLogsWithTrace(string? trace)
        {
            ProgressNotification notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = NotificationsStrings.LogsExportOngoing,
            };

            notifications?.Post(notification);

            const string archive_filename = "compressed-logs.zip";

            try
            {
                GlobalStatistics.OutputToLog();
                Logger.Flush();

                var logStorage = Logger.Storage;

                using (var outStream = exportStorage.CreateFileSafely(archive_filename))
                using (var zip = ZipArchive.CreateArchive())
                {
                    foreach (string? f in logStorage.GetFiles(string.Empty, "*.log"))
                        FileUtils.AttemptOperation(z => z.AddEntry(f, logStorage.GetStream(f), closeStream: true), zip, throwOnFailure: false);

                    if (trace != null)
                        zip.AddEntry("performance/" + Path.GetFileName(trace), File.OpenRead(trace), closeStream: true);

                    zip.SaveTo(outStream, new ZipWriterOptions(CompressionType.Deflate));
                }
            }
            catch
            {
                notification.State = ProgressNotificationState.Cancelled;

                // cleanup if export is failed or canceled.
                exportStorage.Delete(archive_filename);
                throw;
            }

            notification.CompletionText = NotificationsStrings.LogsExportFinished;
            notification.CompletionClickAction = () => exportStorage.PresentFileExternally(archive_filename);

            notification.State = ProgressNotificationState.Completed;
        }
    }
}
