// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeCameraStateEvaluatorTest
    {
        [TestCase(DodgeCameraEasing.Linear, 50)]
        [TestCase(DodgeCameraEasing.EaseIn, 25)]
        [TestCase(DodgeCameraEasing.EaseOut, 75)]
        [TestCase(DodgeCameraEasing.EaseInOut, 50)]
        [TestCase(DodgeCameraEasing.Smooth, 50)]
        public void TestMovementCurveAtHalfTime(DodgeCameraEasing easing, float expectedX)
        {
            var evaluator = new DodgeCameraStateEvaluator(new[]
            {
                createChange(easing),
            });

            Assert.That(evaluator.Evaluate(500).X, Is.EqualTo(expectedX).Within(0.001f));
        }

        [TestCase(DodgeCameraEasing.Overshoot)]
        [TestCase(DodgeCameraEasing.Bounce)]
        [TestCase(DodgeCameraEasing.Elastic)]
        public void TestEffectCurvesFinishAtAuthoredOffset(DodgeCameraEasing easing)
        {
            var evaluator = new DodgeCameraStateEvaluator(new[]
            {
                createChange(easing),
            });

            Assert.That(evaluator.Evaluate(1000), Is.EqualTo(new Vector2(100, 0)));
        }

        [Test]
        public void TestOvershootMovesBeyondAuthoredOffset()
        {
            var evaluator = new DodgeCameraStateEvaluator(new[]
            {
                createChange(DodgeCameraEasing.Overshoot),
            });

            Assert.That(evaluator.Evaluate(750).X, Is.GreaterThan(100));
        }

        [Test]
        public void TestContinuousScrollIgnoresMovementCurve()
        {
            DodgeCameraChange change = createChange(DodgeCameraEasing.Elastic);
            change.Continuous = true;

            var evaluator = new DodgeCameraStateEvaluator(new[] { change });

            Assert.That(evaluator.Evaluate(500), Is.EqualTo(new Vector2(50, 0)));
            Assert.That(evaluator.Evaluate(2000), Is.EqualTo(new Vector2(200, 0)));
        }

        private static DodgeCameraChange createChange(DodgeCameraEasing easing) => new DodgeCameraChange
        {
            StartTime = 0,
            Duration = 1000,
            Position = Vector2.Zero,
            EndPosition = new Vector2(100, 0),
            Easing = easing,
        };
    }
}
