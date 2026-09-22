using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Testing;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Chat;
using osu.Game.Overlays;
using osu.Game.Overlays.Chat;
using osu.Game.Overlays.Settings;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Edit.Checks;
using osu.Game.Rulesets.Edit.Checks.Components;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.Edit;
using osu.Game.Screens.Edit.Components;
using osu.Game.Screens.Edit.Components.RadioButtons;
using osu.Game.Screens.Edit.Components.TernaryButtons;
using osu.Game.Screens.Edit.Compose.Components.Timeline;
using osu.Game.Screens.Edit.Verify;
using osu.Game.Screens.Footer;
using osu.Game.Screens.Select;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneLiveThemeReactivity : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Green);

        [TearDownSteps]
        public void TearDown()
        {
            AddStep("reset theme to default", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [Test]
        public void TestCoreControlsLiveThemeSwitching()
        {
            RoundedButton button = null!;
            ShowMoreButton showMore = null!;
            LabelledTextBox textBox = null!;
            Nub nub = null!;

            AddStep("reset theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);

            AddStep("create controls", () =>
            {
                Child = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 15),
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        button = new RoundedButton
                        {
                            Text = "Action Button",
                            Width = 200,
                            Height = 40,
                        },
                        showMore = new ShowMoreButton
                        {
                            Text = "Show More",
                        },
                        textBox = new LabelledTextBox
                        {
                            Label = "Sample Field",
                            Width = 300,
                        },
                        nub = new Nub
                        {
                            Width = 60,
                            Height = 30,
                        },
                    }
                };
            });

            AddUntilStep("wait for controls to load", () => button.IsLoaded && showMore.IsLoaded && textBox.IsLoaded && nub.IsLoaded);

            AddAssert("initial theme is default", () => OverlayColourProvider.CurrentTheme.Value == ThemeMode.Default);
            AddAssert("initial button text matches foreground contrast", () =>
                ((Colour4)button.ChildrenOfType<SpriteText>().First().Colour) == (Colour4)OsuColour.ForegroundTextColourFor(button.BackgroundColour));
            AddAssert("initial showMore text matches provider foreground1", () =>
                showMore.ChildrenOfType<SpriteText>().First().Colour == colourProvider.Foreground1);
            AddAssert("initial nub accent matches highlight1", () => nub.AccentColour == colourProvider.Highlight1);

            // Switch to Light Theme
            AddStep("switch to light theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);

            AddAssert("content1 is dark in light theme", () => ((Colour4)colourProvider.Content1).ToHSL().Z < 0.4f);
            AddAssert("background4 is light in light theme", () => ((Colour4)colourProvider.Background4).ToHSL().Z > 0.6f);
            AddAssert("showMore text updated to light theme foreground1", () => showMore.ChildrenOfType<SpriteText>().First().Colour == colourProvider.Foreground1);
            AddAssert("nub accent matches light highlight1", () => nub.AccentColour == colourProvider.Highlight1);
            AddAssert("button text contrast matches light background", () =>
                ((Colour4)button.ChildrenOfType<SpriteText>().First().Colour) == (Colour4)OsuColour.ForegroundTextColourFor(button.BackgroundColour));

            // Switch to Dark Theme
            AddStep("switch to dark theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Dark);

            AddAssert("content1 is monochrome in dark theme", () =>
            {
                var col = (Colour4)colourProvider.Content1;
                return col.R == col.G && col.G == col.B;
            });
            AddAssert("background4 is monochrome in dark theme", () =>
            {
                var col = (Colour4)colourProvider.Background4;
                return col.R == col.G && col.G == col.B;
            });
            AddAssert("showMore text updated to dark theme foreground1", () => showMore.ChildrenOfType<SpriteText>().First().Colour == colourProvider.Foreground1);
            AddAssert("button text is light in dark theme", () =>
                ((Colour4)button.ChildrenOfType<SpriteText>().First().Colour) == (Colour4)OsuColour.ForegroundTextColourFor(button.BackgroundColour) &&
                ((Colour4)button.ChildrenOfType<SpriteText>().First().Colour).ToHSL().Z > 0.8f);

            // Restore Default Theme
            AddStep("restore default theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);

            AddAssert("showMore text restored to default foreground1", () => showMore.ChildrenOfType<SpriteText>().First().Colour == colourProvider.Foreground1);
            AddAssert("nub accent restored to default highlight1", () => nub.AccentColour == colourProvider.Highlight1);
            AddAssert("button text restored to contrast", () =>
                ((Colour4)button.ChildrenOfType<SpriteText>().First().Colour) == (Colour4)OsuColour.ForegroundTextColourFor(button.BackgroundColour));
        }

        [Test]
        public void TestForegroundContrastCalculation()
        {
            AddAssert("dark background produces light text", () =>
                OsuColour.ForegroundTextColourFor(Colour4.Black).Equals(OsuColour.Gray(0.9f)));
            AddAssert("light background produces dark text", () =>
                OsuColour.ForegroundTextColourFor(Colour4.White).Equals(OsuColour.Gray(0.2f)));
            AddAssert("yellow background produces dark text", () =>
                OsuColour.ForegroundTextColourFor(Colour4.Yellow).Equals(OsuColour.Gray(0.2f)));
            AddAssert("navy background produces light text", () =>
                OsuColour.ForegroundTextColourFor(Colour4.Navy).Equals(OsuColour.Gray(0.9f)));
        }

        [Test]
        public void TestScreenFooterButtonActiveContrast()
        {
            ScreenFooterButton footerButton = null!;

            AddStep("reset theme to default", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddStep("create footer button", () =>
            {
                Child = footerButton = new ScreenFooterButton
                {
                    Text = "Mods",
                    AccentColour = Colour4.FromHex("#b2ff66"),
                    Width = 120,
                    Action = () => { },
                };
            });

            AddUntilStep("wait for button loaded", () => footerButton.IsLoaded);
            AddStep("set active", () => footerButton.OverlayState.Value = Visibility.Visible);
            AddUntilStep("active text colour contrasts with bright accent", () =>
            {
                var text = footerButton.ChildrenOfType<SpriteText>().First(t => t.Text == "Mods");
                return ((Colour4)text.Colour) == (Colour4)OsuColour.ForegroundTextColourFor(footerButton.AccentColour);
            });

            AddStep("switch to light theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);
            AddUntilStep("active text colour still contrasts with bright accent in light theme", () =>
            {
                var text = footerButton.ChildrenOfType<SpriteText>().First(t => t.Text == "Mods");
                return ((Colour4)text.Colour) == (Colour4)OsuColour.ForegroundTextColourFor(footerButton.AccentColour);
            });
        }

        [Test]
        public void TestTextBoxLiveThemeSwitching()
        {
            FocusedTextBox focusedBox = null!;
            OutlinedTextBox outlinedBox = null!;
            OsuTextBox osuBox = null!;

            AddStep("reset theme to default", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddStep("create textboxes", () =>
            {
                Child = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 15),
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        focusedBox = new FocusedTextBox
                        {
                            Width = 250,
                            PlaceholderText = "Search...",
                        },
                        outlinedBox = new OutlinedTextBox
                        {
                            Width = 250,
                            PlaceholderText = "Outlined input...",
                        },
                        osuBox = new OsuTextBox
                        {
                            Width = 250,
                            Text = "Sample text",
                        },
                    }
                };
            });

            AddUntilStep("wait for textboxes to load", () => focusedBox.IsLoaded && outlinedBox.IsLoaded && osuBox.IsLoaded);

            // Switch to Light Theme
            AddStep("switch to light theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);

            AddUntilStep("outlined textbox border matches light theme", () =>
                outlinedBox.BorderColour == colourProvider.Light4.Opacity(0.5f));
            AddUntilStep("focused textbox border matches highlight1", () =>
                focusedBox.BorderColour == colourProvider.Highlight1);

            // Switch to Dark Theme
            AddStep("switch to dark theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Dark);

            AddUntilStep("focused textbox border matches dark highlight1", () =>
                focusedBox.BorderColour == colourProvider.Highlight1);

            // Restore Default Theme
            AddStep("restore default theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [Test]
        public void TestCarouselPanelGroupLiveThemeSwitching()
        {
            PanelGroup panelGroup = null!;

            AddStep("reset theme to default", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddStep("create panel group", () =>
            {
                Child = panelGroup = new PanelGroup
                {
                    Item = new CarouselItem(new GroupDefinition('A', "Group A")),
                    Width = 300,
                };
            });

            AddUntilStep("wait for panel group to load", () => panelGroup.IsLoaded);
            AddAssert("initial count text is white", () => panelGroup.CountTextColour == Color4.White);
            AddAssert("initial background is Background5", () => panelGroup.ModernBackgroundColour == colourProvider.Background5);

            // Switch to Light Theme
            AddStep("switch to light theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);

            AddUntilStep("count text stays white in light theme", () => panelGroup.CountTextColour == Color4.White);
            AddUntilStep("background updates to light Background5", () => panelGroup.ModernBackgroundColour == colourProvider.Background5);
            AddUntilStep("triangles update to light theme gradient", () =>
                panelGroup.TrianglesColour.Equals(ColourInfo.GradientHorizontal(colourProvider.Background6, colourProvider.Background5)));

            // Switch to Dark Theme
            AddStep("switch to dark theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Dark);

            AddUntilStep("count text stays white in dark theme", () => panelGroup.CountTextColour == Color4.White);
            AddUntilStep("background updates to dark Background5", () => panelGroup.ModernBackgroundColour == colourProvider.Background5);
            AddUntilStep("triangles update to dark theme gradient", () =>
                panelGroup.TrianglesColour.Equals(ColourInfo.GradientHorizontal(colourProvider.Background6, colourProvider.Background5)));

            // Restore Default Theme
            AddStep("restore default theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [Test]
        public void TestPanelBeatmapSetContrastInLightTheme()
        {
            PanelBeatmapSet panelSet = null!;

            AddStep("reset theme to default", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddStep("create panel beatmap set", () =>
            {
                var beatmapSet = CreateWorkingBeatmap(Ruleset.Value).BeatmapSetInfo;
                Child = panelSet = new PanelBeatmapSet
                {
                    Item = new CarouselItem(new GroupedBeatmapSet(null, beatmapSet)),
                    Width = 400,
                };
            });

            AddUntilStep("wait for panel beatmap set to load", () => panelSet.IsLoaded);

            // Switch to Light Theme
            AddStep("switch to light theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);

            AddUntilStep("title text remains white over beatmap artwork in light theme", () => panelSet.TitleTextColour == Color4.White);
            AddUntilStep("artist text remains high-contrast white in light theme", () => panelSet.ArtistTextColour == Color4.White.Opacity(0.85f));
            AddUntilStep("chevron icon is high contrast dark against white box in light theme", () => panelSet.ChevronIconColour == colourProvider.Content1);

            // Switch to Dark Theme
            AddStep("switch to dark theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Dark);

            AddUntilStep("chevron icon is Background5 in dark theme", () => panelSet.ChevronIconColour == colourProvider.Background5);
            AddUntilStep("title text remains white in dark theme", () => panelSet.TitleTextColour == Color4.White);

            // Restore Default Theme
            AddStep("restore default theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }

        [Test]
        public void TestWedgeStatisticDifficultyContrastInLightTheme()
        {
            BeatmapTitleWedge.StatisticDifficulty statisticDifficulty = null!;

            AddStep("reset theme to default", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddStep("create statistic difficulty", () =>
            {
                Child = statisticDifficulty = new BeatmapTitleWedge.StatisticDifficulty
                {
                    Width = 200,
                    Value = new BeatmapTitleWedge.StatisticDifficulty.Data("Overall Difficulty", 5f, 5f, 10f),
                };
            });

            AddUntilStep("wait for statistic to load", () => statisticDifficulty.IsLoaded);
            AddUntilStep("track bar uses semantic Background2 in default theme", () =>
                statisticDifficulty.TrackBarColour == colourProvider.Background2);

            // Switch to Light Theme
            AddStep("switch to light theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);

            AddUntilStep("track bar uses dark contrast base in light theme", () =>
                statisticDifficulty.TrackBarColour == Color4.Black.Opacity(0.25f));
            AddAssert("track bar has contrast against light container background", () =>
            {
                float trackL = ((Colour4)statisticDifficulty.TrackBarColour).ToLinear().R * 0.2126f +
                               ((Colour4)statisticDifficulty.TrackBarColour).ToLinear().G * 0.7152f +
                               ((Colour4)statisticDifficulty.TrackBarColour).ToLinear().B * 0.0722f;
                float bgL = ((Colour4)colourProvider.Background5).ToLinear().R * 0.2126f +
                            ((Colour4)colourProvider.Background5).ToLinear().G * 0.7152f +
                            ((Colour4)colourProvider.Background5).ToLinear().B * 0.0722f;
                return Math.Abs(bgL - trackL) > 0.4f;
            });

            // Switch to Dark Theme
            AddStep("switch to dark theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Dark);

            AddUntilStep("track bar uses semantic Background2 in dark theme", () =>
                statisticDifficulty.TrackBarColour == colourProvider.Background2);

            // Restore Default Theme
            AddStep("restore default theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddUntilStep("track bar restored to default Background2", () =>
                statisticDifficulty.TrackBarColour == colourProvider.Background2);
        }

        [Test]
        public void TestFormTextBoxLiveThemeReactivity()
        {
            FormTextBox formTextBox = null!;

            AddStep("reset theme to default", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddStep("create form text box", () =>
            {
                Child = formTextBox = new FormTextBox
                {
                    Width = 300,
                    Caption = "Field Caption",
                    PlaceholderText = "Placeholder text",
                    Current = { Value = "Typed text" },
                };
            });

            AddUntilStep("wait for form text box to load", () => formTextBox.IsLoaded);
            AddAssert("inner box background is transparent", () =>
                ((Colour4)formTextBox.InnerBackgroundColour) == Colour4.Transparent && formTextBox.InnerBackgroundAlpha == 0);
            AddAssert("form text box subtree tint is white", () =>
                ((Colour4)formTextBox.SubtreeColour) == Colour4.White);
            AddAssert("placeholder text matches colourProvider Foreground1", () =>
                ((Colour4)formTextBox.PlaceholderColour) == (Colour4)colourProvider.Foreground1);
            AddAssert("typed text matches colourProvider Content1", () =>
                ((Colour4)formTextBox.TextColour) == (Colour4)colourProvider.Content1);

            // Switch to Light Theme
            AddStep("switch to light theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);

            AddUntilStep("inner box stays transparent in light theme", () =>
                ((Colour4)formTextBox.InnerBackgroundColour) == Colour4.Transparent && formTextBox.InnerBackgroundAlpha == 0);
            AddUntilStep("form text box subtree tint remains white in light theme", () =>
                ((Colour4)formTextBox.SubtreeColour) == Colour4.White);
            AddUntilStep("placeholder matches light theme Foreground1", () =>
                ((Colour4)formTextBox.PlaceholderColour) == (Colour4)colourProvider.Foreground1);
            AddUntilStep("typed text matches light theme Content1 (high contrast dark)", () =>
                ((Colour4)formTextBox.TextColour) == (Colour4)colourProvider.Content1);

            // Switch to Dark Theme
            AddStep("switch to dark theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Dark);

            AddUntilStep("typed text matches dark theme Content1", () =>
                ((Colour4)formTextBox.TextColour) == (Colour4)colourProvider.Content1);
            AddUntilStep("inner box stays transparent in dark theme", () =>
                ((Colour4)formTextBox.InnerBackgroundColour) == Colour4.Transparent && formTextBox.InnerBackgroundAlpha == 0);

            // Restore Default Theme
            AddStep("restore default theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddUntilStep("typed text restored to default Content1", () =>
                ((Colour4)formTextBox.TextColour) == (Colour4)colourProvider.Content1);
        }

        private partial class TestCarouselPanel : Panel
        {
            public override MenuItem[]? ContextMenuItems => null;
            public void TestPrepareForUse() => PrepareForUse();
            public void TestFreeAfterUse() => FreeAfterUse();
        }

        [Test]
        public void TestCarouselPanelHoverAndPoolingLiveThemeReactivity()
        {
            TestCarouselPanel panel = null!;

            AddStep("reset theme to default", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddStep("create carousel panel", () =>
            {
                Child = panel = new TestCarouselPanel
                {
                    Item = new CarouselItem(new GroupDefinition('A', "Group A")),
                    Width = 300,
                    Height = 80,
                };
            });

            AddUntilStep("wait for panel to load", () => panel.IsLoaded);
            AddAssert("initial hover layer colour matches Highlight1 10%", () =>
                panel.HoverLayerColour == colourProvider.Highlight1.Opacity(0.1f));

            // Switch to Light Theme
            AddStep("switch to light theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);

            AddUntilStep("hover layer colour updates to light Highlight1 10%", () =>
                panel.HoverLayerColour == colourProvider.Highlight1.Opacity(0.1f));

            // Simulate pooled reuse: FreeAfterUse then PrepareForUse
            AddStep("free after use", () => panel.TestFreeAfterUse());
            AddStep("prepare for use in light theme", () => panel.TestPrepareForUse());
            AddUntilStep("re-prepared panel has light Highlight1 10%", () =>
                panel.HoverLayerColour == colourProvider.Highlight1.Opacity(0.1f));

            // Switch to Dark Theme
            AddStep("switch to dark theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Dark);
            AddUntilStep("hover layer colour updates to dark Highlight1 10%", () =>
                panel.HoverLayerColour == colourProvider.Highlight1.Opacity(0.1f));

            AddStep("free after use in dark theme", () => panel.TestFreeAfterUse());
            AddStep("prepare for use in dark theme", () => panel.TestPrepareForUse());
            AddUntilStep("re-prepared panel has dark Highlight1 10%", () =>
                panel.HoverLayerColour == colourProvider.Highlight1.Opacity(0.1f));

            // Restore Default Theme
            AddStep("restore default theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddUntilStep("hover layer colour restored to default Highlight1 10%", () =>
                panel.HoverLayerColour == colourProvider.Highlight1.Opacity(0.1f));
        }

        [Test]
        public void TestChatLineAndDaySeparatorLiveThemeReactivity()
        {
            ChatLine chatLine = null!;
            DaySeparator daySeparator = null!;

            AddStep("reset theme to default", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddStep("create chat line and day separator", () =>
            {
                var msg = new Message(123)
                {
                    Sender = new APIUser { Username = "TestSender", Id = 999 },
                    Content = "Plain message text",
                    Timestamp = DateTimeOffset.Now,
                };

                Child = new FillFlowContainer
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0, 10),
                    AutoSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        daySeparator = new DaySeparator(DateTimeOffset.Now) { Width = 400 },
                        chatLine = new ChatLine(msg) { Width = 400 },
                    }
                };
            });

            AddUntilStep("wait for chat items to load", () => chatLine.IsLoaded && daySeparator.IsLoaded);
            AddAssert("chat line timestamp matches Background1", () =>
                chatLine.TimestampColour == colourProvider.Background1);
            AddAssert("chat line text matches Content1", () =>
                chatLine.ContentSpriteTexts.Any(t => t.Colour == colourProvider.Content1));
            AddAssert("day separator date text matches Content1", () =>
                daySeparator.DateTextColour == colourProvider.Content1);
            AddAssert("day separator line matches Background5", () =>
                daySeparator.LineColour == colourProvider.Background5);

            // Switch to Light Theme
            AddStep("switch to light theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);

            AddUntilStep("chat line timestamp updates to light Content2", () =>
                chatLine.TimestampColour == colourProvider.Content2);
            AddAssert("chat line timestamp has contrast against chat background4", () =>
            {
                float textL = ((Colour4)chatLine.TimestampColour).ToLinear().R * 0.2126f +
                              ((Colour4)chatLine.TimestampColour).ToLinear().G * 0.7152f +
                              ((Colour4)chatLine.TimestampColour).ToLinear().B * 0.0722f;
                float bgL = ((Colour4)colourProvider.Background4).ToLinear().R * 0.2126f +
                            ((Colour4)colourProvider.Background4).ToLinear().G * 0.7152f +
                            ((Colour4)colourProvider.Background4).ToLinear().B * 0.0722f;
                return (bgL + 0.05f) / (textL + 0.05f) > 3.0f;
            });
            AddUntilStep("chat line text updates to light Content1", () =>
                chatLine.ContentSpriteTexts.Any(t => t.Colour == colourProvider.Content1));
            AddUntilStep("day separator date text updates to light Content1", () =>
                daySeparator.DateTextColour == colourProvider.Content1);
            AddUntilStep("day separator line updates to light Background5", () =>
                daySeparator.LineColour == colourProvider.Background5);

            // Switch to Dark Theme
            AddStep("switch to dark theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Dark);

            AddUntilStep("chat line timestamp updates to dark Background1", () =>
                chatLine.TimestampColour == colourProvider.Background1);
            AddUntilStep("chat line text updates to dark Content1", () =>
                chatLine.ContentSpriteTexts.Any(t => t.Colour == colourProvider.Content1));
            AddUntilStep("day separator date text updates to dark Content1", () =>
                daySeparator.DateTextColour == colourProvider.Content1);
            AddUntilStep("day separator line updates to dark Background5", () =>
                daySeparator.LineColour == colourProvider.Background5);

            // Restore Default Theme
            AddStep("restore default theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddUntilStep("chat line timestamp restored to default Background1", () =>
                chatLine.TimestampColour == colourProvider.Background1);
        }

        private partial class TestSettingsSection : EditorRoundedScreenSettingsSection
        {
            protected override string HeaderText => "Section Title";
        }

        private partial class TestSettings : EditorRoundedScreenSettings
        {
            protected override IReadOnlyList<Drawable> CreateSections() => Array.Empty<Drawable>();
        }

        [Test]
        public void TestEditorChromeAndPanelsLiveThemeReactivity()
        {
            TableHeaderText headerText = null!;
            TestSettingsSection settingsSection = null!;
            TestSettings settings = null!;
            EditorToolButton toolButton = null!;
            EditorRadioButton radioButton = null!;
            DrawableTernaryButton ternaryButton = null!;
            TimelineButton timelineButton = null!;
            PlaybackControl playbackControl = null!;
            TimeInfoContainer timeInfo = null!;

            AddStep("reset theme to default", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddStep("create editor components", () =>
            {
                Child = new PopoverContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0, 10),
                        AutoSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            headerText = new TableHeaderText("Column Header"),
                            new Container
                            {
                                Width = 300,
                                AutoSizeAxes = Axes.Y,
                                Child = settingsSection = new TestSettingsSection(),
                            },
                            new Container
                            {
                                Size = new Vector2(300, 100),
                                Child = settings = new TestSettings(),
                            },
                            toolButton = new EditorToolButton("Tool", () => new SpriteIcon { Icon = FontAwesome.Solid.PencilAlt }, () => null) { Width = 150 },
                            radioButton = new EditorRadioButton("Radio", () => { }) { Width = 150 },
                            ternaryButton = new DrawableTernaryButton { Description = "Ternary", Width = 150 },
                            timelineButton = new TimelineButton { Icon = FontAwesome.Solid.FastForward },
                            new DependencyProvidingContainer
                            {
                                AutoSizeAxes = Axes.Both,
                                CachedDependencies = new (Type, object)[]
                                {
                                    (typeof(EditorClock), new EditorClock()),
                                    (typeof(EditorBeatmap), new EditorBeatmap(new Beatmap { BeatmapInfo = new BeatmapInfo { Ruleset = new OsuRuleset().RulesetInfo } })),
                                },
                                Child = new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 5),
                                    Children = new Drawable[]
                                    {
                                        playbackControl = new PlaybackControl { Width = 300 },
                                        timeInfo = new TimeInfoContainer { Width = 300 },
                                    }
                                },
                            },
                        }
                    }
                };
            });

            AddUntilStep("wait for editor controls to load", () =>
                headerText.IsLoaded && settingsSection.IsLoaded && settings.IsLoaded &&
                toolButton.IsLoaded && radioButton.IsLoaded && ternaryButton.IsLoaded &&
                timelineButton.IsLoaded && playbackControl.IsLoaded && timeInfo.IsLoaded);

            AddAssert("table header text matches Content2", () => headerText.Colour == colourProvider.Content2);
            AddAssert("settings section header matches Content1", () => settingsSection.HeaderTextColour == colourProvider.Content1);
            AddAssert("settings background matches Background6", () => settings.BackgroundColour == colourProvider.Background6);
            AddAssert("playback background matches Background4", () => playbackControl.BackgroundColour == colourProvider.Background4);
            AddAssert("playback label matches Content2", () => playbackControl.LabelColour == colourProvider.Content2);
            AddAssert("playback play button icon matches Light3", () => playbackControl.PlayButtonIconColour == colourProvider.Light3);
            AddAssert("time info background matches Background5", () => timeInfo.BackgroundColour == colourProvider.Background5);
            AddAssert("time info track timer matches Content1", () => timeInfo.TrackTimerColour == colourProvider.Content1);
            AddAssert("timeline button icon matches Light3", () => timelineButton.CurrentIconColour == colourProvider.Light3);

            // Switch to Light Theme
            AddStep("switch to light theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);

            AddUntilStep("table header text updates to light Content2", () => headerText.Colour == colourProvider.Content2);
            AddUntilStep("settings section header updates to light Content1", () => settingsSection.HeaderTextColour == colourProvider.Content1);
            AddUntilStep("settings background updates to light Background6", () => settings.BackgroundColour == colourProvider.Background6);
            AddUntilStep("tool button default icon colour is Light4 in light theme", () => toolButton.DefaultIconColour == colourProvider.Light4);
            AddUntilStep("tool button text colour contrasts with background", () =>
                toolButton.CurrentTextColour == OsuColour.ForegroundTextColourFor(toolButton.BackgroundColour));
            AddUntilStep("radio button default icon colour is Light4 in light theme", () => radioButton.DefaultIconColour == colourProvider.Light4);
            AddUntilStep("radio button text colour contrasts with background", () =>
                radioButton.CurrentTextColour == OsuColour.ForegroundTextColourFor(radioButton.BackgroundColour));
            AddUntilStep("ternary button default icon colour is Light4 in light theme", () => ternaryButton.DefaultIconColour == colourProvider.Light4);
            AddUntilStep("ternary button text colour contrasts with background", () =>
                ternaryButton.CurrentTextColour == OsuColour.ForegroundTextColourFor(ternaryButton.BackgroundColour));
            AddUntilStep("playback background updates to light Background4", () => playbackControl.BackgroundColour == colourProvider.Background4);
            AddUntilStep("playback label updates to light Content2", () => playbackControl.LabelColour == colourProvider.Content2);
            AddUntilStep("playback play button icon matches light Light3", () => playbackControl.PlayButtonIconColour == colourProvider.Light3);
            AddAssert("playback play button icon contrasts with background", () =>
            {
                float iconL = ((Colour4)playbackControl.PlayButtonIconColour).ToLinear().R * 0.2126f +
                              ((Colour4)playbackControl.PlayButtonIconColour).ToLinear().G * 0.7152f +
                              ((Colour4)playbackControl.PlayButtonIconColour).ToLinear().B * 0.0722f;
                float bgL = ((Colour4)playbackControl.BackgroundColour).ToLinear().R * 0.2126f +
                            ((Colour4)playbackControl.BackgroundColour).ToLinear().G * 0.7152f +
                            ((Colour4)playbackControl.BackgroundColour).ToLinear().B * 0.0722f;
                return (bgL + 0.05f) / (iconL + 0.05f) > 3.0f;
            });
            AddUntilStep("time info background updates to light Background5", () => timeInfo.BackgroundColour == colourProvider.Background5);
            AddUntilStep("time info track timer updates to light Content1", () => timeInfo.TrackTimerColour == colourProvider.Content1);
            AddAssert("time info track timer contrasts with background", () =>
            {
                float textL = ((Colour4)timeInfo.TrackTimerColour).ToLinear().R * 0.2126f +
                              ((Colour4)timeInfo.TrackTimerColour).ToLinear().G * 0.7152f +
                              ((Colour4)timeInfo.TrackTimerColour).ToLinear().B * 0.0722f;
                float bgL = ((Colour4)timeInfo.BackgroundColour).ToLinear().R * 0.2126f +
                            ((Colour4)timeInfo.BackgroundColour).ToLinear().G * 0.7152f +
                            ((Colour4)timeInfo.BackgroundColour).ToLinear().B * 0.0722f;
                return (bgL + 0.05f) / (textL + 0.05f) > 4.5f;
            });
            AddUntilStep("timeline button icon matches light Light3", () => timelineButton.CurrentIconColour == colourProvider.Light3);

            // Switch to Dark Theme
            AddStep("switch to dark theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Dark);

            AddUntilStep("table header text updates to dark Content2", () => headerText.Colour == colourProvider.Content2);
            AddUntilStep("settings section header updates to dark Content1", () => settingsSection.HeaderTextColour == colourProvider.Content1);
            AddUntilStep("tool button default icon colour is Darken(0.5) in dark theme", () =>
                toolButton.DefaultIconColour == toolButton.DefaultBackgroundColour.Darken(0.5f));
            AddUntilStep("playback background updates to dark Background4", () => playbackControl.BackgroundColour == colourProvider.Background4);
            AddUntilStep("playback play button icon matches dark Light3", () => playbackControl.PlayButtonIconColour == colourProvider.Light3);
            AddUntilStep("time info background updates to dark Background5", () => timeInfo.BackgroundColour == colourProvider.Background5);
            AddUntilStep("time info track timer updates to dark Content1", () => timeInfo.TrackTimerColour == colourProvider.Content1);

            // Restore Default Theme
            AddStep("restore default theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddUntilStep("settings background restored to default Background6", () => settings.BackgroundColour == colourProvider.Background6);
            AddUntilStep("time info background restored to default Background5", () => timeInfo.BackgroundColour == colourProvider.Background5);
            AddUntilStep("time info track timer restored to default Content1", () => timeInfo.TrackTimerColour == colourProvider.Content1);
        }

        [Test]
        public void TestIssueTableAndDrawableIssueLiveThemeReactivity()
        {
            IssueTable issueTable = null!;
            Issue sampleIssue = null!;

            AddStep("reset theme to default", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
            AddStep("create issue table with unassigned pooled rows", () =>
            {
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(EditorClock), new EditorClock()),
                        (typeof(EditorBeatmap), new EditorBeatmap(new Beatmap { BeatmapInfo = new BeatmapInfo { Ruleset = new OsuRuleset().RulesetInfo } })),
                    },
                    Child = issueTable = new IssueTable
                    {
                        RelativeSizeAxes = Axes.Both,
                    }
                };
            });

            AddUntilStep("wait for issue table to load (50 pooled items with null Current.Value)", () => issueTable.IsLoaded);

            // Switch to Light Theme with unassigned pooled items - tests P0 NRE guard
            AddStep("switch to light theme (pooled null items update without NRE)", () =>
                OverlayColourProvider.CurrentTheme.Value = ThemeMode.Light);

            AddStep("add sample issue to table", () =>
            {
                sampleIssue = new Issue(new IssueTemplate(new CheckZeroByteFiles(), IssueType.Problem, "Zero byte audio file: {0}"), "test.mp3");
                issueTable.Issues.Add(sampleIssue);
            });

            AddUntilStep("wait for row to display", () =>
                issueTable.ChildrenOfType<IssueTable.DrawableIssue>().Any(d => d.Current.Value == sampleIssue));

            AddAssert("drawable issue text colours use light theme tokens", () =>
            {
                var row = issueTable.ChildrenOfType<IssueTable.DrawableIssue>().First(d => d.Current.Value == sampleIssue);
                return row.DetailTextColour == colourProvider.Content1 &&
                       row.TimestampTextColour == colourProvider.Content1 &&
                       row.CategoryTextColour == colourProvider.Content2;
            });

            // Switch to Dark Theme
            AddStep("switch to dark theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Dark);

            AddUntilStep("drawable issue text colours update to dark theme tokens", () =>
            {
                var row = issueTable.ChildrenOfType<IssueTable.DrawableIssue>().First(d => d.Current.Value == sampleIssue);
                return row.DetailTextColour == colourProvider.Content1 &&
                       row.TimestampTextColour == colourProvider.Content1 &&
                       row.CategoryTextColour == colourProvider.Content2;
            });

            // Restore Default Theme
            AddStep("restore default theme", () => OverlayColourProvider.CurrentTheme.Value = ThemeMode.Default);
        }
    }
}
