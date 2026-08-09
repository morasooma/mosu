// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Configuration;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Dodge.Configuration
{
    public class DodgeRulesetConfigManager : RulesetConfigManager<DodgeRulesetSetting>
    {
        public const double DEFAULT_MISS_SOUND_VOLUME = 60;
        public const double DEFAULT_PLAYFIELD_DIM = 1;
        public const double DEFAULT_GRAZE_INDICATOR_BRIGHTNESS = 0.35;

        public DodgeRulesetConfigManager(SettingsStore? settings, RulesetInfo ruleset, int? variant = null)
            : base(settings!, ruleset, variant)
        {
        }

        protected override void InitialiseDefaults()
        {
            base.InitialiseDefaults();

            SetDefault(DodgeRulesetSetting.MissSoundEnabled, true);
            SetDefault(DodgeRulesetSetting.MissSoundVolume, DEFAULT_MISS_SOUND_VOLUME, 0, 100, 1);
            SetDefault(DodgeRulesetSetting.PlayfieldDim, DEFAULT_PLAYFIELD_DIM, 0, 1, 0.01);
            SetDefault(DodgeRulesetSetting.GrazeIndicatorBrightness, DEFAULT_GRAZE_INDICATOR_BRIGHTNESS, 0, 1, 0.01);
        }
    }

    public enum DodgeRulesetSetting
    {
        MissSoundEnabled,
        MissSoundVolume,
        PlayfieldDim,
        GrazeIndicatorBrightness,
    }
}
