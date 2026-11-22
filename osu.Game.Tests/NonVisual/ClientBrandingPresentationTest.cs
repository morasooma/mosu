using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using osu.Game.Beatmaps;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;

namespace osu.Game.Tests.NonVisual
{
    [TestFixture]
    public class ClientBrandingPresentationTest
    {
        [Test]
        public void TestBuiltinSkinLabelsHideCreditsWithoutChangingMetadata()
        {
            foreach (SkinInfo current in new[] { ArgonSkin.CreateInfo(), ArgonProSkin.CreateInfo(), TrianglesSkin.CreateInfo(), DefaultLegacySkin.CreateInfo(), MorasoomaSkin.CreateInfo() })
            {
                Assert.That(current.ToString(), Is.EqualTo(current.Name));
                var saved = new SkinInfo("osu! old skin name", "team osu!") { ID = current.ID, Protected = true };
                Assert.That(saved.ToString(), Is.EqualTo(current.Name), "Existing saved built-in metadata must use the new label");
                Assert.That(saved.Name, Is.EqualTo("osu! old skin name"));
                Assert.That(saved.Creator, Is.EqualTo("team osu!"));
                saved.Protected = false;
                Assert.That(saved.ToString(), Is.EqualTo("osu! old skin name (team osu!)"), "Do not relabel editable/user skins");
                saved.ID = Guid.NewGuid(); saved.Protected = true;
                Assert.That(saved.ToString(), Is.EqualTo("osu! old skin name (team osu!)"));
            }
            Assert.That(ArgonSkin.CreateInfo().Creator, Is.EqualTo("team osu!"), "Original attribution is not removed");
        }

        [TestCase(0, "Standard")]
        [TestCase(1, "Taiko")]
        [TestCase(2, "Catch")]
        [TestCase(3, "Mania")]
        [TestCase(4, "Standard Relax")]
        [TestCase(5, "Standard Autopilot")]
        [TestCase(6, "Taiko Relax")]
        [TestCase(7, "Catch Relax")]
        [TestCase(10, "Dodge")]
        public void TestInvitationModeLabels(int id, string expected)
        {
            var method = typeof(osu.Game.Overlays.Toolbar.ToolbarServerInvitation).GetMethod("modeName", BindingFlags.NonPublic | BindingFlags.Static)!;
            Assert.That(method.Invoke(null, new object[] { id }), Is.EqualTo(expected));
        }

        [TestCase("osu.Game.Rulesets.Osu.OsuRuleset, osu.Game.Rulesets.Osu")]
        [TestCase("osu.Game.Rulesets.Taiko.TaikoRuleset, osu.Game.Rulesets.Taiko")]
        [TestCase("osu.Game.Rulesets.Catch.CatchRuleset, osu.Game.Rulesets.Catch")]
        [TestCase("osu.Game.Rulesets.Mania.ManiaRuleset, osu.Game.Rulesets.Mania")]
        [TestCase("osu.Game.Rulesets.Dodge.DodgeRuleset, osu.Game.Rulesets.Dodge")]
        public void TestGeneratedReplayUsers(string typeName)
        {
            var ruleset = (Ruleset)Activator.CreateInstance(Type.GetType(typeName, throwOnError: true)!)!;
            IBeatmap beatmap = ruleset switch
            {
                osu.Game.Rulesets.Osu.OsuRuleset => new osu.Game.Rulesets.Osu.Beatmaps.OsuBeatmap(),
                osu.Game.Rulesets.Taiko.TaikoRuleset => new osu.Game.Rulesets.Taiko.Beatmaps.TaikoBeatmap(),
                osu.Game.Rulesets.Catch.CatchRuleset => new osu.Game.Rulesets.Catch.Beatmaps.CatchBeatmap(),
                osu.Game.Rulesets.Mania.ManiaRuleset => new osu.Game.Rulesets.Mania.Beatmaps.ManiaBeatmap(new osu.Game.Rulesets.Mania.Beatmaps.StageDefinition(4)),
                _ => new Beatmap<osu.Game.Rulesets.Dodge.Objects.DodgeHitObject>(),
            };
            foreach (Mod mod in new[] { ruleset.GetAutoplayMod()!, ruleset.CreateMod<ModCinema>()! })
            {
                var data = ((ICreateReplayData)mod).CreateReplayData(beatmap, Array.Empty<Mod>());
                Assert.That(data.User.Username, Is.EqualTo("MoraBot"), mod.GetType().Name);
                Assert.That(data.User.IsBot, Is.True);
            }
        }
    }
}
