// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Dodge.Edit;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeSavedPatternPreviewLayoutTest
    {
        [Test]
        public void TestRingEmitterProducesCentredSourceAndProjectileMarkers()
        {
            var emitter = new DodgeEmitter
            {
                Position = new Vector2(256, 192),
                AimPosition = new Vector2(376, 192),
                MovementEndPosition = new Vector2(256, 192),
                BulletCount = 8,
                SpreadAngle = 360,
            };

            var markers = DodgeSavedPatternPreviewLayout.Build(new DodgeHitObject[] { emitter });

            Assert.That(markers.Count(marker => marker.Role == DodgeSavedPatternMarkerRole.Emitter), Is.EqualTo(1));
            Assert.That(markers.Count(marker => marker.Role == DodgeSavedPatternMarkerRole.Projectile), Is.EqualTo(8));
            Assert.That(markers.Single(marker => marker.Role == DodgeSavedPatternMarkerRole.Emitter).Start.X, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(markers.Single(marker => marker.Role == DodgeSavedPatternMarkerRole.Emitter).Start.Y, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void TestMixedPatternSupportsEveryObjectType()
        {
            DodgeHitObject[] objects =
            {
                new DodgeBullet { Position = Vector2.Zero, EndPosition = new Vector2(100) },
                new DodgeEmitter { Position = new Vector2(50), AimPosition = new Vector2(100, 50), MovementEndPosition = new Vector2(50), BulletCount = 2 },
                new DodgeBeam { Position = new Vector2(0, 100), EndPosition = new Vector2(100, 100) },
                new DodgeArenaChange { TargetPosition = Vector2.Zero, TargetSize = new Vector2(100) },
                new DodgeCameraChange { Position = Vector2.Zero, EndPosition = new Vector2(25) },
            };

            var markers = DodgeSavedPatternPreviewLayout.Build(objects);

            Assert.That(markers, Is.Not.Empty);
            Assert.That(markers, Has.Count.LessThanOrEqualTo(DodgeSavedPatternPreviewLayout.MAX_MARKERS));
            Assert.That(markers.All(marker => marker.Start.X is >= 0.1f and <= 0.9f && marker.Start.Y is >= 0.1f and <= 0.9f), Is.True);
            Assert.That(markers.Select(marker => marker.Role), Does.Contain(DodgeSavedPatternMarkerRole.Projectile));
            Assert.That(markers.Select(marker => marker.Role), Does.Contain(DodgeSavedPatternMarkerRole.Emitter));
            Assert.That(markers.Select(marker => marker.Role), Does.Contain(DodgeSavedPatternMarkerRole.Structure));
        }

        [Test]
        public void TestNormalisationPreservesEqualWorldDistances()
        {
            DodgeHitObject[] objects =
            {
                new DodgeBullet { Position = Vector2.Zero, EndPosition = new Vector2(100, 0) },
                new DodgeBullet { Position = Vector2.Zero, EndPosition = new Vector2(0, 100) },
            };

            var markers = DodgeSavedPatternPreviewLayout.Build(objects);
            float horizontalDistance = (markers[0].End - markers[0].Start).Length;
            float verticalDistance = (markers[1].End - markers[1].Start).Length;

            Assert.That(horizontalDistance, Is.EqualTo(verticalDistance).Within(0.001f));
        }
    }
}
