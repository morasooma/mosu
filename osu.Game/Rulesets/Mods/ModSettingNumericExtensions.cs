// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Mods
{
    public static class ModSettingNumericExtensions
    {
        public static bool TrySetExactNumericValue(this IBindable bindable, object source)
        {
            string? rawText = source as string ?? Convert.ToString(source, CultureInfo.InvariantCulture);

            try
            {
                switch (bindable)
                {
                    case BindableNumber<double> bindableDouble:
                        bindableDouble.SetExactValue(Convert.ToDouble(source, CultureInfo.InvariantCulture), rawText);
                        return true;

                    case BindableNumber<float> bindableFloat:
                        bindableFloat.SetExactValue(Convert.ToSingle(source, CultureInfo.InvariantCulture), rawText);
                        return true;

                    case BindableNumber<int> bindableInt:
                        bindableInt.SetExactValue(Convert.ToInt32(source, CultureInfo.InvariantCulture));
                        return true;

                    case DifficultyBindable difficultyBindable when source is null:
                        difficultyBindable.SetExactValue(null);
                        return true;

                    case DifficultyBindable difficultyBindable:
                        difficultyBindable.SetExactValue(Convert.ToSingle(source, CultureInfo.InvariantCulture), rawText);
                        return true;
                }
            }
            catch
            {
            }

            return false;
        }

        public static void SetExactValue(this BindableNumber<double> bindable, double value, string? rawText = null)
        {
            if (value < bindable.MinValue)
                bindable.MinValue = value;

            if (value > bindable.MaxValue)
                bindable.MaxValue = value;

            relaxPrecision(bindable, rawText);
            bindable.Value = value;
        }

        public static void SetExactValue(this BindableNumber<float> bindable, float value, string? rawText = null)
        {
            if (value < bindable.MinValue)
                bindable.MinValue = value;

            if (value > bindable.MaxValue)
                bindable.MaxValue = value;

            relaxPrecision(bindable, rawText);
            bindable.Value = value;
        }

        public static void SetExactValue(this BindableNumber<int> bindable, int value)
        {
            if (value < bindable.MinValue)
                bindable.MinValue = value;

            if (value > bindable.MaxValue)
                bindable.MaxValue = value;

            bindable.Value = value;
        }

        public static void SetExactValue(this DifficultyBindable bindable, float? value, string? rawText = null)
        {
            if (value == null)
            {
                bindable.Value = null;
                return;
            }

            if (value < bindable.MinValue)
            {
                bindable.ExtendedMinValue = bindable.ExtendedMinValue.HasValue
                    ? Math.Min(bindable.ExtendedMinValue.Value, value.Value)
                    : value.Value;
                bindable.ExtendedLimits.Value = true;
            }

            if (value > bindable.MaxValue)
            {
                bindable.ExtendedMaxValue = bindable.ExtendedMaxValue.HasValue
                    ? Math.Max(bindable.ExtendedMaxValue.Value, value.Value)
                    : value.Value;
                bindable.ExtendedLimits.Value = true;
            }

            relaxPrecision(bindable.CurrentNumber, rawText);
            bindable.Value = value;
        }

        private static void relaxPrecision(BindableNumber<double> bindable, string? rawText)
        {
            double requiredPrecision = getRequiredPrecision(rawText);

            if (requiredPrecision > 0 && (bindable.Precision == 0 || requiredPrecision < bindable.Precision))
                bindable.Precision = requiredPrecision;
        }

        private static void relaxPrecision(BindableNumber<float> bindable, string? rawText)
        {
            float requiredPrecision = (float)getRequiredPrecision(rawText);

            if (requiredPrecision > 0 && (bindable.Precision == 0 || requiredPrecision < bindable.Precision))
                bindable.Precision = requiredPrecision;
        }

        private static double getRequiredPrecision(string? rawText)
        {
            if (string.IsNullOrWhiteSpace(rawText))
                return 0;

            int separatorIndex = Math.Max(rawText.LastIndexOf('.'), rawText.LastIndexOf(','));

            if (separatorIndex < 0)
                return 0;

            int exponentIndexLower = rawText.IndexOf('e');
            int exponentIndexUpper = rawText.IndexOf('E');
            int exponentIndex = exponentIndexLower >= 0 && exponentIndexUpper >= 0
                ? Math.Min(exponentIndexLower, exponentIndexUpper)
                : Math.Max(exponentIndexLower, exponentIndexUpper);
            string fractionalPart = exponentIndex >= 0
                ? rawText[(separatorIndex + 1)..exponentIndex]
                : rawText[(separatorIndex + 1)..];

            int digits = 0;

            foreach (char c in fractionalPart)
            {
                if (char.IsDigit(c))
                    digits++;
            }

            return digits > 0 ? Math.Pow(10, -digits) : 0;
        }
    }
}
