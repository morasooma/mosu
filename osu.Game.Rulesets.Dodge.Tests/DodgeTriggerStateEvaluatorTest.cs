// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Rulesets.Dodge;
using osu.Game.Rulesets.Dodge.Objects;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeTriggerStateEvaluatorTest
    {
        [Test]
        public void TestHudVisibilityFollowsLatestTrigger()
        {
            var evaluator = new DodgeTriggerStateEvaluator(new[]
            {
                new DodgeTrigger { StartTime = 1000, Action = DodgeTriggerAction.HideHud },
                new DodgeTrigger { StartTime = 2000, Action = DodgeTriggerAction.ShowHud },
            });

            Assert.That(evaluator.HudVisibleAt(500), Is.Null);
            Assert.That(evaluator.HudVisibleAt(1000), Is.False);
            Assert.That(evaluator.HudVisibleAt(1999), Is.False);
            Assert.That(evaluator.HudVisibleAt(2000), Is.True);
        }

        [Test]
        public void TestHudToggleUsesVisibleAsDefaultAndSupportsRewind()
        {
            var evaluator = new DodgeTriggerStateEvaluator(new[]
            {
                new DodgeTrigger { StartTime = 1000, Action = DodgeTriggerAction.ToggleHud },
                new DodgeTrigger { StartTime = 2000, Action = DodgeTriggerAction.ToggleHud },
            });

            Assert.That(evaluator.HudVisibleAt(999), Is.Null);
            Assert.That(evaluator.HudVisibleAt(1000), Is.False);
            Assert.That(evaluator.HudVisibleAt(2000), Is.True);
            Assert.That(evaluator.HudVisibleAt(1000), Is.False);
        }

        [Test]
        public void TestTrailToggle()
        {
            var evaluator = new DodgeTriggerStateEvaluator(new[]
            {
                new DodgeTrigger { StartTime = 500, Action = DodgeTriggerAction.TrailDisable },
                new DodgeTrigger { StartTime = 1500, Action = DodgeTriggerAction.TrailEnable },
            });

            Assert.That(evaluator.TrailEnabledAt(400), Is.Null);
            Assert.That(evaluator.TrailEnabledAt(600), Is.False);
            Assert.That(evaluator.TrailEnabledAt(1500), Is.True);
        }

        [Test]
        public void TestClearBulletsDeterminism()
        {
            var evaluator = new DodgeTriggerStateEvaluator(new[]
            {
                new DodgeTrigger { StartTime = 1000, Action = DodgeTriggerAction.ClearBullets },
            });

            // Projectiles which started before the clear are removed at/after it.
            Assert.That(evaluator.ProjectileExists(projectileStartTime: 900, currentTime: 999), Is.True);
            Assert.That(evaluator.ProjectileExists(projectileStartTime: 900, currentTime: 1000), Is.False);
            // Projectiles spawned after the clear remain on the field.
            Assert.That(evaluator.ProjectileExists(projectileStartTime: 1100, currentTime: 1100), Is.True);
        }

        [Test]
        public void TestTimedEffectsBoundaries()
        {
            var evaluator = new DodgeTriggerStateEvaluator(new[]
            {
                new DodgeTrigger { StartTime = 1000, Duration = 400, Strength = 1f, Action = DodgeTriggerAction.ScreenShake },
            });

            Assert.That(evaluator.ScreenShakeAt(999), Is.Null);
            Assert.That(evaluator.ScreenShakeAt(1000), Is.Not.Null);
            Assert.That(evaluator.ScreenShakeAt(1400), Is.Not.Null);
            Assert.That(evaluator.ScreenShakeAt(1401), Is.Null);
        }

        [Test]
        public void TestRewindProducesIdenticalState()
        {
            var evaluator = new DodgeTriggerStateEvaluator(new[]
            {
                new DodgeTrigger { StartTime = 800, Action = DodgeTriggerAction.HideHud },
                new DodgeTrigger { StartTime = 1200, Action = DodgeTriggerAction.ClearBullets },
            });

            // Forward pass.
            bool? hudAt1500 = evaluator.HudVisibleAt(1500);
            bool existsAt1500 = evaluator.ProjectileExists(1000, 1500);

            // Seek back to 900 then re-evaluate the same target time: pure function of time.
            Assert.That(evaluator.HudVisibleAt(900), Is.False);
            Assert.That(evaluator.ProjectileExists(1000, 900), Is.True);

            Assert.That(evaluator.HudVisibleAt(1500), Is.EqualTo(hudAt1500));
            Assert.That(evaluator.ProjectileExists(1000, 1500), Is.EqualTo(existsAt1500));
        }
    }
}
