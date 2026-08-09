// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Skinning;

namespace osu.Game.Rulesets.Dodge.Skinning
{
    public enum DodgeSkinComponents
    {
        Player,
        Arena,
        ArenaBorder,
        BulletCircle,
        BulletSquare,
        BulletDiamond,
        BulletTriangle,
        GrazeEffect,
        CollisionEffect,
        PlayerTrail,
    }

    public class DodgeSkinComponentLookup : SkinComponentLookup<DodgeSkinComponents>
    {
        public DodgeSkinComponentLookup(DodgeSkinComponents component)
            : base(component)
        {
        }
    }
}
