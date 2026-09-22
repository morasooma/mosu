// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Globalization;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Overlays.Settings;
using osu.Game.Utils;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Torii's PA mod. Shifts pitch without changing playback speed by combining
    /// inverse frequency and tempo adjustments.
    /// </summary>
    public class ModPitchAdjust : Mod, IApplicableToTrack, IToriiServerMod
    {
        public override string Name => "Pitch Adjust";
        public override string Acronym => "PA";
        public override IconUsage? Icon => FontAwesome.Solid.Music;
        public override ModType Type => ModType.Fun;
        public override LocalisableString Description => "Shift the song's pitch up or down without changing playback speed.";
        public override bool Ranked => true;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(ModAdaptiveSpeed),
        };

        private const double safe_min = 0.5;
        private const double safe_max = 2.0;
        private const double extended_min = 0.1;
        private const double extended_max = 3.0;

        [SettingSource(
            "Pitch shift",
            "Multiplier applied to pitch (1.0 = no change).",
            SettingControlType = typeof(MultiplierSettingsSlider))]
        public BindableNumber<double> PitchShift { get; } = new BindableDouble(1.0)
        {
            MinValue = safe_min,
            MaxValue = safe_max,
            Default = 1.0,
            Precision = 0.01,
        };

        [SettingSource(
            "Extended limits",
            "Allow extreme pitch shifts from 0.1× to 3.0×. Values outside the normal range may heavily distort audio.")]
        public BindableBool ExtendedLimits { get; } = new BindableBool();

        private readonly BindableDouble frequencyAdjustment = new BindableDouble(1);
        private readonly BindableDouble tempoAdjustment = new BindableDouble(1);

        public ModPitchAdjust()
        {
            PitchShift.BindValueChanged(value =>
            {
                frequencyAdjustment.Value = value.NewValue;
                tempoAdjustment.Value = 1.0 / value.NewValue;
            }, true);

            ExtendedLimits.BindValueChanged(value =>
            {
                if (value.NewValue)
                {
                    PitchShift.MaxValue = extended_max;
                    PitchShift.MinValue = extended_min;
                }
                else
                {
                    PitchShift.MaxValue = safe_max;
                    PitchShift.MinValue = safe_min;
                }
            }, true);
        }

        public void ApplyToTrack(IAdjustableAudioComponent track)
        {
            track.AddAdjustment(AdjustableProperty.Frequency, frequencyAdjustment);
            track.AddAdjustment(AdjustableProperty.Tempo, tempoAdjustment);
        }

        internal override void CopyAdjustedSetting(IBindable target, object source)
        {
            // APIMod applies properties in declaration order. Widen the range before an
            // extended pitch value is copied so replay/leaderboard values are not clamped.
            if (ReferenceEquals(target, PitchShift))
            {
                double? incoming = tryExtractDoubleValue(source);

                if (incoming.HasValue && (incoming.Value > safe_max || incoming.Value < safe_min))
                    ExtendedLimits.Value = true;
            }

            base.CopyAdjustedSetting(target, source);
        }

        private static double? tryExtractDoubleValue(object source)
        {
            if (source is IBindable bindable)
                source = BindableValueAccessor.GetValue(bindable) ?? source;

            return source switch
            {
                double value => value,
                float value => value,
                int value => value,
                long value => value,
                decimal value => (double)value,
                string value when double.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsed) => parsed,
                _ => null,
            };
        }
    }
}
