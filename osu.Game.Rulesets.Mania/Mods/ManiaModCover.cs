// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Rulesets.Mania.UI;

namespace osu.Game.Rulesets.Mania.Mods
{
    public class ManiaModCover : ManiaModWithPlayfieldCover
    {
        public override string Name => "Cover";
        public override string Acronym => "CO";
        public override IconUsage? Icon => OsuIcon.ModCover;

        public override LocalisableString Description => global::osu.Game.Localisation.ClientTranslationStrings.J3627;

        protected override CoverExpandDirection ExpandDirection => Direction.Value;

        public override Type[] IncompatibleMods => base.IncompatibleMods.Concat(new[]
        {
            typeof(ManiaModHidden),
            typeof(ManiaModFadeIn)
        }).ToArray();

        public override bool Ranked => true;

        public override bool ValidForFreestyleAsRequiredMod => false;

        [SettingSource(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3628), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3629))]
        public override BindableNumber<float> Coverage { get; } = new BindableFloat(0.5f)
        {
            Precision = 0.1f,
            MinValue = 0.2f,
            MaxValue = 0.8f,
            Default = 0.5f,
        };

        [SettingSource(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3630), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3631))]
        public Bindable<CoverExpandDirection> Direction { get; } = new Bindable<CoverExpandDirection>();
    }
}
