// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Tests.Visual.UserInterface
{
    [TestFixture]
    public partial class TestSceneOsuTooltip : OsuManualInputManagerTestScene
    {
        private TestTooltipContainer container = null!;

        private static readonly string[] test_case_tooltip_string =
        [
            "Hello!!",
            string.Concat(Enumerable.Repeat("Hello ", 100)),

            //TODO: o!f issue: https://github.com/ppy/osu-framework/issues/5007
            //Enable after o!f fixed
            // $"H{new string('e', 500)}llo",
        ];

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Padding = new MarginPadding(100),
                Children = new Drawable[]
                {
                    new Box
                    {
                        Colour = Colour4.Red.Opacity(0.5f),
                        RelativeSizeAxes = Axes.Both,
                    },
                    container = new TestTooltipContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = new OsuSpriteText
                        {
                            Text = "Hover me!",
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Font = OsuFont.GetFont(size: 50)
                        }
                    },
                },
            };
        });

        [TestCaseSource(nameof(test_case_tooltip_string))]
        public void TestTooltipBasic(string text)
        {
            AddStep("Set tooltip content", () => container.TooltipText = text);

            AddStep("Move mouse to container", () => InputManager.MoveMouseTo(new Vector2(InputManager.ScreenSpaceDrawQuad.Centre.X, InputManager.ScreenSpaceDrawQuad.Centre.Y)));

            OsuTooltipContainer.OsuTooltip? tooltip = null!;

            AddUntilStep("Wait for the tooltip shown", () =>
            {
                tooltip = container.FindClosestParent<OsuTooltipContainer>().ChildrenOfType<OsuTooltipContainer.OsuTooltip>().FirstOrDefault();
                return tooltip != null && tooltip.Alpha == 1;
            });

            AddAssert("Check tooltip is under width limit", () => tooltip != null && tooltip.Width <= 500);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestSharedPositionFiltering(bool customTooltip)
        {
            InspectableTooltipContainer tooltips = null!;
            Drawable provider = null!;

            AddStep("create isolated tooltips", () => Child = tooltips = new InspectableTooltipContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = provider = customTooltip
                    ? new CustomTooltipProvider { RelativeSizeAxes = Axes.Both }
                    : new TestTooltipContainer { RelativeSizeAxes = Axes.Both, TooltipText = "stabilised" },
            });
            AddStep("hover provider", () => InputManager.MoveMouseTo(provider.ScreenSpaceDrawQuad.Centre));
            AddUntilStep("tooltip visible", () => tooltips.DisplayedTooltip.Alpha == 1);
            AddStep("small cursor movement", () => InputManager.MoveMouseTo(provider.ScreenSpaceDrawQuad.Centre + new Vector2(12, 0)));
            AddAssert("shared filter trails raw anchor", () => tooltips.RawAnchor.X > tooltips.FilteredAnchor.X);
            AddAssert("tooltip uses filtered anchor", () => tooltips.DisplayedTooltip.X > tooltips.FilteredAnchor.X);
            AddUntilStep("settles at new anchor", () => (tooltips.RawAnchor - tooltips.FilteredAnchor).Length < 0.5f);
        }

        private partial class InspectableTooltipContainer : OsuTooltipContainer
        {
            public Drawable DisplayedTooltip => (Drawable)CurrentTooltip;
            public Vector2 RawAnchor { get; private set; }
            public Vector2 FilteredAnchor { get; private set; }

            public InspectableTooltipContainer()
                : base(null!)
            {
            }

            protected override Vector2 FilterTooltipCursorPosition(Vector2 position)
            {
                RawAnchor = position;
                return FilteredAnchor = base.FilterTooltipCursorPosition(position);
            }
        }

        private partial class CustomTooltipProvider : Container, IHasCustomTooltip
        {
            public object TooltipContent => (LocalisableString)"custom stabilised";
            public ITooltip GetCustomTooltip() => new TooltipContainer.Tooltip();
        }

        internal sealed partial class TestTooltipContainer : Container, IHasTooltip
        {
            public LocalisableString TooltipText { get; set; }
        }
    }
}
