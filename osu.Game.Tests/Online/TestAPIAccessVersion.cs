// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online.API;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    public class TestAPIAccessVersion
    {
        [TestCase("2026.816.0-gay-edition", 20260816)]
        [TestCase("2026.711.0-lazer", 20260711)]
        [TestCase("2026.1225.0-lazer", 20261225)]
        [TestCase("b20260412", 20260412)]
        public void TestProfileClientVersionIsConvertedToApiVersion(string clientVersion, int expected)
        {
            Assert.That(APIAccess.TryParseClientVersion(clientVersion, out int actual), Is.True);
            Assert.That(actual, Is.EqualTo(expected));
        }

        [TestCase("")]
        [TestCase("local debug")]
        [TestCase("2026.1332.0-lazer")]
        public void TestInvalidProfileClientVersionIsRejected(string clientVersion)
        {
            Assert.That(APIAccess.TryParseClientVersion(clientVersion, out _), Is.False);
        }
    }
}
