// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// A device that fires a fan of projectiles on a loop.
    /// </summary>
    internal partial class WorldEmitter : WorldHazardDevice
    {
        /// <summary>How many projectiles leave the device each cycle.</summary>
        public int ProjectileCount { get; set; } = 5;

        /// <summary>The arc the fan covers, in degrees. 360 fires a ring.</summary>
        public float Spread { get; set; } = 60;

        public int ProjectileDamage { get; set; } = 8;
        public float ProjectileSpeed { get; set; } = 150;
        public float ProjectileRange { get; set; } = 600;

        public override float? FixedDepth => null;
        public override string LayoutKind => "emitter";

        public WorldEmitter(OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select)
            : base(colours, editing, select, FontAwesome.Solid.Bullseye, colours.Red1)
        {
        }
    }
}
