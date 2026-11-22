using System;
using System.Linq;
using System.Globalization;
using System.Resources;
using osu.Game.Localisation;
using NUnit.Framework;
using osu.Game.Rulesets.Mania;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class LocalRankedPlayVisualModsTest
    {
        [TestCase("en", "Local visual mod", "None")]
        [TestCase("ru", "Локальный визуальный мод", "Без мода")]
        [TestCase("es", "Mod visual local", "Ninguno")]
        [TestCase("zh", "本地视觉模组", "无")]
        public void TranslationsAreEmbedded(string culture, string caption, string none)
        {
            var resources = new ResourceManager("osu.Game.Localisation.RankedPlayVisualMods", typeof(RankedPlayVisualModsStrings).Assembly);
            Assert.That(resources.GetString("caption", new CultureInfo(culture)), Is.EqualTo(caption));
            Assert.That(resources.GetString("none", new CultureInfo(culture)), Is.EqualTo(none));
            Assert.That(resources.GetString("hint", new CultureInfo(culture)), Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void AvailableModsFollowRuleset()
        {
            Assert.That(LocalRankedPlayVisualMods.Available(new OsuRuleset()), Is.EquivalentTo(new[] { "None", "HD", "TC" }));
            Assert.That(LocalRankedPlayVisualMods.Available(new ManiaRuleset()), Is.EquivalentTo(new[] { "None", "HD", "CO", "FI" }));
        }

        [TestCase("None")]
        [TestCase("HR")]
        [TestCase("RX")]
        [TestCase("CO")]
        [TestCase("FI")]
        public void RejectsUnsupportedOrNonVisualMod(string acronym)
        {
            Assert.That(new LocalRankedPlayVisualMods(acronym).Resolve(new OsuRuleset(), Array.Empty<Mod>()), Is.Empty);
        }

        [Test]
        public void RejectsDuplicateAndConflictingRequiredMods()
        {
            var preference = new LocalRankedPlayVisualMods("HD");
            Assert.That(preference.Resolve(new OsuRuleset(), new Mod[] { new OsuModHidden() }), Is.Empty);
            Assert.That(preference.Resolve(new OsuRuleset(), new Mod[] { new OsuModTraceable() }), Is.Empty);
            Assert.That(preference.Resolve(new OsuRuleset(), new Mod[] { new OsuModHardRock() }), Has.Length.EqualTo(1));
        }

        [Test]
        public void ResolvesFreshDefaultInstancesWithoutMutatingRequiredMods()
        {
            var preference = new LocalRankedPlayVisualMods("HD");
            Mod[] required = { new OsuModRelax() };
            var first = preference.Resolve(new OsuRuleset(), required);
            var second = preference.Resolve(new OsuRuleset(), required);
            Assert.That(first.Single(), Is.TypeOf<OsuModHidden>());
            Assert.That(first.Single(), Is.Not.SameAs(second.Single()));
            Assert.That(required.Single(), Is.TypeOf<OsuModRelax>());
            Assert.That(((OsuModHidden)first.Single()).OnlyFadeApproachCircles.Value, Is.False);
        }
    }
}
