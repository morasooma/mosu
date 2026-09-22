// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using NUnit.Framework;
using osu.Game.Rulesets.Osu.Mods;

namespace osu.Game.Rulesets.Osu.Tests.Mods
{
    [TestFixture]
    public class OsuModMosuRelaxTest
    {
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
