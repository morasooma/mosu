// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Skinning;

namespace osu.Game.Tests.Skins
{
    [TestFixture]
    public class MosuSkinSettingsTest
    {
        [Test]
        public void TestDefaultScale()
        {
            Assert.That(new MosuSkinSettings().ManiaNoteScalePercent.Value, Is.EqualTo(100));
        }

        [TestCase(0, 20)]
        [TestCase(20, 20)]
        [TestCase(75, 75)]
        [TestCase(100, 100)]
        [TestCase(250, 100)]
        public void TestScaleIsClamped(int value, int expected)
        {
            var settings = new MosuSkinSettings();
            settings.SetManiaNoteScalePercent(value);

            Assert.That(settings.ManiaNoteScalePercent.Value, Is.EqualTo(expected));
        }
    }
}
