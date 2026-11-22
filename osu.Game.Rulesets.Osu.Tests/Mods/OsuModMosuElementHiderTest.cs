using System;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    public class OsuModMosuElementHiderTest
    {
        [Test]
        public void TestRegisteredInMorasooma()
        {
            var mod = new OsuRuleset().CreateModFromAcronym("EH");
            Assert.That(mod, Is.TypeOf<OsuModMosuElementHider>());
            Assert.That(mod!.Type, Is.EqualTo(ModType.Mosu));
        }

        [Test]
        public void TestSkinEditorRemainsRankedWithCustomSettings()
        {
            var mod = new OsuModMosuElementHider();
            Assert.That(mod.Ranked, Is.True);
            mod.HideNumbers.Value = true;
            mod.HideFollowPoints.Value = true;
            mod.HideSliderTicks.Value = true;
            mod.HideReverseArrows.Value = false;
            Assert.That(mod.Ranked, Is.True);
        }

        [Test]
        public void TestDefaultRestrictions()
        {
            var mod = new OsuModMosuElementHider();
            Assert.That(mod.Hides(OsuSetting.ForkShowSliderBall), Is.True);
            Assert.That(mod.Hides(OsuSetting.ForkShowSliderFollowCircle), Is.True);
            Assert.That(mod.Hides(OsuSetting.ForkShowReverseArrows), Is.True);
            Assert.That(mod.Hides(OsuSetting.ForkShowSpinners), Is.False);
            Assert.That(mod.Hides(OsuSetting.ForkShowHitCircleNumbers), Is.False);
            Assert.That(mod.Hides(OsuSetting.ForkShowFollowPoints), Is.False);
            Assert.That(mod.Hides(OsuSetting.ForkShowSliderTicks), Is.False);
            Assert.That(mod.Hides(OsuSetting.HitLighting), Is.False);
        }

        [Test]
        public void TestSettingsRoundTrip()
        {
            var mod = new OsuModMosuElementHider();
            mod.HideNumbers.Value = true;
            mod.HideFollowPoints.Value = true;
            mod.HideSliderBall.Value = false;
            mod.HideSliderFollowCircle.Value = false;
            mod.HideReverseArrows.Value = false;
            mod.HideSliderTicks.Value = true;
            mod.HideSpinners.Value = true;

            var apiMod = JsonConvert.DeserializeObject<APIMod>(JsonConvert.SerializeObject(new APIMod(mod)))!;
            var restored = (OsuModMosuElementHider)apiMod.ToMod(new OsuRuleset());
            foreach (OsuSetting setting in Enum.GetValues<OsuSetting>())
                Assert.That(restored.Hides(setting), Is.EqualTo(mod.Hides(setting)), setting.ToString());
        }
    }
}
