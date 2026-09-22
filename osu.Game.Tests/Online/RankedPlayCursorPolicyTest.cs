// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Screens.OnlinePlay.Multiplayer;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class RankedPlayCursorPolicyTest
    {
        [TestCase(false, MatchType.RankedPlay, 0, null, false)]
        [TestCase(true, MatchType.HeadToHead, 0, null, false)]
        [TestCase(true, MatchType.RankedPlay, 1, null, false)]
        [TestCase(true, MatchType.RankedPlay, 0, "AP", false)]
        [TestCase(true, MatchType.RankedPlay, 0, null, true)]
        [TestCase(true, MatchType.RankedPlay, 0, "RX", true)]
        public void TestEligibility(bool isRankedPlaySession, MatchType matchType, int rulesetId, string? requiredMod, bool expected)
        {
            APIMod[] mods = requiredMod == null ? [] : [new APIMod { Acronym = requiredMod }];

            Assert.That(MultiplayerPlayer.SupportsRankedPlayCursor(isRankedPlaySession, matchType, rulesetId, mods), Is.EqualTo(expected));
        }

        [TestCase(false, true, 0, false)]
        [TestCase(true, false, 0, false)]
        [TestCase(true, true, 0.5, true)]
        [TestCase(true, true, 1, false)]
        public void TestBreakVisibility(bool hasPosition, bool isBreak, double sampleAgeSeconds, bool expected)
        {
            Assert.That(RankedPlayRemoteCursor.ShouldBeVisible(hasPosition, isBreak, TimeSpan.FromSeconds(sampleAgeSeconds)), Is.EqualTo(expected));
        }
    }
}
