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
