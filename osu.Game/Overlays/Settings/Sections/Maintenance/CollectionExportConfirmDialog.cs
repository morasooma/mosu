// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Dialog;
using osuTK;
using Realms;

namespace osu.Game.Overlays.Settings.Sections.Maintenance
{
    public partial class CollectionExportConfirmDialog : PopupDialog
    {
        private readonly OsuDropdown<BeatmapCollection> dropdown;

        public CollectionExportConfirmDialog(List<BeatmapCollection> collections, RealmAccess realm, Action<BeatmapCollection> onConfirm)
        {
            HeaderText = "Экспорт карт из коллекции";
            BodyText = "Выберите коллекцию для экспорта.";
            Icon = FontAwesome.Solid.FileArchive;

            var dropdownContainer = new Container
            {
                RelativeSizeAxes = Axes.X,
                Height = 45,
                Padding = new MarginPadding { Horizontal = 50 },
                Child = dropdown = new CollectionDropdown
                {
                    RelativeSizeAxes = Axes.X,
                    Items = collections,
                }
            };

            MainContent.Child = dropdownContainer;

            dropdown.Current.BindValueChanged(c =>
            {
                if (c.NewValue != null)
                {
                    int beatmapCount = realm.Run(r =>
                    {
                        var liveCollection = r.Find<BeatmapCollection>(c.NewValue.ID);
                        if (liveCollection == null)
                        {
                            osu.Framework.Logging.Logger.Log($"[ExportDebug] liveCollection is NULL for ID: {c.NewValue.ID}");
                            return 0;
                        }

                        var hashes = new HashSet<string>(liveCollection.BeatmapMD5Hashes);
                        osu.Framework.Logging.Logger.Log($"[ExportDebug] Collection: '{liveCollection.Name}', Hashes count: {hashes.Count}, ID: {liveCollection.ID}");
                        
                        if (hashes.Count == 0)
                            return 0;

                        // Log first 3 hashes
                        int printedHashes = 0;
                        foreach (var hash in hashes)
                        {
                            if (printedHashes++ >= 3) break;
                            osu.Framework.Logging.Logger.Log($"[ExportDebug] - Collection Hash: '{hash}'");
                        }

                        var allBeatmaps = r.All<BeatmapInfo>().Filter("BeatmapSet.DeletePending == false").ToList();
                        osu.Framework.Logging.Logger.Log($"[ExportDebug] Total non-deleted BeatmapInfo in DB: {allBeatmaps.Count}");

                        // Log first 3 beatmap hashes
                        int printedBeatmaps = 0;
                        foreach (var b in allBeatmaps)
                        {
                            if (printedBeatmaps++ >= 3) break;
                            osu.Framework.Logging.Logger.Log($"[ExportDebug] - DB Beatmap: '{b.Metadata.Title}', MD5: '{b.MD5Hash}'");
                        }

                        var matchingBeatmaps = allBeatmaps.Where(b => hashes.Contains(b.MD5Hash)).ToList();
                        osu.Framework.Logging.Logger.Log($"[ExportDebug] Matching beatmaps count: {matchingBeatmaps.Count}");

                        var matchingSets = matchingBeatmaps
                                .Select(b => b.BeatmapSet)
                                .Where(s => s != null)
                                .Select(s => s!)
                                .DistinctBy(s => s.ID)
                                .ToList();

                        osu.Framework.Logging.Logger.Log($"[ExportDebug] Unique matching beatmap sets: {matchingSets.Count}");
                        return matchingSets.Count;
                    });

                    BodyText = $"Коллекция: {c.NewValue.Name}\nБудет экспортировано {beatmapCount} сетов карт.";
                }
            }, true);

            Buttons = new PopupDialogButton[]
            {
                new PopupDialogOkButton
                {
                    Text = "Экспортировать",
                    Action = () =>
                    {
                        if (dropdown.Current.Value != null)
                            onConfirm(dropdown.Current.Value);
                    }
                },
                new PopupDialogCancelButton
                {
                    Text = "Отмена"
                }
            };
        }

        private partial class CollectionDropdown : OsuDropdown<BeatmapCollection>
        {
            protected override LocalisableString GenerateItemText(BeatmapCollection item) => item?.Name ?? string.Empty;
        }
    }
}
