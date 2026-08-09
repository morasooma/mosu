// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using NUnit.Framework;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Scoring.Render;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public partial class ReplayRenderGameTest
    {
        [Test]
        public void TestReplayRenderGameUsesOfflineDummyApi()
        {
            var game = new TestReplayRenderGame();
            IAPIProvider api = game.CreateRenderApiProvider(new EndpointConfiguration());

            Assert.Multiple(() =>
            {
                Assert.That(api, Is.TypeOf<DummyAPIAccess>());
                Assert.That(((DummyAPIAccess)api).State.Value, Is.EqualTo(APIState.Offline));
                Assert.That(game.OfficialBeatmapIntegrationEnabled, Is.False);
            });
        }

        private sealed partial class TestReplayRenderGame : ReplayRenderGame
        {
            public TestReplayRenderGame()
                : base(Array.Empty<string>())
            {
            }

            public IAPIProvider CreateRenderApiProvider(EndpointConfiguration endpoints) => base.CreateAPIProvider(endpoints);
            public bool OfficialBeatmapIntegrationEnabled => EnableOfficialBeatmapIntegration;
        }
    }
}
