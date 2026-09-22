// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Dodge.Mods
{
    public class DodgeModHalfTime : ModHalfTime
    {
        public override bool Ranked => false;
    }

    public class DodgeModDaycore : ModDaycore
    {
        public override bool Ranked => false;
    }

    public class DodgeModDoubleTime : ModDoubleTime
    {
        public override bool Ranked => false;
    }

    public class DodgeModNightcore : ModNightcore<DodgeHitObject>
    {
        public override bool Ranked => false;
    }
}
