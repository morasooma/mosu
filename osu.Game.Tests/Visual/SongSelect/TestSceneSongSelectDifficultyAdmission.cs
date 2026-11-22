using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Screens.Select;
using osu.Game.Skinning.Components;
using osu.Game.Tests.Beatmaps;

namespace osu.Game.Tests.Visual.SongSelect
{
    [HeadlessTest]
    public partial class TestSceneSongSelectDifficultyAdmission : SongSelectComponentsTestScene
    {
        protected override bool UseFreshStoragePerRun => true;

        private AdmissionProbe cache = null!;
        private BeatmapTitleWedge.DifficultyDisplay display = null!;
        private BeatmapAttributeText skinText = null!;
        private WorkingBeatmap[] maps = Array.Empty<WorkingBeatmap>();

        // Supply the real song-select dependency identity without loading another screen.
        private readonly SoloSongSelect songSelectScope = new SoloSongSelect();

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();
            AddStep("load isolated cache and real difficulty wedge", () =>
            {
                Ruleset.Value = new OsuRuleset().RulesetInfo;
                maps = Enumerable.Range(0, 8).Select(i => new TestWorkingBeatmap(new Beatmap
                {
                    BeatmapInfo = new BeatmapInfo(Ruleset.Value)
                    {
                        ID = Guid.NewGuid(),
                        DifficultyName = $"Scroll {i}",
                        StarRating = i + 1,
                    },
                })).Cast<WorkingBeatmap>().ToArray();
                Child = new DependencyProvidingContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    CachedDependencies = new (Type, object)[]
                    {
                        (typeof(BeatmapDifficultyCache), cache = new AdmissionProbe()),
                        (typeof(ISongSelect), songSelectScope),
                    },
                    Children = new Drawable[]
                    {
                        cache,
                        display = new BeatmapTitleWedge.DifficultyDisplay(),
                        skinText = new BeatmapAttributeText
                        {
                            Attribute = { Value = BeatmapAttribute.Title },
                            Template = { Value = "{Value}" },
                        },
                    },
                };
            });
            AddUntilStep("wedge, skin text and cache loaded", () => cache.IsLoaded && display.IsLoaded && skinText.IsLoaded);
        }

        [TestCase(null)]
        [TestCase(BeatmapAttribute.MaxPP)]
        [TestCase(BeatmapAttribute.Title)]
        public void TestScrollingDoesNotAdmitFullCalculationsAndIdleSelectionStillCompletes(BeatmapAttribute? skinAttribute)
        {
            long selectionStart = 0;
            for (int i = 0; i < 8; i++)
            {
                int index = i;
                AddStep($"scroll to map {i}", () =>
                {
                    selectionStart = Stopwatch.GetTimestamp();
                    Beatmap.Value = maps[index];
                    if (index == 0 && skinAttribute != null)
                    {
                        skinText.Attribute.Value = skinAttribute.Value;
                        skinText.Template.Value = skinAttribute == BeatmapAttribute.MaxPP ? "{Value}" : "{MaxPP}";
                    }
                });
                AddAssert($"map {i} star seed is immediately visible", () =>
                    display.ChildrenOfType<osu.Game.Beatmaps.Drawables.StarRatingDisplay>().Single().Current.Value.Stars == index + 1);
                // Headless step time is accelerated, but difficulty admission uses Task.Delay's
                // real clock. Allow immediate/150 ms admission bugs to fire before moving on.
                AddUntilStep($"hold map {i} for 300 real ms", () => Stopwatch.GetElapsedTime(selectionStart).TotalMilliseconds >= 300);
            }

            AddStep("report full work admitted while scrolling", () =>
                TestContext.Progress.WriteLine($"SCROLL_ADMISSION selections={maps.Length} full_starts={cache.Starts}"));
            AddAssert("no heavy calculation for transient selections", () => cache.Starts == 0);
            AddUntilStep("final idle selection calculated", () => cache.Starts == 1 && displayedStatistics().Count == 1);
            AddAssert("latest counts published", () => displayedStatistics()[0].Content == "Scroll 7");
            AddAssert("no separate unmodded fallback", () => display.CountFallbacksStarted == 0);
            if (skinAttribute != null)
                AddUntilStep("idle skin PP is still calculated", () => skinText.ChildrenOfType<SpriteText>().Single().Text.ToString() == "800");
            AddStep("revisit transient selection and clear old counts", () =>
            {
                Beatmap.Value = maps[0];
                display.ChildrenOfType<BeatmapTitleWedge.DifficultyStatisticsDisplay>().First().Statistics = Array.Empty<BeatmapTitleWedge.StatisticDifficulty.Data>();
            });
            AddStep("return to cached final selection", () => Beatmap.Value = maps[^1]);
            AddAssert("cached counts published without admission delay", () => displayedStatistics().Single().Content == "Scroll 7");
            AddAssert("cache hit did not start another calculation", () => cache.Starts == 1);
            if (skinAttribute != null)
                AddAssert("cached skin PP is visible without admission delay", () => skinText.ChildrenOfType<SpriteText>().Single().Text.ToString() == "800");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestModChangeRefreshesCurrentSelectionWithoutRevivingCancelledScrollRequests(bool includeSkinPp)
        {
            long selectionStart = 0;
            AddStep("select first transient map", () =>
            {
                selectionStart = Stopwatch.GetTimestamp();
                Beatmap.Value = maps[0];
                if (includeSkinPp)
                    skinText.Attribute.Value = BeatmapAttribute.MaxPP;
            });
            AddUntilStep("hold first map for 300 real ms", () => Stopwatch.GetElapsedTime(selectionStart).TotalMilliseconds >= 300);
            AddStep("select final map", () => Beatmap.Value = maps[^1]);
            AddAssert("selection has not admitted full work", () => cache.Starts == 0);
            AddStep("change mod on current selection", () => SelectedMods.Value = new Mod[] { new OsuModDoubleTime() });
            AddUntilStep("explicit mod change calculated", () => cache.Starts == 1);
            if (includeSkinPp)
                AddUntilStep("skin PP follows explicit mod change", () => skinText.ChildrenOfType<SpriteText>().Single().Text.ToString() == "800");
            AddAssert("only current map calculated", () => cache.LastMap == maps[^1].BeatmapInfo.ID);
            AddAssert("actual selected mod reaches calculator", () => cache.LastMods.Single() is OsuModDoubleTime);
            AddStep("begin another selection then dispose wedge", () =>
            {
                Beatmap.Value = maps[1];
                // Apply the scheduled selection update so disposal cancels a real pending admission.
                display.UpdateSubTree();
                ((DependencyProvidingContainer)display.Parent!).Remove(display, true);
                ((DependencyProvidingContainer)skinText.Parent!).Remove(skinText, true);
                selectionStart = Stopwatch.GetTimestamp();
            });
            AddUntilStep("wait past abandoned admission", () => Stopwatch.GetElapsedTime(selectionStart).TotalMilliseconds >= 1200);
            AddAssert("disposed and previously cancelled selections never calculated", () => cache.Starts == 1);
        }

        [Test]
        public void TestSkinStarDemandUsesPreviewDelayAndNeverStartsPp()
        {
            long selectionStart = 0;
            AddStep("isolate skin star consumer", () =>
            {
                ((DependencyProvidingContainer)display.Parent!).Remove(display, true);
                Beatmap.Value = maps[0];
                selectionStart = Stopwatch.GetTimestamp();
                skinText.Attribute.Value = BeatmapAttribute.StarRating;
            });
            AddAssert("star seed displayed immediately", () => skinText.ChildrenOfType<SpriteText>().Single().Text.ToString() == "1.00");
            AddUntilStep("star-only work admitted", () => cache.Starts == 1);
            AddAssert("preview delay was preserved", () => Stopwatch.GetElapsedTime(selectionStart, cache.LastStart).TotalMilliseconds
                >= Screens.Select.SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE);
            AddAssert("star text requests no PP work", () => !cache.LastCalculatePerformance);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestRemovingSkinPpDemandCancelsPendingAdmission(bool dispose)
        {
            long selectionStart = 0;
            AddStep("isolate skin PP consumer", () =>
            {
                ((DependencyProvidingContainer)display.Parent!).Remove(display, true);
                Beatmap.Value = maps[0];
                skinText.Attribute.Value = BeatmapAttribute.MaxPP;
                selectionStart = Stopwatch.GetTimestamp();
            });
            AddUntilStep("hold map for 300 real ms", () => Stopwatch.GetElapsedTime(selectionStart).TotalMilliseconds >= 300);
            AddAssert("skin consumer has not admitted heavy work", () => cache.Starts == 0);
            AddStep("remove skin PP demand", () =>
            {
                if (dispose)
                    ((DependencyProvidingContainer)skinText.Parent!).Remove(skinText, true);
                else
                    skinText.Attribute.Value = BeatmapAttribute.Title;
            });
            AddUntilStep("wait past abandoned admission", () => Stopwatch.GetElapsedTime(selectionStart).TotalMilliseconds >= 1200);
            AddAssert("abandoned skin request never calculated", () => cache.Starts == 0);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
                songSelectScope.Dispose();

            base.Dispose(isDisposing);
        }

        private IReadOnlyList<BeatmapTitleWedge.StatisticDifficulty.Data> displayedStatistics()
            => display.ChildrenOfType<BeatmapTitleWedge.DifficultyStatisticsDisplay>().First().Statistics;

        private partial class AdmissionProbe : BeatmapDifficultyCache
        {
            private int starts;
            public int Starts => Volatile.Read(ref starts);
            public Guid LastMap { get; private set; }
            public Mod[] LastMods { get; private set; } = Array.Empty<Mod>();
            public bool LastCalculatePerformance { get; private set; }
            public long LastStart { get; private set; }

            protected override Task<StarDifficulty?> ComputeValueAsync(DifficultyCacheLookup lookup, CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                LastMap = lookup.BeatmapInfo.ID;
                LastMods = lookup.OrderedMods;
                LastCalculatePerformance = lookup.CalculatePerformance;
                LastStart = Stopwatch.GetTimestamp();
                Interlocked.Increment(ref starts);
                var difficulty = new DifficultyAttributes { StarRating = lookup.BeatmapInfo.StarRating, MaxCombo = 10 };
                return Task.FromResult<StarDifficulty?>(lookup.CalculatePerformance
                    ? new StarDifficulty(difficulty,
                        new PerformanceAttributes { Total = lookup.BeatmapInfo.StarRating * 100 },
                        new[] { new StarDifficulty.CountStatistic("Objects", null, lookup.BeatmapInfo.DifficultyName) })
                    : new StarDifficulty(difficulty));
            }
        }
    }
}
