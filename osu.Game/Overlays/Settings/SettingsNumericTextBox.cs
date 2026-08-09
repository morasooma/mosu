// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using System.Numerics;
using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Game.Extensions;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osu.Game.Graphics.UserInterfaceV2;

namespace osu.Game.Overlays.Settings
{
    public partial class SettingsNumericTextBox<T> : CompositeDrawable, ISettingsItem
        where T : struct, INumber<T>, IMinMaxValue<T>
    {
        private BindableNumber<T> current = null!;

        public BindableNumber<T> Current
        {
            get => current;
            set => current = value;
        }

        private FormTextBox numberBox = null!;

        public SettingsNumericTextBox()
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
                HintText = "Type a precise value for this setting.",
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
            if (!newText || string.IsNullOrWhiteSpace(numberBox.Current.Value))
            {
                Current.TriggerChange();
                return;
            }

            try
            {
                switch (Current)
                {
                    case BindableNumber<double> bindableDouble:
                        if (tryParseDouble(numberBox.Current.Value, out double doubleValue))
                            bindableDouble.SetExactValue(doubleValue, numberBox.Current.Value);
                        break;

                    case BindableNumber<float> bindableFloat:
                        if (tryParseFloat(numberBox.Current.Value, out float floatValue))
                            bindableFloat.SetExactValue(floatValue, numberBox.Current.Value);
                        break;

                    case BindableNumber<int> bindableInt:
                        if (tryParseInt(numberBox.Current.Value, out int intValue))
                            bindableInt.SetExactValue(intValue);
                        break;

                    default:
                        ((IParseable)Current).Parse(numberBox.Current.Value, CultureInfo.CurrentCulture);
                        break;
                }
            }
            catch
            {
            }

            Current.TriggerChange();
        }

        private void updateDisplay()
            => numberBox.Current.Value = Current.Value.ToStandardFormattedString(OsuSliderBar<T>.MAX_DECIMAL_DIGITS);

        private static bool tryParseDouble(string text, out double value)
            => double.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out value)
               || double.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
               || double.TryParse(text.Replace(',', '.'), NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        private static bool tryParseFloat(string text, out float value)
            => float.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.CurrentCulture, out value)
               || float.TryParse(text, NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value)
               || float.TryParse(text.Replace(',', '.'), NumberStyles.Float | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);

        private static bool tryParseInt(string text, out int value)
            => int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out value)
               || int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

        public event Action? SettingChanged;

        public bool HasClassicDefault => false;

        public void ApplyClassicDefault()
            => throw new InvalidOperationException("Classic default is not available for exact numeric input.");

        public void ApplyDefault() => Current.SetDefault();
    }
}
