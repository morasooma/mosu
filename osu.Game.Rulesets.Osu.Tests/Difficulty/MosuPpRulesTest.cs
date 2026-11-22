using System;
using NUnit.Framework;
using MosuPpRxCs;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class MosuPpRulesTest
    {
        [Test]
        public void HeavyMisses()
        {
            Assert.That(RxHeavyMissPenalty.Multiplier(15), Is.EqualTo(1));
            Assert.That(RxHeavyMissPenalty.Multiplier(25), Is.EqualTo(Math.Exp(-1.1)).Within(0.000001));
        }

        [Test]
        public void CircleSizeKnots()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RxCsPpBuff.Multiplier(3.5), Is.EqualTo(1));
                Assert.That(RxCsPpBuff.Multiplier(4), Is.EqualTo(1.05).Within(0.0001));
                Assert.That(RxCsPpBuff.Multiplier(8), Is.EqualTo(1.5));
                Assert.That(RxCsPpBuff.Multiplier(10), Is.EqualTo(3));
            });
        }

        [Test]
        public void OnePointGuardCutsStackedMapButNotMovingMap()
        {
            RxBeatmap stacked = makeMap(i => new RxVec2(256, 192));
            RxBeatmap moving = makeMap(i => new RxVec2(40 + i % 16 * 25, 80 + i / 16 * 25));
            Assert.That(RxOnePointMapGuard.Multiplier(stacked), Is.LessThan(0.04));
            Assert.That(RxOnePointMapGuard.Multiplier(moving), Is.EqualTo(1));
            Assert.That(RxPointVarietyNerf.Multiplier(stacked), Is.LessThan(0.2));
            Assert.That(RxPointVarietyNerf.Multiplier(moving), Is.EqualTo(1));
        }

        [Test]
        public void StreamShareRequiresShortSpacingAndSixNotes()
        {
            var stream = makeMap(i => new RxVec2(20 + i * 6, 100));
            var jumps = makeMap(i => new RxVec2(i % 2 == 0 ? 0 : 500, 100));
            Assert.That(RxStreamMapNerf.StreamShare(stream), Is.EqualTo(1));
            Assert.That(RxStreamMapNerf.Multiplier(1.3, 1), Is.EqualTo(0.75));
            Assert.That(RxStreamMapNerf.Multiplier(1.3, 0), Is.EqualTo(1));
            Assert.That(RxStreamMapNerf.StreamShare(jumps), Is.Zero);
        }

        [Test]
        public void ExtremeJumpCountIgnoresSlowTransitions()
        {
            var fast = new RxBeatmap();
            var slow = new RxBeatmap();
            for (int i = 0; i < 200; i++)
            {
                var position = new RxVec2(i % 2 == 0 ? 0 : 300, 100);
                fast.HitObjects.Add(new RxHitObject { Kind = RxHitObjectKind.Circle, StartTime = i * 50, Position = position });
                slow.HitObjects.Add(new RxHitObject { Kind = RxHitObjectKind.Circle, StartTime = i * 100, Position = position });
            }
            Assert.That(RxExtremeJumpNerf.ExtremeJumpCount(fast), Is.EqualTo(199));
            Assert.That(RxExtremeJumpNerf.ExtremeJumpCount(slow), Is.Zero);
        }

        [Test]
        public void ShortHighCsNerfIsLimitedToShortMaps()
        {
            var map = makeMap(i => new RxVec2(100, 100));
            map.CircleSize = 6;
            Assert.That(RxShortHighCsNerf.Multiplier(map), Is.EqualTo(0.85));
            for (int i = 64; i < 700; i++)
                map.HitObjects.Add(new RxHitObject { Kind = RxHitObjectKind.Circle, StartTime = i * 100, Position = new RxVec2(100, 100) });
            Assert.That(RxShortHighCsNerf.Multiplier(map), Is.EqualTo(1));
        }

        private static RxBeatmap makeMap(Func<int, RxVec2> position)
        {
            var map = new RxBeatmap();
            for (int i = 0; i < 64; i++)
                map.HitObjects.Add(new RxHitObject { Kind = RxHitObjectKind.Circle, StartTime = i * 100, Position = position(i) });
            return map;
        }
    }
}
