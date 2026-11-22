// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Fork;
using osu.Game.Skinning;

namespace osu.Game.Overlays.SkinEditor
{
    public partial class SkinFontDropdown : SettingsDropdown<string>
    {
        public bool IncludeOriginalFont { get; init; }

        protected override OsuDropdown<string> CreateDropdown() => new FontDropdownControl(this);

        private partial class FontDropdownControl : DropdownControl
        {
            private readonly SkinFontDropdown settingsDropdown;

            [Resolved]
            private OsuGame? game { get; set; }

            [Resolved(CanBeNull = true)]
            private IDialogOverlay? dialogOverlay { get; set; }

            public FontDropdownControl(SkinFontDropdown settingsDropdown)
            {
                this.settingsDropdown = settingsDropdown;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                Current.BindValueChanged(onSelectionChanged);
                updateItems();
            }

            protected override bool OnHover(osu.Framework.Input.Events.HoverEvent e)
            {
                if (game != null)
                {
                    game.RefreshCustomFontList();
                    updateItems();
                }

                return base.OnHover(e);
            }

            protected override osu.Framework.Localisation.LocalisableString GenerateItemText(string item)
                => string.IsNullOrEmpty(item) ? osu.Game.Localisation.FontSettingsStrings.Default : base.GenerateItemText(item);

            private void onSelectionChanged(ValueChangedEvent<string> selection)
            {
                if (string.IsNullOrEmpty(selection.NewValue) || FontAdjustableSkinComponent.TryParseTypefaceFamily(selection.NewValue, out _))
                    return;

                if (selection.NewValue.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                    || selection.NewValue.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                {
                    if (dialogOverlay != null)
                    {
                        string revertValue = selection.OldValue ?? Current.Default;
                        Scheduler.Add(() =>
                        {
                            dialogOverlay.Push(new UnsupportedFontDialog(() => Current.Value = revertValue));
                        });
                    }
                    else
                    {
                        Current.Value = selection.OldValue ?? Current.Default;
                    }
                }
            }

            private void updateItems()
            {
                ClearItems();

                if (settingsDropdown.IncludeOriginalFont)
                    AddDropdownItem(string.Empty);
                foreach (var item in FontAdjustableSkinComponent.GetAvailableFontFamilies()
                             .Where(f => !f.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".otf", StringComparison.OrdinalIgnoreCase)).Distinct())
                    AddDropdownItem(item);
            }
        }
    }
}