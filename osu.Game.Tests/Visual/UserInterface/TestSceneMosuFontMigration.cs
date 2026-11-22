using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Fonts;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Settings;
using osu.Game.Overlays.Settings.Sections.Fork;

namespace osu.Game.Tests.Visual.UserInterface
{
    [HeadlessTest]
    public partial class TestSceneMosuFontMigration : OsuTestScene
    {
        [Resolved]
        private FontStore fonts { get; set; } = null!;

        private OsuConfigManager config = null!;
        private OsuSpriteText normal = null!;
        private OsuSpriteText asyncText = null!;

        protected override ITestSceneTestRunner CreateRunner() => new OsuTestSceneTestRunner { LoadFonts = true };

        [BackgroundDependencyLoader]
        private void load()
        {
            Dependencies.Cache(config = new OsuConfigManager(LocalStorage));
            Dependencies.Cache(new OverlayColourProvider(OverlayColourScheme.Blue));
        }

        [Test]
        public void TestRealClientFontRegistrationAndRussianLayout()
        {
            AddStep("create real UI labels", () => Child = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    normal = new OsuSpriteText { Text = "Русский ёж Україна — Tiếng Việt 0123456789", Font = OsuFont.Default.With(size: 24) },
                    asyncText = new OsuSpriteText { AsyncText = "Асинхронный ёж 123", Font = OsuFont.TorusAlternate.With(size: 24) },
                }
            });
            AddUntilStep("both labels loaded and published", () => normal.IsLoaded && asyncText.Text.ToString() == "Асинхронный ёж 123");
            AddAssert("labels have positive geometry", () => normal.DrawWidth > 0 && asyncText.DrawWidth > 0 && normal.LineBaseHeight > 0);
            AddAssert("old UI baseline retained at size 24", () => System.Math.Abs(normal.LineBaseHeight - 24 * 0.84f) < 0.01f);
            Task? check = null;
            AddStep("inspect actual registered fonts off Update", () => check = Task.Run(() =>
            {
                Assert.That(fonts.Get("Torus-Regular", 'H')!.Height, Is.EqualTo(0.60f).Within(0.00001));
                Assert.That(fonts.Get("Torus-Alternate-Regular", 'H')!.Height, Is.EqualTo(0.60f).Within(0.00001));
                Assert.That(fonts.Get(string.Empty, 'ё')!.XAdvance, Is.EqualTo(fonts.Get("Nunito-Regular", 'ё')!.XAdvance));
                foreach (string weight in new[] { "Light", "Regular", "SemiBold", "Bold" })
                {
                    foreach (string family in new[] { "Torus", "Torus-Alternate" })
                    {
                        string asset = MosuFontResources.LegacyFaces[$"Fonts/{family}/{family}-{weight}"];
                        string realName = asset[(asset.LastIndexOf('/') + 1)..];
                        Assert.That(fonts.Get(family + "-" + weight, 'ё')!.XAdvance, Is.EqualTo(fonts.Get(realName, 'ё')!.XAdvance));
                    }
                }
                foreach (string family in new[] { "Nunito", "MosuRoundedAlt", "Syncopate", "Michroma", "Inter" })
                    foreach (FontWeight weight in System.Enum.GetValues<FontWeight>())
                        Assert.That(fonts.Get($"{family}-{weight}", '1'), Is.Not.Null, $"{family}-{weight}");
                Assert.That(fonts.Get("Venera-Light", '1')!.XAdvance, Is.EqualTo(fonts.Get("Syncopate-Regular", '1')!.XAdvance));
                Assert.That(fonts.Get("Venera-Bold", '1')!.XAdvance, Is.EqualTo(fonts.Get("Michroma-Regular", '1')!.XAdvance));
                Assert.That(fonts.Get("Venera-Black", '1')!.XAdvance, Is.EqualTo(fonts.Get("Michroma-Regular", '1')!.XAdvance));
                Assert.That(fonts.Get("Michroma-Regular", 'ё'), Is.Null);
                Assert.That(fonts.Get(string.Empty, 'ё'), Is.Not.Null);
            }));
            AddUntilStep("font registration checked", () => check!.IsCompleted);
            AddAssert("registration assertions passed", () => { check!.GetAwaiter().GetResult(); return true; });
            AddStep("select new family in common mode", () =>
            {
                config.SetValue(OsuSetting.ForkUseSeparateFonts, false);
                config.SetValue(OsuSetting.ForkCustomUIFont, "Nunito");
            });
            AddUntilStep("async custom label rebuilt", () => asyncText.Font.Family == "Nunito" && asyncText.Text.ToString() == "Асинхронный ёж 123");
            AddAssert("own font remains selected for Russian", () => normal.Font.Family == "Nunito" && normal.DrawWidth > 0);
        }

        [Test]
        public void TestMissingCyrillicMatchesFallbackSpriteGeometry()
        {
            OsuSpriteText display = null!;
            OsuSpriteText reference = null!;
            AddStep("load display font Cyrillic and reference", () => Child = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    display = new OsuSpriteText { Text = "Русский ёж", Font = OsuFont.Numeric.With(size: 24), UseFullGlyphHeight = false },
                    reference = new OsuSpriteText { Text = "Русский ёж", Font = OsuFont.GetFont("Nunito", 24, FontWeight.Bold), UseFullGlyphHeight = false },
                }
            });
            AddUntilStep("labels loaded", () => display.IsLoaded && reference.IsLoaded);
            AddAssert("same ink height", () => Math.Abs(display.DrawHeight - reference.DrawHeight) < 0.01f);
            AddAssert("same baseline", () => Math.Abs(display.LineBaseHeight - reference.LineBaseHeight) < 0.01f);
            // The display font supplies its own spaces. Compare an uninterrupted
            // Cyrillic word for advance/kerning rather than replacing those spaces.
            AddStep("compare word widths", () => display.Text = reference.Text = "Русский");
            AddAssert("same Cyrillic width", () => Math.Abs(display.DrawWidth - reference.DrawWidth) < 0.01f);
        }

        [Test]
        public void TestNonLatinGlyphSizesThroughClientFontStore()
        {
            Task? check = null;
            AddStep("measure real script glyphs", () => check = Task.Run(() =>
            {
                foreach (string family in new[] { "Torus", "Torus-Alternate", "Venera", "Syncopate", "Michroma", "Inter" })
                foreach (string weight in new[] { "Light", "Regular", "Bold" })
                {
                    var font = new FontUsage(family, 24, weight);
                    var builder = new osu.Framework.Text.TextBuilder(fonts, font);
                    builder.AddText("HНxхРРусский 日本語 한글");
                    if (family.StartsWith("Torus", StringComparison.Ordinal))
                        Assert.That(builder.Characters[1].Height, Is.EqualTo(builder.Characters[0].Height).Within(0.3), font.FontName);
                    foreach (var glyph in builder.Characters.Where(g => g.Character is 'Н' or 'х' or 'Р'))
                    {
                        // Display faces have no Cyrillic: TextBuilder requests
                        // "-Weight" before the empty family, not plain "Weight".
                        var expected = fonts.Get(font.FontName, glyph.Character)
                                       ?? fonts.Get(weight == "Light" ? "Nunito-Light" : weight == "Bold" ? "Nunito-Bold" : "Nunito-Regular", glyph.Character)!;
                        Assert.That(glyph.Height, Is.EqualTo(expected.Height * 24).Within(0.0001), font.FontName + glyph.Character);
                        Assert.That(glyph.Baseline, Is.EqualTo(expected.Baseline * 24).Within(0.0001), font.FontName + glyph.Character);
                    }
                    foreach (var glyph in builder.Characters.Where(g => g.Character is '日' or '本' or '한'))
                        Assert.That(glyph.Height, Is.InRange(13f, 16f), font.FontName + glyph.Character);
                }
            }));
            AddUntilStep("script measurements complete", () => check!.IsCompleted);
            AddAssert("script metrics matched", () => { check!.GetAwaiter().GetResult(); return true; });
        }

        [Test]
        public void TestPortableArchiveRegistersRealGlyphsAndPreservesMetrics()
        {
            FontPackLibrary.Validated? pack = null;
            Task? operation = null;
            AddStep("export and import portable default roles", () =>
            {
                var game = Dependencies.Get<OsuGameBase>();
                operation = Task.Run(async () =>
                {
                    using var stream = new MemoryStream();
                    FontPackExporter.Export(stream, new FontConfiguration(), LocalStorage, Array.Empty<FontPackLibrary.Validated>());
                    stream.Position = 0;
                    pack = FontPackLibrary.Validate(stream);
                    stream.Position = 0;
                    await game.ImportFontPack(stream);
                });
            });
            AddUntilStep("package imported", () => operation!.IsCompleted);
            AddAssert("import succeeded", () => { operation!.GetAwaiter().GetResult(); return true; });
            AddAssert("imported display names use actual replacement families", () =>
            {
                var game = Dependencies.Get<OsuGameBase>();
                return game.FontPackDisplayName(pack!.InstalledFamily("Torus")).StartsWith("Nunito · ", StringComparison.Ordinal)
                       && game.FontPackDisplayName(pack.InstalledFamily("Torus-Alternate")).StartsWith("MosuRoundedAlt / Nunito · ", StringComparison.Ordinal)
                       && game.FontPackDisplayName(pack.InstalledFamily("Venera")).StartsWith("Syncopate / Michroma · ", StringComparison.Ordinal);
            });
            AddStep("compare actual imported atlas glyphs", () => operation = Task.Run(() =>
            {
                foreach (string family in new[] { "Torus", "Torus-Alternate", "Venera", "Inter" })
                foreach (string weight in new[] { "Light", "Regular", "SemiBold", "Bold" })
                foreach (char character in "HlxёЯ123")
                {
                    var original = fonts.Get(family + "-" + weight, character);
                    var imported = fonts.Get(pack!.InstalledFamily(family) + "-" + weight, character);
                    Assert.That(imported == null, Is.EqualTo(original == null), family + weight + character);
                    if (original == null) continue;
                    Assert.That(imported!.Texture, Is.Not.Null);
                    Assert.That(imported.XAdvance, Is.EqualTo(original.XAdvance).Within(0.00001), family + weight + character);
                    Assert.That(imported.Height, Is.EqualTo(original.Height).Within(0.00001), family + weight + character);
                    Assert.That(imported.Baseline, Is.EqualTo(original.Baseline).Within(0.00001), family + weight + character);
                }
            }));
            AddUntilStep("glyph comparison complete", () => operation!.IsCompleted);
            AddAssert("portable appearance preserved", () => { operation!.GetAwaiter().GetResult(); return true; });
            AddStep("apply separate imported fonts live", () =>
            {
                pack!.InstalledSettings().Apply(config);
                Child = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both, Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        normal = new OsuSpriteText { Text = "Русский ёж Latin", Font = OsuFont.Torus.With(size: 24) },
                        asyncText = new OsuSpriteText { AsyncText = "Heading Заголовок", Font = OsuFont.TorusAlternate.With(size: 24, weight: FontWeight.Bold) },
                    }
                };
            });
            AddUntilStep("imported async text visible", () => asyncText.IsLoaded && asyncText.Text.ToString() == "Heading Заголовок");
            AddAssert("role families stay independent", () => normal.Font.Family == pack!.InstalledFamily("Torus") && asyncText.Font.Family == pack.InstalledFamily("Torus-Alternate"));
        }

        [Test]
        public void TestExplicitFamiliesRetainWeightFallbackAfterImport()
        {
            Task? check = null;
            FontPackLibrary.Validated? explicitPack = null;
            AddStep("import all explicit UI families", () =>
            {
                var game = Dependencies.Get<OsuGameBase>();
                check = Task.Run(async () =>
                {
                    using var stream = new MemoryStream();
                    FontPackExporter.Export(stream, new FontConfiguration
                    {
                        Common = "Inter", Main = "Nunito", Alternate = "MosuRoundedAlt", Numeric = "Syncopate", Inter = "Michroma"
                    }, LocalStorage, Array.Empty<FontPackLibrary.Validated>());
                    stream.Position = 0;
                    var pack = FontPackLibrary.Validate(stream);
                    stream.Position = 0;
                    string importPath = LocalStorage.GetFullPath("test-import.MORAFN");
                    await File.WriteAllBytesAsync(importPath, stream.ToArray());
                    Assert.That(game.HandledExtensions, Does.Contain(".morafn"));
                    await game.Import(importPath);
                    explicitPack = pack;
                });
            });
            AddUntilStep("explicit package imported via file handler", () => check!.IsCompleted);
            AddAssert("file handler succeeded", () => { check!.GetAwaiter().GetResult(); return true; });
            AddStep("repeat import", () =>
            {
                var game = Dependencies.Get<OsuGameBase>();
                int count = OsuGameBase.AvailableCustomUIFonts.Count;
                check = Task.Run(async () =>
                {
                    await game.Import(LocalStorage.GetFullPath("test-import.MORAFN"));
                    Assert.That(OsuGameBase.AvailableCustomUIFonts.Count, Is.EqualTo(count));
                });
            });
            AddUntilStep("repeat import complete", () => check!.IsCompleted);
            AddAssert("repeat import succeeded", () => { check!.GetAwaiter().GetResult(); return true; });
            AddStep("compare explicit imported glyphs", () => check = Task.Run(() =>
            {
                    var pack = explicitPack!;
                    foreach (string family in new[] { "Inter", "Nunito", "MosuRoundedAlt", "Syncopate", "Michroma" })
                    foreach (FontWeight weight in Enum.GetValues<FontWeight>())
                    foreach (string style in new[] { "", "Italic" })
                    foreach (char c in "H1lё")
                    {
                        var original = fonts.Get($"{family}-{weight}{style}", c);
                        var imported = fonts.Get($"{pack.InstalledFamily(family)}-{weight}{style}", c);
                        Assert.That(imported == null, Is.EqualTo(original == null), family + weight + style + c);
                        if (original != null)
                            Assert.That(imported!.XAdvance, Is.EqualTo(original.XAdvance).Within(0.00001), family + weight + style + c);
                    }

            }));
            AddUntilStep("all imported faces checked", () => check!.IsCompleted);
            AddAssert("explicit family weights preserved", () => { check!.GetAwaiter().GetResult(); return true; });
            AddStep("export active imported selection", () =>
            {
                var export = Dependencies.Get<OsuGameBase>().ExportFontPack();
                check = Task.Run(async () =>
                {
                    string filename = await export;
                    using var exported = File.OpenRead(filename);
                    Assert.That(FontPackLibrary.Validate(exported).Manifest.Families.Count, Is.EqualTo(5));
                });
            });
            AddUntilStep("runtime export complete", () => check!.IsCompleted);
            AddAssert("runtime export validated", () => { check!.GetAwaiter().GetResult(); return true; });
        }

        [TestCase(14)]
        [TestCase(18)]
        [TestCase(24)]
        [TestCase(32)]
        public void TestLegacyFontSizeKeepsActualSpriteTextGeometry(float size)
        {
            OsuSpriteText torus = null!;
            OsuSpriteText alternate = null!;
            AddStep("create cap probes", () => Child = new FillFlowContainer
            {
                AutoSizeAxes = Axes.Both,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    torus = new OsuSpriteText { Text = "H", Font = OsuFont.Torus.With(size: size), UseFullGlyphHeight = false },
                    alternate = new OsuSpriteText { Text = "H", Font = OsuFont.TorusAlternate.With(size: size), UseFullGlyphHeight = false },
                }
            });
            AddUntilStep("probes loaded", () => torus.IsLoaded && alternate.IsLoaded);
            AddAssert("Torus cap uses balanced UI scale", () => System.Math.Abs(torus.DrawHeight - size * 0.60f) < 0.01f);
            AddAssert("Alternate cap uses balanced UI scale", () => System.Math.Abs(alternate.DrawHeight - size * 0.60f) < 0.01f);
            // Ink-height mode strips the glyph's top bearing (TextBuilderGlyph),
            // unlike the full-height labels above. Expected values use raw H metrics.
            AddAssert("ink-relative baselines respect scaled bearings", () =>
                System.Math.Abs(torus.LineBaseHeight - size * (102 - 29) * 0.60f / 75) < 0.01f
                && System.Math.Abs(alternate.LineBaseHeight - size * (89 - 10) * 0.60f / 80) < 0.01f);
        }

        [Test]
        public void TestFittedLabelWidthsAndLowercaseAcrossWeights()
        {
            Task? check = null;
            AddStep("compare measured legacy label widths", () => check = Task.Run(() =>
            {
                // Advances including kerning from original atlases at size 100.
                // Numeric fixtures avoid distributing the restricted originals.
                var samples = new[]
                {
                    ("Torus-Light", 16.89f), ("Torus-Regular", 17.02f),
                    ("Torus-SemiBold", 17.18f), ("Torus-Bold", 17.23f),
                    ("Torus-Alternate-Light", 16.88f), ("Torus-Alternate-Regular", 16.96f),
                    ("Torus-Alternate-SemiBold", 17.16f), ("Torus-Alternate-Bold", 17.22f),
                };
                const string text = "Current server: Settings Options 0123456789";
                foreach (var (name, previousWidth) in samples)
                {
                    float width = 0;
                    osu.Framework.Text.ITexturedCharacterGlyph? previous = null;
                    foreach (char c in text)
                    {
                        var glyph = fonts.Get(name, c)!;
                        width += glyph.XAdvance + (previous == null ? 0 : glyph.GetKerning(previous));
                        previous = glyph;
                    }
                    Assert.That(width / previousWidth, Is.InRange(0.94f, 1.10f), name);
                    Assert.That(fonts.Get(name, 'x')!.Height, Is.InRange(0.41f, 0.44f), name);
                }
            }));
            AddUntilStep("metric comparison completed", () => check!.IsCompleted);
            AddAssert("widths and lowercase remain close", () => { check!.GetAwaiter().GetResult(); return true; });
        }

        [Test]
        public void TestLetterSpacingAcrossRealFontWeights()
        {
            Task? check = null;
            AddStep("check close letter pairs and tracking", () => check = Task.Run(() =>
            {
                foreach (string family in new[] { "Torus", "Torus-Alternate", "Nunito", "MosuRoundedAlt" })
                foreach (string weight in new[] { "Light", "Regular", "SemiBold", "Bold" })
                {
                    string name = family + "-" + weight;
                    var left = fonts.Get(name, 'l')!;
                    var right = fonts.Get(name, 'l')!;
                    float gap = left.XAdvance + right.GetKerning(left) + right.XOffset - left.XOffset - left.Width;
                    Assert.That(gap, Is.GreaterThanOrEqualTo(0.025f), name);
                    // Width and coverage remain untouched by tracking.
                    Assert.That(fonts.Get(name, 'H')!.Height, Is.EqualTo(0.60f).Within(0.00001));
                }
            }));
            AddUntilStep("spacing check completed", () => check!.IsCompleted);
            AddAssert("letters retain breathing room", () => { check!.GetAwaiter().GetResult(); return true; });
        }

        [Test]
        public void TestFontChoicesUseRealNamesAndPreserveLegacySelection()
        {
            FontSettingsPanel panel = null!;
            AddStep("load settings with saved compatibility family", () =>
            {
                config.SetValue(OsuSetting.ForkUseSeparateFonts, false);
                config.SetValue(OsuSetting.ForkCustomUIFont, "Torus");
                Child = panel = new FontSettingsPanel();
            });
            AddUntilStep("font selectors loaded", () => panel.IsLoaded && panel.ChildrenOfType<FormDropdown<string>>().Count() == 5);
            AddAssert("file association is not a manual settings action", () => panel.ChildrenOfType<SettingsButtonV2>().Count() == 3);
            AddAssert("saved alias is not reset by opening settings", () => config.Get<string>(OsuSetting.ForkCustomUIFont) == "Torus");
            AddAssert("other selectors only offer actual families", () => panel.ChildrenOfType<FormDropdown<string>>()
                .Where(d => d.Current.Value != "Torus").All(d => !d.Items.Any(FontConfiguration.IsLegacyAlias)));
            AddStep("apply another saved theme alias while settings open", () => config.SetValue(OsuSetting.ForkMainFont, "Venera"));
            AddUntilStep("theme alias retained as compatibility choice", () => panel.ChildrenOfType<FormDropdown<string>>().Any(d => d.Current.Value == "Venera" && d.Items.Contains("Venera")));
            AddAssert("labels never claim original fonts are installed", () =>
            {
                var texts = panel.ChildrenOfType<SpriteText>().Select(t => t.Text.ToString()).ToArray();
                return !texts.Any(t => t.Contains("Torus", StringComparison.Ordinal) || t.Contains("Venera", StringComparison.Ordinal))
                       && texts.Any(t => t.Contains("Nunito", StringComparison.Ordinal));
            });
            AddStep("restore configuration", () =>
            {
                config.SetValue(OsuSetting.ForkCustomUIFont, "Default");
                config.SetValue(OsuSetting.ForkMainFont, "Default");
                config.SetValue(OsuSetting.ForkUseSeparateFonts, true);
            });
        }

        [Test]
        public void TestRussianFontFixControlIsGoneFromBothSettingsLocations()
        {
            ForkInterfaceSettings original = null!;
            ThemeInterfaceSection theme = null!;
            AddStep("create settings", () => Child = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[] { original = new ForkInterfaceSettings(), theme = new ThemeInterfaceSection() }
            });
            AddUntilStep("settings loaded", () => original.IsLoaded && theme.IsLoaded);
            AddAssert("no obsolete checkbox or favorite", () =>
                !original.ChildrenOfType<SettingsItemV2>().Any(i => i.FavouriteId == "ui:ForkInterfaceSettings:russianFontFixCheckbox")
                && !original.ChildrenOfType<FormCheckBox>().Any(c => c.Caption.ToString().Contains("Russian font fix"))
                && !theme.ChildrenOfType<FormCheckBox>().Any(c => c.Caption.ToString().Contains("Russian font fix")));
        }
    }
}
