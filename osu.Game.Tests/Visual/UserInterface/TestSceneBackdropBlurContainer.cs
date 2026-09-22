// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input.Events;
using osu.Framework.Testing;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneBackdropBlurContainer : OsuManualInputManagerTestScene
    {
        private bool previousTransparency;

        [Cached(typeof(IBackdropBlurSource))]
        private readonly BackdropBlurContainer backdropSource = new BackdropBlurContainer
        {
            SceneContent = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Blue,
            },
        };

        private readonly Container surfaceParent;

        public TestSceneBackdropBlurContainer()
        {
            Add(backdropSource);
            Add(surfaceParent = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Alpha = 0,
                Child = new BackdropBlurSurface(),
            });
        }

        [TearDown]
        public void TearDown() => OverlayTransparency.Enabled.Value = previousTransparency;

        [Test]
        public void TestHiddenParentReleasesBuffering()
        {
            AddStep("enable transparency", () =>
            {
                previousTransparency = OverlayTransparency.Enabled.Value;
                OverlayTransparency.Enabled.Value = true;
            });
            AddAssert("surface registered", () => backdropSource.RegisteredConsumers == 1);
            AddAssert("buffering initially disabled", () => !backdropSource.BufferingActive);
            AddStep("show surface parent", () => surfaceParent.Alpha = 1);
            AddUntilStep("buffering enabled", () => backdropSource.BufferingActive);
            AddStep("hide surface parent", () => surfaceParent.Alpha = 0);
            AddUntilStep("buffering disabled", () => !backdropSource.BufferingActive);
        }

        [Test]
        public void TestContextMenuPresentedOutsideCapturedScene()
        {
            TestContextMenuTarget target = null!;

            AddStep("enable transparency", () =>
            {
                previousTransparency = OverlayTransparency.Enabled.Value;
                OverlayTransparency.Enabled.Value = true;
                OverlayTransparency.BlurStrength.Value = 1;
            });
            AddStep("add context menu inside scene", () => backdropSource.SceneContent = new OsuContextMenuContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = target = new TestContextMenuTarget
                {
                    RelativeSizeAxes = Axes.Both,
                },
            });
            AddStep("open context menu", () =>
            {
                InputManager.MoveMouseTo(target);
                InputManager.Click(MouseButton.Right);
            });
            AddUntilStep("context menu open", () => this.ChildrenOfType<OsuContextMenu>().SingleOrDefault()?.State == MenuState.Open);
            AddAssert("menu outside framebuffer", () => this.ChildrenOfType<OsuContextMenu>().Single().FindClosestParent<BufferedContainer>() == null);
            AddUntilStep("buffering enabled", () => backdropSource.BufferingActive);
            AddStep("close context menu", () =>
            {
                InputManager.MoveMouseTo(ScreenSpaceDrawQuad.TopLeft + new osuTK.Vector2(5));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("buffering disabled", () => !backdropSource.BufferingActive);
            AddStep("remove context menu", () => backdropSource.SceneContent = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Blue,
            });
            AddUntilStep("menu surface released", () => backdropSource.RegisteredConsumers == 1);
        }

        [Test]
        public void TestPopoverPresentedOutsideCapturedScene()
        {
            TestPopoverTarget target = null!;

            AddStep("enable transparency", () =>
            {
                previousTransparency = OverlayTransparency.Enabled.Value;
                OverlayTransparency.Enabled.Value = true;
                OverlayTransparency.BlurStrength.Value = 1;
            });
            AddStep("add popover inside scene", () => backdropSource.SceneContent = new PopoverContainer
            {
                RelativeSizeAxes = Axes.Both,
                Child = target = new TestPopoverTarget
                {
                    RelativeSizeAxes = Axes.Both,
                },
            });
            AddStep("open popover", () =>
            {
                InputManager.MoveMouseTo(target);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("popover open", () => this.ChildrenOfType<OsuPopover>().SingleOrDefault()?.IsPresent == true);
            AddAssert("popover outside framebuffer", () => this.ChildrenOfType<OsuPopover>().Single().FindClosestParent<BufferedContainer>() == null);
            AddUntilStep("buffering enabled", () => backdropSource.BufferingActive);
            AddStep("close popover", () => target.HidePopover());
            AddUntilStep("buffering disabled", () => !backdropSource.BufferingActive);
            AddStep("remove popover", () => backdropSource.SceneContent = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Blue,
            });
            AddUntilStep("popover surface released", () => backdropSource.RegisteredConsumers == 1);
        }

        [Test]
        public void TestDropdownPresentedOutsideCapturedScene()
        {
            TestDropdown dropdown = null!;

            AddStep("enable transparency", () =>
            {
                previousTransparency = OverlayTransparency.Enabled.Value;
                OverlayTransparency.Enabled.Value = true;
                OverlayTransparency.BlurStrength.Value = 1;
            });
            AddStep("add dropdown inside scene", () => backdropSource.SceneContent = dropdown = new TestDropdown
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Width = 250,
                Items = new[] { "First", "Second", "Third" },
            });
            AddStep("open dropdown", () =>
            {
                InputManager.MoveMouseTo(dropdown.HeaderDrawable);
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("dropdown open", () => dropdown.MenuDrawable.State == MenuState.Open);
            AddAssert("dropdown outside framebuffer", () => dropdown.MenuDrawable.FindClosestParent<BufferedContainer>() == null);
            AddUntilStep("buffering enabled", () => backdropSource.BufferingActive);
            AddStep("select dropdown item", () =>
            {
                InputManager.MoveMouseTo(dropdown.MenuDrawable.ChildrenOfType<Menu.DrawableMenuItem>().ElementAt(1));
                InputManager.Click(MouseButton.Left);
            });
            AddUntilStep("dropdown closed", () => dropdown.MenuDrawable.State == MenuState.Closed);
            AddAssert("item selected", () => dropdown.Current.Value == "Second");
            AddUntilStep("buffering disabled", () => !backdropSource.BufferingActive);
            AddStep("remove dropdown", () => backdropSource.SceneContent = new Box
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.Blue,
            });
            AddUntilStep("dropdown surface released", () => backdropSource.RegisteredConsumers == 1);
        }

        private partial class TestContextMenuTarget : Container, IHasContextMenu
        {
            public MenuItem[] ContextMenuItems => new MenuItem[]
            {
                new OsuMenuItem("Portal blur test"),
            };
        }

        private partial class TestPopoverTarget : Container, IHasPopover
        {
            public Popover GetPopover() => new OsuPopover
            {
                Child = new Box
                {
                    Size = new osuTK.Vector2(100),
                    Colour = Color4.Red,
                },
            };

            protected override bool OnClick(ClickEvent e)
            {
                this.ShowPopover();
                return true;
            }
        }

        private partial class TestDropdown : OsuDropdown<string>
        {
            public DropdownHeader HeaderDrawable => Header;

            public DropdownMenu MenuDrawable => Menu;
        }
    }
}
