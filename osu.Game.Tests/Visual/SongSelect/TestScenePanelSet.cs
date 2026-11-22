// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Layout;
using osu.Framework.Testing;
using osu.Framework.Text;
using osu.Framework.Utils;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Configuration;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Carousel;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.Cursor;
using osu.Game.Overlays;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using osu.Game.Tests.Beatmaps;
using osu.Game.Tests.Visual.UserInterface;
using osuTK;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestScenePanelSet : ThemeComparisonTestScene
    {
        [Resolved]
        private BeatmapManager beatmaps { get; set; } = null!;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private BeatmapSetInfo beatmapSet = null!;
        // Only supplies the screen identity checked by stable-style panel bindings.
        // Never added to the drawable tree or loaded as a screen in this fixture.
        private readonly SoloSongSelect panelSongSelect = new SoloSongSelect();

        public TestScenePanelSet()
            : base(false)
        {
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            beatmapSet = beatmaps.GetAllUsableBeatmapSets().FirstOrDefault(b => b.OnlineID == 241526)
                         ?? beatmaps.GetAllUsableBeatmapSets().FirstOrDefault(b => !b.Protected)
                         ?? TestResources.CreateTestBeatmapSetInfo();
        });

        [Test]
        public void TestDisplay()
        {
            AddStep("display", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
        }

        [Test]
        public void TestSpreadReusesDotsAcrossCarouselRecycling()
        {
            PanelBeatmapSet.SpreadDisplay spread = null!;
            BeatmapSetInfo first = null!;
            BeatmapSetInfo second = null!;
            int dots = 0;
            AddStep("display", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("spread loaded", () => this.ChildrenOfType<PanelBeatmapSet.SpreadDisplay>().Any(s => s.CreatedDifficultyDots > 0));
            AddStep("capture spread", () =>
            {
                spread = this.ChildrenOfType<PanelBeatmapSet.SpreadDisplay>().First(s => s.CreatedDifficultyDots > 0);
                first = spread.BeatmapSet.Value!;
                second = first.Detach();
                dots = spread.CreatedDifficultyDots;
            });
            AddStep("recycle repeatedly", () =>
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var update = typeof(PanelBeatmapSet.SpreadDisplay).GetMethod("updateBeatmapSet", flags)!;
                for (int i = 0; i < 100; i++)
                {
                    spread.BeatmapSet.Value = i % 2 == 0 ? second : first;
                    update.Invoke(spread, null);
                }
            });
            AddAssert("no new dots after recycling", () => spread.CreatedDifficultyDots == dots);
            AddStep("clear", () => spread.BeatmapSet.Value = null);
            AddWaitStep("process clear", 2);
            AddStep("restore", () => spread.BeatmapSet.Value = first);
            AddWaitStep("process restore", 2);
            AddAssert("dots reused after clear", () => spread.CreatedDifficultyDots == dots);
        }

        [Test]
        public void TestRandomBeatmap()
        {
            AddStep("random beatmap", () =>
            {
                var randomSet = beatmaps.GetAllUsableBeatmapSets().MinBy(_ => RNG.Next());
                randomSet ??= TestResources.CreateTestBeatmapSetInfo();
                beatmapSet = randomSet;

                CreateThemedContent(OverlayColourScheme.Aquamarine);
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestHiddenUpdateButtonDoesNotOffsetModernMetadata(bool oldPreviews)
        {
            PanelBeatmapSet[] panels = null!;
            PanelUpdateBeatmapButton[] buttons = null!;
            FillFlowContainer[] parents = null!;
            Vector2[][] positions = null!;

            AddStep("prepare up-to-date modern panels", () =>
            {
                config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern);
                config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, oldPreviews);
                beatmapSet = TestResources.CreateTestBeatmapSetInfo();
                beatmapSet.Status = BeatmapOnlineStatus.Graveyard;
                // Isolate geometry from concurrent cold glyph preparation in pooled labels.
                ContentContainer.Add(new OsuSpriteText
                {
                    Font = OsuFont.Style.Heading1.With(typeface: Typeface.TorusAlternate),
                    Text = beatmapSet.Metadata.Title,
                });
                ContentContainer.Add(new OsuSpriteText
                {
                    Font = OsuFont.Style.Body.With(weight: FontWeight.SemiBold),
                    Text = beatmapSet.Metadata.Artist,
                });
            });
            AddStep("display panels", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("metadata and hidden buttons ready", () =>
                this.ChildrenOfType<PanelBeatmapSet>().Any()
                && this.ChildrenOfType<PanelBeatmapSet>().All(panel =>
                    panel.ChildrenOfType<PanelUpdateBeatmapButton>().Single().Alpha == 0
                    && panel.ChildrenOfType<BeatmapSetOnlineStatusPill>().Any(pill => pill.Alpha > 0)
                    && new[] { "titleText", "artistText" }.All(name =>
                        !string.IsNullOrEmpty(((SpriteText)typeof(PanelBeatmapSet)
                            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel)!).Text.ToString()))));
            AddWaitStep("settle layout and expansion", 10);
            AddStep("capture metadata positions and remove hidden buttons", () =>
            {
                panels = this.ChildrenOfType<PanelBeatmapSet>().ToArray();
                buttons = panels.Select(panel => panel.ChildrenOfType<PanelUpdateBeatmapButton>().Single()).ToArray();
                parents = buttons.Select(button => (FillFlowContainer)button.Parent!).ToArray();
                positions = panels.Select(metadataPositions).ToArray();
                for (int i = 0; i < buttons.Length; i++)
                    parents[i].Remove(buttons[i], false);
            });
            AddWaitStep("settle without hidden buttons", 5);
            AddStep("hidden buttons must not affect title artist or status positions", () =>
            {
                Vector2[][] withoutButtons = panels.Select(metadataPositions).ToArray();
                for (int i = 0; i < buttons.Length; i++)
                    parents[i].Add(buttons[i]);

                for (int i = 0; i < panels.Length; i++)
                {
                    Vector2[] withoutButton = withoutButtons[i];
                    for (int j = 0; j < withoutButton.Length; j++)
                        Assert.That((withoutButton[j] - positions[i][j]).Length, Is.LessThan(0.01f),
                            $"panel {i}, metadata {j} moved after removing an invisible update button");
                }
            });
        }

        private static Vector2[] metadataPositions(PanelBeatmapSet panel)
            => new[] { "titleText", "artistText", "statusPill" }.Select(name =>
                ((Drawable)typeof(PanelBeatmapSet).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(panel)!).ToSpaceOfOtherDrawable(Vector2.Zero, panel)).ToArray();

        [Test]
        public void TestOldPreviewOffsetsMetadata()
        {
            AddStep("enable old previews", () => config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, true));
            AddStep("display", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("metadata clears old preview", () => this.ChildrenOfType<PanelBeatmapSet>().Any() && this.ChildrenOfType<PanelBeatmapSet>().All(panel => panel.MetadataClearsLegacyPreview));
            AddUntilStep("white backing follows rounded map edge", () => this.ChildrenOfType<PanelBeatmapSet>().Any() && this.ChildrenOfType<PanelBeatmapSet>().All(panel => panel.ExpandedBackgroundMatchesRoundedEdge));
            AddStep("disable old previews", () => config.SetValue(OsuSetting.ForkSongSelectOldCarouselPreviews, false));
        }

        [Test]
        public void TestStableCollapsedMapCardComposition()
        {
            AddStep("enable stable style", () => config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned));
            AddStep("display", () => CreateThemedContent(OverlayColourScheme.Aquamarine));
            AddUntilStep("map cards loaded", () => this.ChildrenOfType<PanelBeatmapSet>().Any());
            AddAssert("inactive map card uses stable pink tint", () => this.ChildrenOfType<PanelBeatmapSet>().Where(panel => !panel.Expanded.Value).All(panel => panel.LegacyInactiveFullCardTintIsStablePink));
            AddAssert("map card uses skinned button background", () => this.ChildrenOfType<PanelBeatmapSet>().Where(panel => !panel.Expanded.Value).All(panel => panel.LegacySkinnedButtonBackgroundVisible));
        }

        [Test]
        public void TestSelectionRefreshIsLimitedToStableStyle()
        {
            BeatmapInfo selected = null!;
            AddStep("display modern panels", () =>
            {
                config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern);
                selected = beatmapSet.Beatmaps.First();
                Beatmap.Value = new TestWorkingBeatmap(new Beatmap { BeatmapInfo = selected });
                CreateThemedContent(OverlayColourScheme.Aquamarine);
            });
            AddUntilStep("panels loaded", () => this.ChildrenOfType<PanelBeatmapSet>().Any());
            AddAssert("modern has no classic difficulty", () => this.ChildrenOfType<PanelBeatmapSet>().All(p => !p.ShowsSelectedDifficulty));
            AddStep("switch stable style", () => config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned));
            AddUntilStep("expanded stable panels show selected difficulty", () =>
                this.ChildrenOfType<PanelBeatmapSet>().Any(p => p.Expanded.Value)
                && this.ChildrenOfType<PanelBeatmapSet>().Where(p => p.Expanded.Value)
                    .All(p => p.ShowsSelectedDifficulty && p.DisplayedSelectedDifficulty == selected.DifficultyName));
            AddStep("select another set", () =>
            {
                var other = TestResources.CreateTestBeatmapSetInfo().Beatmaps.First();
                Beatmap.Value = new TestWorkingBeatmap(new Beatmap { BeatmapInfo = other });
            });
            AddAssert("stable difficulty clears for unmatched set", () => this.ChildrenOfType<PanelBeatmapSet>().All(p => !p.ShowsSelectedDifficulty));
            AddStep("return modern", () => config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern));
            AddAssert("modern remains without classic difficulty", () => this.ChildrenOfType<PanelBeatmapSet>().All(p => !p.ShowsSelectedDifficulty));
        }

        [TestCase(ForkSongSelectStyle.Modern)]
        [TestCase(ForkSongSelectStyle.LegacySkinned)]
        public void TestUnchangedPanelUpdateRetainsTextBuilder(ForkSongSelectStyle style)
        {
            AddStep("display panels", () =>
            {
                config.SetValue(OsuSetting.ForkSongSelectStyle, style);
                CreateThemedContent(OverlayColourScheme.Aquamarine);
            });
            AddUntilStep("panels loaded", () => this.ChildrenOfType<PanelBeatmapSet>().Any());
            AddUntilStep("title and artist glyph preparation finished", () => this.ChildrenOfType<PanelBeatmapSet>()
                .All(panel => new[] { "titleText", "artistText" }.All(name =>
                {
                    var text = (SpriteText)typeof(PanelBeatmapSet).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(panel)!;
                    return text.IsLoaded && !string.IsNullOrEmpty(text.Text.ToString());
                })));
            AddWaitStep("finish layout", 3);
            AddStep("unchanged Update retains title and artist layout", () =>
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                foreach (var panel in this.ChildrenOfType<PanelBeatmapSet>())
                {
                    var texts = new[] { "titleText", "artistText" }
                        .Select(name => (SpriteText)typeof(PanelBeatmapSet).GetField(name, flags)!.GetValue(panel)!).ToArray();
                    var builders = texts.Select(text =>
                    {
                        _ = text.LineBaseHeight;
                        return ((LayoutValue<TextBuilder>)typeof(SpriteText).GetField("textBuilderCache", flags)!.GetValue(text)!).Value;
                    }).ToArray();
                    // Several unchanged frames must preserve the builder even with a
                    // configured family. This is not just a FontUsage value comparison.
                    for (int frame = 0; frame < 20; frame++)
                        typeof(PanelBeatmapSet).GetMethod("Update", flags)!.Invoke(panel, null);
                    for (int i = 0; i < texts.Length; i++)
                    {
                        _ = texts[i].LineBaseHeight;
                        var cache = (LayoutValue<TextBuilder>)typeof(SpriteText).GetField("textBuilderCache", flags)!.GetValue(texts[i])!;
                        Assert.That(cache.Value, Is.SameAs(builders[i]), "unchanged panel Update must not recreate the TextBuilder");

                        // Control: explicit reassignment must still refresh the layout.
                        texts[i].Font = texts[i].Font;
                        _ = texts[i].LineBaseHeight;
                        Assert.That(cache.Value, Is.Not.SameAs(builders[i]));
                    }
                }
            });
        }

        [Test]
        public void TestPanelFontTracksCustomFamilyAndStyleChanges()
        {
            AddStep("display modern custom panels", () =>
            {
                config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern);
                config.SetValue(OsuSetting.ForkUseSeparateFonts, false);
                config.SetValue(OsuSetting.ForkCustomUIFont, "Comfortaa Regular.fnt");
                CreateThemedContent(OverlayColourScheme.Aquamarine);
            });
            AddUntilStep("panels loaded", () => this.ChildrenOfType<PanelBeatmapSet>().Any());
            AddUntilStep("custom family preserved", () => metadataFontsMatch("Comfortaa Regular.fnt", 22, 16));
            AddStep("switch stable style", () => config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.LegacySkinned));
            AddUntilStep("stable sizes keep custom family", () => metadataFontsMatch("Comfortaa Regular.fnt", StableCarouselLayout.FontPixels(16), StableCarouselLayout.FontPixels(12)));
            AddStep("change custom font", () => config.SetValue(OsuSetting.ForkCustomUIFont, "Torus"));
            AddUntilStep("new family propagated", () => metadataFontsMatch("Torus", StableCarouselLayout.FontPixels(16), StableCarouselLayout.FontPixels(12)));
            AddStep("restore default and modern", () =>
            {
                config.SetValue(OsuSetting.ForkCustomUIFont, "Default");
                config.SetValue(OsuSetting.ForkSongSelectStyle, ForkSongSelectStyle.Modern);
            });
            AddUntilStep("default families restored", () => metadataFontsMatch(null, 22, 16));
            AddStep("assign headings separately", () =>
            {
                config.SetValue(OsuSetting.ForkUseSeparateFonts, true);
                config.SetValue(OsuSetting.ForkAlternateFont, "Inter");
            });
            AddUntilStep("only heading role changed", () => this.ChildrenOfType<PanelBeatmapSet>().All(panel =>
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
                var title = (SpriteText)typeof(PanelBeatmapSet).GetField("titleText", flags)!.GetValue(panel)!;
                var artist = (SpriteText)typeof(PanelBeatmapSet).GetField("artistText", flags)!.GetValue(panel)!;
                return title.Font.Family == "Inter" && artist.Font.Family == "Torus";
            }));
            AddStep("restore heading role", () => config.SetValue(OsuSetting.ForkAlternateFont, "Default"));
        }

        private bool metadataFontsMatch(string? family, float titleSize, float artistSize)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            return this.ChildrenOfType<PanelBeatmapSet>().All(panel =>
            {
                var title = (SpriteText)typeof(PanelBeatmapSet).GetField("titleText", flags)!.GetValue(panel)!;
                var artist = (SpriteText)typeof(PanelBeatmapSet).GetField("artistText", flags)!.GetValue(panel)!;
                return title.Font.Family == (family ?? "Torus-Alternate") && artist.Font.Family == (family ?? "Torus")
                       && title.Font.Size == titleSize && artist.Font.Size == artistSize;
            });
        }

        protected override Drawable CreateContent()
        {
            return new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[] { (typeof(ISongSelect), panelSongSelect) },
                Child = new OsuContextMenuContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = new FillFlowContainer
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Width = 0.5f,
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Direction = FillDirection.Vertical,
                        Spacing = new Vector2(0f, 5f),
                        Children = new Drawable[]
                        {
                            new PanelBeatmapSet
                            {
                                Item = new CarouselItem(new GroupedBeatmapSet(null, beatmapSet))
                            },
                            new PanelBeatmapSet
                            {
                                Item = new CarouselItem(new GroupedBeatmapSet(null, beatmapSet)),
                                KeyboardSelected = { Value = true }
                            },
                            new PanelBeatmapSet
                            {
                                Item = new CarouselItem(new GroupedBeatmapSet(null, beatmapSet)),
                                Expanded = { Value = true }
                            },
                            new PanelBeatmapSet
                            {
                                Item = new CarouselItem(new GroupedBeatmapSet(null, beatmapSet)),
                                KeyboardSelected = { Value = true },
                                Expanded = { Value = true }
                            },
                        }
                    }
                }
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                panelSongSelect.Dispose();

            base.Dispose(isDisposing);
        }
    }
}
