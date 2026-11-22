using System.Linq;
using NUnit.Framework;
using osu.Game.Online.Leaderboards;
using osu.Game.Scoring;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LeaderboardPpSortingTest
    {
        [Test]
        public void TestPpSortUsesPerformanceThenScoreAndTreatsMissingPpAsZero()
        {
            var highestScore = new ScoreInfo { PP = 10, TotalScore = 1_000_000 };
            var highestPp = new ScoreInfo { PP = 100.5, TotalScore = 100 };
            var tiedPp = new ScoreInfo { PP = 100.5, TotalScore = 200 };
            var missingPp = new ScoreInfo { TotalScore = 500 };
            var zeroPp = new ScoreInfo { PP = 0, TotalScore = 600 };
            ScoreInfo[] scores = [highestScore, missingPp, highestPp, zeroPp, tiedPp];

            Assert.That(scores.OrderByCriteria(LeaderboardSortMode.PP).ToArray(),
                Is.EqualTo(new[] { tiedPp, highestPp, highestScore, zeroPp, missingPp }));
            Assert.That(scores.OrderByCriteria(LeaderboardSortMode.Score).First(), Is.SameAs(highestScore));
        }
    }
}
