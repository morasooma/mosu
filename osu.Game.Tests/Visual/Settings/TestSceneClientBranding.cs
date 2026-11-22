using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays.Settings.Sections;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Settings
{
    [HeadlessTest]
    public partial class TestSceneClientBranding : OsuGameTestScene
    {
        [Test]
        public void TestSavedBuiltinSkinsUseCleanLabelsInRealSettings()
        {
            SkinSection section = null!;
            SkinInfo[] builtins = { ArgonSkin.CreateInfo(), ArgonProSkin.CreateInfo(), TrianglesSkin.CreateInfo(), DefaultLegacySkin.CreateInfo(), MorasoomaSkin.CreateInfo() };
            AddStep("simulate old builtin metadata in isolated library", () =>
            {
                Game.Dependencies.Get<RealmAccess>().Write(r =>
                {
                    foreach (SkinInfo builtin in builtins)
                    {
                        SkinInfo saved = r.Find<SkinInfo>(builtin.ID)!;
                        saved.Name = "osu! " + builtin.Name + " (upstream, 2022)";
                        saved.Creator = "team osu!";
                    }
                });
                Game.LocalConfig.SetValue(OsuSetting.Skin, SkinInfo.ARGON_SKIN.ToString());
            });
            AddStep("restart isolated game without migrating metadata", () =>
            {
                Remove(Game, true);
                CreateGame();
            });
            AddUntilStep("game and settings loaded", () => Game.IsLoaded && Game.Settings.IsLoaded);
            AddStep("open real skin settings", () => Game.Settings.Show());
            AddUntilStep("skin controls loaded", () =>
            {
                section = Game.Settings.SectionsContainer.Children.OfType<SkinSection>().SingleOrDefault()!;
                return section?.IsLoaded == true && section.ChildrenOfType<FormDropdown<Live<SkinInfo>>>().Count() == 6;
            });
            AddStep("check global and five per-mode dropdowns", () =>
            {
                foreach (var dropdown in section.ChildrenOfType<FormDropdown<Live<SkinInfo>>>())
                {
                    string[] labels = dropdown.FilterTerms.Skip(1).Select(t => t.ToString()).ToArray();
                    foreach (SkinInfo builtin in builtins)
                        Assert.That(labels, Does.Contain(builtin.Name));
                    Assert.That(labels.Any(l => l.Contains("osu!") || l.Contains("upstream")), Is.False);
                }
                Assert.That(Game.Dependencies.Get<SkinManager>().CurrentSkinInfo.Value.ID, Is.EqualTo(SkinInfo.ARGON_SKIN));
                Game.Dependencies.Get<RealmAccess>().Run(r =>
                {
                    foreach (SkinInfo builtin in builtins)
                    {
                        SkinInfo saved = r.Find<SkinInfo>(builtin.ID)!;
                        Assert.That(saved.Name, Is.EqualTo("osu! " + builtin.Name + " (upstream, 2022)"));
                        Assert.That(saved.Creator, Is.EqualTo("team osu!"));
                    }
                });
            });
        }
    }
}
