using NUnit.Framework;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Tests.Scoring
{
    [TestFixture]
    public class StableSpinnerStateTest
    {
        [TestCase(0, 6)]
        [TestCase(5, 10)]
        [TestCase(10, 15)]
        public void RequiredHalfTurnsMatchStable(double od, int expected)
            => Assert.That(new StableSpinnerState(2000, od).RequiredHalfTurns, Is.EqualTo(expected));

        [TestCase(1, 10, 0)]
        [TestCase(2, 10, 100)]
        [TestCase(13, 10, 0)]
        [TestCase(14, 10, 100)]
        [TestCase(15, 10, 1100)]
        [TestCase(16, 10, 100)]
        [TestCase(17, 10, 1100)]
        [TestCase(18, 15, 100)]
        [TestCase(19, 15, 0)]
        [TestCase(20, 15, 1100)]
        public void HalfTurnScoreMatchesStableBranch(int halfTurn, int required, int expected)
            => Assert.That(StableSpinnerState.ScoreForHalfTurn(halfTurn, required), Is.EqualTo(expected));

        [Test]
        public void BonusContinuesBeyondLazerTickLimit()
        {
            var state = new StableSpinnerState(2000, 5);
            for (int i = 1; i <= 1999; i++)
                state.Advance(i, 1, 0.05);
            Assert.That(state.HalfTurns, Is.GreaterThan(20));
            Assert.That(state.BonusCount, Is.GreaterThan(5));
            Assert.That(state.GetJudgement(), Is.EqualTo(HitResult.Great));
        }

        [Test]
        public void VelocityCapIsIndependentOfExcessiveInput()
        {
            var normal = new StableSpinnerState(2000, 5);
            var excessive = new StableSpinnerState(2000, 5);
            for (int i = 1; i <= 1999; i++)
            {
                normal.Advance(i, 1, 0.05);
                excessive.Advance(i, 1, 500);
            }
            Assert.That(excessive.HalfTurns, Is.EqualTo(normal.HalfTurns));
            Assert.That(excessive.SpinScore, Is.EqualTo(normal.SpinScore));
        }

        [Test]
        public void RewindRestoresAndReplayingDoesNotDoubleCount()
        {
            var state = new StableSpinnerState(2000, 5);
            for (int i = 1; i <= 1000; i++)
                state.Advance(i, 1, 0.05);
            long halfway = state.SpinScore;
            for (int i = 1001; i <= 1999; i++)
                state.Advance(i, 1, 0.05);
            long final = state.SpinScore;
            state.Advance(1000, -999, 0.05);
            Assert.That(state.SpinScore, Is.EqualTo(halfway));
            for (int i = 1001; i <= 1999; i++)
                state.Advance(i, 1, 0.05);
            Assert.That(state.SpinScore, Is.EqualTo(final));
        }

        [Test]
        public void RelaxRetainsStableQuarterSpeedDeceleration()
        {
            var normal = new StableSpinnerState(2000, 5);
            var relax = new StableSpinnerState(2000, 5);
            for (int i = 1; i <= 100; i++)
            {
                normal.Advance(i, 1, 0.05);
                relax.Advance(i, 1, 0.05, relax: true);
            }
            float normalDelta = normal.Advance(101, 1, 0);
            float relaxDelta = relax.Advance(101, 1, 0, relax: true);
            Assert.That(relaxDelta, Is.GreaterThan(normalDelta));
        }

        [Test]
        public void StationaryInputDoesNotGenerateScore()
        {
            var state = new StableSpinnerState(2000, 5);
            for (int i = 1; i <= 1999; i++)
                state.Advance(i, 1, 0);
            Assert.That(state.SpinScore, Is.Zero);
            Assert.That(state.GetJudgement(), Is.EqualTo(HitResult.Miss));
        }
    }
}
