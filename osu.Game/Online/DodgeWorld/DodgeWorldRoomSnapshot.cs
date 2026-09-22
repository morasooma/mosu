// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;

namespace osu.Game.Online.DodgeWorld
{
    /// <summary>
    /// One mob as the server sees it.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class DodgeWorldMob
    {
        [Key(0)]
        public int Id { get; set; }

        [Key(1)]
        public float X { get; set; }

        [Key(2)]
        public float Y { get; set; }

        [Key(3)]
        public int Health { get; set; }

        [Key(4)]
        public int MaximumHealth { get; set; }

        /// <summary>
        /// The spawn zone that produced this mob. Carried because the client draws a zone's mobs with
        /// the texture the author gave that zone.
        /// </summary>
        [Key(5)]
        public string ZoneId { get; set; } = string.Empty;
    }

    /// <summary>
    /// One shot in flight.
    /// </summary>
    /// <remarks>
    /// Carries its velocity as well as its position, so that a client can carry it on between snapshots
    /// instead of showing it jumping twenty times a second.
    /// </remarks>
    [Serializable]
    [MessagePackObject]
    public class DodgeWorldProjectile
    {
        [Key(0)]
        public int Id { get; set; }

        [Key(1)]
        public float X { get; set; }

        [Key(2)]
        public float Y { get; set; }

        [Key(3)]
        public float VelocityX { get; set; }

        [Key(4)]
        public float VelocityY { get; set; }
    }

    /// <summary>
    /// A volley about to be fired, which is the warning a player gets to move out of the way.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class DodgeWorldTelegraph
    {
        [Key(0)]
        public int Id { get; set; }

        [Key(1)]
        public float X { get; set; }

        [Key(2)]
        public float Y { get; set; }

        /// <summary>The directions the volley will take, in radians.</summary>
        [Key(3)]
        public float[] Angles { get; set; } = Array.Empty<float>();

        [Key(4)]
        public float Range { get; set; }
    }

    /// <summary>
    /// One beam of a room's course, as the server has it right now.
    /// </summary>
    /// <remarks>
    /// Sent rather than derived, even though a beam's cycle is fixed by the room: the client has no clock
    /// in common with the server, so it cannot know where in that cycle the room currently is.
    /// </remarks>
    [Serializable]
    [MessagePackObject]
    public class DodgeWorldBeam
    {
        [Key(0)]
        public int Id { get; set; }

        [Key(1)]
        public float X { get; set; }

        [Key(2)]
        public float Y { get; set; }

        /// <summary>Where the line points, in degrees.</summary>
        [Key(3)]
        public float AngleDegrees { get; set; }

        [Key(4)]
        public float Length { get; set; }

        [Key(5)]
        public float Width { get; set; }

        /// <summary>Whether the beam is lethal at this moment.</summary>
        [Key(6)]
        public bool Active { get; set; }

        /// <summary>Whether the beam is showing itself before becoming lethal.</summary>
        [Key(7)]
        public bool Warning { get; set; }
    }

    /// <summary>
    /// Everything hostile in a room, as of one moment on the server.
    /// </summary>
    /// <remarks>
    /// A whole snapshot rather than a stream of appear/move/disappear events: it costs a little more
    /// bandwidth and removes every ordering problem, including what a client that joined mid-fight or
    /// missed a message should believe. Anything absent from a snapshot is gone.
    /// </remarks>
    [Serializable]
    [MessagePackObject]
    public class DodgeWorldRoomSnapshot
    {
        [Key(0)]
        public DodgeWorldMob[] Mobs { get; set; } = Array.Empty<DodgeWorldMob>();

        [Key(1)]
        public DodgeWorldProjectile[] Projectiles { get; set; } = Array.Empty<DodgeWorldProjectile>();

        [Key(2)]
        public DodgeWorldTelegraph[] Telegraphs { get; set; } = Array.Empty<DodgeWorldTelegraph>();

        /// <summary>The room's beams. Fixtures rather than events, so they are all reported every time.</summary>
        [Key(3)]
        public DodgeWorldBeam[] Beams { get; set; } = Array.Empty<DodgeWorldBeam>();
    }

    /// <summary>
    /// The answer to entering a room.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    public class DodgeWorldRoomJoin
    {
        [Key(0)]
        public DodgeWorldUser[] Players { get; set; } = Array.Empty<DodgeWorldUser>();

        /// <summary>
        /// Whether the server is running this room's mobs.
        /// </summary>
        /// <remarks>
        /// False when the room is not part of the published world — an unpublished draft, or a world the
        /// server could not read. The client then falls back to simulating the room's mobs itself, which
        /// is what it did before any of this existed, rather than standing in an empty room.
        /// </remarks>
        [Key(1)]
        public bool ServerSimulated { get; set; }
    }
}
