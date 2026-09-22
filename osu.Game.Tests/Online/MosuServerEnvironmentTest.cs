// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Online;

namespace osu.Game.Tests.Online
{
    [TestFixture]
    [NonParallelizable]
    public class MosuServerEnvironmentTest
    {
        [Test]
        public void TestBreakSkippingIsLazerOnly()
        {
            bool previousThirdParty = MosuServerEnvironment.IsThirdPartyServer;
            bool previousStable = MosuServerEnvironment.UsesStableProtocol;

            try
            {
                MosuServerEnvironment.IsThirdPartyServer = false;
                MosuServerEnvironment.UsesStableProtocol = false;
                Assert.That(MosuServerEnvironment.SupportsBreakSkipping, Is.True);

                MosuServerEnvironment.IsThirdPartyServer = true;
                Assert.That(MosuServerEnvironment.SupportsBreakSkipping, Is.True);

                MosuServerEnvironment.IsThirdPartyServer = false;
                MosuServerEnvironment.UsesStableProtocol = true;
                Assert.That(MosuServerEnvironment.SupportsBreakSkipping, Is.False);
            }
            finally
            {
                MosuServerEnvironment.IsThirdPartyServer = previousThirdParty;
                MosuServerEnvironment.UsesStableProtocol = previousStable;
            }
        }
    }
}
