using System;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    public class OsuModMosuAimAssistTest
    {
        [Test]
        public void TestDefaultAndOldSettingsStillUseMosu()
        {
            var mod = new OsuModMosuAimAssist();
            Assert.That(mod.Algorithm.Value, Is.EqualTo(MosuAimAssistAlgorithm.Mosu));
            Assert.That(mod.Strength.Value, Is.EqualTo(0.5f));
            Assert.That(mod.UsesDefaultConfiguration, Is.True);
            Assert.That(mod.ShowRealCursor.Value, Is.False);
            var old = JsonConvert.DeserializeObject<APIMod>("{\"acronym\":\"AA\",\"settings\":{\"strength\":0.3,\"show_debug\":true}}")!;
            var restored = (OsuModMosuAimAssist)old.ToMod(new OsuRuleset());
            Assert.That(restored.Algorithm.Value, Is.EqualTo(MosuAimAssistAlgorithm.Mosu));
            Assert.That(restored.Strength.Value, Is.EqualTo(0.3f));
            Assert.That(restored.ShowDebug.Value, Is.True);
        }

        [TestCase(MosuAimAssistAlgorithm.Mosu)]
        [TestCase(MosuAimAssistAlgorithm.MapleV2)]
        [TestCase(MosuAimAssistAlgorithm.MapleV3)]
        public void TestRoundTripAndClonePreserveAllSettings(MosuAimAssistAlgorithm algorithm)
        {
            var mod = new OsuModMosuAimAssist
            {
                Algorithm = { Value = algorithm },
                Strength = { Value = 0.32f },
                MapleV2Power = { Value = 0.73f },
                MapleV2AssistOnSliders = { Value = true },
                MapleV3Power = { Value = 1.41f },
                MapleV3SliderPower = { Value = 0.23f },
                ShowRealCursor = { Value = true },
                ShowDebug = { Value = true },
            };
            var api = JsonConvert.DeserializeObject<APIMod>(JsonConvert.SerializeObject(new APIMod(mod)))!;
            var restored = (OsuModMosuAimAssist)api.ToMod(new OsuRuleset());
            Assert.That(restored, Is.EqualTo(mod));
            Assert.That(mod.DeepClone(), Is.EqualTo(mod));
            Assert.That(restored.GetSettingVisibility(nameof(mod.Strength))!.Value, Is.EqualTo(algorithm == MosuAimAssistAlgorithm.Mosu));
            Assert.That(restored.GetSettingVisibility(nameof(mod.MapleV2Power))!.Value, Is.EqualTo(algorithm == MosuAimAssistAlgorithm.MapleV2));
            Assert.That(restored.GetSettingVisibility(nameof(mod.MapleV3Power))!.Value, Is.EqualTo(algorithm == MosuAimAssistAlgorithm.MapleV3));
            restored.ResetSettingsToDefaults();
            Assert.That(restored.UsesDefaultConfiguration, Is.True);
            Assert.That(restored.Algorithm.Value, Is.EqualTo(MosuAimAssistAlgorithm.Mosu));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestGeneratedControlsSwitchVisibilityWithoutLosingValues(bool exactInput)
        {
            var mod = new OsuModMosuAimAssist();
            var controls = mod.CreateSettingsControls(exactInput).ToArray();
            try
            {
                int numberControls = exactInput ? 2 : 1;
                Assert.That(controls.Length, Is.EqualTo(exactInput ? 12 : 8));
                Assert.That(controls[0].Alpha, Is.EqualTo(1)); // algorithm
                Assert.That(controls.Skip(1).Take(numberControls).All(c => c.Alpha == 1), Is.True); // Mosu
                Assert.That(controls.Skip(1 + numberControls).Take(controls.Length - 3 - numberControls).All(c => c.Alpha == 0), Is.True);
                mod.MapleV2Power.Value = 0.72f;
                mod.Algorithm.Value = MosuAimAssistAlgorithm.MapleV2;
                Assert.That(controls.Skip(1).Take(numberControls).All(c => c.Alpha == 0), Is.True);
                Assert.That(controls.Skip(1 + numberControls).Take(numberControls + 1).All(c => c.Alpha == 1), Is.True);
                mod.Algorithm.Value = MosuAimAssistAlgorithm.MapleV3;
                Assert.That(controls.Skip(1 + numberControls).Take(numberControls + 1).All(c => c.Alpha == 0), Is.True);
                Assert.That(controls.Skip(2 + numberControls * 2).Take(numberControls * 2).All(c => c.Alpha == 1), Is.True);
                Assert.That(controls[^2].Alpha, Is.EqualTo(1)); // shared real cursor
                Assert.That(controls[^1].Alpha, Is.EqualTo(1)); // shared debug
                mod.Algorithm.Value = MosuAimAssistAlgorithm.Mosu;
                Assert.That(mod.MapleV2Power.Value, Is.EqualTo(0.72f));
                Assert.That(mod.Strength.Value, Is.EqualTo(0.5f));
            }
            finally
            {
                foreach (var control in controls)
                    control.Dispose();
            }
        }

        [TestCase(MosuAimAssistAlgorithm.MapleV2)]
        [TestCase(MosuAimAssistAlgorithm.MapleV3)]
        public void TestMaplePenaltyDoesNotDependOnHiddenMosuStrength(MosuAimAssistAlgorithm algorithm)
        {
            var mod = new OsuModMosuAimAssist { Algorithm = { Value = algorithm }, Strength = { Value = 0 } };
            Assert.That(mod.PerformancePenaltyStrength, Is.EqualTo(0.5f));
            mod.Strength.Value = 0.7f;
            Assert.That(mod.PerformancePenaltyStrength, Is.EqualTo(0.5f));
        }

        [TestCase("en", "Algorithm", "Assist on sliders")]
        [TestCase("ru", "Алгоритм", "Помощь на слайдерах")]
        [TestCase("es", "Algoritmo", "Asistencia en sliders")]
        [TestCase("zh", "算法", "滑条辅助")]
        [TestCase("ko", "알고리즘", "슬라이더 보정")]
        public void TestLocalisationThroughRuntime(string locale, string algorithm, string sliders)
        {
            using var config = new TestConfig();
            using var manager = new LocalisationManager(config);
            manager.AddLanguage(locale, new ResourceManagerLocalisationStore(locale));
            config.SetValue(FrameworkSetting.Locale, locale);
            Assert.That(manager.GetLocalisedString(AimAssistAlgorithmStrings.Algorithm), Is.EqualTo(algorithm));
            Assert.That(manager.GetLocalisedString(AimAssistAlgorithmStrings.Sliders), Is.EqualTo(sliders));
            foreach (var property in typeof(AimAssistAlgorithmStrings).GetProperties())
            {
                var text = (LocalisableString)property.GetValue(null)!;
                Assert.That(manager.GetLocalisedString(text), Is.Not.Empty);
                if (locale != "en")
                    Assert.That(manager.GetLocalisedString(text), Is.Not.EqualTo(text.ToString()), property.Name);
            }
        }

        private sealed class TestConfig : FrameworkConfigManager
        {
            protected override string Filename => null!;
            public TestConfig() : base(null!) { }
        }
    }
}
