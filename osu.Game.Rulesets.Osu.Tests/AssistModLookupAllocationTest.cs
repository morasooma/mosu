using System;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.UI;

namespace osu.Game.Rulesets.Osu.Tests
{
    [TestFixture]
    public class AssistModLookupAllocationTest
    {
        private const BindingFlags instance = BindingFlags.Instance | BindingFlags.NonPublic;

        [TestCase(false)]
        [TestCase(true)]
        public void TestAimLookupCachesPresenceAndAbsenceWithoutAllocating(bool present)
        {
            using var ruleset = new DrawableOsuRuleset(new OsuRuleset(), new OsuBeatmap(), present
                ? new Mod[] { new OsuModMosuAimAssist() }
                : new Mod[] { new OsuModHidden() });
            using var controller = new AimAssistController();
            verify(controller, ruleset, "cacheMosuAimAssistMod", "mosuAimAssistMod", present);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestRelaxLookupCachesPresenceAndAbsenceWithoutAllocating(bool present)
        {
            using var ruleset = new DrawableOsuRuleset(new OsuRuleset(), new OsuBeatmap(), present
                ? new Mod[] { new OsuModMosuRelax() }
                : new Mod[] { new OsuModHidden() });
            using var controller = new RelaxController();
            verify(controller, ruleset, "cacheMosuRelaxMod", "mosuRelaxMod", present);
        }

        private static void verify(object controller, DrawableOsuRuleset ruleset, string method, string modField, bool present)
        {
            Type type = controller.GetType();
            var owner = type.GetField("drawableRuleset", instance)!;
            var mod = type.GetField(modField, instance)!;
            var lookup = (Action)type.GetMethod(method, instance)!.CreateDelegate(typeof(Action), controller);

            // A standalone playfield can initially have no DrawableRuleset ancestor.
            lookup();
            owner.SetValue(controller, ruleset);
            lookup();
            Assert.That(mod.GetValue(controller), present ? Is.SameAs(ruleset.Mods[0]) : Is.Null);

            for (int i = 0; i < 1000; i++)
                lookup();
            long start = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++)
                lookup();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - start;
            Assert.That(allocated, Is.Zero, "Repeated gameplay lookups must not allocate, including absent mods.");

            using var replacement = new DrawableOsuRuleset(new OsuRuleset(), new OsuBeatmap(), new Mod[] { new OsuModHidden() });
            owner.SetValue(controller, replacement);
            lookup();
            Assert.That(mod.GetValue(controller), Is.Null, "A replacement ruleset must not retain the previous mod.");
        }
    }
}
