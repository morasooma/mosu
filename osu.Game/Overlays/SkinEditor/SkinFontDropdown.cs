// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
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
        public FontAdjustableSkinComponent FontComponent => (FontAdjustableSkinComponent)SettingSourceObject;

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

            private void onSelectionChanged(ValueChangedEvent<string> selection)
            {
                if (FontAdjustableSkinComponent.TryParseTypefaceFamily(selection.NewValue, out _))
                    return;

                if (selection.NewValue.EndsWith(".ttf", StringComparison.OrdinalIgnoreCase)
                    || selection.NewValue.EndsWith(".otf", StringComparison.OrdinalIgnoreCase))
                {
                    if (dialogOverlay != null)
                    {
                        string revertValue = selection.OldValue ?? settingsDropdown.FontComponent.FontDisplay.Default;
                        Scheduler.Add(() =>
                        {
                            dialogOverlay.Push(new UnsupportedFontDialog(() => Current.Value = revertValue));
                        });
                    }
                    else
                    {
                        Current.Value = selection.OldValue ?? settingsDropdown.FontComponent.FontDisplay.Default;
                    }
                }
            }

            private void updateItems()
            {
                ClearItems();

                foreach (var item in FontAdjustableSkinComponent.GetAvailableFontFamilies())
                    AddDropdownItem(item);
            }
        }
    }
}