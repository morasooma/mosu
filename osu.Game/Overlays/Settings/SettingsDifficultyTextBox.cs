// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Overlays.Settings
{
    public partial class SettingsDifficultyTextBox : CompositeDrawable, ISettingsItem
    {
        public DifficultyBindable Current { get; set; } = null!;

        private FormTextBox numberBox = null!;

        public SettingsDifficultyTextBox()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Padding = new MarginPadding
            {
                Left = SettingsPanel.CONTENT_MARGINS * 2,
                Right = SettingsPanel.CONTENT_MARGINS
            };
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = numberBox = new FormTextBox
            {
                RelativeSizeAxes = Axes.X,
                Caption = "Exact value",
                HintText = "Type a precise value for this setting. Leave empty to use the beatmap value.",
                PlaceholderText = "Beatmap value",
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            numberBox.OnCommit += onCommit;

            Current.BindValueChanged(_ =>
            {
                updateDisplay();
                SettingChanged?.Invoke();
            }, true);
            Current.BindDisabledChanged(disabled => numberBox.ReadOnly = disabled, true);
        }

        private void onCommit(TextBox sender, bool newText)
        {
            if (!newText)
            {
                Current.TriggerChange();
                return;
            }

            if (string.IsNullOrWhiteSpace(numberBox.Current.Value))
            {
                Current.Value = null;
                Current.TriggerChange();
                return;
            }

            try
            {
                if (tryParseFloat(numberBox.Current.Value, out float value))
                    Current.SetExactValue(value, numberBox.Current.Value);
            }
            catch
            {
            }

            Current.TriggerChange();
        }

        private void updateDisplay()
            => numberBox.Current.Value = Current.Value?.ToStandardFormattedString(OsuSliderBar<float>.MAX_DECIMAL_DIGITS) ?? string.Empty;

        private static bool tryParseFloat(string text, out float value)
            => float.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out value)
               || float.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
               || float.TryParse(text.Replace(',', '.'), NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        public event Action? SettingChanged;

        public bool HasClassicDefault => false;

        public void ApplyClassicDefault()
            => throw new InvalidOperationException("Classic default is not available for exact numeric input.");

        public void ApplyDefault() => Current.SetDefault();
    }
}
