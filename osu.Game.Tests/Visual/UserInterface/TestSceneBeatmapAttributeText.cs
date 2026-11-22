// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Models;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Skinning.Components;
using osu.Game.Skinning;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.Visual.UserInterface
{
    public partial class TestSceneBeatmapAttributeText : OsuTestScene
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private BeatmapAttributeText text = null!;
        private CalculationProbe cache = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("load isolated cache and metadata-only text", () => Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[] { (typeof(BeatmapDifficultyCache), cache = new CalculationProbe()) },
                Children = new Drawable[]
                {
                    cache,
                    text = new BeatmapAttributeText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Attribute = { Value = BeatmapAttribute.Title },
                    },
                },
            });
            AddUntilStep("cache and text loaded", () => cache.IsLoaded && text.IsLoaded);
        }

        [SetUp]
        public void Setup() => Schedule(() =>
        {
            SelectedMods.SetDefault();
            Ruleset.Value = new OsuRuleset().RulesetInfo;
            Beatmap.Value = CreateWorkingBeatmap(new TestBeatmap(new OsuRuleset().RulesetInfo)
            {
                BeatmapInfo =
                {
                    BPM = 100,
                    DifficultyName = "_Difficulty",
                    Status = BeatmapOnlineStatus.Loved,
                    Metadata =
                    {
                        Title = "_Title",
                        TitleUnicode = "_Title",
                        Artist = "_Artist",
                        ArtistUnicode = "_Artist",
                        Author = new RealmUser { Username = "_Creator" },
                        Source = "_Source",
                    },
                    Difficulty =
                    {
                        CircleSize = 1,
                        DrainRate = 2,
                        OverallDifficulty = 3,
                        ApproachRate = 4,
                    }
                }
            });
        });

        [TestCase(BeatmapAttribute.CircleSize, "Circle Size: 1")]
        [TestCase(BeatmapAttribute.HPDrain, "HP Drain: 2")]
        [TestCase(BeatmapAttribute.Accuracy, "Accuracy: 3")]
        [TestCase(BeatmapAttribute.ApproachRate, "Approach Rate: 4")]
        [TestCase(BeatmapAttribute.Title, "Title: _Title")]
        [TestCase(BeatmapAttribute.Artist, "Artist: _Artist")]
        [TestCase(BeatmapAttribute.Creator, "Creator: _Creator")]
        [TestCase(BeatmapAttribute.DifficultyName, "Difficulty: _Difficulty")]
        [TestCase(BeatmapAttribute.Source, "Source: _Source")]
        [TestCase(BeatmapAttribute.RankedStatus, "Beatmap Status: Loved")]
        [TestCase(BeatmapAttribute.BPM, "BPM: 100")]
        [TestCase(BeatmapAttribute.Length, "Length: 01:15")]
        public void TestAttributeDisplay(BeatmapAttribute attribute, string expectedText)
        {
            AddStep($"set attribute: {attribute}", () => text.Attribute.Value = attribute);
            AddAssert("check correct text", getText, () => Is.EqualTo(expectedText));
            AddAssert("metadata does not calculate SR or PP", () => cache.Starts == 0);
        }

        [Test]
        public void TestFixedTextAreaAndOptionalScrolling()
        {
            AddStep("set long text in fixed area", () =>
            {
                text.Template.Value = "A very long title that must scroll within the fixed text viewport";
                text.UseCustomSize.Value = true;
                text.Size = new osuTK.Vector2(180, 48);
            });
            AddUntilStep("fixed bounds applied", () => text.DrawWidth == 180 && text.DrawHeight == 48);
            AddAssert("overflow masked", () => text.Masking);
            AddUntilStep("text scrolls", () => text.ChildrenOfType<osu.Framework.Graphics.Containers.FillFlowContainer>().First().X < -1);
            AddStep("disable scrolling", () => text.ScrollOverflow.Value = false);
            AddUntilStep("text stays at start", () => Math.Abs(text.ChildrenOfType<osu.Framework.Graphics.Containers.FillFlowContainer>().First().X) < 0.1f);
            AddAssert("fixed bounds remain", () => text.DrawWidth == 180 && text.DrawHeight == 48);
            AddStep("roundtrip settings", () =>
            {
                using var copy = (BeatmapAttributeText)text.CreateSerialisedInfo().CreateInstance();
                Assert.That(copy.UseCustomSize.Value, Is.True);
                Assert.That(copy.AutoSizeAxes, Is.EqualTo(Axes.None));
                Assert.That(copy.Width, Is.EqualTo(180));
                Assert.That(copy.Height, Is.EqualTo(48));
                Assert.That(copy.ScrollOverflow.Value, Is.False);
            });
            AddStep("return to automatic", () =>
            {
                text.UseCustomSize.Value = false;
            });
            AddAssert("auto size restored", () => text.AutoSizeAxes, () => Is.EqualTo(Axes.Both));
            AddAssert("no unnecessary clipping", () => !text.Masking);
        }

        [Test]
        public void TestCustomSizeToggleControlsAndLegacyMigration()
        {
            AddStep("one toggle instead of dimension sliders", () =>
            {
                var properties = text.GetSettingsSourceProperties().Select(p => p.Item2.Name).ToArray();
                Assert.That(properties, Does.Contain(nameof(BeatmapAttributeText.UseCustomSize)));
                Assert.That(properties, Does.Not.Contain("TextAreaWidth"));
                Assert.That(properties, Does.Not.Contain("TextAreaHeight"));
                var size = text.DrawSize;
                text.UseCustomSize.Value = true;
                Assert.That(text.DrawSize, Is.EqualTo(size));
                Assert.That(text.AutoSizeAxes, Is.EqualTo(Axes.None));
            });
            AddStep("restore old slider format", () =>
            {
                var info = new SerialisedDrawableInfo { Type = typeof(BeatmapAttributeText) };
                info.Settings["text_area_width"] = 210;
                info.Settings["text_area_height"] = 52;
                using var copy = (BeatmapAttributeText)info.CreateInstance();
                Assert.That(copy.UseCustomSize.Value, Is.True);
                Assert.That(copy.Size, Is.EqualTo(new osuTK.Vector2(210, 52)));
                Assert.That(copy.CreateSerialisedInfo().Settings.ContainsKey("text_area_width"), Is.False);
            });
        }

        [Test]
        public void TestChangeBeatmap()
        {
            AddStep("set title attribute", () => text.Attribute.Value = BeatmapAttribute.Title);
            AddAssert("check initial title", getText, () => Is.EqualTo("Title: _Title"));

            AddStep("change to beatmap with another title", () => Beatmap.Value = CreateWorkingBeatmap(new TestBeatmap(new OsuRuleset().RulesetInfo)
            {
                BeatmapInfo =
                {
                    Metadata =
                    {
                        Title = "Another"
                    }
                }
            }));

            AddAssert("check new title", getText, () => Is.EqualTo("Title: Another"));
        }

        [Test]
        public void TestWithMods()
        {
            AddStep("set beatmap", () => Beatmap.Value = CreateWorkingBeatmap(new TestBeatmap(new OsuRuleset().RulesetInfo)
            {
                BeatmapInfo =
                {
                    BPM = 100,
                    Length = 30000,
                    Difficulty =
                    {
                        ApproachRate = 10,
                        CircleSize = 9.5f
                    }
                }
            }));

            test(BeatmapAttribute.BPM, new OsuModDoubleTime(), "BPM: 100", "BPM: 150");
            test(BeatmapAttribute.Length, new OsuModDoubleTime(), "Length: 00:30", "Length: 00:20");
            test(BeatmapAttribute.ApproachRate, new OsuModDoubleTime(), "Approach Rate: 10", "Approach Rate: 11");
            test(BeatmapAttribute.CircleSize, new OsuModHardRock(), "Circle Size: 9.5", "Circle Size: 10");

            void test(BeatmapAttribute attribute, Mod mod, string before, string after)
            {
                AddStep($"set attribute: {attribute}", () => text.Attribute.Value = attribute);
                AddAssert("check text is correct", getText, () => Is.EqualTo(before));

                AddStep("add DT mod", () => SelectedMods.Value = new[] { mod });
                AddAssert("check text is correct", getText, () => Is.EqualTo(after));
                AddStep("clear mods", () => SelectedMods.SetDefault());
            }
        }

        [Test]
        public void TestStarRating()
        {
            AddStep("set test ruleset", () => Ruleset.Value = new TestRuleset().RulesetInfo);
            AddStep("set star rating attribute", () => text.Attribute.Value = BeatmapAttribute.StarRating);
            AddAssert("check star rating is 0", getText, () => Is.EqualTo("Star Rating: 0.00"));

            // Adding mod
            TestMod mod = null!;
            AddStep("add mod with difficulty 1", () => SelectedMods.Value = new[] { mod = new TestMod { Difficulty = { Value = 1 } } });
            AddUntilStep("check star rating is 1", getText, () => Is.EqualTo("Star Rating: 1.00"));

            // Changing mod setting
            AddStep("change mod difficulty to 2", () => mod.Difficulty.Value = 2);
            AddUntilStep("check star rating is 2", getText, () => Is.EqualTo("Star Rating: 2.00"));
        }

        [Test]
        public void TestMaxPp()
        {
            AddStep("set test ruleset", () => Ruleset.Value = new TestRuleset().RulesetInfo);
            AddStep("set max pp attribute", () => text.Attribute.Value = BeatmapAttribute.MaxPP);
            AddAssert("check max pp is 0", getText, () => Is.EqualTo("Max PP: 0"));

            // Adding mod
            TestMod mod = null!;
            AddStep("add mod with pp 1", () => SelectedMods.Value = new[] { mod = new TestMod { Performance = { Value = 1 } } });
            AddUntilStep("check max pp is 1", getText, () => Is.EqualTo("Max PP: 1"));

            // Changing beatmap must immediately reset the displayed value while the new calculation is pending.
            AddStep("change beatmap and check immediate reset", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new TestBeatmap(new TestRuleset().RulesetInfo));
                Assert.That(getText(), Is.EqualTo("Max PP: 0"));
            });
            AddUntilStep("max pp updates for new beatmap", getText, () => Is.EqualTo("Max PP: 1"));

            // Changing mod setting
            AddStep("change mod pp to 2", () => mod.Performance.Value = 2);
            AddUntilStep("check max pp is 2", getText, () => Is.EqualTo("Max PP: 2"));
        }

        [TestCase("{Title} / {StarRating}", "_Title / 3.00", false)]
        [TestCase("{Title} / {MaxPP}", "_Title / 7", true)]
        [TestCase("{StarRating} / {MaxPP} / {MaxPP}", "3.00 / 7 / 7", true)]
        [TestCase("{{MaxPP}}", "{7}", true)]
        public void TestTemplateOnlyCalculationDemand(string template, string expected, bool performance)
        {
            AddStep("select test calculation inputs", () =>
            {
                Ruleset.Value = new TestRuleset().RulesetInfo;
                SelectedMods.Value = new[] { new TestMod { Difficulty = { Value = 3 }, Performance = { Value = 7 } } };
            });
            AddWaitStep("allow inputs to settle before adding consumer", 3);
            AddStep("set template", () => text.Template.Value = template);
            AddUntilStep("template receives exact calculated values", getText, () => Is.EqualTo(expected));
            AddAssert("only required tier calculated", () => cache.FullStarts == (performance ? 1 : 0) && cache.PreviewStarts == (performance ? 0 : 1));
            AddStep("formatting-only change", () => text.Template.Value = template + "!");
            AddUntilStep("formatting change uses current value", getText, () => Is.EqualTo(expected + "!"));
            AddAssert("same demand keeps existing request", () => cache.Starts == 1);
        }

        [TestCase("{maxpp}")]
        [TestCase("{MaxPPsuffix}")]
        [TestCase("{MaxPP:0}")]
        [TestCase("MaxPP StarRating")]
        public void TestUnrecognizedTemplateDoesNotCalculate(string template)
        {
            AddStep("set literal template", () => text.Template.Value = template);
            AddAssert("literal formatting preserved", getText, () => Is.EqualTo(template));
            AddStep("change map, mods and ruleset", () =>
            {
                Beatmap.Value = CreateWorkingBeatmap(new TestBeatmap(new TestRuleset().RulesetInfo));
                SelectedMods.Value = new[] { new TestMod { Performance = { Value = 7 } } };
                Ruleset.Value = new TestRuleset().RulesetInfo;
            });
            AddWaitStep("allow cache refresh", 5);
            AddAssert("no unused SR or PP requests", () => cache.Starts == 0);
        }

        [TestCase(BeatmapAttribute.StarRating, false)]
        [TestCase(BeatmapAttribute.MaxPP, true)]
        public void TestPrimaryAttributeDemandWithoutValuePlaceholder(BeatmapAttribute attribute, bool performance)
        {
            AddStep("set primary calculation attribute with title-only template", () =>
            {
                Ruleset.Value = new TestRuleset().RulesetInfo;
                text.Template.Value = "{Title}";
            });
            AddWaitStep("allow ruleset refresh before adding consumer", 3);
            AddStep("select primary attribute", () => text.Attribute.Value = attribute);
            AddUntilStep("primary calculation finishes", () => cache.Finished == 1);
            AddAssert("primary attribute tier requested", () => cache.FullStarts == (performance ? 1 : 0) && cache.PreviewStarts == (performance ? 0 : 1));
            AddAssert("title-only template unchanged", getText, () => Is.EqualTo("_Title"));
        }

        [Test]
        public void TestFullToStarOnlyDemandReusesExactCachedResult()
        {
            AddStep("set calculation inputs", () =>
            {
                Ruleset.Value = new TestRuleset().RulesetInfo;
                SelectedMods.Value = new[] { new TestMod { Difficulty = { Value = 3 }, Performance = { Value = 7 } } };
            });
            AddWaitStep("allow input refresh", 3);
            AddStep("request full result", () => text.Template.Value = "{StarRating} / {MaxPP}");
            AddUntilStep("full result visible", getText, () => Is.EqualTo("3.00 / 7"));
            AddStep("remove PP demand", () => text.Template.Value = "{StarRating}");
            AddAssert("exact cached stars visible immediately", getText, () => Is.EqualTo("3.00"));
            AddStep("restore PP demand", () => text.Template.Value = "{StarRating} / {MaxPP}");
            AddAssert("exact cached PP visible immediately", getText, () => Is.EqualTo("3.00 / 7"));
            AddWaitStep("allow cache publications", 3);
            AddAssert("no preview or duplicate full calculation", () => cache.FullStarts == 1 && cache.PreviewStarts == 0);
        }

        [Test]
        public void TestTemplateRemovalCancelsPendingRequest()
        {
            AddStep("set gated inputs", () =>
            {
                Ruleset.Value = new TestRuleset().RulesetInfo;
                cache.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            });
            AddWaitStep("allow ruleset refresh", 3);
            AddStep("add template-only PP demand", () => text.Template.Value = "{MaxPP}");
            AddUntilStep("full pending", () => cache.FullStarts == 1);
            AddStep("remove template placeholder", () => text.Template.Value = "{Title}");
            AddUntilStep("unused full canceled", () => cache.Cancelled == 1 && cache.Finished == 1);
            AddStep("release obsolete full", () => cache.Gate!.SetResult());
            AddWaitStep("allow obsolete publication", 3);
            AddAssert("only title displayed", getText, () => Is.EqualTo("_Title"));
            AddAssert("no new calculation", () => cache.Starts == 1);
        }

        [Test]
        public void TestDemandChangesCancelPendingRequestsAndCanReactivate()
        {
            AddStep("gate star-only request", () =>
            {
                Ruleset.Value = new TestRuleset().RulesetInfo;
                cache.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            });
            AddWaitStep("allow ruleset refresh before adding consumer", 3);
            AddStep("select stars", () => text.Attribute.Value = BeatmapAttribute.StarRating);
            AddUntilStep("preview pending", () => cache.PreviewStarts == 1);
            AddStep("upgrade demand to PP", () => text.Attribute.Value = BeatmapAttribute.MaxPP);
            AddUntilStep("preview canceled, full pending", () => cache.Cancelled == 1 && cache.FullStarts == 1);
            AddStep("remove calculation demand", () => text.Attribute.Value = BeatmapAttribute.Title);
            AddUntilStep("both obsolete requests canceled", () => cache.Cancelled == 2 && cache.Finished == 2);
            AddStep("release obsolete requests and refresh inputs", () =>
            {
                cache.Gate!.SetResult();
                SelectedMods.Value = new[] { new TestMod { Performance = { Value = 7 } } };
            });
            AddWaitStep("allow refresh", 5);
            AddAssert("unused canceled requests never restarted", () => cache.Starts == 2);
            AddAssert("no stale difficulty published", getText, () => Is.EqualTo("Title: _Title"));
            AddStep("reactivate PP via template", () => text.Template.Value = "{MaxPP}");
            AddUntilStep("new PP calculated", getText, () => Is.EqualTo("7"));
            AddAssert("reactivation requested full tier", () => cache.FullStarts == 2);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestPendingMapChangeAndDisposalCancelCalculation(bool dispose)
        {
            AddStep("request gated PP", () =>
            {
                Ruleset.Value = new TestRuleset().RulesetInfo;
                cache.Gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            });
            AddWaitStep("allow ruleset refresh before adding consumer", 3);
            AddStep("select PP", () => text.Attribute.Value = BeatmapAttribute.MaxPP);
            AddUntilStep("old full pending", () => cache.FullStarts == 1);
            AddStep("replace map", () => Beatmap.Value = CreateWorkingBeatmap(new TestBeatmap(new TestRuleset().RulesetInfo)));
            AddUntilStep("old full canceled and replacement pending", () => cache.Cancelled == 1 && cache.FullStarts == 2);
            AddAssert("new map immediately resets PP", getText, () => Is.EqualTo("Max PP: 0"));
            AddStep("dispose or remove template demand", () =>
            {
                if (dispose)
                    ((DependencyProvidingContainer)text.Parent!).Remove(text, true);
                else
                    text.Attribute.Value = BeatmapAttribute.Title;
            });
            AddUntilStep("replacement request canceled", () => cache.Cancelled == 2 && cache.Finished == 2);
            AddStep("release gates", () => cache.Gate!.SetResult());
            AddWaitStep("allow obsolete completions", 5);
            AddAssert("no calculation survives removed consumer", () => cache.Starts == 2);
        }

        [Test]
        public void TestStarOnlyDemandFollowsRulesetAndModSettings()
        {
            TestMod mod = null!;
            AddStep("set star-only text", () =>
            {
                Ruleset.Value = new TestRuleset().RulesetInfo;
                SelectedMods.Value = new[] { mod = new TestMod { Difficulty = { Value = 3 } } };
            });
            AddWaitStep("allow input refresh before adding consumer", 3);
            AddStep("select stars", () => text.Attribute.Value = BeatmapAttribute.StarRating);
            AddUntilStep("initial exact stars", getText, () => Is.EqualTo("Star Rating: 3.00"));
            AddStep("change mod setting", () => mod.Difficulty.Value = 4);
            AddUntilStep("changed exact stars", getText, () => Is.EqualTo("Star Rating: 4.00"));
            AddStep("change ruleset", () => Ruleset.Value = new AlternateTestRuleset().RulesetInfo);
            // The game clears selected mods on a ruleset change; the new calculation must follow that too.
            AddUntilStep("changed ruleset stars", getText, () => Is.EqualTo("Star Rating: 0.00"));
            AddUntilStep("all distinct requests computed", () => cache.PreviewStarts == 3);
            AddAssert("no autoplay PP work requested", () => cache.FullStarts == 0);
        }

        private string getText() => text.ChildrenOfType<SpriteText>().First().Text.ToString();

        private partial class CalculationProbe : BeatmapDifficultyCache
        {
            private int fullStarts;
            private int previewStarts;
            private int cancelled;
            private int finished;
            public int FullStarts => Volatile.Read(ref fullStarts);
            public int PreviewStarts => Volatile.Read(ref previewStarts);
            public int Starts => FullStarts + PreviewStarts;
            public int Cancelled => Volatile.Read(ref cancelled);
            public int Finished => Volatile.Read(ref finished);
            public TaskCompletionSource? Gate { get; set; }

            protected override async Task<StarDifficulty?> ComputeValueAsync(DifficultyCacheLookup lookup, CancellationToken cancellationToken = default)
            {
                if (lookup.CalculatePerformance)
                    Interlocked.Increment(ref fullStarts);
                else
                    Interlocked.Increment(ref previewStarts);
                try
                {
                    if (Gate != null)
                        await Gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
                    return await base.ComputeValueAsync(lookup, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Increment(ref cancelled);
                    throw;
                }
                finally
                {
                    Interlocked.Increment(ref finished);
                }
            }
        }

        private class AlternateTestRuleset : TestRuleset
        {
            public override string ShortName => "alternate";
        }

        private class TestRuleset : Ruleset
        {
            public override IEnumerable<Mod> GetModsFor(ModType type) => new[]
            {
                new TestMod()
            };

            public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap)
                => new OsuRuleset().CreateBeatmapConverter(beatmap);

            public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap)
                => new TestDifficultyCalculator(new TestRuleset().RulesetInfo, beatmap);

            public override PerformanceCalculator CreatePerformanceCalculator()
                => new TestPerformanceCalculator(new TestRuleset());

            public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
                => null!;

            public override string Description => string.Empty;
            public override string ShortName => string.Empty;
        }

        private class TestDifficultyCalculator : DifficultyCalculator
        {
            public TestDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap)
                : base(ruleset, beatmap)
            {
            }

            protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills)
                => new DifficultyAttributes(mods, mods.OfType<TestMod>().SingleOrDefault()?.Difficulty.Value ?? 0);

            protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods)
                => Array.Empty<DifficultyHitObject>();

            protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods)
                => Array.Empty<Skill>();
        }

        private class TestPerformanceCalculator : PerformanceCalculator
        {
            public TestPerformanceCalculator(Ruleset ruleset)
                : base(ruleset)
            {
            }

            protected override PerformanceAttributes CreatePerformanceAttributes(ScoreInfo score, DifficultyAttributes attributes)
                => new PerformanceAttributes { Total = score.Mods.OfType<TestMod>().SingleOrDefault()?.Performance.Value ?? 0 };
        }

        private class TestMod : Mod
        {
            [SettingSource("difficulty")]
            public BindableDouble Difficulty { get; } = new BindableDouble(0);

            [SettingSource("performance")]
            public BindableDouble Performance { get; } = new BindableDouble(0);

            [JsonConstructor]
            public TestMod()
            {
            }

            public override string Name => string.Empty;
            public override LocalisableString Description => string.Empty;
            public override string Acronym => "Test";
        }
    }
}
