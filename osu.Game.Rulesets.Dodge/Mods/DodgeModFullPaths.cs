// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.Objects.Drawables;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;

namespace osu.Game.Rulesets.Dodge.Mods
{
    public class DodgeModFullPaths : Mod, IApplicableToDrawableHitObject
    {
        public override string Name => "Full Paths";

        public override string Acronym => "FP";

        public override IconUsage? Icon => OsuIcon.ModTraceable;

        public override ModType Type => ModType.Mosu;

        public override LocalisableString Description => DodgeEditorStrings.FullPathsDescription;

        public override bool Ranked => false;

        public void ApplyToDrawableHitObject(DrawableHitObject drawable)
        {
            switch (drawable)
            {
                case DrawableDodgeHitObject bullet:
                    bullet.ShowFullTrajectory = true;
                    break;

                case DrawableDodgeEmitter emitter:
                    emitter.ShowFullTrajectories = true;
                    break;
            }
        }
    }
}
