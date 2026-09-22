// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Skinning;
using osuTK;
using Realms;
using WebCommonStrings = osu.Game.Resources.Localisation.Web.CommonStrings;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class SkinSection : SettingsSection
    {
        private SkinDropdown skinDropdown;
        private readonly BindableNumber<int> maniaNoteScale = new BindableNumber<int>(100)
        {
            MinValue = 20,
            MaxValue = 100,
            Precision = 1,
            Default = 100,
        };
        private BindableNumber<int> boundManiaNoteScale;

        public override LocalisableString Header => SkinSettingsStrings.SkinSectionHeader;

        public override Drawable CreateIcon() => new SpriteIcon
        {
            Icon = OsuIcon.SkinB
        };

        public override IEnumerable<LocalisableString> FilterTerms => base.FilterTerms.Concat(new LocalisableString[] { "skins" });

        private readonly List<Live<SkinInfo>> dropdownItems = new List<Live<SkinInfo>>();
        private readonly List<RulesetSkinDropdown> rulesetSkinDropdowns = new List<RulesetSkinDropdown>();

        [Resolved]
        private SkinManager skins { get; set; }

        [Resolved]
        private RealmAccess realm { get; set; }

        private IDisposable realmSubscription;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load([CanBeNull] SkinEditorOverlay skinEditor, OsuConfigManager config)
        {
            var separateSkinsPerRuleset = config.GetBindable<bool>(OsuSetting.ForkSeparateSkinsPerRuleset);

            var maniaNoteScaleItem = new SettingsItemV2(new FormSliderBar<int>
            {
                Caption = "Mania playfield scale",
                HintText = "Scales the mania notefield around the receptor line, including notes, receptors, judgements and column spacing.",
                Current = maniaNoteScale,
                KeyboardStep = 1,
                LabelFormat = value => $"{value}%",
            });

            maniaNoteScaleItem.SettingChanged += saveManiaNoteScale;

            SettingsItemV2 createRulesetSkinItem(LocalisableString caption, OsuSetting setting)
            {
                var dropdown = new RulesetSkinDropdown(
                    skins,
                    config.GetBindable<string>(setting),
                    config.GetBindable<string>(OsuSetting.Skin),
                    separateSkinsPerRuleset)
                {
                    AlwaysShowSearchBar = true,
                    AllowNonContiguousMatching = true,
                    Caption = caption,
                };

                rulesetSkinDropdowns.Add(dropdown);

                return new SettingsItemV2(dropdown)
                {
                    CanBeShown = { BindTarget = separateSkinsPerRuleset },
                };
            }

            Children = new Drawable[]
            {
                new SettingsItemV2(skinDropdown = new SkinDropdown
                {
                    AlwaysShowSearchBar = true,
                    AllowNonContiguousMatching = true,
                    Caption = SkinSettingsStrings.CurrentSkin,
                    Current = skins.CurrentSkinInfo,
                }),
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 5),
                    Padding = SettingsPanel.CONTENT_PADDING,
                    Children = new Drawable[]
                    {
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 40,
                            Direction = FillDirection.Horizontal,
                            Children = new Drawable[]
                            {
                                new RenameSkinButton { Padding = new MarginPadding { Right = 2.5f }, RelativeSizeAxes = Axes.X, Width = 0.5f },
                                new ExportSkinButton { Padding = new MarginPadding { Left = 2.5f }, RelativeSizeAxes = Axes.X, Width = 0.5f },
                            },
                        },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 40,
                            Direction = FillDirection.Horizontal,
                            Children = new Drawable[]
                            {
                                new EditSkinIniButton(skinEditor) { Padding = new MarginPadding { Right = 2.5f }, RelativeSizeAxes = Axes.X, Width = 0.5f },
                                new DeleteSkinButton { Padding = new MarginPadding { Left = 2.5f }, RelativeSizeAxes = Axes.X, Width = 0.5f },
                            },
                        },
                    },
                },
                new SettingsButtonV2
                {
                    Text = SkinSettingsStrings.SkinLayoutEditor,
                    Action = () => skinEditor?.ToggleVisibility(),
                },
                new SkinPinButton(),
                maniaNoteScaleItem,
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = ForkSettingsStrings.SeparateSkinsPerRulesetCaption,
                    HintText = ForkSettingsStrings.SeparateSkinsPerRulesetHint,
                    Current = separateSkinsPerRuleset,
                }),
                createRulesetSkinItem(ForkSettingsStrings.OsuSkinCaption, OsuSetting.ForkOsuSkin),
                createRulesetSkinItem(ForkSettingsStrings.TaikoSkinCaption, OsuSetting.ForkTaikoSkin),
                createRulesetSkinItem(ForkSettingsStrings.CatchSkinCaption, OsuSetting.ForkCatchSkin),
                createRulesetSkinItem(ForkSettingsStrings.ManiaSkinCaption, OsuSetting.ForkManiaSkin),
                createRulesetSkinItem(ForkSettingsStrings.DodgeSkinCaption, OsuSetting.ForkDodgeSkin),
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            realmSubscription = realm.RegisterForNotifications(_ => realm.Realm.All<SkinInfo>()
                                                                         .Where(s => !s.DeletePending)
                                                                         .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase), skinsChanged);

            skins.PinnedSkins.Changed += refreshSkinList;

            skinDropdown.Current.BindValueChanged(skin =>
            {
                if (skin.NewValue.ID == SkinInfo.RANDOM_SKIN)
                {
                    // before selecting random, set the skin back to the previous selection.
                    // this is done because at this point it will be random_skin_info, and would
                    // cause SelectRandomSkin to be unable to skip the previous selection.
                    skins.CurrentSkinInfo.Value = skin.OldValue;
                    skins.SelectRandomSkin();
                }
            });

            skins.CurrentSkin.BindValueChanged(_ => updateManiaNoteScaleSource(), true);
        }

        private void updateManiaNoteScaleSource()
        {
            if (boundManiaNoteScale != null)
                maniaNoteScale.UnbindFrom(boundManiaNoteScale);

            boundManiaNoteScale = skins.CurrentSkin.Value.MosuSettings.ManiaNoteScalePercent;
            maniaNoteScale.BindTo(boundManiaNoteScale);
        }

        private void saveManiaNoteScale()
        {
            int value = maniaNoteScale.Value;

            if (skins.EnsureMutableSkin())
                skins.CurrentSkin.Value.MosuSettings.SetManiaNoteScalePercent(value);

            skins.Save(skins.CurrentSkin.Value);
        }

        private void skinsChanged(IRealmCollection<SkinInfo> sender, ChangeSet changes)
        {
            // This can only mean that realm is recycling, else we would see the protected skins.
            // Because we are using `Live<>` in this class, we don't need to worry about this scenario too much.
            if (!sender.Any())
                return;
            // For simplicity repopulate the full list.
            dropdownItems.Clear();
            dropdownItems.AddRange(skins.GetAllUsableSkins());

            Schedule(() =>
            {
                skinDropdown.Items = dropdownItems;

                foreach (var dropdown in rulesetSkinDropdowns)
                    dropdown.SetItems(dropdownItems);
            });
        }

        private void refreshSkinList() => Schedule(() =>
        {
            dropdownItems.Clear();
            dropdownItems.AddRange(skins.GetAllUsableSkins());
            skinDropdown.Items = dropdownItems;

            foreach (var dropdown in rulesetSkinDropdowns)
                dropdown.SetItems(dropdownItems);
        });

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            realmSubscription?.Dispose();

            if (skins != null)
                skins.PinnedSkins.Changed -= refreshSkinList;
        }

        private partial class SkinDropdown : FormDropdown<Live<SkinInfo>>
        {
            [Resolved]
            private SkinManager skinManager { get; set; }

            protected override LocalisableString GenerateItemText(Live<SkinInfo> item)
                => item.PerformRead(s => skinManager.PinnedSkins.IsPinned(item.ID) ? $"♥ {s}" : s.ToString());
        }

        public partial class SkinPinButton : SettingsButtonV2
        {
            [Resolved]
            private SkinManager skins { get; set; }

            private Bindable<Skin> currentSkin;
            private SpriteIcon icon;

            [BackgroundDependencyLoader]
            private void load()
            {
                Action = togglePinned;
                Content.Add(icon = new SpriteIcon
                {
                    Anchor = Anchor.CentreLeft,
                    Origin = Anchor.CentreLeft,
                    X = 16,
                    Size = new Vector2(16),
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                currentSkin = skins.CurrentSkin.GetBoundCopy();
                currentSkin.BindValueChanged(_ => updateState());
                currentSkin.BindDisabledChanged(_ => updateState(), true);
                skins.PinnedSkins.Changed += onPinnedChanged;
            }

            private void onPinnedChanged() => Schedule(updateState);

            private void updateState()
            {
                bool pinned = skins.PinnedSkins.IsPinned(currentSkin.Value.SkinInfo.ID);
                Text = pinned ? SkinSettingsStrings.UnpinSkin : SkinSettingsStrings.PinSkin;
                icon.Icon = pinned ? FontAwesome.Solid.Heart : FontAwesome.Regular.Heart;
                Enabled.Value = !currentSkin.Disabled;
            }

            private void togglePinned() => skins.TogglePinned(skins.CurrentSkinInfo.Value);

            protected override void Dispose(bool isDisposing)
            {
                if (skins != null)
                    skins.PinnedSkins.Changed -= onPinnedChanged;

                base.Dispose(isDisposing);
            }
        }

        private partial class RulesetSkinDropdown : SkinDropdown
        {
            private readonly SkinManager skins;
            private readonly Bindable<string> configuredSkin;
            private readonly Bindable<string> globalSkin;
            private readonly Bindable<bool> separateSkinsEnabled;

            private IReadOnlyList<Live<SkinInfo>> items = Array.Empty<Live<SkinInfo>>();
            private bool updatingSelection;

            public RulesetSkinDropdown(SkinManager skins, Bindable<string> configuredSkin, Bindable<string> globalSkin, Bindable<bool> separateSkinsEnabled)
            {
                this.skins = skins;
                this.configuredSkin = configuredSkin;
                this.globalSkin = globalSkin;
                this.separateSkinsEnabled = separateSkinsEnabled;

                Current = new Bindable<Live<SkinInfo>>(skins.CurrentSkinInfo.Value);
                configuredSkin.BindValueChanged(_ => updateSelection());
                globalSkin.BindValueChanged(_ => updateSelection());
                separateSkinsEnabled.BindValueChanged(_ => updateSelection());

                Current.BindValueChanged(selection =>
                {
                    if (!updatingSelection && selection.NewValue != null)
                        configuredSkin.Value = selection.NewValue.ID.ToString();
                });
            }

            public void SetItems(IEnumerable<Live<SkinInfo>> availableSkins)
            {
                items = availableSkins.Where(s => s.ID != SkinInfo.RANDOM_SKIN).ToArray();
                Items = items;
                updateSelection();
            }

            private void updateSelection()
            {
                if (items.Count == 0)
                    return;

                Live<SkinInfo> selectedSkin = findSkin(configuredSkin.Value)
                                              ?? findSkin(globalSkin.Value)
                                              ?? findSkin(skins.CurrentSkinInfo.Value.ID.ToString())
                                              ?? items[0];

                updatingSelection = true;
                Current.Value = selectedSkin;
                updatingSelection = false;

                // Keep hidden, unused entries untouched until the feature is enabled for the first time.
                if (separateSkinsEnabled.Value && configuredSkin.Value != selectedSkin.ID.ToString())
                    configuredSkin.Value = selectedSkin.ID.ToString();
            }

            private Live<SkinInfo> findSkin(string skinId)
            {
                if (!Guid.TryParse(skinId, out Guid id))
                    return null;

                return items.FirstOrDefault(s => s.ID == id);
            }
        }

        public partial class RenameSkinButton : SettingsButtonV2, IHasPopover
        {
            [Resolved]
            private SkinManager skins { get; set; }

            private Bindable<Skin> currentSkin;

            [BackgroundDependencyLoader]
            private void load()
            {
                Text = CommonStrings.Rename;
                Action = this.ShowPopover;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                currentSkin = skins.CurrentSkin.GetBoundCopy();
                currentSkin.BindValueChanged(_ => updateState());
                currentSkin.BindDisabledChanged(_ => updateState(), true);
            }

            private void updateState() => Enabled.Value = !currentSkin.Disabled && currentSkin.Value.SkinInfo.PerformRead(s => !s.Protected);

            public Popover GetPopover()
            {
                return new RenameSkinPopover();
            }
        }

        public partial class ExportSkinButton : SettingsButtonV2
        {
            [Resolved]
            private SkinManager skins { get; set; }

            private Bindable<Skin> currentSkin;

            [BackgroundDependencyLoader]
            private void load()
            {
                Text = CommonStrings.Export;
                Action = export;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                currentSkin = skins.CurrentSkin.GetBoundCopy();
                currentSkin.BindValueChanged(_ => updateState());
                currentSkin.BindDisabledChanged(_ => updateState(), true);
            }

            private void updateState() => Enabled.Value = !currentSkin.Disabled && currentSkin.Value.SkinInfo.PerformRead(s => !s.Protected);

            private void export()
            {
                try
                {
                    skins.ExportCurrentSkin();
                }
                catch (Exception e)
                {
                    Logger.Log($"Could not export current skin: {e.Message}", level: LogLevel.Error);
                }
            }
        }

        public partial class EditSkinIniButton : SettingsButtonV2
        {
            [CanBeNull]
            private readonly SkinEditorOverlay skinEditor;

            [Resolved]
            private SkinManager skins { get; set; } = null!;

            private Bindable<Skin> currentSkin = null!;
            private bool editing;

            public EditSkinIniButton([CanBeNull] SkinEditorOverlay skinEditor)
            {
                this.skinEditor = skinEditor;
            }

            [BackgroundDependencyLoader]
            private void load()
            {
                Text = SkinSettingsStrings.EditSkinIni;
                Action = () => _ = editSkinIni();
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                currentSkin = skins.CurrentSkin.GetBoundCopy();
                currentSkin.BindValueChanged(_ => updateState());
                currentSkin.BindDisabledChanged(_ => updateState(), true);
            }

            private void updateState() => Enabled.Value = RuntimeInfo.IsDesktop
                                                       && !editing
                                                       && skinEditor != null
                                                       && !currentSkin.Disabled
                                                       && currentSkin.Value.SkinInfo.PerformRead(s => !s.Protected);

            private async Task editSkinIni()
            {
                if (editing || skinEditor == null)
                    return;

                editing = true;
                updateState();

                try
                {
                    var skin = currentSkin.Value.SkinInfo.PerformRead(s => s.Detach());
                    await skinEditor.EditSkinIniExternally(skin).ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    Logger.Log($"Could not edit skin.ini: {e.Message}", level: LogLevel.Error);
                }
                finally
                {
                    editing = false;
                    Schedule(updateState);
                }
            }
        }

        public partial class DeleteSkinButton : DangerousSettingsButtonV2
        {
            [Resolved]
            private SkinManager skins { get; set; }

            [Resolved(CanBeNull = true)]
            private IDialogOverlay dialogOverlay { get; set; }

            private Bindable<Skin> currentSkin;

            [BackgroundDependencyLoader]
            private void load()
            {
                Text = WebCommonStrings.ButtonsDelete;
                Action = delete;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                currentSkin = skins.CurrentSkin.GetBoundCopy();
                currentSkin.BindValueChanged(_ => updateState());
                currentSkin.BindDisabledChanged(_ => updateState(), true);
            }

            private void updateState() => Enabled.Value = !currentSkin.Disabled && currentSkin.Value.SkinInfo.PerformRead(s => !s.Protected);

            private void delete()
            {
                dialogOverlay?.Push(new SkinDeleteDialog(currentSkin.Value));
            }
        }

        public partial class SkinDeleteDialog : DeletionDialog
        {
            private readonly Skin skin;

            public SkinDeleteDialog(Skin skin)
            {
                this.skin = skin;
                BodyText = skin.SkinInfo.Value.Name;
            }

            [BackgroundDependencyLoader]
            private void load(SkinManager manager)
            {
                DangerousAction = () =>
                {
                    manager.Delete(skin.SkinInfo.Value);
                    manager.CurrentSkinInfo.SetDefault();
                };
            }
        }

        public partial class RenameSkinPopover : OsuPopover
        {
            [Resolved]
            private SkinManager skins { get; set; }

            private readonly FocusedTextBox textBox;

            public RenameSkinPopover()
            {
                AutoSizeAxes = Axes.Both;
                Origin = Anchor.TopCentre;

                RoundedButton renameButton;

                Child = new FillFlowContainer
                {
                    Direction = FillDirection.Vertical,
                    AutoSizeAxes = Axes.Y,
                    Width = 250,
                    Spacing = new Vector2(10f),
                    Children = new Drawable[]
                    {
                        textBox = new FocusedTextBox
                        {
                            PlaceholderText = SkinSettingsStrings.SkinName,
                            FontSize = OsuFont.DEFAULT_FONT_SIZE,
                            RelativeSizeAxes = Axes.X,
                            SelectAllOnFocus = true,
                        },
                        renameButton = new RoundedButton
                        {
                            Height = 40,
                            RelativeSizeAxes = Axes.X,
                            MatchingFilter = true,
                            Text = WebCommonStrings.ButtonsSave,
                        }
                    }
                };

                renameButton.Action += rename;
                textBox.OnCommit += (_, _) => rename();
            }

            protected override void PopIn()
            {
                textBox.Text = skins.CurrentSkinInfo.Value.Value.Name;
                textBox.TakeFocus();

                base.PopIn();
            }

            private void rename()
            {
                skins.Rename(skins.CurrentSkinInfo.Value, textBox.Text);
                PopOut();
            }
        }
    }
}
