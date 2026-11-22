// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Legacy;
using osu.Game.Online.API;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class LeaderboardManagerTest
    {
        [Test]
        public async Task TestFailureFormattingRunsOffCallerAndPreservesDiagnostic()
        {
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            var exception = new FormattingProbeException(entered, release);
            int caller = Environment.CurrentManagedThreadId;
            string? logged = null;
            void onEntry(LogEntry entry)
            {
                if (entry.Message == "leaderboard formatting probe")
                    logged = entry.Exception?.ToString();
            }

            Logger.NewEntry += onEntry;
            Task? task = null;
            try
            {
                task = LeaderboardManager.LogFailureAsync("leaderboard formatting probe", exception);
                Assert.That(entered.Wait(TimeSpan.FromSeconds(5)), Is.True);
                Assert.That(exception.FormattingThread, Is.Not.EqualTo(caller));
                Assert.That(task.IsCompleted, Is.False, "formatting should still be blocked without blocking the request callback");
                release.Set();
                await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Assert.That(logged, Is.EqualTo("complete leaderboard diagnostic"));
            }
            finally
            {
                release.Set();
                if (task != null)
                    await task.WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                Logger.NewEntry -= onEntry;
            }
        }

        private class FormattingProbeException : Exception
        {
            private readonly ManualResetEventSlim entered;
            private readonly ManualResetEventSlim release;
            public int FormattingThread;

            public FormattingProbeException(ManualResetEventSlim entered, ManualResetEventSlim release)
            {
                this.entered = entered;
                this.release = release;
            }

            public override string ToString()
            {
                FormattingThread = Environment.CurrentManagedThreadId;
                entered.Set();
                if (!release.Wait(TimeSpan.FromSeconds(10)))
                    throw new TimeoutException("Leaderboard formatting gate was not released.");
                return "complete leaderboard diagnostic";
            }
        }

        [Test]
        public void TestNotFoundMeansBeatmapUnavailable()
        {
            var exception = new APIException("Beatmap not found", null, HttpStatusCode.NotFound);

            Assert.That(LeaderboardManager.GetFailureState(exception), Is.EqualTo(LeaderboardFailState.BeatmapUnavailable));
        }

        [Test]
        public void TestOtherFailureMeansNetworkFailure()
        {
            var exception = new APIException("Service unavailable", null, HttpStatusCode.ServiceUnavailable);

            Assert.That(LeaderboardManager.GetFailureState(exception), Is.EqualTo(LeaderboardFailState.NetworkFailure));
        }

        [Test]
        public void TestUnknownLocalMapUsesChecksumLookupOnlyWhenLeaderboardIsOpened()
        {
            var beatmap = new BeatmapInfo { OnlineID = -1, MD5Hash = "d3509f8057630f8774e07d2514e603d1" };
            Assert.That(LeaderboardManager.requiresChecksumLookup(beatmap), Is.True);

            beatmap.MD5Hash = string.Empty;
            Assert.That(LeaderboardManager.requiresChecksumLookup(beatmap), Is.False);
        }

        [Test]
        public void TestStableRelaxLeaderboardUsesBaseRulesetAndRelaxBit()
        {
            RulesetInfo relaxRuleset = new OsuRuleset().RulesetInfo.CreateSpecialRuleset(RulesetInfo.OSU_RELAX_MODE_SHORTNAME, RulesetInfo.OSU_RELAX_ONLINE_ID);

            (RulesetInfo ruleset, LegacyMods specialModeMod) = LeaderboardManager.NormaliseStableRuleset(relaxRuleset);

            Assert.Multiple(() =>
            {
                Assert.That(ruleset.ShortName, Is.EqualTo(RulesetInfo.OSU_MODE_SHORTNAME));
                Assert.That(ruleset.OnlineID, Is.Zero);
                Assert.That(specialModeMod, Is.EqualTo(LegacyMods.Relax));
            });
        }

        [Test]
        public void TestStableNormalLeaderboardDoesNotAddSpecialMod()
        {
            RulesetInfo normalRuleset = new OsuRuleset().RulesetInfo;

            (RulesetInfo ruleset, LegacyMods specialModeMod) = LeaderboardManager.NormaliseStableRuleset(normalRuleset);

            Assert.Multiple(() =>
            {
                Assert.That(ruleset, Is.SameAs(normalRuleset));
                Assert.That(specialModeMod, Is.EqualTo(LegacyMods.None));
            });
        }
    }
}
