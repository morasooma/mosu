// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using osu.Game.Beatmaps;
using osu.Framework.Platform;
using osu.Game.Extensions;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets;
using osu.Game.Utils;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Database
{
    public class LegacyScoreExporter : LegacyExporter<ScoreInfo>
    {
        private readonly osu.Game.Configuration.OsuConfigManager? configManager;

        public LegacyScoreExporter(Storage storage, osu.Game.Configuration.OsuConfigManager? configManager = null)
            : base(storage)
        {
            this.configManager = configManager;
        }

        protected override string GetFilename(ScoreInfo score)
        {
            string scoreString = score.GetDisplayString();
            string filename = $"{scoreString} ({score.Date.LocalDateTime:yyyy-MM-dd_HH-mm})";

            return filename;
        }

        protected override string FileExtension => @".osr";

        public async Task ExportAsync(Score score, IBeatmap? beatmap, CancellationToken cancellationToken = default)
        {
            string itemFilename = GetFilename(score.ScoreInfo).GetValidFilename();

            if (itemFilename.Length > MAX_FILENAME_LENGTH - FileExtension.Length)
                itemFilename = itemFilename.Remove(MAX_FILENAME_LENGTH - FileExtension.Length);

            IEnumerable<string> existingExports = ExportStorage
                                                  .GetFiles(string.Empty, $"{itemFilename}*{FileExtension}")
                                                  .Concat(ExportStorage.GetDirectories(string.Empty));

            string filename = NamingUtils.GetNextBestFilename(existingExports, $"{itemFilename}{FileExtension}");

            ProgressNotification notification = new ProgressNotification
            {
                State = ProgressNotificationState.Active,
                Text = NotificationsStrings.FileExportOngoing(itemFilename),
            };

            PostNotification?.Invoke(notification);

            using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, notification.CancellationToken);

            bool clientModsConverted = false;

            try
            {
                using var stream = ExportStorage.CreateFileSafely(filename);

                await Task.Run(() =>
                {
                    var exportScore = score.DeepClone();
                    exportScore.ScoreInfo.Mods = LegacyScoreExportModConverter.GetExportMods(exportScore.ScoreInfo.Ruleset.CreateInstance(), beatmap, exportScore.ScoreInfo.Mods, out clientModsConverted);

                    bool exportOnlyClicks = configManager?.Get<bool>(osu.Game.Configuration.OsuSetting.ForkExportReplayOnlyClicks) == true;

                    if (exportOnlyClicks && exportScore.Replay != null)
                    {
                        var filteredFrames = new List<osu.Game.Rulesets.Replays.ReplayFrame>();
                        osu.Game.Replays.Legacy.ReplayButtonState lastButtonState = osu.Game.Replays.Legacy.ReplayButtonState.None;

                        var holdObjects = beatmap?.HitObjects.Where(h => osu.Game.Rulesets.Objects.HitObjectExtensions.GetEndTime(h) > h.StartTime).ToList() ?? new List<osu.Game.Rulesets.Objects.HitObject>();

                        foreach (var frame in exportScore.Replay.Frames)
                        {
                            osu.Game.Replays.Legacy.LegacyReplayFrame legacyFrame;
                            if (frame is osu.Game.Replays.Legacy.LegacyReplayFrame lf)
                                legacyFrame = lf;
                            else if (frame is osu.Game.Rulesets.Replays.Types.IConvertibleReplayFrame convertibleFrame && beatmap != null)
                                legacyFrame = convertibleFrame.ToLegacy(beatmap);
                            else
                                continue;

                            bool stateChanged = legacyFrame.ButtonState != lastButtonState;
                            bool shouldSave = false;

                            if (stateChanged)
                            {
                                shouldSave = true;
                            }
                            else if (legacyFrame.ButtonState != osu.Game.Replays.Legacy.ReplayButtonState.None)
                            {
                                bool isSliderOrSpinnerActive = false;
                                foreach (var obj in holdObjects)
                                {
                                    if (legacyFrame.Time >= obj.StartTime && legacyFrame.Time <= osu.Game.Rulesets.Objects.HitObjectExtensions.GetEndTime(obj))
                                    {
                                        isSliderOrSpinnerActive = true;
                                        break;
                                    }
                                }

                                if (isSliderOrSpinnerActive)
                                    shouldSave = true;
                            }

                            if (shouldSave)
                            {
                                filteredFrames.Add(legacyFrame);
                                lastButtonState = legacyFrame.ButtonState;
                            }
                        }
                        exportScore.Replay.Frames = filteredFrames;
                    }

                    new LegacyScoreEncoder(exportScore, beatmap).Encode(stream);
                }, linkedSource.Token).ConfigureAwait(false);
            }
            catch
            {
                notification.State = ProgressNotificationState.Cancelled;

                ExportStorage.Delete(filename);
                throw;
            }

            if (clientModsConverted)
                PostNotification?.Invoke(new ProgressCompletionNotification { Text = "Client mods were converted for export." });

            notification.CompletionText = NotificationsStrings.FileExportFinished(itemFilename);
            notification.CompletionClickAction = () => ExportStorage.PresentFileExternally(filename);
            notification.State = ProgressNotificationState.Completed;
        }

        public override void ExportToStream(ScoreInfo model, Stream outputStream, ProgressNotification? notification, CancellationToken cancellationToken = default)
        {
            var file = model.Files.SingleOrDefault();
            if (file == null)
                return;

            using var inputStream = UserFileStorage.GetStream(file.File.GetStoragePath());
            inputStream?.CopyTo(outputStream);
        }
    }
}
