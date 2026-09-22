// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeHitObjectClonerTest
    {
        [Test]
        public void TestEveryObjectTypeKeepsAuthoredProperties()
        {
            var bullet = withCommon(new DodgeBullet
            {
                Duration = 900,
                Position = new Vector2(10, 20),
                EndPosition = new Vector2(300, 200),
                Shape = DodgeBulletShape.Triangle,
                ContinueUntilExit = true,
            });
            var emitter = withCommon(new DodgeEmitter
            {
                Duration = 800,
                Position = new Vector2(30, 40),
                AimPosition = new Vector2(300, 40),
                MovementEndPosition = new Vector2(30, 300),
                MoveSource = true,
                BulletCount = 13,
                SpreadAngle = 210,
                BurstCount = 7,
                BurstInterval = 125,
                BurstBeatDivisor = 4,
                BurstRotation = 27.5f,
                Shape = DodgeBulletShape.Diamond,
                ContinueUntilExit = true,
            });
            var beam = withCommon(new DodgeBeam
            {
                Duration = 600,
                Position = new Vector2(10, 90),
                EndPosition = new Vector2(500, 90),
                BeamWidth = 73,
            });
            var arena = withCommon(new DodgeArenaChange
            {
                Duration = 700,
                TargetPosition = new Vector2(50, 40),
                TargetSize = new Vector2(300, 220),
                TargetRotation = 18,
                KiaiShakeAngle = 4.5f,
                BorderOpacity = 0.45f,
                Easing = DodgeCameraEasing.Overshoot,
            });
            var camera = withCommon(new DodgeCameraChange
            {
                Duration = 500,
                Position = new Vector2(100, 100),
                EndPosition = new Vector2(180, 140),
                Continuous = true,
                Easing = DodgeCameraEasing.EaseInOut,
            });
            var trigger = withCommon(new DodgeTrigger
            {
                Duration = 450,
                Position = new Vector2(200, 30),
                Action = DodgeTriggerAction.ScreenShake,
                Strength = 0.8f,
            });

            assertBullet((DodgeBullet)DodgeHitObjectCloner.Clone(bullet, 250, new Vector2(5, 7)), bullet);
            assertEmitter((DodgeEmitter)DodgeHitObjectCloner.Clone(emitter, 250, new Vector2(5, 7)), emitter);
            assertBeam((DodgeBeam)DodgeHitObjectCloner.Clone(beam, 250, new Vector2(5, 7)), beam);
            assertArena((DodgeArenaChange)DodgeHitObjectCloner.Clone(arena, 250, new Vector2(5, 7)), arena);
            assertCamera((DodgeCameraChange)DodgeHitObjectCloner.Clone(camera, 250, new Vector2(5, 7)), camera);
            assertTrigger((DodgeTrigger)DodgeHitObjectCloner.Clone(trigger, 250, new Vector2(5, 7)), trigger);
        }

        private static T withCommon<T>(T source)
            where T : DodgeHitObject
        {
            source.StartTime = 1000;
            source.Colour = Colour4.FromHex("#123456");
            source.OutlineColour = Colour4.FromHex("#FEDCBA");
            source.Opacity = 0.65f;
            source.OutlineThickness = 2.5f;
            source.MovementType = DodgeMovementType.Sine;
            source.MovementEasing = DodgeMovementEasing.EaseInOut;
            source.WaveAmplitude = 44;
            source.WaveCycles = 5;
            source.WavePhase = 37;
            source.TrajectoryGuideStyle = DodgeTrajectoryGuideStyle.FullPath;
            return source;
        }

        private static void assertCommon(DodgeHitObject clone, DodgeHitObject source)
        {
            Assert.That(clone, Is.Not.SameAs(source));
            Assert.That(clone.StartTime, Is.EqualTo(source.StartTime + 250));
            Assert.That(clone.Colour, Is.EqualTo(source.Colour));
            Assert.That(clone.OutlineColour, Is.EqualTo(source.OutlineColour));
            Assert.That(clone.Opacity, Is.EqualTo(source.Opacity));
            Assert.That(clone.OutlineThickness, Is.EqualTo(source.OutlineThickness));
            Assert.That(clone.MovementType, Is.EqualTo(source.MovementType));
            Assert.That(clone.MovementEasing, Is.EqualTo(source.MovementEasing));
            Assert.That(clone.WaveAmplitude, Is.EqualTo(source.WaveAmplitude));
            Assert.That(clone.WaveCycles, Is.EqualTo(source.WaveCycles));
            Assert.That(clone.WavePhase, Is.EqualTo(source.WavePhase));
            Assert.That(clone.TrajectoryGuideStyle, Is.EqualTo(source.TrajectoryGuideStyle));
        }

        private static void assertBullet(DodgeBullet clone, DodgeBullet source)
        {
            assertCommon(clone, source);
            Assert.That(clone.Position, Is.EqualTo(source.Position + new Vector2(5, 7)));
            Assert.That(clone.EndPosition, Is.EqualTo(source.EndPosition + new Vector2(5, 7)));
            Assert.That(clone.Duration, Is.EqualTo(source.Duration));
            Assert.That(clone.Shape, Is.EqualTo(source.Shape));
            Assert.That(clone.ContinueUntilExit, Is.EqualTo(source.ContinueUntilExit));
        }

        private static void assertEmitter(DodgeEmitter clone, DodgeEmitter source)
        {
            assertCommon(clone, source);
            Assert.That(clone.Position, Is.EqualTo(source.Position + new Vector2(5, 7)));
            Assert.That(clone.AimPosition, Is.EqualTo(source.AimPosition + new Vector2(5, 7)));
            Assert.That(clone.MovementEndPosition, Is.EqualTo(source.MovementEndPosition + new Vector2(5, 7)));
            Assert.That(clone.BulletCount, Is.EqualTo(source.BulletCount));
            Assert.That(clone.SpreadAngle, Is.EqualTo(source.SpreadAngle));
            Assert.That(clone.BurstCount, Is.EqualTo(source.BurstCount));
            Assert.That(clone.BurstInterval, Is.EqualTo(source.BurstInterval));
            Assert.That(clone.BurstBeatDivisor, Is.EqualTo(source.BurstBeatDivisor));
            Assert.That(clone.BurstRotation, Is.EqualTo(source.BurstRotation));
        }

        private static void assertBeam(DodgeBeam clone, DodgeBeam source)
        {
            assertCommon(clone, source);
            Assert.That(clone.Position, Is.EqualTo(source.Position + new Vector2(5, 7)));
            Assert.That(clone.EndPosition, Is.EqualTo(source.EndPosition + new Vector2(5, 7)));
            Assert.That(clone.BeamWidth, Is.EqualTo(source.BeamWidth));
        }

        private static void assertArena(DodgeArenaChange clone, DodgeArenaChange source)
        {
            assertCommon(clone, source);
            Assert.That(clone.TargetPosition, Is.EqualTo(source.TargetPosition + new Vector2(5, 7)));
            Assert.That(clone.TargetSize, Is.EqualTo(source.TargetSize));
            Assert.That(clone.TargetRotation, Is.EqualTo(source.TargetRotation));
            Assert.That(clone.KiaiShakeAngle, Is.EqualTo(source.KiaiShakeAngle));
            Assert.That(clone.BorderOpacity, Is.EqualTo(source.BorderOpacity));
            Assert.That(clone.Easing, Is.EqualTo(source.Easing));
        }

        private static void assertCamera(DodgeCameraChange clone, DodgeCameraChange source)
        {
            assertCommon(clone, source);
            Assert.That(clone.Position, Is.EqualTo(source.Position + new Vector2(5, 7)));
            Assert.That(clone.EndPosition, Is.EqualTo(source.EndPosition + new Vector2(5, 7)));
            Assert.That(clone.Continuous, Is.EqualTo(source.Continuous));
            Assert.That(clone.Easing, Is.EqualTo(source.Easing));
        }

        private static void assertTrigger(DodgeTrigger clone, DodgeTrigger source)
        {
            assertCommon(clone, source);
            Assert.That(clone.Position, Is.EqualTo(source.Position + new Vector2(5, 7)));
            Assert.That(clone.Duration, Is.EqualTo(source.Duration));
            Assert.That(clone.Action, Is.EqualTo(source.Action));
            Assert.That(clone.Strength, Is.EqualTo(source.Strength));
        }
    }
}
