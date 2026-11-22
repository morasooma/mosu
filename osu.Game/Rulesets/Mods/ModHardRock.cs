// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;

namespace osu.Game.Rulesets.Mods
{
    public abstract class ModHardRock : Mod, IApplicableToDifficulty
    {
        public override string Name => "Hard Rock";
        public override string Acronym => "HR";
        public override IconUsage? Icon => OsuIcon.ModHardRock;
        public override ModType Type => ModType.DifficultyIncrease;
        public override LocalisableString Description => global::osu.Game.Localisation.ClientTranslationStrings.J4226;
        public override Type[] IncompatibleMods => new[] { typeof(ModEasy), typeof(ModDifficultyAdjust) };
        public override bool Ranked => UsesDefaultConfiguration;
        public override bool ValidForFreestyleAsRequiredMod => true;

        [SettingSource(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J4227), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J4228))]
        public BindableBool LockHPAdjust { get; } = new BindableBool();

        protected const float ADJUST_RATIO = 1.4f;

        public override IEnumerable<(LocalisableString setting, LocalisableString value)> SettingDescription
        {
            get
            {
                if (!Online.MosuServerEnvironment.IsThirdPartyServer && !LockHPAdjust.IsDefault)
                    yield return (global::osu.Game.Localisation.ClientTranslationStrings.J4227, LockHPAdjust.Value ? global::osu.Game.Localisation.ClientTranslationStrings.J4229 : global::osu.Game.Localisation.ClientTranslationStrings.J4230);
            }
        }

        public virtual void ApplyToDifficulty(BeatmapDifficulty difficulty)
        {
            if (Online.MosuServerEnvironment.IsThirdPartyServer || !LockHPAdjust.Value)
            {
                difficulty.DrainRate = Math.Min(difficulty.DrainRate * ADJUST_RATIO, 10.0f);
            }
        }
    }
}
