// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Catch;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Taiko;

namespace osu.Game.Tests.Rulesets.Mods
{
    [TestFixture]
    public class ModMosuRateModsTest
    {
        private static IEnumerable<Ruleset> rulesets()
        {
            yield return new OsuRuleset();
            yield return new TaikoRuleset();
            yield return new CatchRuleset();
            yield return new ManiaRuleset();
        }

        [TestCaseSource(nameof(rulesets))]
        public void TestStaticBpmAndTargetDifficultyAvailable(Ruleset ruleset)
        {
            Mod[] mods = ruleset.GetModsFor(ModType.Mosu).ToArray();

            Assert.Multiple(() =>
            {
                Assert.That(mods, Has.Exactly(1).InstanceOf<ModMosuStaticBpm>());
                Assert.That(mods, Has.Exactly(1).InstanceOf<ModMosuTargetDifficulty>());
                Assert.That(mods, Has.Exactly(1).InstanceOf<ModAudioEffects>());
            });
        }
    }
}
