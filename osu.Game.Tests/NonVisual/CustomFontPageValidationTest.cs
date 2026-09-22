// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Framework.Testing;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class CustomFontPageValidationTest
    {
        [Test]
        public void TestMissingPageIsRejected()
        {
            using var storage = new TemporaryNativeStorage($"custom-font-missing-{Guid.NewGuid():N}");

            Assert.That(OsuGameBase.HasCompleteCustomFontPages(storage, "Quicksand.fnt", 1, out string missingPage), Is.False);
            Assert.That(missingPage, Is.EqualTo("Quicksand.fnt_0.png"));
        }

        [Test]
        public void TestEmptyPageIsRejected()
        {
            using var storage = new TemporaryNativeStorage($"custom-font-empty-{Guid.NewGuid():N}");
            using (storage.CreateFileSafely("Quicksand.fnt_0.png"))
            {
            }

            Assert.That(OsuGameBase.HasCompleteCustomFontPages(storage, "Quicksand.fnt", 1, out string missingPage), Is.False);
            Assert.That(missingPage, Is.EqualTo("Quicksand.fnt_0.png"));
        }

        [Test]
        public void TestAllPagesAreAcceptedUsingFrameworkNaming()
        {
            using var storage = new TemporaryNativeStorage($"custom-font-complete-{Guid.NewGuid():N}");

            for (int page = 0; page < 12; page++)
            {
                using var stream = storage.CreateFileSafely($"Example.fnt_{page:00}.png");
                stream.WriteByte(1);
            }

            Assert.That(OsuGameBase.HasCompleteCustomFontPages(storage, "Example.fnt", 12, out string missingPage), Is.True);
            Assert.That(missingPage, Is.Empty);
        }
    }
}
