// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.UI.Cursor;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class OsuCircleSizeScaleTest
    {
        [Test]
        public void TestOutOfRangeCircleSizeScaleStaysPositive()
        {
            float scale = LegacyRulesetExtensions.CalculateScaleFromCircleSize(18, true);

            Assert.That(scale, Is.GreaterThan(0));
        }

        [Test]
        public void TestOsuHitObjectUsesSignedScaleButKeepsPositiveRadius()
        {
            var hitCircle = new HitCircle();
            hitCircle.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty { CircleSize = 18 });

            Assert.Multiple(() =>
            {
                Assert.That(hitCircle.Scale, Is.LessThan(0));
                Assert.That(hitCircle.Radius, Is.GreaterThan(0));
            });
        }

        [Test]
        public void TestSignedScaleSanitizationPreservesNegativeSign()
        {
            float signedScale = LegacyRulesetExtensions.SanitizeSignedScale(LegacyRulesetExtensions.CalculateSignedScaleFromCircleSize(18, true));

            Assert.Multiple(() =>
            {
                Assert.That(signedScale, Is.LessThan(0));
                Assert.That(MathF.Abs(signedScale), Is.GreaterThanOrEqualTo(0.01f));
            });
        }

        [Test]
        public void TestOutOfRangeCursorScaleStaysPositive()
        {
            float scale = OsuCursor.GetScaleForCircleSize(18);

            Assert.That(scale, Is.GreaterThan(0));
        }
    }
}
