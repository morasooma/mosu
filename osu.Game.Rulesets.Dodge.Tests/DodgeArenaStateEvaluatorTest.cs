// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeArenaStateEvaluatorTest
    {
        [Test]
        public void TestAppearanceInterpolatesWithArenaGeometry()
        {
            var first = new DodgeArenaChange
            {
                StartTime = 0,
                TargetPosition = Vector2.Zero,
                TargetSize = DodgePlayfield.BASE_SIZE,
                Colour = Colour4.Black,
                Opacity = 1,
                OutlineColour = Colour4.White,
                BorderOpacity = 1,
            };
            var second = new DodgeArenaChange
            {
                StartTime = 1000,
                Duration = 1000,
                TargetPosition = new Vector2(100, 50),
                TargetSize = new Vector2(300, 200),
                Colour = Colour4.Red,
                Opacity = 0.2f,
                OutlineColour = Colour4.Blue,
                BorderOpacity = 0.4f,
            };

            DodgeArenaState state = new DodgeArenaStateEvaluator(new[] { first, second }).Evaluate(1500);

            Assert.Multiple(() =>
            {
                Assert.That(state.Position, Is.EqualTo(new Vector2(50, 25)));
                Assert.That(state.Size, Is.EqualTo(new Vector2(406, 292)));
                Assert.That(state.BackgroundColour, Is.EqualTo(new Colour4(0.5f, 0, 0, 1)));
                Assert.That(state.BackgroundOpacity, Is.EqualTo(0.6f).Within(0.001f));
                Assert.That(state.BorderColour, Is.EqualTo(new Colour4(0.5f, 0.5f, 1, 1)));
                Assert.That(state.BorderOpacity, Is.EqualTo(0.7f).Within(0.001f));
            });
        }

        [Test]
        public void TestDefaultAppearanceMatchesLegacyArena()
        {
            DodgeArenaState state = new DodgeArenaStateEvaluator(System.Array.Empty<DodgeArenaChange>()).Evaluate(0);

            Assert.Multiple(() =>
            {
                Assert.That(state.BackgroundColour, Is.EqualTo(Colour4.Black));
                Assert.That(state.BackgroundOpacity, Is.EqualTo(1));
                Assert.That(state.BorderColour, Is.EqualTo(Colour4.White));
                Assert.That(state.BorderOpacity, Is.EqualTo(1));
            });
        }
    }
}
