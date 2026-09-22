// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Framework.Bindables;
using osu.Game.Online.DodgeWorld;
using osuTK;

namespace osu.Game.Tests.Visual.Online
{
    /// <summary>
    /// A Dodge World client with no server behind it: it records what would have been sent and lets a test
    /// play the part of the hub.
    /// </summary>
    internal partial class TestDodgeWorldClient : DodgeWorldClient
    {
        public override IBindable<bool> IsConnected => isConnected;

        private readonly BindableBool isConnected = new BindableBool();

        public readonly List<string> Joins = new List<string>();

        public int Leaves { get; private set; }

        public int Sent { get; private set; }

        public int Attacks { get; private set; }

        /// <summary>
        /// Who the server will say is already in the room on the next join.
        /// </summary>
        public DodgeWorldUser[] Occupants = [];

        /// <summary>Whether the server will claim to be running the room's mobs.</summary>
        public bool ServerRunsTheRoom;

        public void SetConnected(bool connected) => isConnected.Value = connected;

        protected override Task<DodgeWorldRoomJoin> JoinRoomInternal(string roomId, DodgeWorldPlayerState state)
        {
            Joins.Add(roomId);
            return Task.FromResult(new DodgeWorldRoomJoin { Players = Occupants, ServerSimulated = ServerRunsTheRoom });
        }

        protected override Task LeaveRoomInternal()
        {
            Leaves++;
            return Task.CompletedTask;
        }

        protected override Task UpdateStateInternal(DodgeWorldPlayerState state)
        {
            Sent++;
            return Task.CompletedTask;
        }

        protected override Task AttackInternal(Vector2 direction)
        {
            Attacks++;
            return Task.CompletedTask;
        }

        protected override Task DisconnectInternal() => Task.CompletedTask;

        public override Task Reconnect() => Task.CompletedTask;
    }
}
