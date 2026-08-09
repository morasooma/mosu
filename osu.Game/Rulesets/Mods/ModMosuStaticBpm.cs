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
    /// Adjusts playback rate to bring the most common beatmap BPM to a user-selected target.
    /// </summary>
    public abstract class ModMosuStaticBpm : Mod, IApplicableToRate, IApplicableAfterBeatmapConversion, IApplicableToDifficulty,
                                             IProvideAPIModSettings, IUpdatableByBeatmapInfo, IApplicableToHitObject
    {
        private const int default_target_bpm = 180;
        private const double min_speed_change = 0.5;
        private const double max_speed_change = 2.0;
        private const string api_speed_change_setting = "speed_change";

        private readonly BindableDouble referenceBpm = new BindableDouble();
        private readonly RateAdjustModHelper rateAdjustHelper;
        private bool preserveExplicitApiSpeedChange;

        public override string Name => "Static BPM";
        public override string Acronym => "SB";
        public override ModType Type => ModType.Mosu;
        public override LocalisableString Description => MosuModsStrings.ModStaticBpmDescription;
        public override bool Ranked => true;
        public override bool ValidForFreestyleAsRequiredMod => true;
        public override bool ValidForMultiplayerAsFreeMod => false;
        public override Type[] IncompatibleMods => new[] { typeof(IApplicableToRate) };
        public override string ExtendedIconInformation => $"{TargetBpm.Value} BPM";

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModStaticBpmTargetBpm), nameof(MosuModsStrings.ModStaticBpmTargetBpmDescription), 0)]
        public BindableInt TargetBpm { get; } = new BindableInt(default_target_bpm)
        {
            Default = default_target_bpm,
            MinValue = 100,
            MaxValue = 300,
            Precision = 1,
        };

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModAdjustPitch), nameof(MosuModsStrings.ModAdjustPitchDescription))]
        public BindableBool AdjustPitch { get; } = new BindableBool();

        [SettingSource(typeof(MosuModsStrings), nameof(MosuModsStrings.ModLockDifficultyAdjust), nameof(MosuModsStrings.ModLockDifficultyAdjustDescription))]
        public BindableBool LockDifficultyAdjust { get; } = new BindableBool();

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
                yield return (MosuModsStrings.ModStaticBpmTargetBpm, $"{TargetBpm.Value} BPM");

                if (referenceBpm.Value > 0)
                    yield return (MosuModsStrings.ModSpeedChange, $"{SpeedChange.Value:N2}x");

                if (!AdjustPitch.IsDefault)
                    yield return (MosuModsStrings.ModAdjustPitch, AdjustPitch.Value ? MosuModsStrings.Yes : MosuModsStrings.No);

                if (!LockDifficultyAdjust.IsDefault)
                    yield return (MosuModsStrings.ModLockDifficultyAdjust, LockDifficultyAdjust.Value ? MosuModsStrings.Yes : MosuModsStrings.No);
            }
        }

        protected ModMosuStaticBpm()
        {
            rateAdjustHelper = new RateAdjustModHelper(SpeedChange);
            rateAdjustHelper.HandleAudioAdjustments(AdjustPitch);

            TargetBpm.BindValueChanged(_ =>
            {
                preserveExplicitApiSpeedChange = false;
                recalculateSpeedChange();
            }, true);
        }

        public override Mod DeepClone()
        {
            var clone = (ModMosuStaticBpm)base.DeepClone();
            clone.referenceBpm.Value = referenceBpm.Value;
            clone.SpeedChange.Value = SpeedChange.Value;
            clone.preserveExplicitApiSpeedChange = preserveExplicitApiSpeedChange;
            return clone;
        }

        public void ApplyToTrack(IAdjustableAudioComponent track) => rateAdjustHelper.ApplyToTrack(track);

        public void ApplyToSample(IAdjustableAudioComponent sample) => sample.AddAdjustment(AdjustableProperty.Frequency, SpeedChange);

        public double ApplyToRate(double time, double rate = 1) => rate * SpeedChange.Value;

        public void ApplyToBeatmap(IBeatmap beatmap) => updateReferenceBpm(getBeatmapBpm(beatmap));

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

        public void UpdateFromBeatmapInfo(IBeatmapInfo beatmapInfo) => updateReferenceBpm(beatmapInfo?.BPM ?? double.NaN);

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

        private void updateReferenceBpm(double bpm)
        {
            referenceBpm.Value = double.IsFinite(bpm) && bpm > 0 ? bpm : 0;
            recalculateSpeedChange();
        }

        private void recalculateSpeedChange()
        {
            if (preserveExplicitApiSpeedChange)
                return;

            if (referenceBpm.Value <= 0)
            {
                SpeedChange.Value = 1;
                return;
            }

            SpeedChange.Value = Math.Clamp(TargetBpm.Value / referenceBpm.Value, min_speed_change, max_speed_change);
        }

        private static double getBeatmapBpm(IBeatmap beatmap)
        {
            double bpm = beatmap.BeatmapInfo.BPM;

            if (double.IsFinite(bpm) && bpm > 0)
                return bpm;

            double beatLength = beatmap.GetMostCommonBeatLength();

            if (!double.IsFinite(beatLength) || beatLength <= 0)
                return double.NaN;

            return 60000 / beatLength;
        }
    }
}
