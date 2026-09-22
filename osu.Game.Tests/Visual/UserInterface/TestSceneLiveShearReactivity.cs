using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Screens.Footer;
using osuTK;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneLiveShearReactivity : OsuTestScene
    {
        [Cached]
        private OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Test]
        public void TestShearedControlsReactToDisableShear()
        {
            ShearedButton button = null!;
            ShearedSliderBar<double> slider = null!;
            ShearedSearchTextBox textBox = null!;
            ShearedNub nub = null!;
            ShearAligningWrapper wrapper = null!;
            Container innerBox = null!;
            ScreenFooterButton footerButton = null!;

            AddStep("reset shear to default", () => OsuGame.DisableShear.Value = false);

            AddStep("create components", () =>
            {
                Child = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(10),
                    Children = new Drawable[]
                    {
                        button = new ShearedButton
                        {
                            Text = "Test Button",
                            Width = 200,
                            Height = 40,
                        },
                        slider = new ShearedSliderBar<double>
                        {
                            Current = new BindableDouble(50) { MinValue = 0, MaxValue = 100 },
                            Width = 200,
                        },
                        textBox = new ShearedSearchTextBox
                        {
                            Width = 200,
                        },
                        nub = new ShearedNub(),
                        wrapper = new ShearAligningWrapper(innerBox = new Container
                        {
                            Width = 200,
                            Height = 30,
                        }),
                        footerButton = new ScreenFooterButton
                        {
                            Text = "Footer",
                            Width = 120,
                        },
                    }
                };
            });

            AddAssert("initial button shear is default", () => button.Shear == OsuGame.SHEAR);
            AddAssert("initial slider shear is default", () => slider.Shear == OsuGame.SHEAR);
            AddAssert("initial textBox shear is default", () => textBox.Shear == OsuGame.SHEAR);
            AddAssert("initial nub shear is default", () => nub.ChildrenOfType<Container>().Any(c => c != (Container)nub && c.Shear == OsuGame.SHEAR));
            AddAssert("initial footerButton shear is default", () => footerButton.ChildrenOfType<Container>().Any(c => c.Shear == OsuGame.SHEAR));

            AddStep("disable shear live", () => OsuGame.DisableShear.Value = true);

            AddAssert("button shear is zero", () => button.Shear == Vector2.Zero);
            AddAssert("slider shear is zero", () => slider.Shear == Vector2.Zero);
            AddAssert("textBox shear is zero", () => textBox.Shear == Vector2.Zero);
            AddAssert("nub shear is zero", () => nub.ChildrenOfType<Container>().Where(c => c != (Container)nub).All(c => c.Shear == Vector2.Zero));
            AddAssert("wrapper padding is zero", () => wrapper.Padding.Left == 0);
            AddAssert("footerButton shear is zero", () => footerButton.ChildrenOfType<Container>().All(c => c.Shear == Vector2.Zero));

            AddStep("re-enable shear live", () => OsuGame.DisableShear.Value = false);

            AddAssert("button shear restored", () => button.Shear == OsuGame.SHEAR);
            AddAssert("slider shear restored", () => slider.Shear == OsuGame.SHEAR);
            AddAssert("textBox shear restored", () => textBox.Shear == OsuGame.SHEAR);
            AddAssert("nub shear restored", () => nub.ChildrenOfType<Container>().Any(c => c != (Container)nub && c.Shear == OsuGame.SHEAR));
            AddAssert("footerButton shear restored", () => footerButton.ChildrenOfType<Container>().Any(c => c.Shear == OsuGame.SHEAR));
        }

        [Test]
        public void TestShearedDropdownReactsToDisableShear()
        {
            ShearedDropdown<string> dropdown = null!;

            AddStep("reset shear to default", () => OsuGame.DisableShear.Value = false);

            AddStep("create dropdown", () =>
            {
                Child = dropdown = new ShearedDropdown<string>("Test Label")
                {
                    Width = 200,
                    Items = new[] { "Option 1", "Option 2" },
                };
            });

            AddUntilStep("wait for dropdown loaded", () => dropdown.IsLoaded);
            AddAssert("dropdown header shear is default", () =>
                dropdown.ChildrenOfType<ShearedDropdown<string>.ShearedDropdownHeader>().First().Shear == OsuGame.SHEAR);
            AddAssert("search text box has reverse shear", () =>
                dropdown.ChildrenOfType<OsuTextBox>().First().ChildrenOfType<Container>().Any(c => c.Shear == -OsuGame.SHEAR));

            AddStep("disable shear live", () => OsuGame.DisableShear.Value = true);
            AddAssert("dropdown header shear is zero", () =>
                dropdown.ChildrenOfType<ShearedDropdown<string>.ShearedDropdownHeader>().First().Shear == Vector2.Zero);
            AddAssert("search text box shear is zero", () =>
                dropdown.ChildrenOfType<OsuTextBox>().First().ChildrenOfType<Container>().All(c => c.Shear == Vector2.Zero));

            AddStep("re-enable shear live", () => OsuGame.DisableShear.Value = false);
            AddAssert("dropdown header shear restored", () =>
                dropdown.ChildrenOfType<ShearedDropdown<string>.ShearedDropdownHeader>().First().Shear == OsuGame.SHEAR);
            AddAssert("search text box shear restored", () =>
                dropdown.ChildrenOfType<OsuTextBox>().First().ChildrenOfType<Container>().Any(c => c.Shear == -OsuGame.SHEAR));
        }
    }
}
