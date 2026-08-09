// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Online.API.Requests;
using osu.Game.Users;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class WeeklyRankingResponseTest
    {
        [Test]
        public void TestDeserializesRankChange()
        {
            var response = JsonConvert.DeserializeObject<GetWeeklyRankingsResponse>(@"{
                'ranking': [{
                    'global_rank': 3,
                    'rank_change': {
                        'status': 'up',
                        'delta': 12,
                        'previous_rank': 15
                    }
                }],
                'total': 1,
                'comparison_days': 7,
                'snapshot_date': '2026-08-02',
                'history_available': true
            }");

            Assert.That(response, Is.Not.Null);

            var weeklyResponse = response!;

            Assert.That(weeklyResponse.Users, Has.Count.EqualTo(1));
            Assert.That(weeklyResponse.Users[0].GlobalRank, Is.EqualTo(3));
            Assert.That(weeklyResponse.Users[0].RankChange.Status, Is.EqualTo(WeeklyRankChangeStatus.Up));
            Assert.That(weeklyResponse.Users[0].RankChange.Delta, Is.EqualTo(12));
            Assert.That(weeklyResponse.Users[0].RankChange.PreviousRank, Is.EqualTo(15));
            Assert.That(weeklyResponse.ComparisonDays, Is.EqualTo(7));
            Assert.That(weeklyResponse.HistoryAvailable, Is.True);
        }
    }
}
