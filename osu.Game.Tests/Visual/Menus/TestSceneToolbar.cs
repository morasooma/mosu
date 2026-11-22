// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System.Collections.Generic;
using System.Threading.Tasks;
using osu.Game.Online.Metadata;
using System.Linq;
using JetBrains.Annotations;
using Moq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Testing;
using osu.Framework.Utils;
using osu.Game.Graphics.Containers;
using osu.Game.Overlays;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Toolbar;
using osu.Game.Rulesets;
using osu.Framework.Graphics.Effects;
using osuTK;
using osuTK.Graphics;
using osuTK.Input;

namespace osu.Game.Tests.Visual.Menus
{
    [TestFixture]
    public partial class TestSceneToolbar : OsuManualInputManagerTestScene
    {
        private TestToolbar toolbar;

        [Resolved]
        private IRulesetStore rulesets { get; set; }

        [Cached]
        private readonly NowPlayingOverlay nowPlayingOverlay = new NowPlayingOverlay
        {
            Anchor = Anchor.TopRight,
            Origin = Anchor.TopRight,
            Y = Toolbar.HEIGHT,
        };

        [Cached]
        private readonly VolumeOverlay volumeOverlay = new VolumeOverlay
        {
            Anchor = Anchor.CentreLeft,
            Origin = Anchor.CentreLeft,
        };

        private readonly Mock<TestNotificationOverlay> notifications = new Mock<TestNotificationOverlay>();

        private readonly BindableInt unreadNotificationCount = new BindableInt();

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.CacheAs<MetadataClient>(new osu.Game.Tests.Visual.Metadata.TestMetadataClient());
            Dependencies.CacheAs<INotificationOverlay>(notifications.Object);
            notifications.SetupGet(n => n.UnreadCount).Returns(unreadNotificationCount);
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            Remove(nowPlayingOverlay, false);
            Remove(volumeOverlay, false);

            Children = new Drawable[]
            {
                nowPlayingOverlay,
                volumeOverlay,
                toolbar = new TestToolbar { State = { Value = Visibility.Visible } },
            };
        });

        [Test]
        public void TestSocialConnectionChangesFromNetworkThread()
        {
            Task change = null;
            Bindable<bool> connection = new Bindable<bool>();
            AddStep("resolve metadata connection", () => ((IBindable<bool>)connection).BindTo(Dependencies.Get<MetadataClient>().IsConnected));
            AddStep("disconnect on network thread", () => change = Task.Run(() => connection.Value = false));
            AddUntilStep("disconnect completed without thread exception", () => change.IsCompletedSuccessfully);
            AddWaitStep("process scheduled UI change", 3);
            AddStep("reconnect on network thread", () => change = Task.Run(() => connection.Value = true));
            AddUntilStep("reconnect completed without thread exception", () => change.IsCompletedSuccessfully);
            AddWaitStep("process scheduled UI change", 3);
            AddStep("unbind connection", () => connection.UnbindAll());
        }

        [Test]
        public void TestNotificationCounter()
        {
            setNotifications(1);
            setNotifications(2);
            setNotifications(3);
            setNotifications(0);
            setNotifications(144);

            void setNotifications(int count)
                => AddStep($"set notification count to {count}",
                    () => unreadNotificationCount.Value = count);
        }

        [Test]
        public void TestToolbarIconsRenderWithoutLinkedOverlays()
        {
            AddAssert("unregistered overlays are absent", () => toolbar.ChildrenOfType<ToolbarSettingsButton>().Single().StateContainer == null
                && toolbar.ChildrenOfType<ToolbarRankingsButton>().Single().StateContainer == null
                && toolbar.ChildrenOfType<ToolbarSupporterQuestsButton>().Single().StateContainer == null);
            AddAssert("every overlay button has a visible icon", () => toolbar.ChildrenOfType<ToolbarOverlayToggleButton>().Where(button => button is not ToolbarUserButton).All(button =>
                button.ChildrenOfType<ConstrainedIconContainer>().Any(container => container.Alpha > 0
                    && container.Icon is osu.Framework.Graphics.Sprites.SpriteIcon icon && icon.IsLoaded && icon.IsPresent)));
            AddAssert("quests and library stay adjacent", () =>
            {
                var buttons = toolbar.ChildrenOfType<FillFlowContainer>().Single(flow => flow.Children.Any(b => b is ToolbarSupporterQuestsButton)).Children.ToArray();
                int quests = System.Array.FindIndex(buttons, b => b is ToolbarSupporterQuestsButton);
                return buttons[quests + 1] is ToolbarThemeLibraryButton;
            });
        }

        [Test]
        public void TestMemoryDisplayComesAfterToolbarIcons()
        {
            AddAssert("memory display is last right-side item", () =>
            {
                var rightItems = toolbar.ChildrenOfType<FillFlowContainer>()
                                        .Single(flow => flow.Children.Any(child => child is ToolbarMemoryDisplay))
                                        .Children;

                return rightItems.Last() is ToolbarMemoryDisplay;
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestRulesetSwitchingShortcut(bool toolbarHidden)
        {
            ToolbarRulesetSelector rulesetSelector = null;

            if (toolbarHidden)
                AddStep("hide toolbar", () => toolbar.Hide());

            AddStep("retrieve ruleset selector", () => rulesetSelector = toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single());

            for (int i = 0; i < 4; i++)
            {
                var expected = rulesets.AvailableRulesets.ElementAt(i);
                var numberKey = Key.Number1 + i;

                AddStep($"switch to ruleset {i} via shortcut", () =>
                {
                    InputManager.PressKey(Key.ControlLeft);
                    InputManager.Key(numberKey);
                    InputManager.ReleaseKey(Key.ControlLeft);
                });

                AddUntilStep("ruleset switched", () => rulesetSelector.Current.Value.Equals(expected));
            }
        }

        [TestCase(OverlayActivation.All)]
        [TestCase(OverlayActivation.Disabled)]
        public void TestButtonKeyboardInputRespectsOverlayActivation(OverlayActivation mode)
        {
            AddStep($"set activation mode to {mode}", () => toolbar.OverlayActivationMode.Value = mode);
            AddStep("hide toolbar", () => toolbar.Hide());

            if (mode == OverlayActivation.Disabled)
                AddAssert("check buttons not accepting input", () => InputManager.NonPositionalInputQueue.OfType<ToolbarButton>().Count(), () => Is.Zero);
            else
                AddAssert("check buttons accepting input", () => InputManager.NonPositionalInputQueue.OfType<ToolbarButton>().Count(), () => Is.Not.Zero);
        }

        [TestCase(OverlayActivation.All)]
        [TestCase(OverlayActivation.Disabled)]
        public void TestRespectsOverlayActivation(OverlayActivation mode)
        {
            AddStep($"set activation mode to {mode}", () => toolbar.OverlayActivationMode.Value = mode);
            AddStep("hide toolbar", () => toolbar.Hide());
            AddStep("try to show toolbar", () => toolbar.Show());

            if (mode == OverlayActivation.Disabled)
                AddAssert("toolbar still hidden", () => toolbar.State.Value == Visibility.Hidden);
            else
                AddAssert("toolbar is visible", () => toolbar.State.Value == Visibility.Visible);
        }

        [Test]
        public void TestAutoHideCanRevealManuallyHiddenToolbar()
        {
            AddStep("manually hide toolbar", () =>
            {
                InputManager.PressKey(Key.ControlLeft);
                InputManager.Key(Key.T);
                InputManager.ReleaseKey(Key.ControlLeft);
            });
            AddAssert("toolbar hidden", () => toolbar.State.Value == Visibility.Hidden);

            AddStep("enable auto-hide and reveal", () =>
            {
                toolbar.AutoHideActive = true;
                toolbar.Show();
            });
            AddAssert("toolbar revealed", () => toolbar.State.Value == Visibility.Visible);
        }

        [Test]
        public void TestScrollInput()
        {
            OsuScrollContainer scroll = null;

            AddStep("add scroll layer", () => Add(scroll = new OsuScrollContainer
            {
                Depth = 1f,
                RelativeSizeAxes = Axes.Both,
                Child = new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = DrawHeight * 2,
                    Colour = ColourInfo.GradientVertical(Color4.Gray, Color4.DarkGray),
                }
            }));

            AddStep("hover toolbar", () => InputManager.MoveMouseTo(toolbar));
            AddStep("perform scroll", () => InputManager.ScrollVerticalBy(500));
            AddAssert("not scrolled", () => scroll.Current == 0);
        }

        [Test]
        public void TestVolumeControlViaMusicButtonScroll()
        {
            AddStep("hover toolbar music button", () => InputManager.MoveMouseTo(this.ChildrenOfType<ToolbarMusicButton>().Single()));

            AddStep("reset volume", () => Audio.Volume.Value = 1);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("scroll down", () => InputManager.ScrollVerticalBy(-10), 5);
            AddAssert("volume lowered down", () => Audio.Volume.Value < 1);
            AddRepeatStep("scroll up", () => InputManager.ScrollVerticalBy(10), 5);
            AddAssert("volume raised up", () => Audio.Volume.Value == 1);

            AddStep("move mouse away", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddAssert("button not hovered", () => !this.ChildrenOfType<ToolbarMusicButton>().Single().IsHovered);

            AddStep("set volume to 0.5", () => Audio.Volume.Value = 0.5);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("scroll down", () => InputManager.ScrollVerticalBy(-10), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
            AddRepeatStep("scroll up", () => InputManager.ScrollVerticalBy(10), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
        }

        [Test]
        public void TestVolumeControlViaMusicButtonArrowKeys()
        {
            AddStep("hover toolbar music button", () => InputManager.MoveMouseTo(this.ChildrenOfType<ToolbarMusicButton>().Single()));

            AddStep("reset volume", () => Audio.Volume.Value = 1);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("arrow down", () => InputManager.Key(Key.Down), 5);
            AddAssert("volume lowered down", () => Audio.Volume.Value < 1);
            AddRepeatStep("arrow up", () => InputManager.Key(Key.Up), 5);
            AddAssert("volume raised up", () => Audio.Volume.Value == 1);

            AddStep("hide volume overlay", () => volumeOverlay.Hide());
            AddStep("move mouse away", () => InputManager.MoveMouseTo(Vector2.Zero));
            AddAssert("button not hovered", () => !this.ChildrenOfType<ToolbarMusicButton>().Single().IsHovered);

            AddStep("set volume", () => Audio.Volume.Value = 0.5);
            AddStep("hide volume overlay", () => volumeOverlay.Hide());

            AddRepeatStep("arrow down", () => InputManager.Key(Key.Down), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
            AddRepeatStep("arrow up", () => InputManager.Key(Key.Up), 5);
            AddAssert("volume not changed", () => Audio.Volume.Value == 0.5);
        }

        [Test]
        public void TestRulesetSelectorOverflow()
        {
            AddStep("set toolbar width", () =>
            {
                toolbar.RelativeSizeAxes = Axes.None;
                toolbar.Width = 400;
            });
            AddStep("move mouse over rankings button", () =>
            {
                var button = toolbar.ChildrenOfType<ToolbarRankingsButton>().Single();
                InputManager.MoveMouseTo(button);
            });
            AddAssert("no ruleset toggle buttons hovered", () => !toolbar.ChildrenOfType<ToolbarRulesetTabButton>().Any(button => button.IsHovered));
            AddUntilStep("toolbar gradient visible", () => toolbar.ChildrenOfType<Toolbar.ToolbarBackground>().Single().Children.All(d => d.Alpha > 0));
        }

        [Test]
        public void TestTopBarVariantsSwitchingAndGeometry()
        {
            AddStep("enable custom theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);

            AddStep("set variant to Flat", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Flat;
                theme.TopBarRadius = 10f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("variant is Flat", () => toolbar.CurrentVariant == TopBarVariant.Flat);
            AddAssert("height is 40", () => toolbar.Height == Toolbar.HEIGHT);
            AddAssert("content not masked", () => !toolbar.ContentContainer.Masking);
            AddAssert("pods not masked", () => !toolbar.LeftPod.Masking && !toolbar.RightPod.Masking);

            AddStep("set variant to Rounded with 14px radius", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Rounded;
                theme.TopBarRadius = 14f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("variant is Rounded", () => toolbar.CurrentVariant == TopBarVariant.Rounded);
            AddAssert("height is 40", () => toolbar.Height == Toolbar.HEIGHT);
            AddAssert("content masked", () => toolbar.ContentContainer.Masking);
            AddAssert("content corner radius is 14", () => toolbar.ContentContainer.CornerRadius == 14f);
            AddAssert("content height is 54", () => toolbar.ContentContainer.Height == 40f + 14f);
            AddAssert("pods not masked", () => !toolbar.LeftPod.Masking && !toolbar.RightPod.Masking);

            AddStep("change Rounded radius to 20px", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarRadius = 20f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("content corner radius is 20", () => toolbar.ContentContainer.CornerRadius == 20f);
            AddAssert("content height is 60", () => toolbar.ContentContainer.Height == 40f + 20f);

            AddStep("set variant to Floating with 16px radius", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Floating;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("variant is Floating", () => toolbar.CurrentVariant == TopBarVariant.Floating);
            AddAssert("height is 58", () => toolbar.Height == Toolbar.FLOATING_HEIGHT);
            AddAssert("wrapper has floating padding", () =>
                toolbar.WrapperContainer.Padding.Top == Toolbar.FLOATING_TOP_MARGIN &&
                toolbar.WrapperContainer.Padding.Left == Toolbar.FLOATING_HORIZONTAL_MARGIN);
            AddAssert("content masked", () => toolbar.ContentContainer.Masking);
            AddAssert("content corner radius is 16", () => toolbar.ContentContainer.CornerRadius == 16f);
            AddAssert("pods not masked", () => !toolbar.LeftPod.Masking && !toolbar.RightPod.Masking);

            AddStep("set variant to Attached with 12px radius", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Attached;
                theme.TopBarRadius = 12f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("variant is Attached", () => toolbar.CurrentVariant == TopBarVariant.Attached);
            AddAssert("height is 40", () => toolbar.Height == Toolbar.HEIGHT);
            AddAssert("content not masked", () => !toolbar.ContentContainer.Masking);
            AddAssert("left pod masked with 12px radius", () => toolbar.LeftPod.Masking && toolbar.LeftPod.CornerRadius == 12f);
            AddAssert("right pod masked with 12px radius", () => toolbar.RightPod.Masking && toolbar.RightPod.CornerRadius == 12f);
            AddAssert("left pod height is 52", () => toolbar.LeftPod.Height == 40f + 12f);
            AddAssert("right pod height is 52", () => toolbar.RightPod.Height == 40f + 12f);

            AddStep("reset variant to Flat", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Flat;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("variant restored to Flat", () => toolbar.CurrentVariant == TopBarVariant.Flat);
            AddAssert("content not masked after reset", () => !toolbar.ContentContainer.Masking);
            AddAssert("pods not masked after reset", () => !toolbar.LeftPod.Masking && !toolbar.RightPod.Masking);
            AddStep("restore default theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [TestCase(800f)]
        [TestCase(1366f)]
        [TestCase(1920f)]
        public void TestTopBarVariantsAcrossScreenWidths(float screenWidth)
        {
            AddStep("enable custom theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);

            AddStep($"set screen width to {screenWidth}", () =>
            {
                toolbar.RelativeSizeAxes = Axes.None;
                toolbar.Width = screenWidth;
            });

            foreach (var variant in new[] { TopBarVariant.Flat, TopBarVariant.Rounded, TopBarVariant.Attached, TopBarVariant.Floating })
            {
                AddStep($"set variant to {variant}", () =>
                {
                    var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                    theme.TopBarVariant = variant;
                    theme.TopBarRadius = 12f;
                    OverlayColourProvider.CustomTheme.Value = theme;
                });

                AddAssert($"toolbar has width {screenWidth}", () => toolbar.DrawWidth == screenWidth);

                AddAssert("left home button inside toolbar horizontal bounds", () =>
                {
                    var homeBtn = toolbar.ChildrenOfType<ToolbarHomeButton>().Single();
                    return homeBtn.IsPresent &&
                           homeBtn.ScreenSpaceDrawQuad.AABBFloat.Left >= toolbar.ScreenSpaceDrawQuad.AABBFloat.Left &&
                           homeBtn.ScreenSpaceDrawQuad.AABBFloat.Right <= toolbar.ScreenSpaceDrawQuad.AABBFloat.Right;
                });

                AddAssert("right user button inside toolbar horizontal bounds", () =>
                {
                    var userBtn = toolbar.ChildrenOfType<ToolbarUserButton>().Single();
                    return userBtn.IsPresent &&
                           userBtn.ScreenSpaceDrawQuad.AABBFloat.Left >= toolbar.ScreenSpaceDrawQuad.AABBFloat.Left &&
                           userBtn.ScreenSpaceDrawQuad.AABBFloat.Right <= toolbar.ScreenSpaceDrawQuad.AABBFloat.Right;
                });
            }

            AddStep("restore full width and default theme", () =>
            {
                toolbar.RelativeSizeAxes = Axes.X;
                toolbar.Width = 1f;
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default;
            });
        }

        [Test]
        public void TestToolbarButtonsDecoupledFromContentContainerMasking()
        {
            AddStep("enable custom theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("set variant to Floating with 16px radius", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Floating;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("contentContainer is masked", () => toolbar.ContentContainer.Masking);
            AddAssert("buttons not inside contentContainer to avoid tooltip clipping", () =>
                !toolbar.ContentContainer.ChildrenOfType<FillFlowContainer>().Any());
            AddStep("restore default theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [Test]
        public void TestToolbarHiddenVariantSwitchKeepsOffset()
        {
            AddStep("hide toolbar", () =>
            {
                toolbar.Hide();
                toolbar.FinishTransforms();
            });
            AddAssert("toolbar Y is -HEIGHT", () => toolbar.Y == -Toolbar.HEIGHT);

            AddStep("enable custom theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("switch to Floating while hidden", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Floating;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("toolbar Y is -FLOATING_HEIGHT while hidden", () => toolbar.Y == -Toolbar.FLOATING_HEIGHT);

            AddStep("switch to Flat while hidden", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Flat;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("toolbar Y is -HEIGHT while hidden", () => toolbar.Y == -Toolbar.HEIGHT);

            AddStep("show toolbar", () =>
            {
                toolbar.Show();
                toolbar.FinishTransforms();
            });
            AddAssert("toolbar Y is 0 when shown", () => toolbar.Y == 0);
            AddStep("restore default theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [Test]
        public void TestFloatingToolbarHoverGradientBounds()
        {
            AddStep("enable custom theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("set variant to Floating", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Floating;
                theme.TopBarRadius = 12f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            AddAssert("toolbar background padding has horizontal 12", () =>
            {
                var bg = toolbar.ChildrenOfType<Toolbar.ToolbarBackground>().Single();
                return bg.Padding.Left == 12 && bg.Padding.Right == 12;
            });

            AddAssert("gradient draw width matches toolbar draw width minus margins", () =>
            {
                var bg = toolbar.ChildrenOfType<Toolbar.ToolbarBackground>().Single();
                var gradient = bg.GradientBox;
                return Precision.AlmostEquals(gradient.DrawWidth, toolbar.DrawWidth - 24f, 1f);
            });

            AddAssert("gradient does not overflow toolbar right boundary", () =>
            {
                var bg = toolbar.ChildrenOfType<Toolbar.ToolbarBackground>().Single();
                var gradient = bg.GradientBox;
                return gradient.ScreenSpaceDrawQuad.AABBFloat.Right <= toolbar.ScreenSpaceDrawQuad.AABBFloat.Right + 0.5f;
            });

            AddStep("set variant to Flat", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Flat;
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            AddAssert("toolbar background padding is 0 for Flat", () =>
            {
                var bg = toolbar.ChildrenOfType<Toolbar.ToolbarBackground>().Single();
                return bg.Padding.Left == 0 && bg.Padding.Right == 0;
            });

            AddAssert("gradient draw width matches toolbar draw width for Flat", () =>
            {
                var bg = toolbar.ChildrenOfType<Toolbar.ToolbarBackground>().Single();
                var gradient = bg.GradientBox;
                return Precision.AlmostEquals(gradient.DrawWidth, toolbar.DrawWidth, 1f);
            });

            AddStep("restore default theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [Test]
        public void TestTopBarAccentAndHoverReactiveBinding()
        {
            AddStep("enable custom theme mode with TopBar accent and hover", () =>
            {
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom;
                var theme = new CustomThemeDefinition();
                theme.SetContextOverride(ThemeSurfaceContext.TopBar, SurfaceColourRole.Accent, Colour4.FromHex("#FF2288"));
                theme.SetContextOverride(ThemeSurfaceContext.TopBar, SurfaceColourRole.ControlHover, Colour4.FromHex("#22FF88"));
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            AddStep("set top bar surface", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetContextOverride(ThemeSurfaceContext.TopBar, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#48352D"));
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("ruleset selector background follows TopBar surface", () =>
                toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single().BackgroundColour == Colour4.FromHex("#48352D"));
            AddStep("change only background", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value.DeepClone();
                theme.SetContextOverride(ThemeSurfaceContext.TopBar, SurfaceColourRole.SurfaceBackground, Colour4.FromHex("#23415A"));
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("selector background updates reactively", () =>
                toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single().BackgroundColour == Colour4.FromHex("#23415A"));
            AddAssert("ruleset selector line matches TopBar accent", () =>
            {
                var selector = toolbar.ChildrenOfType<ToolbarRulesetSelector>().Single();
                var line = selector.ChildrenOfType<Circle>().Single();
                return line.Parent != null && ((Colour4)line.Parent.Colour) == Colour4.FromHex("#FF2288");
            });

            AddStep("restore default theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [Test]
        public void TestDownwardRoundedToolbarGeometry()
        {
            AddStep("enable custom theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("set variant to DownwardRounded with 16 radius", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.DownwardRounded;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            // 1. Height reservation must remain standard 40px
            AddAssert("toolbar layout height remains 40px", () => toolbar.Height == Toolbar.HEIGHT);
            AddAssert("toolbar wrapper padding is zero", () => toolbar.WrapperContainer.Padding.Equals(new MarginPadding(0)));
            AddAssert("contentContainer offset is zero", () => toolbar.ContentContainer.Y == 0f);
            AddAssert("no edge shadow outline applied", () => toolbar.ContentContainer.EdgeEffect.Type == EdgeEffectType.None);
            AddAssert("contentContainer has no convex corner radius", () => toolbar.ContentContainer.CornerRadius == 0f);
            AddAssert("contentContainer masking is false", () => !toolbar.ContentContainer.Masking);

            // 2. Structural presence of downward fillets
            AddAssert("left fillet is visible with correct dimensions", () =>
            {
                var fillet = toolbar.LeftDownwardFillet;
                return fillet != null && fillet.Alpha > 0 && fillet.DrawWidth == 16f && fillet.DrawHeight == 16f;
            });
            AddAssert("right fillet is visible with correct dimensions", () =>
            {
                var fillet = toolbar.RightDownwardFillet;
                return fillet != null && fillet.Alpha > 0 && fillet.DrawWidth == 16f && fillet.DrawHeight == 16f;
            });

            // 3. Screen-space pixel / geometry discrimination vs Rounded
            // Point A (x = 2, y = 40 + 2): Inside downward ear -> filled, not transparent
            AddAssert("point (2, 42) is inside left fillet quad and not transparent", () =>
            {
                var screenPos = toolbar.ToScreenSpace(new Vector2(2, Toolbar.HEIGHT + 2));
                return toolbar.LeftDownwardFillet.ScreenSpaceDrawQuad.Contains(screenPos)
                       && !toolbar.IsInverseCornerTransparentAt(new Vector2(2, Toolbar.HEIGHT + 2));
            });

            // Point B (x = 14, y = 40 + 14): Inside concave cutout quadrant -> transparent
            AddAssert("point (14, 54) is in transparent cutout", () =>
            {
                return toolbar.IsInverseCornerTransparentAt(new Vector2(14, Toolbar.HEIGHT + 14));
            });

            // 4. Center bottom edge must NOT extend downward
            AddAssert("center point (DrawWidth/2, 45) is outside toolbar", () =>
            {
                var centerPos = toolbar.ToScreenSpace(new Vector2(toolbar.DrawWidth / 2f, Toolbar.HEIGHT + 5));
                return !toolbar.ContentContainer.ScreenSpaceDrawQuad.Contains(centerPos);
            });

            AddStep("restore default theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [Test]
        public void TestDownwardRoundedDiffersFromRoundedAtSameRadius()
        {
            AddStep("enable custom theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);

            // Verify Rounded at radius 16
            AddStep("set variant to Rounded with 16 radius", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Rounded;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            AddAssert("Rounded: contentContainer is masked", () => toolbar.ContentContainer.Masking);
            AddAssert("Rounded: contentContainer has corner radius 16", () => toolbar.ContentContainer.CornerRadius == 16f);
            AddAssert("Rounded: contentContainer has shadow", () => toolbar.ContentContainer.EdgeEffect.Type == EdgeEffectType.Shadow);
            AddAssert("Rounded: left fillet is hidden", () => toolbar.LeftDownwardFillet.Alpha == 0);
            AddAssert("Rounded: right fillet is hidden", () => toolbar.RightDownwardFillet.Alpha == 0);
            AddAssert("Rounded: point (2, 42) is outside content quad", () =>
            {
                var screenPos = toolbar.ToScreenSpace(new Vector2(2, Toolbar.HEIGHT + 2));
                return !toolbar.ContentContainer.ScreenSpaceDrawQuad.Contains(screenPos);
            });

            // Switch to DownwardRounded at same radius 16
            AddStep("set variant to DownwardRounded with 16 radius", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.DownwardRounded;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            AddAssert("DownwardRounded: contentContainer is not masked", () => !toolbar.ContentContainer.Masking);
            AddAssert("DownwardRounded: contentContainer corner radius is 0", () => toolbar.ContentContainer.CornerRadius == 0f);
            AddAssert("DownwardRounded: contentContainer has no shadow", () => toolbar.ContentContainer.EdgeEffect.Type == EdgeEffectType.None);
            AddAssert("DownwardRounded: left fillet is visible", () => toolbar.LeftDownwardFillet.Alpha > 0);
            AddAssert("DownwardRounded: right fillet is visible", () => toolbar.RightDownwardFillet.Alpha > 0);
            AddAssert("DownwardRounded: point (2, 42) is inside left fillet ear", () =>
            {
                var screenPos = toolbar.ToScreenSpace(new Vector2(2, Toolbar.HEIGHT + 2));
                return toolbar.LeftDownwardFillet.ScreenSpaceDrawQuad.Contains(screenPos)
                       && !toolbar.IsInverseCornerTransparentAt(new Vector2(2, Toolbar.HEIGHT + 2));
            });
            AddAssert("DownwardRounded: point (14, 54) is in transparent cutout", () =>
            {
                return toolbar.IsInverseCornerTransparentAt(new Vector2(14, Toolbar.HEIGHT + 14));
            });

            AddStep("restore default theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [Test]
        public void TestDownwardRoundedRadiusZeroBehavesLikeFlat()
        {
            AddStep("enable custom theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);
            AddStep("set variant to DownwardRounded with 0 radius", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.DownwardRounded;
                theme.TopBarRadius = 0f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });

            AddAssert("toolbar layout height remains 40px", () => toolbar.Height == Toolbar.HEIGHT);
            AddAssert("left fillet is hidden", () => toolbar.LeftDownwardFillet.Alpha == 0);
            AddAssert("right fillet is hidden", () => toolbar.RightDownwardFillet.Alpha == 0);
            AddAssert("contentContainer is not masked", () => !toolbar.ContentContainer.Masking);
            AddAssert("contentContainer corner radius is 0", () => toolbar.ContentContainer.CornerRadius == 0f);
            AddAssert("contentContainer has no shadow", () => toolbar.ContentContainer.EdgeEffect.Type == EdgeEffectType.None);

            AddStep("restore default theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [Test]
        public void TestDownwardRoundedLiveSwitching()
        {
            AddStep("enable custom theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Custom);

            // 1. Flat
            AddStep("set variant to Flat", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Flat;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("Flat: fillets hidden", () => toolbar.LeftDownwardFillet.Alpha == 0 && toolbar.RightDownwardFillet.Alpha == 0);

            // 2. Switch to DownwardRounded
            AddStep("switch to DownwardRounded", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.DownwardRounded;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("DownwardRounded: fillets visible", () => toolbar.LeftDownwardFillet.Alpha > 0 && toolbar.RightDownwardFillet.Alpha > 0);

            // 3. Switch to Rounded
            AddStep("switch to Rounded", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Rounded;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("Rounded: fillets hidden", () => toolbar.LeftDownwardFillet.Alpha == 0 && toolbar.RightDownwardFillet.Alpha == 0);

            // 4. Switch back to DownwardRounded
            AddStep("switch back to DownwardRounded", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.DownwardRounded;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("DownwardRounded: fillets visible again", () => toolbar.LeftDownwardFillet.Alpha > 0 && toolbar.RightDownwardFillet.Alpha > 0);

            // 5. Switch to Attached
            AddStep("switch to Attached", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Attached;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("Attached: fillets hidden", () => toolbar.LeftDownwardFillet.Alpha == 0 && toolbar.RightDownwardFillet.Alpha == 0);

            // 6. Switch to Floating
            AddStep("switch to Floating", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.Floating;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("Floating: fillets hidden", () => toolbar.LeftDownwardFillet.Alpha == 0 && toolbar.RightDownwardFillet.Alpha == 0);

            // 7. Switch back to DownwardRounded
            AddStep("switch back to DownwardRounded", () =>
            {
                var theme = OverlayColourProvider.CustomTheme.Value?.DeepClone() ?? new CustomThemeDefinition();
                theme.TopBarVariant = TopBarVariant.DownwardRounded;
                theme.TopBarRadius = 16f;
                OverlayColourProvider.CustomTheme.Value = theme;
            });
            AddAssert("DownwardRounded: fillets visible once more", () => toolbar.LeftDownwardFillet.Alpha > 0 && toolbar.RightDownwardFillet.Alpha > 0);

            AddStep("restore default theme mode", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        public partial class TestToolbar : Toolbar
        {
            public new Bindable<OverlayActivation> OverlayActivationMode => base.OverlayActivationMode as Bindable<OverlayActivation>;
        }

        // interface mocks break hot reload, mocking this stub implementation instead works around it.
        // see: https://github.com/moq/moq4/issues/1252
        [UsedImplicitly]
        public class TestNotificationOverlay : INotificationOverlay
        {
            public virtual void Post(Notification notification)
            {
            }

            public virtual void Hide()
            {
            }

            public virtual IBindable<int> UnreadCount { get; } = new Bindable<int>();

            public IEnumerable<Notification> AllNotifications => Enumerable.Empty<Notification>();
        }
    }
}
