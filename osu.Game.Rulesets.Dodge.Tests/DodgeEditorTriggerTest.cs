// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Dodge.Edit;
using osu.Game.Rulesets.Dodge.Objects;

namespace osu.Game.Rulesets.Dodge.Tests
{
    [TestFixture]
    public class DodgeEditorTriggerTest
    {
        [Test]
        public void TestTriggerHasDedicatedTimelineLane()
        {
            var composer = new DodgeHitObjectComposer(new DodgeRuleset());
            var instant = new DodgeTrigger { StartTime = 1000, Duration = 800, Action = DodgeTriggerAction.ClearBullets };
            var timed = new DodgeTrigger { StartTime = 1000, Duration = 800, Action = DodgeTriggerAction.ScreenShake };

            Assert.Multiple(() =>
            {
                Assert.That(composer.TimelineLaneCount, Is.EqualTo(6));
                Assert.That(composer.GetTimelineLane(instant).Index, Is.EqualTo(5));
                Assert.That(composer.GetTimelineDisplayEndTime(instant), Is.EqualTo(1000));
                Assert.That(composer.GetTimelineDisplayEndTime(timed), Is.EqualTo(1800));
            });
        }
    }
}
