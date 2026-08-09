// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;

namespace osu.Game.Tests.Rulesets.Mods
{
    [TestFixture]
    public class ModPresetTest
    {
        [TestCase("not json")]
        [TestCase("null")]
        [TestCase("{}")]
        public void TestInvalidModsJsonReturnsEmptyCollection(string modsJson)
        {
            var preset = new ModPreset
            {
                Name = "Broken preset",
                Ruleset = new OsuRuleset().RulesetInfo,
                ModsJson = modsJson,
            };

            Assert.That(preset.Mods, Is.Empty);
        }

        [Test]
        public void TestInvalidEntriesAreSkipped()
        {
            var preset = new ModPreset
            {
                Name = "Partially broken preset",
                Ruleset = new OsuRuleset().RulesetInfo,
                ModsJson = "[{\"acronym\":\"HR\"},null,{\"acronym\":\"DT\",\"settings\":null}]",
            };

            Assert.That(preset.Mods.Select(mod => mod.Acronym), Is.EqualTo(new[] { "HR" }));
        }
    }
}
