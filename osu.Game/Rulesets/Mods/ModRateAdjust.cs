// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;

namespace osu.Game.Rulesets.Mods
{
    public abstract class ModRateAdjust : Mod, IApplicableToRate, IApplicableToDifficulty
    {
        public sealed override bool ValidForFreestyleAsRequiredMod => true;
        public sealed override bool ValidForMultiplayerAsFreeMod => false;

        public abstract BindableNumber<double> SpeedChange { get; }

        [SettingSource("Блокировать AR/OD", "Сохранять скорость сближения (AR) и общую сложность (OD) оригинальными независимо от изменения скорости.")]
        public BindableBool LockDifficultyAdjust { get; } = new BindableBool();


        public abstract void ApplyToTrack(IAdjustableAudioComponent track);

        public virtual void ApplyToSample(IAdjustableAudioComponent sample)
        {
            sample.AddAdjustment(AdjustableProperty.Frequency, SpeedChange);
        }

        public double ApplyToRate(double time, double rate) => rate * SpeedChange.Value;

        public override Type[] IncompatibleMods => !Online.MosuServerEnvironment.IsThirdPartyServer && LockDifficultyAdjust.Value
            ? new[] { typeof(ModTimeRamp), typeof(ModAdaptiveSpeed), typeof(ModRateAdjust), typeof(ModDifficultyAdjust) }
            : new[] { typeof(ModTimeRamp), typeof(ModAdaptiveSpeed), typeof(ModRateAdjust) };

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!SpeedChange.IsDefault)
                    yield return ("Изменение скорости", FormattableString.Invariant($@"{SpeedChange.Value:N2}x"));

                if (!Online.MosuServerEnvironment.IsThirdPartyServer && !LockDifficultyAdjust.IsDefault)
                    yield return ("Блокировать AR/OD", LockDifficultyAdjust.Value ? "Да" : "Нет");
            }
        }

        public override string ExtendedIconInformation => SpeedChange.IsDefault ? string.Empty : FormattableString.Invariant($"{SpeedChange.Value:N2}x");

        public virtual void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            if (Online.MosuServerEnvironment.IsThirdPartyServer)
                return;

            if (!LockDifficultyAdjust.Value)
                return;

            AdjustLockedDifficulty(difficulty, difficulty.ApproachRate, difficulty.OverallDifficulty, SpeedChange.Value);
        }

        /// <summary>
        /// Applies the locked AR/OD to <paramref name="difficulty"/>.
        /// Override in ruleset-specific subclasses to compensate for speed change
        /// (so that the effective wall-clock approach time and hit windows match the original).
        /// The default implementation simply restores the original AR/OD values.
        /// </summary>
        protected virtual void AdjustLockedDifficulty(BeatmapDifficulty difficulty, float originalAR, float originalOD, double speedFactor)
        {
            difficulty.ApproachRate = originalAR;
            difficulty.OverallDifficulty = originalOD;
        }
    }
}
