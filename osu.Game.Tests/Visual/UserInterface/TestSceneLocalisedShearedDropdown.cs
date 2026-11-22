using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Screens.Select;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneLocalisedShearedDropdown : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        [Test]
        public void TestLeaderboardControlsStayOnOneRow()
        {
            BeatmapDetailsArea.Header header = null!;

            AddStep("create narrow leaderboard header", () =>
            {
                Child = header = new BeatmapDetailsArea.Header { Width = 650, Height = 35 };
            });
            AddStep("show rankings", () => ((Bindable<BeatmapDetailsArea.Header.Selection>)header.Type).Value = BeatmapDetailsArea.Header.Selection.Ranking);
            AddAssert("rankings stay on one row", () => header.Height == 35);
            AddAssert("dropdown is beside tabs", () => header.ChildrenOfType<ShearedDropdown<osu.Game.Screens.Play.Leaderboards.BeatmapLeaderboardScope>>()
                .Single().ToSpaceOfOtherDrawable(osuTK.Vector2.Zero, header).Y < 10);
            AddStep("widen header", () => header.Width = 1200);
            AddUntilStep("header returns to one row", () => header.Height == 35);
            AddStep("narrow and show details", () =>
            {
                header.Width = 650;
                ((Bindable<BeatmapDetailsArea.Header.Selection>)header.Type).Value = BeatmapDetailsArea.Header.Selection.Details;
            });
            AddUntilStep("details need only one row", () => header.Height == 35);
        }

        [TestCase(140)]
        [TestCase(180)]
        [TestCase(280)]
        public void TestLongLabelReservesSpaceForValue(float width)
        {
            ShearedDropdown<string> dropdown = null!;
            ShearedDropdown<string>.ShearedDropdownHeader header = null!;

            AddStep("create Russian dropdown", () =>
            {
                Child = dropdown = new ShearedDropdown<string>("Сортировка")
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = width,
                    Items = new[] { "Исполнитель", "Дата добавления" },
                    Current = { Value = "Исполнитель" },
                };
            });
            AddStep("find header", () => header = dropdown.ChildrenOfType<ShearedDropdown<string>.ShearedDropdownHeader>().Single());
            AddUntilStep("label uses no more than half the header", () => header.LabelContainer.DrawWidth <= width / 2 + 1);
            AddAssert("selected value has visible space", () => header.ChildrenOfType<TruncatingSpriteText>()
                .Single(t => t.Text.ToString() == "Исполнитель").DrawWidth >= width / 2 - 21);

            AddStep("switch to English label", () => header.LeftSideLabel = "Sort");
            AddUntilStep("short label returns unused space", () => header.LabelContainer.DrawWidth < width / 2);
            AddStep("switch back to Russian", () => header.LeftSideLabel = "Группировка");
            AddUntilStep("long label is constrained after switching", () => header.LabelContainer.DrawWidth <= width / 2 + 1);
            AddAssert("truncated label has full tooltip", () => header.LabelContainer.ChildrenOfType<TruncatingSpriteText>()
                .Single().TooltipText.ToString() == "Группировка");
        }
    }
}
