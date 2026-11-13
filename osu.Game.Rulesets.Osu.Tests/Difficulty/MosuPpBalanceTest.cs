// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.Difficulty.Relax.MosuPpRelax;

namespace osu.Game.Rulesets.Osu.Tests.Difficulty
{
    [TestFixture]
    public class MosuPpBalanceTest
    {
        [Test]
        public void TestApprovedReleaseDefaults()
        {
            var b = MosuPpBalance.Current;
            Assert.Multiple(() =>
            {
                Assert.That(MosuPpBalance.ReleaseVersion, Is.EqualTo("2.0.0"));
                Assert.That(b.GlobalScale, Is.EqualTo(0.86673));
                Assert.That(b.OwnerRules && b.UseHitAreaDistances && b.UsePhysicalEquivalence, Is.True);
                Assert.That(b.PhysicalChallengeStrength, Is.EqualTo(1));
                Assert.That(b.LongMapNerf, Is.Zero);
                Assert.That(b.HighArReading, Is.False);
                Assert.That(b.StreamNerf, Is.Zero);
            });
        }

        [Test]
        public void TestClientCannotLoadLabOverrides()
        {
            Assert.Multiple(() =>
            {
                Assert.That(typeof(MosuPpBalance).GetMethod("ConfigureForLab"), Is.Null);
                Assert.That(typeof(MosuPpBalance).GetProperty(nameof(MosuPpBalance.Current))!.GetSetMethod(true), Is.Null);
            });
        }
    }
}
