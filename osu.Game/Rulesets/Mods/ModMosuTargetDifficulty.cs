// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Approximates a playback rate that brings the beatmap to a user-selected star rating.
    /// </summary>
    public abstract class ModMosuTargetDifficulty : Mod, IApplicableToRate, IApplicableAfterBeatmapConversion, IApplicableToDifficulty,
                                                   IProvideAPIModSettings, IUpdatableByBeatmapInfo, IApplicableToHitObject
    {
        private const double default_target_difficulty = 6.0;
        private const double min_speed_change = 0.5;
        private const double max_speed_change = 2.0;
        private const string api_speed_change_setting = "speed_change";

        private readonly BindableDouble referenceDifficulty = new BindableDouble();
        private readonly RateAdjustModHelper rateAdjustHelper;
        private bool preserveExplicitApiSpeedChange;

        public override string Name => "Target Difficulty";
        public override string Acronym => "TS";
        public override ModType Type => ModType.Mosu;
        public override LocalisableString Description => MosuModsStrings.ModTargetDifficultyDescription;
        public override bool Ranked => true;
        public override bool ValidForFreestyleAsRequiredMod => true;
        public override bool ValidForMultiplayerAsFreeMod => false;
        public override Type[] IncompatibleMods => new[] { typeof(IApplicableToRate) };
        public override string ExtendedIconInformation => $"{TargetDifficulty.Value:N1}*";

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModAdjustPitch), nameof(MosuModsStrings.ModAdjustPitchDescription))]
        public BindableBool AdjustPitch { get; } = new BindableBool();

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModLockDifficultyAdjust), nameof(MosuModsStrings.ModLockDifficultyAdjustDescription))]
        public BindableBool LockDifficultyAdjust { get; } = new BindableBool();

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModTargetDifficultyTarget), nameof(MosuModsStrings.ModTargetDifficultyTargetDescription), 0)]
        public BindableDouble TargetDifficulty { get; } = new BindableDouble(default_target_difficulty)
        {
            Default = default_target_difficulty,
            MinValue = 1.0,
            MaxValue = 12.0,
            Precision = 0.1,
        };

        public BindableDouble SpeedChange { get; } = new BindableDouble(1)
        {
            Default = 1,
            MinValue = min_speed_change,
            MaxValue = max_speed_change,
            Precision = 0.0001,
        };

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                yield return (MosuModsStrings.ModTargetDifficultyTarget, $"{TargetDifficulty.Value:N1}*");

                if (referenceDifficulty.Value > 0)
                    yield return (MosuModsStrings.ModSpeedChange, $"{SpeedChange.Value:N2}x");

                if (!AdjustPitch.IsDefault)
                    yield return (MosuModsStrings.ModAdjustPitch, AdjustPitch.Value ? MosuModsStrings.Yes : MosuModsStrings.No);

                if (!LockDifficultyAdjust.IsDefault)
                    yield return (MosuModsStrings.ModLockDifficultyAdjust, LockDifficultyAdjust.Value ? MosuModsStrings.Yes : MosuModsStrings.No);
            }
        }

        protected ModMosuTargetDifficulty()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);

            TargetDifficulty.BindValueChanged(_ =>
            {
                preserveExplicitApiSpeedChange = false;
                recalculateSpeedChange();
            }, true);
        }

        public override Mod DeepClone()
        {
            var clone = (ModMosuTargetDifficulty)base.DeepClone();
            clone.referenceDifficulty.Value = referenceDifficulty.Value;
            clone.SpeedChange.Value = SpeedChange.Value;
            clone.preserveExplicitApiSpeedChange = preserveExplicitApiSpeedChange;
            return clone;
        }

        public void ApplyToTrack(IAdjustableAudioComponent track) => rateAdjustHelper.ApplyToTrack(track);

        public void ApplyToSample(IAdjustableAudioComponent sample) => sample.AddAdjustment(AdjustableProperty.Frequency, SpeedChange);

        public double ApplyToRate(double time, double rate = 1) => rate * SpeedChange.Value;

        public void ApplyToBeatmap(IBeatmap beatmap) => updateReferenceDifficulty(beatmap.BeatmapInfo.StarRating);

        public void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            // Difficulty locking is applied to hit windows after conversion.
        }

        public void ApplyToHitObject(HitObject hitObject)
        {
            if (!LockDifficultyAdjust.Value || SpeedChange.Value <= 0)
                return;

            applyLockDifficultyToHitObject(hitObject, SpeedChange.Value);
        }

        private void applyLockDifficultyToHitObject(HitObject hitObject, double speedChange)
        {
            ApplyLockDifficultyToHitObjectSelf(hitObject, speedChange);

            foreach (HitObject nested in hitObject.NestedHitObjects)
                applyLockDifficultyToHitObject(nested, speedChange);
        }

        protected virtual void ApplyLockDifficultyToHitObjectSelf(HitObject hitObject, double speedChange)
        {
            if (hitObject.HitWindows != null)
                hitObject.HitWindows.CustomSpeedMultiplier = speedChange;
        }

        public void UpdateFromBeatmapInfo(IBeatmapInfo beatmapInfo) => updateReferenceDifficulty(beatmapInfo?.StarRating ?? double.NaN);

        public void AddAPIModSettings(Dictionary<string, object> settings)
        {
            if (double.IsFinite(SpeedChange.Value) && SpeedChange.Value > 0)
                settings[api_speed_change_setting] = SpeedChange.Value;
        }

        public bool TryApplyAPIModSetting(string settingKey, object settingValue)
        {
            if (settingKey != api_speed_change_setting)
                return false;

            bool applied = SpeedChange.TrySetExactNumericValue(settingValue);

            if (applied)
                preserveExplicitApiSpeedChange = true;

            return applied;
        }

        private void updateReferenceDifficulty(double difficulty)
        {
            referenceDifficulty.Value = double.IsFinite(difficulty) && difficulty > 0 ? difficulty : 0;
            recalculateSpeedChange();
        }

        private void recalculateSpeedChange()
        {
            if (preserveExplicitApiSpeedChange)
                return;

            if (referenceDifficulty.Value <= 0)
            {
                SpeedChange.Value = 1;
                return;
            }

            // Star rating approximately scales with rate^1.35 in all built-in rulesets.
            double targetSpeed = Math.Pow(TargetDifficulty.Value / referenceDifficulty.Value, 1.0 / 1.35);
            SpeedChange.Value = Math.Clamp(targetSpeed, min_speed_change, max_speed_change);
        }
    }
}
