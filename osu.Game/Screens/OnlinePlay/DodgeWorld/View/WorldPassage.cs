// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal abstract partial class WorldPassage : EditableWorldEntity
    {
        /// <summary>
        /// Caption of a passage whose destination has not been chosen yet.
        /// </summary>
        public const string UNLINKED_DESTINATION = "Unlinked passage";

        public string Destination { get; set; } = string.Empty;
        public string? DestinationRoomId { get; set; }

        /// <summary>
        /// The object in the destination room to arrive beside, or null to work it out on arrival.
        /// </summary>
        /// <remarks>
        /// Left null for almost every passage on purpose: a passage whose destination room has one leading
        /// back here arrives at that one, so a two-way corridor needs nothing said about it. This is for
        /// the cases that rule cannot answer — a one-way drop, or a room with several ways back.
        /// </remarks>
        public string? ArrivalEntityId { get; set; }

        /// <summary>
        /// The exact spot in the destination room the player appears at, when the author placed it by hand.
        /// Overrides everything else.
        /// </summary>
        public Vector2? ArrivalPoint { get; set; }

        /// <summary>
        /// Which way the player walks out of the room through this passage, or null for a passage with no
        /// direction of its own.
        /// </summary>
        /// <remarks>
        /// Used to decide which side of the way back somebody arriving stands on: through a door facing
        /// east, you come out west of it. A ring on the floor has no answer to give, and falls back to
        /// stepping towards the middle of the room.
        /// </remarks>
        public virtual Vector2? ExitDirection => null;

        public abstract float TriggerRadius { get; }
        public override bool BlocksMovement => false;

        protected WorldPassage(Func<bool> editing, Action<EditableWorldEntity> select, Color4 selectionColour)
            : base(editing, select, selectionColour)
        {
        }
    }
}
