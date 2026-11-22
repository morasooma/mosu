// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

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
    public class OsuModMosuRelaxTest
    {
        [Test]
        public void TestOldConfigurationAndDefaultKeepMosu()
        {
            var mod = new OsuModMosuRelax();
            Assert.That(mod.Algorithm.Value, Is.EqualTo(MosuRelaxAlgorithm.Mosu));
            var old = JsonConvert.DeserializeObject<APIMod>("{\"acronym\":\"MRX\",\"settings\":{\"base_offset\":12,\"timing_variance\":3}}")!;
            var restored = (OsuModMosuRelax)old.ToMod(new OsuRuleset());
            Assert.That(restored.Algorithm.Value, Is.EqualTo(MosuRelaxAlgorithm.Mosu));
            Assert.That(restored.BaseOffset.Value, Is.EqualTo(12));
            Assert.That(restored.TimingVariance.Value, Is.EqualTo(3));
        }

        [TestCase(MosuRelaxAlgorithm.Mosu)]
        [TestCase(MosuRelaxAlgorithm.Maple)]
        public void TestRoundTripCloneAndReset(MosuRelaxAlgorithm algorithm)
        {
            var mod = new OsuModMosuRelax
            {
                Algorithm = { Value = algorithm },
                TimingVariance = { Value = 6 },
                MapleTargetUR = { Value = 137 },
                MapleOffset = { Value = 12 },
                MapleCircleHold = { Value = 60 },
                MapleCircleHoldError = { Value = 11 },
                MapleSliderHold = { Value = 53 },
                MapleSliderHoldError = { Value = 7 },
                MapleAlternateBPM = { Value = 150 },
                MapleWaitLate = { Value = false },
                MapleDirectionPrediction = { Value = false },
                MaplePredictionAngle = { Value = 60 },
                MaplePredictionScale = { Value = 0.5f },
                MapleLowestHoldTimes = { Value = true },
                MapleSliderAlternationOverride = { Value = true },
                MaplePrimaryButton = { Value = MapleRelaxButton.Right },
                MapleSecondaryButton = { Value = MapleRelaxButton.Left },
            };
            var restored = (OsuModMosuRelax)JsonConvert.DeserializeObject<APIMod>(JsonConvert.SerializeObject(new APIMod(mod)))!.ToMod(new OsuRuleset());
            Assert.That(restored, Is.EqualTo(mod));
            Assert.That(mod.DeepClone(), Is.EqualTo(mod));
            Assert.That(restored.GetSettingVisibility(nameof(mod.TimingVariance))!.Value, Is.EqualTo(algorithm == MosuRelaxAlgorithm.Mosu));
            Assert.That(restored.GetSettingVisibility(nameof(mod.MapleTargetUR))!.Value, Is.EqualTo(algorithm == MosuRelaxAlgorithm.Maple));
            Assert.That(restored.Ranked, Is.False);
            restored.ResetSettingsToDefaults();
            Assert.That(restored.Algorithm.Value, Is.EqualTo(MosuRelaxAlgorithm.Mosu));
            Assert.That(restored.MapleTargetUR.Value, Is.EqualTo(100));
        }

        [Test]
        public void TestAlgorithmAndMapleSettingsDoNotChangeMosuPreset()
        {
            var mod = new OsuModMosuRelax();
            mod.Algorithm.Value = MosuRelaxAlgorithm.Maple;
            mod.MapleTargetUR.Value = 180;
            Assert.That(mod.Preset.Value, Is.EqualTo(MosuRelaxPreset.Reliable));
            Assert.That(mod.TimingVariance.Value, Is.EqualTo(8));
            mod.Algorithm.Value = MosuRelaxAlgorithm.Mosu;
            Assert.That(mod.MapleTargetUR.Value, Is.EqualTo(180));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSettingsControlsSwitchWithoutLosingValues(bool numericInput)
        {
            var mod = new OsuModMosuRelax();
            var controls = mod.CreateSettingsControls(numericInput).ToArray();
            try
            {
                int mosuVisible = controls.Count(c => c.Alpha == 1);
                Assert.That(controls[0], Is.InstanceOf<osu.Game.Overlays.Settings.SettingsEnumDropdown<MosuRelaxAlgorithm>>());
                mod.Algorithm.Value = MosuRelaxAlgorithm.Maple;
                Assert.That(controls[0].Alpha, Is.EqualTo(1));
                Assert.That(mod.GetSettingVisibility(nameof(mod.Preset))!.Value, Is.False);
                Assert.That(mod.GetSettingVisibility(nameof(mod.MapleCircleHold))!.Value, Is.True);
                mod.MapleCircleHold.Value = 55;
                mod.Algorithm.Value = MosuRelaxAlgorithm.Mosu;
                Assert.That(controls.Count(c => c.Alpha == 1), Is.EqualTo(mosuVisible));
                Assert.That(mod.MapleCircleHold.Value, Is.EqualTo(55));
            }
            finally
            {
                foreach (var control in controls)
                    control.Dispose();
            }
        }

        [TestCase("en", "Algorithm")]
        [TestCase("ru", "Алгоритм")]
        [TestCase("es", "Algoritmo")]
        [TestCase("zh", "算法")]
        [TestCase("ko", "알고리즘")]
        public void TestMapleLocalisationThroughRuntime(string locale, string expected)
        {
            using var config = new TestConfig();
            using var manager = new LocalisationManager(config);
            manager.AddLanguage(locale, new ResourceManagerLocalisationStore(locale));
            config.SetValue(FrameworkSetting.Locale, locale);
            Assert.That(manager.GetLocalisedString(RelaxAlgorithmStrings.Algorithm), Is.EqualTo(expected));
            foreach (var property in typeof(RelaxAlgorithmStrings).GetProperties())
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

        [TestCase(MosuRelaxPreset.Natural, 5.0, 10.0, 13f, 172.0)]
        [TestCase(MosuRelaxPreset.Balanced, 4.0, 8.0, 18f, 190.0)]
        [TestCase(MosuRelaxPreset.Reliable, -1.0, 8.0, 26f, 236.0)]
        public void TestPresetAppliesRelaxSettings(MosuRelaxPreset preset, double baseOffset, double variance, float syncRadius, double stableBpm)
        {
            var mod = new OsuModMosuRelax
            {
                BlindTapEnabled = { Value = true },
                StreamBlindMode = { Value = true },
                AimIntentEnabled = { Value = false },
                Preset = { Value = preset },
            };

            Assert.Multiple(() =>
            {
                Assert.That(mod.BaseOffset.Value, Is.EqualTo(baseOffset));
                Assert.That(mod.TimingVariance.Value, Is.EqualTo(variance));
                Assert.That(mod.SyncRadius.Value, Is.EqualTo(syncRadius));
                Assert.That(mod.StableBpm.Value, Is.EqualTo(stableBpm));
                Assert.That(mod.BlindTapEnabled.Value, Is.False);
                Assert.That(mod.StreamBlindMode.Value, Is.False);
                Assert.That(mod.MisaltProbability.Value, Is.Zero);
                Assert.That(mod.AimIntentEnabled.Value, Is.True);
                Assert.That(mod.Preset.Value, Is.EqualTo(preset));
            });
        }

        [Test]
        public void TestManualChangeMarksPresetAsCustom()
        {
            var mod = new OsuModMosuRelax { Preset = { Value = MosuRelaxPreset.Reliable } };

            mod.HoldTime.Value = 50;

            Assert.That(mod.Preset.Value, Is.EqualTo(MosuRelaxPreset.Custom));
        }

        [Test]
        public void TestDefaultUsesReliablePreset()
        {
            var defaults = new OsuModMosuRelax();
            var selected = new OsuModMosuRelax { Preset = { Value = MosuRelaxPreset.Natural } };
            selected.Preset.Value = MosuRelaxPreset.Reliable;

            Assert.Multiple(() =>
            {
                Assert.That(defaults.Preset.Value, Is.EqualTo(MosuRelaxPreset.Reliable));
                Assert.That(defaults.BaseOffset.Value, Is.EqualTo(selected.BaseOffset.Value));
                Assert.That(defaults.TimingVariance.Value, Is.EqualTo(selected.TimingVariance.Value));
                Assert.That(defaults.DynamicDrift.Value, Is.EqualTo(selected.DynamicDrift.Value));
                Assert.That(defaults.HoldTime.Value, Is.EqualTo(selected.HoldTime.Value));
                Assert.That(defaults.AimIntentEnabled.Value, Is.True);
            });
        }

        [Test]
        public void TestDisablingNearTapMarksPresetAsCustom()
        {
            var mod = new OsuModMosuRelax();
            mod.AimIntentEnabled.Value = false;
            Assert.That(mod.Preset.Value, Is.EqualTo(MosuRelaxPreset.Custom));
        }
    }
}
