// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LeaderboardScoreContextMenuTest
    {
        [Test]
        public void TestExternalModReceiverProvidesUseModsAction()
        {
            IReadOnlyList<Mod>? appliedMods = null;

            var score = new LeaderboardScore(new ScoreInfo
            {
                Mods = new Mod[] { new OsuModHidden(), new ModScoreV2() },
            }, 1)
            {
                ApplyModsFromScore = mods => appliedMods = mods,
            };

            var useModsItem = score.ContextMenuItems.Single();
            useModsItem.Action.Value!.Invoke();

            Assert.That(appliedMods, Has.One.InstanceOf<OsuModHidden>());
            Assert.That(appliedMods, Has.None.InstanceOf<ModScoreV2>());
        }
    }
}
