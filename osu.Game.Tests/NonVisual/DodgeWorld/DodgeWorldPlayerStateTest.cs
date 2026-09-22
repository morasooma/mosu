// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using MessagePack;
using NUnit.Framework;
using osu.Game.Online.DodgeWorld;
using osuTK;

namespace osu.Game.Tests.NonVisual.DodgeWorld
{
    /// <summary>
    /// Covers the state that travels between players in a Dodge World room.
    /// </summary>
    [TestFixture]
    public class DodgeWorldPlayerStateTest
    {
        /// <summary>
        /// The hub speaks MessagePack, so a field without a key would silently stop crossing the wire.
        /// </summary>
        [Test]
        public void EveryFieldSurvivesTheWire()
        {
            var original = new DodgeWorldPlayerState
            {
                Position = new Vector2(120.5f, -40),
                Facing = new Vector2(0, -1),
                Moving = true,
                Health = 7,
            };

            var restored = MessagePackSerializer.Deserialize<DodgeWorldPlayerState>(MessagePackSerializer.Serialize(original));

            Assert.That(restored.Position, Is.EqualTo(new Vector2(120.5f, -40)));
            Assert.That(restored.Facing, Is.EqualTo(new Vector2(0, -1)));
            Assert.That(restored.Moving, Is.True);
            Assert.That(restored.Health, Is.EqualTo(7));
            Assert.That(restored, Is.EqualTo(original));
        }

        /// <summary>
        /// These numbers arrive from another client and are used in local maths, so anything unusable
        /// has to be recognisable before it is trusted.
        /// </summary>
        [TestCase(0f, 0f, 0, true)]
        [TestCase(float.NaN, 0f, 0, false)]
        [TestCase(0f, float.PositiveInfinity, 0, false)]
        [TestCase(1e9f, 0f, 0, false)]
        [TestCase(0f, 0f, -1, false)]
        [TestCase(0f, 0f, 5000, false)]
        public void BrokenStateIsRecognised(float x, float y, int health, bool valid)
        {
            var state = new DodgeWorldPlayerState { X = x, Y = y, Health = health };

            Assert.That(state.IsValid, Is.EqualTo(valid));
        }

        [Test]
        public void ACloneIsIndependentOfWhatItCameFrom()
        {
            var original = new DodgeWorldPlayerState { X = 5 };
            DodgeWorldPlayerState clone = original.Clone();

            original.X = 90;

            Assert.That(clone.X, Is.EqualTo(5));
            Assert.That(clone, Is.Not.EqualTo(original));
        }
    }
}
