using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Framework.IO.Stores;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneMosuModIcons : OsuTestScene
    {
        [Resolved]
        private FontStore fonts { get; set; } = null!;

        protected override ITestSceneTestRunner CreateRunner() => new IconTestRunner();

        private partial class IconTestRunner : OsuTestSceneTestRunner
        {
            protected override void InitialiseFonts() => Fonts.AddStore(new OsuIcon.OsuIconStore(Textures));
        }

        [Test]
        public void TestCustomPngsResolveThroughActualIconFontStore()
        {
            Mod[] mods = { new OsuModMosuAimAssist(), new OsuModMosuAntiAimAssist(), new OsuModMosuStaticBpm(), new OsuModMosuTargetDifficulty(), new OsuModMosuMalevich() };
            ModIcon[] icons = mods.Select(m => new ModIcon(m)).ToArray();
            AddStep("show custom mod icons", () => Child = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Horizontal,
                Children = icons,
            });
            AddUntilStep("all mod icons loaded", () => icons.All(i => i.IsLoaded));
            AddAssert("every actual texture glyph resolves", () => mods.All(m =>
            {
                var icon = m.Icon!.Value;
                var glyph = fonts.Get(icon.FontName, icon.Icon);
                TestContext.Out.WriteLine($"{m.Acronym}: font={icon.FontName}, character={(int)icon.Icon}, glyph={(glyph == null ? "null" : $"{glyph.Width}x{glyph.Height}")}");
                return glyph != null && glyph.Width > 0 && glyph.Height > 0;
            }));
        }
    }
}
