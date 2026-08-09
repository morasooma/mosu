// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.SkinEditor;

namespace osu.Game.Skinning
{
    /// <summary>
    /// A skin component that contains text and allows the user to choose its font.
    /// </summary>
    public abstract partial class FontAdjustableSkinComponent : Container, ISerialisableDrawable
    {
        private static readonly string[] built_in_typeface_families =
        {
            @"Torus",
            @"Inter",
            @"Venera",
            @"Noto",
            @"Torus-Alternate",
        };

        public bool UsesFixedAnchor { get; set; }

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Font), ExcludeFromSettingsControls = true)]
        public Bindable<Typeface> Font { get; } = new Bindable<Typeface>(Typeface.Torus);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Font), SettingControlType = typeof(SkinFontDropdown), ExcludeFromSerialisation = true)]
        public Bindable<string> FontDisplay { get; } = new Bindable<string>(OsuFont.GetFamilyString(Typeface.Torus)!);

        /// <summary>
        /// When set, overrides <see cref="Font"/> with a custom font family from the user's Fonts directory.
        /// Not serialised to layout JSON; stored in <see cref="SkinCustomFontInfo"/> instead.
        /// </summary>
        public Bindable<string?> CustomFontFamily { get; } = new Bindable<string?>(null);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.TextWeight), SettingControlType = typeof(WeightDropdown))]
        public Bindable<FontWeight> TextWeight { get; } = new Bindable<FontWeight>(FontWeight.Regular);

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.TextColour))]
        public BindableColour4 TextColour { get; } = new BindableColour4(Colour4.White);

        public static IEnumerable<string> GetAvailableFontFamilies()
        {
            foreach (Typeface typeface in System.Enum.GetValues<Typeface>())
                yield return OsuFont.GetFamilyString(typeface) ?? typeface.ToString();

            foreach (string font in OsuGameBase.AvailableCustomUIFonts)
            {
                if (font == @"Default" || built_in_typeface_families.Contains(font))
                    continue;

                yield return font;
            }
        }

        public static bool TryParseTypefaceFamily(string family, out Typeface typeface)
        {
            foreach (Typeface candidate in System.Enum.GetValues<Typeface>())
            {
                if (OsuFont.GetFamilyString(candidate) == family)
                {
                    typeface = candidate;
                    return true;
                }
            }

            typeface = default;
            return false;
        }

        /// <summary>
        /// Implement to apply the user font selection to one or more components.
        /// </summary>
        protected abstract void SetFont(FontUsage font);

        protected abstract void SetTextColour(Colour4 textColour);

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Font.BindValueChanged(_ => syncFontDisplay());
            CustomFontFamily.BindValueChanged(_ => syncFontDisplay());
            FontDisplay.BindValueChanged(e => applyFontDisplaySelection(e.NewValue));

            TextWeight.BindValueChanged(_ => updateFont(), true);

            TextColour.BindValueChanged(e => SetTextColour(e.NewValue), true);

            syncFontDisplay();
        }

        private void syncFontDisplay()
        {
            string display = !string.IsNullOrEmpty(CustomFontFamily.Value)
                ? CustomFontFamily.Value
                : OsuFont.GetFamilyString(Font.Value) ?? Typeface.Torus.ToString();

            if (FontDisplay.Value != display)
                FontDisplay.Value = display;
        }

        private void applyFontDisplaySelection(string selection)
        {
            if (TryParseTypefaceFamily(selection, out var parsedTypeface))
            {
                CustomFontFamily.Value = null;
                Font.Value = parsedTypeface;
            }
            else
            {
                CustomFontFamily.Value = selection;
            }

            updateFont();
        }

        private void updateFont()
        {
            if (!string.IsNullOrEmpty(CustomFontFamily.Value))
                SetFont(OsuFont.GetFont(CustomFontFamily.Value, weight: TextWeight.Value));
            else
                SetFont(OsuFont.GetFont(Font.Value, weight: TextWeight.Value));
        }

        private partial class WeightDropdown : SettingsDropdown<FontWeight>
        {
            public FontAdjustableSkinComponent FontComponent => (FontAdjustableSkinComponent)SettingSourceObject;
            protected override OsuDropdown<FontWeight> CreateDropdown() => new DropdownControl(this);

            private new partial class DropdownControl : SettingsDropdown<FontWeight>.DropdownControl
            {
                private readonly WeightDropdown settingsDropdown;

                private IBindable<Typeface> font = null!;
                private IBindable<string?> customFontFamily = null!;

                public DropdownControl(WeightDropdown settingsDropdown)
                {
                    this.settingsDropdown = settingsDropdown;
                }

                protected override void LoadComplete()
                {
                    base.LoadComplete();

                    font = settingsDropdown.FontComponent.Font.GetBoundCopy();
                    customFontFamily = settingsDropdown.FontComponent.CustomFontFamily.GetBoundCopy();

                    font.BindValueChanged(_ => updateItems(), true);
                    customFontFamily.BindValueChanged(_ => updateItems(), true);
                }

                private void updateItems()
                {
                    ClearItems();

                    bool useVeneraWeights = string.IsNullOrEmpty(customFontFamily.Value) && font.Value == Typeface.Venera;

                    if (useVeneraWeights)
                    {
                        AddDropdownItem(FontWeight.Light);
                        AddDropdownItem(FontWeight.Bold);
                        AddDropdownItem(FontWeight.Black);

                        Current.Default = FontWeight.Bold;

                        if (!Items.Contains(Current.Value))
                            Current.SetDefault();
                        return;
                    }

                    AddDropdownItem(FontWeight.Light);
                    AddDropdownItem(FontWeight.Regular);
                    AddDropdownItem(FontWeight.SemiBold);
                    AddDropdownItem(FontWeight.Bold);

                    Current.Default = FontWeight.Regular;

                    if (!Items.Contains(Current.Value))
                        Current.SetDefault();
                }
            }
        }
    }
}