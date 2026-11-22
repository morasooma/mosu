using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.UI;
using Image = SixLabors.ImageSharp.Image;
using SixLabors.ImageSharp.PixelFormats;
using osuTK;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneModIconExtendedInformation : OsuTestScene
    {
        [TestCase(0.5f)]
        [TestCase(0.8f)]
        [TestCase(1f)]
        [TestCase(1.5f)]
        public void TestExtendedBackgroundHasNoTransparentSeam(float scale)
        {
            foreach (Mod mod in new Mod[]
                     {
                         new OsuModDifficultyAdjust { CircleSize = { Value = 4.4f } },
                         new OsuModDoubleTime { SpeedChange = { Value = 1.25 } },
                     })
            {
                ModIcon icon = null!;
                AddStep($"load {mod.Acronym}", () => Child = icon = new ModIcon(mod) { Scale = new Vector2(scale) });
                AddUntilStep("loaded", () => icon.IsLoaded);
                AddAssert("only background moves; parameter retains its original position", () =>
                {
                    var content = icon.Children.OfType<Container>().Single(c => c.Name == "extended content");
                    return content.X == ModIcon.MOD_ICON_SIZE.X - 22
                        && content.Children.OfType<OsuSpriteText>().Single().X == 6
                        && content.Children.OfType<Sprite>().Single().X == -6;
                });
                foreach (bool selected in new[] { false, true })
                {
                    AddStep($"selected = {selected}", () => icon.Selected.Value = selected);
                    AddAssert("background alpha silhouettes overlap along the entire join", () => backgroundsJoin(icon));
                    AddAssert("parameter text stays within its background", () =>
                    {
                        var content = icon.Children.OfType<Container>().Single(c => c.Name == "extended content");
                        var text = content.Children.OfType<OsuSpriteText>().Single();
                        var background = content.Children.OfType<Sprite>().Single();
                        return text.ScreenSpaceDrawQuad.TopLeft.X >= background.ScreenSpaceDrawQuad.TopLeft.X
                            && text.ScreenSpaceDrawQuad.BottomRight.X <= background.ScreenSpaceDrawQuad.BottomRight.X;
                    });
                }
                AddStep("hide parameter", () => icon.ShowExtendedInformation = false);
                AddAssert("parameter hidden", () => icon.Children.OfType<Container>().Single(c => c.Name == "extended content").Alpha == 0);
                AddStep("restore parameter", () => icon.ShowExtendedInformation = true);
                AddAssert("parameter restored", () => icon.Children.OfType<Container>().Single(c => c.Name == "extended content").Alpha == 1);
            }
        }

        private static bool backgroundsJoin(ModIcon icon)
        {
            var main = icon.Children.OfType<Container>().Single(c => c.Name == "main content").Children.OfType<Sprite>().First();
            var extension = icon.Children.OfType<Container>().Single(c => c.Name == "extended content").Children.OfType<Sprite>().Single();
            using var resources = new DllResourceStore(typeof(Morasooma.Resources.ResourceAssembly).Assembly);
            using var mainImage = Image.Load<Rgba32>(resources.Get("IconOverrides/Textures/Icons/BeatmapDetails/mod-icon.png"));
            using var extensionImage = Image.Load<Rgba32>(resources.Get("IconOverrides/Textures/Icons/BeatmapDetails/mod-icon-extender.png"));

            // Sample the real alpha contours in the actual Fit-scaled sprite geometry,
            // not the rectangular container bounds (which overlap even with a visible gap).
            for (int row = 10; row <= 90; row++)
            {
                float localY = (row + 0.5f) / mainImage.Height * main.DrawHeight;
                Vector2 screen = main.ToScreenSpace(new Vector2(0, localY));
                int extensionRow = (int)(extension.ToLocalSpace(screen).Y / extension.DrawHeight * extensionImage.Height);
                int mainRight = mainImage.Width - 1;
                while (mainRight >= 0 && mainImage[mainRight, row].A < 128)
                    mainRight--;
                int extensionLeft = 0;
                while (extensionLeft < extensionImage.Width && extensionImage[extensionLeft, extensionRow].A < 128)
                    extensionLeft++;

                float right = main.ToScreenSpace(new Vector2((mainRight + 0.5f) / mainImage.Width * main.DrawWidth, localY)).X;
                float left = extension.ToScreenSpace(new Vector2((extensionLeft + 0.5f) / extensionImage.Width * extension.DrawWidth, 0)).X;
                if (left > right)
                {
                    TestContext.Out.WriteLine($"Transparent seam at row {row}: {left - right:0.000} screen pixels");
                    return false;
                }
            }
            return true;
        }
    }
}
