using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Graphics.Fonts;
using osu.Game.Localisation;
using osu.Game.Overlays.Notifications;

namespace osu.Game.Overlays.Settings.Sections.Fork
{
    public partial class FontSettingsPanel : FillFlowContainer
    {
        [Resolved(canBeNull: true)] private OsuGameBase? game { get; set; }
        [Resolved(canBeNull: true)] private INotificationOverlay? notifications { get; set; }
        [Resolved] private Storage storage { get; set; } = null!;
        private readonly List<FormDropdown<string>> dropdowns = new List<FormDropdown<string>>();
        private IBindable<bool> separate = null!;
        private FillFlowContainer roles = null!;
        private SettingsItemV2 common = null!;
        private bool busy;

        public FontSettingsPanel()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new osuTK.Vector2(0, 5);
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            separate = config.GetBindable<bool>(OsuSetting.ForkUseSeparateFonts);
            common = item(FontSettingsStrings.Common, OsuSetting.ForkCustomUIFont, config);
            roles = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    item(FontSettingsStrings.Main, OsuSetting.ForkMainFont, config),
                    item(FontSettingsStrings.Alternate, OsuSetting.ForkAlternateFont, config),
                    item(FontSettingsStrings.Numeric, OsuSetting.ForkNumericFont, config),
                    item(FontSettingsStrings.Inter, OsuSetting.ForkInterFont, config),
                }
            };
            var selector = new ImportFontButton(file =>
            {
                if (game == null || busy) return;
                _ = run(async () =>
                {
                    using var stream = file.OpenRead();
                    await game.ImportFontPack(stream).ConfigureAwait(false);
                });
            }) { Text = FontSettingsStrings.Import, TooltipText = FontSettingsStrings.Hint };
            Children = new Drawable[]
            {
                new SettingsItemV2(new FormCheckBox
                {
                    Caption = FontSettingsStrings.Separate, HintText = FontSettingsStrings.SeparateHint,
                    Current = config.GetBindable<bool>(OsuSetting.ForkUseSeparateFonts),
                }) { FavouriteId = "OsuSetting.ForkUseSeparateFonts" },
                roles, common,
                selector,
                new SettingsButtonV2
                {
                    Text = FontSettingsStrings.Export,
                    Action = () =>
                    {
                        if (game == null || busy) return;
                        _ = run(async () =>
                        {
                            await game.ExportFontPack().ConfigureAwait(false);
                            Schedule(() => storage.GetStorageForDirectory("exports").PresentExternally());
                        });
                    }
                },
                new SettingsButtonV2 { Text = ForkSettingsStrings.FontsFolderBtn, Action = () => storage.GetStorageForDirectory("Fonts").PresentExternally() },
            };
            separate.BindValueChanged(_ => updateMode(), true);
        }

        private SettingsItemV2 item(LocalisableString caption, OsuSetting setting, OsuConfigManager config)
        {
            var dropdown = new FontDropdown
            {
                Caption = caption,
                HintText = setting == OsuSetting.ForkNumericFont ? FontSettingsStrings.NumericHint : FontSettingsStrings.RoleHint,
                Current = config.GetBindable<string>(setting),
                Items = items(config.Get<string>(setting)),
            };
            dropdown.Current.BindValueChanged(e =>
            {
                if (FontConfiguration.IsLegacyAlias(e.NewValue) && !dropdown.Items.Contains(e.NewValue))
                    dropdown.Items = items(e.NewValue);
            });
            dropdowns.Add(dropdown);
            return new SettingsItemV2(dropdown) { FavouriteId = "OsuSetting." + setting };
        }

        private static IEnumerable<string> items(string selected) => OsuGameBase.AvailableCustomUIFonts.Where(f =>
            (!FontConfiguration.IsLegacyAlias(f) || f == selected)
            && !f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)).ToArray();
        private void updateMode()
        {
            common.CanBeShown.Value = !separate.Value;
            foreach (var role in roles.Children.OfType<SettingsItemV2>()) role.CanBeShown.Value = separate.Value;
            if (separate.Value) { roles.Show(); common.Hide(); }
            else { roles.Hide(); common.Show(); }
        }
        protected override bool OnHover(HoverEvent e)
        {
            game?.RefreshCustomFontList();
            foreach (var dropdown in dropdowns) dropdown.Items = items(dropdown.Current.Value);
            return base.OnHover(e);
        }
        private async Task run(Func<Task> action)
        {
            busy = true;
            try { await action().ConfigureAwait(false); }
            catch (Exception ex)
            {
                osu.Framework.Logging.Logger.Error(ex, "Font package operation failed.");
                Schedule(() => notifications?.Post(new SimpleErrorNotification { Text = FontSettingsStrings.Failed }));
            }
            finally
            {
                Schedule(() =>
                {
                    busy = false;
                    foreach (var dropdown in dropdowns) dropdown.Items = items(dropdown.Current.Value);
                });
            }
        }
        private partial class ImportFontButton : SettingsButtonV2, IHasPopover
        {
            private readonly Bindable<FileInfo?> selected = new Bindable<FileInfo?>();
            public ImportFontButton(Action<FileInfo> import)
            {
                Action = this.ShowPopover;
                selected.BindValueChanged(e =>
                {
                    if (e.NewValue == null) return;
                    this.HidePopover();
                    import(e.NewValue);
                    selected.Value = null;
                });
            }
            public Popover GetPopover() => new FormFileSelector.FileChooserPopover(new[] { ".morafn" }, selected, null, allowClear: false);
        }

        private partial class FontDropdown : FormDropdown<string>
        {
            [Resolved(canBeNull: true)] private OsuGameBase? game { get; set; }
            protected override LocalisableString GenerateItemText(string item) => item == "Default" ? FontSettingsStrings.Default
                : item.StartsWith("morafn-", StringComparison.Ordinal) ? FontSettingsStrings.Pack(game?.FontPackDisplayName(item) ?? item)
                : FontConfiguration.IsLegacyAlias(item) ? FontSettingsStrings.Compatibility(FontConfiguration.DisplayFamilyName(item))
                : OsuGameBase.BuiltInCustomUIFonts.Contains(item) ? FontSettingsStrings.BuiltIn(item) : item;
        }
    }
}
