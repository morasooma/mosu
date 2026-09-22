// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using MessagePack;
using osu.Game.Online.Matchmaking.Events;
using osu.Game.Online.Multiplayer.Countdown;
using osu.Game.Online.Multiplayer.MatchTypes.Dodge;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Online.RankedPlay;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;

namespace osu.Game.Online.Multiplayer
{
    /// <summary>
    /// An event from the server to allow clients to update gameplay to an expected state.
    /// </summary>
    [Serializable]
    [MessagePackObject]
    // IMPORTANT: Add rules to SignalRUnionWorkaroundResolver for new derived types.
    [Union(0, typeof(CountdownStartedEvent))]
    [Union(1, typeof(CountdownStoppedEvent))]
    [Union(2, typeof(MatchmakingAvatarActionEvent))]
    [Union(3, typeof(RankedPlayCardHandReplayEvent))]
    [Union(4, typeof(RollEvent))]
    [Union(5, typeof(TagCoopCursorPositionEvent))]
    [Union(6, typeof(TagCoopPingEvent))]
    [Union(7, typeof(TagCoopJudgementEvent))]
    [Union(8, typeof(DodgePlayerPositionEvent))]
    [Union(9, typeof(DodgePingEvent))]
    [Union(10, typeof(TagCoopReplayFramesEvent))]
    [Union(11, typeof(RankedPlayCursorPositionEvent))]
    [Union(12, typeof(RankedPlayPingEvent))]
    public abstract class MatchServerEvent
    {
    }
}
