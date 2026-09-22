// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;
using osuTK;

namespace osu.Game.Online.DodgeWorld
{
    /// <summary>
    /// Where a player is in a Dodge World room, as it travels over the wire.
    /// </summary>
    /// <remarks>
    /// Sent many times a second, so it carries only what another client needs in order to draw the
    /// player: no room id (the hub knows which room the connection is in) and no username (looked up
    /// once from the user id). Coordinates are plain floats rather than a <see cref="Vector2"/>,
    /// matching the other realtime models, which keep to primitives MessagePack handles natively.
    /// </remarks>
    [Serializable]
    [MessagePackObject]
    public class DodgeWorldPlayerState : IEquatable<DodgeWorldPlayerState>
    {
        [Key(0)]
        public float X { get; set; }

        [Key(1)]
        public float Y { get; set; }

        [Key(2)]
        public float FacingX { get; set; }

        [Key(3)]
        public float FacingY { get; set; }

        [Key(4)]
        public bool Moving { get; set; }

        [Key(5)]
        public int Health { get; set; }

        [IgnoreMember]
        public Vector2 Position
        {
            get => new Vector2(X, Y);
            set
            {
                X = value.X;
                Y = value.Y;
            }
        }

        [IgnoreMember]
        public Vector2 Facing
        {
            get => new Vector2(FacingX, FacingY);
            set
            {
                FacingX = value.X;
                FacingY = value.Y;
            }
        }

        /// <summary>
        /// Whether every field is usable. A state that fails this is dropped rather than trusted:
        /// these numbers reach other players' screens and drive their maths.
        /// </summary>
        [IgnoreMember]
        public bool IsValid => float.IsFinite(X) && float.IsFinite(Y)
                                                 && float.IsFinite(FacingX) && float.IsFinite(FacingY)
                                                 && Math.Abs(X) <= 100_000 && Math.Abs(Y) <= 100_000
                                                 && Health >= 0 && Health <= 1_000;

        public DodgeWorldPlayerState Clone() => new DodgeWorldPlayerState
        {
            X = X,
            Y = Y,
            FacingX = FacingX,
            FacingY = FacingY,
            Moving = Moving,
            Health = Health,
        };

        public bool Equals(DodgeWorldPlayerState? other) =>
            other != null && X == other.X && Y == other.Y && FacingX == other.FacingX
            && FacingY == other.FacingY && Moving == other.Moving && Health == other.Health;

        public override bool Equals(object? obj) => Equals(obj as DodgeWorldPlayerState);

        public override int GetHashCode() => HashCode.Combine(X, Y, FacingX, FacingY, Moving, Health);

        public override string ToString() => $"({X:0}, {Y:0}) moving: {Moving} health: {Health}";
    }
}
