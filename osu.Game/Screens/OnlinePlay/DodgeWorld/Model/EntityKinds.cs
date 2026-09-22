// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// The <see cref="EntityRecord.Kind"/> discriminators understood by this client.
    /// </summary>
    /// <remarks>
    /// These strings are part of the stored document, so they must not be renamed. An unrecognised
    /// kind is skipped on load rather than treated as an error, so that a world published by a newer
    /// client still opens.
    /// </remarks>
    public static class EntityKinds
    {
        public const string SURFACE = "surface";
        public const string COLLISION = "collision";
        public const string MORA = "mora";
        public const string NPC = "npc";
        public const string TERMINAL = "terminal";
        public const string PORTAL = "portal";
        public const string PASSAGE = "passage";
        public const string MOB_SPAWN = "mob-spawn";
        public const string WARP = "warp";

        /// <summary>A device that fires a repeating fan of projectiles, for building courses.</summary>
        public const string EMITTER = "emitter";

        /// <summary>A device that holds a lethal line on and off, for building courses.</summary>
        public const string BEAM = "beam";
    }
}
