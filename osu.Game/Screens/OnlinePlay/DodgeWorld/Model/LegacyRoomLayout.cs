// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Model
{
    /// <summary>
    /// The single-room local layout format that predates <see cref="DodgeWorldDocument"/>.
    /// </summary>
    /// <remarks>
    /// Only ever read, never written, and only from local preview storage. Property names are
    /// intentionally left in PascalCase because that is how the retired format serialised them.
    /// </remarks>
    public sealed class LegacyRoomLayout
    {
        public const int SUPPORTED_VERSION = 3;

        public const string FILENAME = "layout-v3.json";

        public int Version { get; set; }

        public List<EntityRecord> Entities { get; set; } = new List<EntityRecord>();
    }
}
