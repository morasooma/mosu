// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// Dimensions and identifiers shared by the world document, the simulation and the editor.
    /// </summary>
    public static class DodgeWorldDefaults
    {
        public const string DOCUMENT_VERSION_FIELD = "version";

        /// <summary>
        /// The only document version this client understands.
        /// </summary>
        public const int SUPPORTED_DOCUMENT_VERSION = 1;

        /// <summary>
        /// The room every world is expected to contain, and the room published worlds always open in.
        /// </summary>
        public const string ROOT_ROOM_ID = "mora-plaza";

        public static readonly Vector2 MAP_SIZE = new Vector2(2200, 1100);
        public static readonly Vector2 MINIMUM_MAP_SIZE = new Vector2(800, 600);
        public static readonly Vector2 MAXIMUM_MAP_SIZE = new Vector2(6000, 4000);

        public static readonly Vector2 PLAYER_SPAWN = new Vector2(0, 350);
    }
}
