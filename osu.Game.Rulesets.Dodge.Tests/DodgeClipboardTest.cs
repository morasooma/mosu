// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.IO.Serialization;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeClipboardTest
    {
        [Test]
        public void TestAllObjectPropertiesSurviveClipboardRoundTrip()
        {
            var bullet = new DodgeBullet
            {
                StartTime = 1234,
                Duration = 567,
                Position = new Vector2(-12, 34),
                EndPosition = new Vector2(600, 250),
                Shape = DodgeBulletShape.Triangle,
                ContinueUntilExit = true,
                Colour = Colour4.Red,
                OutlineColour = Colour4.Blue,
                Opacity = 0.6f,
                OutlineThickness = 2,
            };
            var emitter = new DodgeEmitter
            {
                StartTime = 2345,
                Duration = 678,
                Position = new Vector2(10, 20),
                AimPosition = new Vector2(300, 400),
                MovementEndPosition = new Vector2(100, 200),
                Shape = DodgeBulletShape.Diamond,
                BulletCount = 13,
                SpreadAngle = 222,
                ContinueUntilExit = true,
                BurstCount = 7,
                BurstInterval = 89,
                BurstBeatDivisor = (int)DodgeEmitterBeatDivisor.Sixth,
                MoveSource = true,
                Colour = Colour4.Green,
                OutlineColour = Colour4.Yellow,
                Opacity = 0.7f,
                OutlineThickness = 3,
            };
            var arena = new DodgeArenaChange
            {
                StartTime = 3456,
                Duration = 789,
                TargetPosition = new Vector2(40, 50),
                TargetSize = new Vector2(320, 240),
            };

            var content = new ClipboardContent
            {
                HitObjects = new HitObject[] { bullet, emitter, arena },
            };

            ClipboardContent result = content.Serialize().Deserialize<ClipboardContent>();
            var resultBullet = (DodgeBullet)result.HitObjects.ElementAt(0);
            var resultEmitter = (DodgeEmitter)result.HitObjects.ElementAt(1);
            var resultArena = (DodgeArenaChange)result.HitObjects.ElementAt(2);

            Assert.Multiple(() =>
            {
                Assert.That(resultBullet.StartTime, Is.EqualTo(bullet.StartTime));
                Assert.That(resultBullet.Duration, Is.EqualTo(bullet.Duration));
                Assert.That(resultBullet.Position, Is.EqualTo(bullet.Position));
                Assert.That(resultBullet.EndPosition, Is.EqualTo(bullet.EndPosition));
                Assert.That(resultBullet.Shape, Is.EqualTo(bullet.Shape));
                Assert.That(resultBullet.ContinueUntilExit, Is.EqualTo(bullet.ContinueUntilExit));
                Assert.That(resultBullet.Colour, Is.EqualTo(bullet.Colour));
                Assert.That(resultBullet.OutlineColour, Is.EqualTo(bullet.OutlineColour));
                Assert.That(resultBullet.Opacity, Is.EqualTo(bullet.Opacity));
                Assert.That(resultBullet.OutlineThickness, Is.EqualTo(bullet.OutlineThickness));

                Assert.That(resultEmitter.StartTime, Is.EqualTo(emitter.StartTime));
                Assert.That(resultEmitter.Duration, Is.EqualTo(emitter.Duration));
                Assert.That(resultEmitter.Position, Is.EqualTo(emitter.Position));
                Assert.That(resultEmitter.AimPosition, Is.EqualTo(emitter.AimPosition));
                Assert.That(resultEmitter.MovementEndPosition, Is.EqualTo(emitter.MovementEndPosition));
                Assert.That(resultEmitter.Shape, Is.EqualTo(emitter.Shape));
                Assert.That(resultEmitter.BulletCount, Is.EqualTo(emitter.BulletCount));
                Assert.That(resultEmitter.SpreadAngle, Is.EqualTo(emitter.SpreadAngle));
                Assert.That(resultEmitter.ContinueUntilExit, Is.EqualTo(emitter.ContinueUntilExit));
                Assert.That(resultEmitter.BurstCount, Is.EqualTo(emitter.BurstCount));
                Assert.That(resultEmitter.BurstInterval, Is.EqualTo(emitter.BurstInterval));
                Assert.That(resultEmitter.BurstBeatDivisor, Is.EqualTo(emitter.BurstBeatDivisor));
                Assert.That(resultEmitter.MoveSource, Is.EqualTo(emitter.MoveSource));
                Assert.That(resultEmitter.Colour, Is.EqualTo(emitter.Colour));
                Assert.That(resultEmitter.OutlineColour, Is.EqualTo(emitter.OutlineColour));
                Assert.That(resultEmitter.Opacity, Is.EqualTo(emitter.Opacity));
                Assert.That(resultEmitter.OutlineThickness, Is.EqualTo(emitter.OutlineThickness));

                Assert.That(resultArena.StartTime, Is.EqualTo(arena.StartTime));
                Assert.That(resultArena.Duration, Is.EqualTo(arena.Duration));
                Assert.That(resultArena.TargetPosition, Is.EqualTo(arena.TargetPosition));
                Assert.That(resultArena.TargetSize, Is.EqualTo(arena.TargetSize));
            });
        }
    }
}
