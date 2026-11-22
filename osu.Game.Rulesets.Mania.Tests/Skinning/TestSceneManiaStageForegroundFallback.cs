using System.IO;
using System.IO.Compression;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Database;
using osu.Game.Rulesets.Mania.Skinning.Legacy;
using osu.Game.Skinning;
using osu.Game.Tests.Visual;

namespace osu.Game.Rulesets.Mania.Tests.Skinning
{
    [HeadlessTest]
    public partial class TestSceneManiaStageForegroundFallback : PlayerTestScene
    {
        [Resolved]
        private SkinManager skins { get; set; } = null!;

        protected override bool HasCustomSteps => true;

        protected override Ruleset CreatePlayerRuleset() => new ManiaRuleset();

        [TestCase(false)]
        [TestCase(true)]
        public void TestUserSkin(bool hasStageBottom)
        {
            AddStep("select user mania skin", () =>
            {
                using var resources = new DllResourceStore(GetType().Assembly);
                var archiveStream = new MemoryStream();
                using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
                {
                    using (var writer = new StreamWriter(archive.CreateEntry("skin.ini").Open()))
                        writer.Write($"[General]\nName: Mania stage foreground test {hasStageBottom}\nVersion: 2.5\n[Mania]\nKeys: 4\n" + (hasStageBottom ? "StageBottom: custom-bottom\n" : string.Empty));

                    copyTexture(archive, "special_skin/mania-key1@2x.png", "mania-key1@2x.png");
                    if (hasStageBottom)
                        copyTexture(archive, "metrics_skin/mania-stage-bottom@2x.png", "custom-bottom@2x.png");
                }

                archiveStream.Position = 0;
                skins.CurrentSkinInfo.Value = skins.Import(new ImportTask(archiveStream, "mania-stage-foreground.osk")).GetResultSafely();

                void copyTexture(ZipArchive archive, string resource, string filename)
                {
                    using var source = resources.GetStream($"Resources/{resource}")!;
                    using var destination = archive.CreateEntry(filename).Open();
                    source.CopyTo(destination);
                }
            });

            CreateTest();
            AddAssert("only the skin's own foreground is drawn", () => countForegroundSprites() == (hasStageBottom ? 1 : 0));
        }

        [Test]
        public void TestBuiltInSkin()
        {
            AddStep("select Morasooma", () => skins.CurrentSkinInfo.Value = skins.DefaultClassicSkin.SkinInfo);
            CreateTest();
            AddAssert("built-in foreground is drawn", () => countForegroundSprites() == 1);
        }

        private int countForegroundSprites() => Player.ChildrenOfType<LegacyStageForeground>().Single().ChildrenOfType<Sprite>().Count();
    }
}
