// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;
using osu.Game.Online.Matchmaking.Events;
using osu.Game.Online.Multiplayer.Countdown;
using osu.Game.Online.Multiplayer.MatchTypes.Dodge;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Online.Multiplayer.MatchTypes.TeamVersus;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Online.RankedPlay;

namespace osu.Game.Online.Multiplayer
{
    /// <summary>
    /// A request from a user to perform an action specific to the current match type.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    // IMPORTANT: Add rules to SignalRUnionWorkaroundResolver for new derived types.
    [Union(0, typeof(ChangeTeamRequest))]
    [Union(1, typeof(StartMatchCountdownRequest))]
    [Union(2, typeof(StopCountdownRequest))]
    [Union(3, typeof(MatchmakingAvatarActionRequest))]
    [Union(4, typeof(RankedPlayCardHandReplayRequest))]
    [Union(5, typeof(SetLockStateRequest))]
    [Union(6, typeof(RollRequest))]
    [Union(7, typeof(ChangeSlotRequest))]
    [Union(8, typeof(TagCoopCursorPositionRequest))]
    [Union(9, typeof(TagCoopPingRequest))]
    [Union(10, typeof(TagCoopJudgementRequest))]
    [Union(11, typeof(DodgePlayerPositionRequest))]
    [Union(12, typeof(DodgePingRequest))]
    [Union(13, typeof(TagCoopReplayFramesRequest))]
    [Union(14, typeof(RankedPlayCursorPositionRequest))]
    [Union(15, typeof(RankedPlayPingRequest))]
    public abstract class MatchUserRequest
    {
    }
}
