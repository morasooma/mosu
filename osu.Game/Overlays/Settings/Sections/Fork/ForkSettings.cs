// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Platform;
using osu.Game.Overlays.Settings.Sections.General;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings;
using osu.Game.Beatmaps;
using osu.Game.Collections;
using osu.Game.Database;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings.Sections.Maintenance;
using osu.Game.Overlays.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using Realms;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Game.Online.API;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class ForkSettings : SettingsSubsection
    {
        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private IDialogOverlay? dialogOverlay { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        [Resolved]
        private SettingsOverlay? settingsOverlay { get; set; }

        [Resolved]
        private INotificationOverlay? notificationOverlay { get; set; }

        [Resolved(canBeNull: true)]
        private IBeatmapApiProvider? beatmapApi { get; set; }

        protected override LocalisableString Header => ForkSettingsStrings.ForkSettingsHeader;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, Storage storage)
        {
            var endTagEnabled = config.GetBindable<bool>(OsuSetting.ForkMorasoomaEndTag);
            var endTagText = new SettingsItemV2(new FormTextBox
            {
                Caption = SmokeTagStrings.TextCaption,
                HintText = SmokeTagStrings.TextHint,
                Current = config.GetBindable<string>(OsuSetting.ForkMorasoomaEndTagText),
                LengthLimit = 16,
            })
            {
                FavouriteId = "OsuSetting.ForkMorasoomaEndTagText",
                CanBeShown = { BindTarget = endTagEnabled },
            };
            var useStable = config.GetBindable<bool>(OsuSetting.ForkUseStableDirectoryDirectly);
            var stablePath = config.GetBindable<string>(OsuSetting.ForkStableDirectoryPath);
            var useOfficialBeatmapService = config.GetBindable<bool>(OsuSetting.ForkUseOfficialBeatmapService);
            var officialBeatmapServiceWarning = new Bindable<SettingsNote.Data?>();
            var pendingStablePath = new Bindable<string>(stablePath.Value);
            stablePath.BindValueChanged(change => pendingStablePath.Value = change.NewValue);
            useOfficialBeatmapService.BindValueChanged(enabled =>
            {
                officialBeatmapServiceWarning.Value = enabled.NewValue
                    ? new SettingsNote.Data(
                        ForkSettingsStrings.UseOfficialOsuBeatmapServiceWarning,
                        SettingsNote.Type.Critical,
                        SettingsNote.Accent.Negative)
                    : null;
            }, true);
            var stablePathTextBox = new FormTextBox
            {
                Caption = ForkSettingsStrings.CustomStableDirectoryCaption,
                HintText = ForkSettingsStrings.CustomStableDirectoryHint,
                PlaceholderText = ForkSettingsStrings.CustomStableDirectoryPlaceholder,
                Current = pendingStablePath,
            };
            stablePathTextBox.OnCommit += (_, newText) =>
            {
                if (newText)
                    stablePath.Value = pendingStablePath.Value.Trim();
            };

            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = PracticeStrings.SettingCaption,
                    HintText = PracticeStrings.SettingHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkPracticeMode),
                })
                {
                    FavouriteId = "OsuSetting.ForkPracticeMode",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ManiaEditorStrings.AutoSeekOnPlacementCaption,
                    HintText = ManiaEditorStrings.AutoSeekOnPlacementHint,
                    Current = config.GetEditorAutoSeekOnPlacement("mania"),
                })
                {
                    FavouriteId = "OsuSetting.ForkManiaEditorAutoSeekOnPlacement",
                    Keywords = new[] { "mania", "mapping", "editor", "autoscroll", "мания", "редактор", "прокрутка" },
                },
                new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.AboutMorasoomaButton,
                    Action = () => dialogOverlay?.Push(new AboutMorasoomaDialog())
                },
                new SettingsButtonV2
                {
                    Text = FavouriteSettingsStrings.Title,
                    Keywords = new[] { "favourite", "favorite", "pinned", "quick", "избранные", "закреплённые" },
                    Action = () => settingsOverlay?.ShowFavouriteSettings(),
                },
                new CheckForUpdatesButton(),
                new SettingsButtonV2
                {
                    Text = GeneralSettingsStrings.OpenOsuFolder,
                    Keywords = new[] { @"logs", @"files", @"access", @"directory" },
                    Action = () => storage.PresentExternally(),
                    Enabled = { Value = RuntimeInfo.IsDesktop },
                },
                new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.TabletSettingsBtn,
                    Action = () => settingsOverlay?.ShowAtControl<osu.Game.Overlays.Settings.Sections.Input.TabletSettings>()
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ReduceVolumeOutsideGameplayCaption,
                    HintText = ForkSettingsStrings.ReduceVolumeOutsideGameplayHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkReduceVolumeOutsideGameplay)
                })
                {
                    FavouriteId = "OsuSetting.ForkReduceVolumeOutsideGameplay",
                },
                new SettingsItemV2(new FormEnumDropdown<osu.Game.Online.API.DownloadMirror>
                {
                    Caption = ForkSettingsStrings.DownloadMirrorCaption,
                    Current = config.GetBindable<osu.Game.Online.API.DownloadMirror>(OsuSetting.BeatmapDownloadMirror)
                })
                {
                    FavouriteId = "OsuSetting.BeatmapDownloadMirror",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.UseOfficialOsuBeatmapServiceCaption,
                    HintText = ForkSettingsStrings.UseOfficialOsuBeatmapServiceHint,
                    Current = useOfficialBeatmapService
                })
                {
                    FavouriteId = "ui:ForkSettingsStrings.UseOfficialOsuBeatmapServiceCaption",
                    Note = { BindTarget = officialBeatmapServiceWarning }
                },
                new OfficialOsuAccountStatusButton(beatmapApi),
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.UseStableDirectoryCaption,
                    HintText = ForkSettingsStrings.UseStableDirectoryHint,
                    Current = useStable
                })
                {
                    FavouriteId = "ui:ForkSettingsStrings.UseStableDirectoryCaption",
                },
                new SettingsItemV2(stablePathTextBox)
                {
                    FavouriteId = "ui:ForkSettings:stablePathTextBox",
                },
                new SettingsItemV2(new FormEnumDropdown<ForkRelaxPpSystem>
                {
                    Caption = ForkSettingsStrings.RelaxPpSystemCaption,
                    HintText = ForkSettingsStrings.RelaxPpSystemHint,
                    Current = config.GetBindable<ForkRelaxPpSystem>(OsuSetting.ForkRelaxPpSystem)
                })
                {
                    FavouriteId = "OsuSetting.ForkRelaxPpSystem",
                },
                new SettingsItemV2(new FormTextBox
                {
                    Caption = ForkSettingsStrings.CustomUsernameCaption,
                    HintText = ForkSettingsStrings.CustomUsernameHint,
                    PlaceholderText = ForkSettingsStrings.CustomUsernamePlaceholder,
                    Current = config.GetBindable<string>(OsuSetting.ForkCustomUsername)
                })
                {
                    FavouriteId = "OsuSetting.ForkCustomUsername",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.MorasoomaEndTagCaption,
                    HintText = SmokeTagStrings.Hint,
                    Current = endTagEnabled
                })
                {
                    FavouriteId = "OsuSetting.ForkMorasoomaEndTag",
                    Keywords = new[] { @"morasooma", @"tag", @"smoke", @"replay", @"signature" },
                },
                endTagText,
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.OverrideRecDiffCaption,
                    HintText = ForkSettingsStrings.OverrideRecDiffHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkCustomRecommendedDifficultyEnabled)
                })
                {
                    FavouriteId = "OsuSetting.ForkCustomRecommendedDifficultyEnabled",
                },
                new SettingsItemV2(new FormSliderBar<double>
                {
                    Caption = ForkSettingsStrings.CustomRecDiffCaption,
                    Current = config.GetBindable<double>(OsuSetting.ForkCustomRecommendedDifficulty),
                    KeyboardStep = 0.1f,
                    LabelFormat = value => $"{value:0.0}*"
                })
                {
                    FavouriteId = "OsuSetting.ForkCustomRecommendedDifficulty",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ForkDisableBeatmapStatusOverwriteCaption,
                    HintText = ForkSettingsStrings.ForkDisableBeatmapStatusOverwriteHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkDisableBeatmapStatusOverwrite)
                })
                {
                    FavouriteId = "OsuSetting.ForkDisableBeatmapStatusOverwrite",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ForkClassicModDefaultCaption,
                    Current = config.GetBindable<bool>(OsuSetting.ForkClassicModDefault)
                })
                {
                    FavouriteId = "OsuSetting.ForkClassicModDefault",
                },
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.ForkHideVisualSliderMissesCaption,
                    Current = config.GetBindable<bool>(OsuSetting.ForkHideVisualSliderMisses)
                })
                {
                    FavouriteId = "OsuSetting.ForkHideVisualSliderMisses",
                },
                new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.ExportAllBeatmapsBtn,
                    Action = () =>
                    {
                        int beatmapCount = realm.Run(r => r.All<BeatmapSetInfo>().Filter("DeletePending == false").Count());
                        dialogOverlay?.Push(new ConfirmDialog(ForkSettingsStrings.ExportAllBeatmapsConfirm(beatmapCount.ToString()), () =>
                        {
                            var setIds = realm.Run(r => r.All<BeatmapSetInfo>()
                                                         .Filter("DeletePending == false")
                                                         .ToList()
                                                         .Select(s => s.ID)
                                                         .ToList());
                            beatmaps.ExportMultiple(setIds);
                        }));
                    }
                },
                new SettingsButtonV2
                {
                    Text = ForkSettingsStrings.ExportCollectionBtn,
                    Action = () =>
                    {
                        var collections = realm.Run(r =>
                        {
                            var list = r.All<BeatmapCollection>().OrderBy(c => c.Name).ToList();
                            foreach (var coll in list)
                            {
                                osu.Framework.Logging.Logger.Log($"[ExportDebug] BEFORE DETACH: Collection '{coll.Name}', hashes count: {coll.BeatmapMD5Hashes.Count}, ID: {coll.ID}");
                            }
                            return list.Detach();
                        });
                        if (collections.Count == 0)
                        {
                            notificationOverlay?.Post(new ProgressCompletionNotification { Text = ForkSettingsStrings.ExportCollectionEmpty });
                            return;
                        }

                        dialogOverlay?.Push(new CollectionExportConfirmDialog(collections, realm, collection =>
                        {
                            var setIds = realm.Run(r =>
                            {
                                var liveCollection = r.Find<BeatmapCollection>(collection.ID);
                                if (liveCollection == null)
                                {
                                    osu.Framework.Logging.Logger.Log($"[ExportDebug] [Action] liveCollection is NULL for ID: {collection.ID}");
                                    return new List<Guid>();
                                }

                                var hashes = new HashSet<string>(liveCollection.BeatmapMD5Hashes);
                                osu.Framework.Logging.Logger.Log($"[ExportDebug] [Action] Selected collection: '{liveCollection.Name}', Hashes count: {hashes.Count}, ID: {liveCollection.ID}");
                                if (hashes.Count == 0)
                                    return new List<Guid>();

                                var allBeatmaps = r.All<BeatmapInfo>().Filter("BeatmapSet.DeletePending == false").ToList();
                                var matchingBeatmaps = allBeatmaps.Where(b => hashes.Contains(b.MD5Hash)).ToList();
                                osu.Framework.Logging.Logger.Log($"[ExportDebug] [Action] Matching beatmaps count: {matchingBeatmaps.Count}");

                                return matchingBeatmaps
                                        .Select(b => b.BeatmapSet)
                                        .Where(s => s != null)
                                        .Select(s => s!.ID)
                                        .Distinct()
                                        .ToList();
                            });

                            if (setIds.Count > 0)
                                beatmaps.ExportMultiple(setIds);
                            else
                                notificationOverlay?.Post(new ProgressCompletionNotification { Text = ForkSettingsStrings.ExportCollectionMatchEmpty });
                        }));
                    }
                },
            };


        }

        private sealed partial class AboutMorasoomaDialog : PopupDialog
        {
            public AboutMorasoomaDialog()
            {
                HeaderText = ForkSettingsStrings.AboutMorasoomaHeader;
                BodyText = ForkSettingsStrings.AboutMorasoomaDescription;
                Icon = FontAwesome.Solid.InfoCircle;
                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton
                    {
                        Text = ForkSettingsStrings.AboutMorasoomaClose,
                    },
                };
            }
        }
    }
}
