using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Graphics;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Skinning;
using osu.Game.Skinning.Components;
using osu.Game.Tests.Beatmaps;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneBeatmapDifficultyDisplay : OsuTestScene
    {
        private BeatmapDifficultyDisplay pill = null!;
        private StarRatingDisplay carouselPill = null!;
        private ProbeCache cache = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("reset selection", () =>
            {
                SelectedMods.SetDefault();
                Ruleset.Value = new OsuRuleset().RulesetInfo;
                Beatmap.Value = CreateWorkingBeatmap(new TestBeatmap(new OsuRuleset().RulesetInfo) { BeatmapInfo = { StarRating = 0 } });
            });
            AddStep("load pill and shared cache", () => Child = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[] { (typeof(BeatmapDifficultyCache), cache = new ProbeCache()) },
                Children = new Drawable[]
                {
                    cache,
                    pill = new BeatmapDifficultyDisplay { Position = new Vector2(200, 150) },
                    carouselPill = new StarRatingDisplay(new StarDifficulty(3, 0), StarRatingDisplaySize.Small) { Position = new Vector2(200, 200) },
                },
            });
            AddUntilStep("difficulty resolved", () => pill.IsLoaded && pill.Current.Value.Stars == 3);
        }

        [Test]
        public void TestUsesCarouselAppearanceAndLiveDifficulty()
        {
            AddAssert("same compact geometry", () => pill.DrawSize, () => Is.EqualTo(carouselPill.DrawSize));
            AddAssert("same difficulty colour", () => pill.DisplayedDifficultyColour, () => Is.EqualTo(carouselPill.DisplayedDifficultyColour));
            AddAssert("same readable foreground", () => pill.DisplayedDifficultyTextColour, () => Is.EqualTo(carouselPill.DisplayedDifficultyTextColour));
            AddAssert("star number displayed", () => pill.ChildrenOfType<SpriteText>().Single().Text.ToString(), () => Is.EqualTo("3.00"));
            AddStep("select DT", () => SelectedMods.Value = new[] { new OsuModDoubleTime() });
            AddUntilStep("cache updates for mods", () => pill.Current.Value.Stars == 6);
            AddAssert("number updated", () => pill.ChildrenOfType<SpriteText>().Single().Text.ToString(), () => Is.EqualTo("6.00"));
            AddAssert("difficulty colour updated", () => pill.DisplayedDifficultyColour, () => Is.EqualTo(new OsuColour().ForStarDifficulty(6)));
            AddStep("change DT setting", () => ((OsuModDoubleTime)SelectedMods.Value.Single()).SpeedChange.Value = 1.2);
            AddUntilStep("mod settings update stars", () => pill.Current.Value.Stars == 4.8);
            AddStep("change map", () => Beatmap.Value = CreateWorkingBeatmap(new TestBeatmap(new OsuRuleset().RulesetInfo) { BeatmapInfo = { StarRating = 2 } }));
            AddUntilStep("new map difficulty", () => pill.Current.Value.Stars == 6.8);
            AddAssert("no PP calculation requested", () => cache.PerformanceRequests, () => Is.Zero);
        }

        [Test]
        public void TestLayoutRoundTrip()
        {
            AddAssert("available as optional component", () => SerialisedDrawableInfo.GetAllAvailableDrawables(), () => Does.Contain(typeof(BeatmapDifficultyDisplay)));
            AddStep("customise placement", () =>
            {
                pill.Position = new Vector2(320, 240);
                pill.Scale = new Vector2(1.5f);
                pill.Rotation = 12;
            });
            AddStep("save and recreate", () =>
            {
                var parent = (DependencyProvidingContainer)Child;
                var saved = pill.CreateSerialisedInfo();
                parent.Remove(pill, true);
                parent.Add(pill = (BeatmapDifficultyDisplay)saved.CreateInstance());
            });
            AddUntilStep("restored pill loaded", () => pill.IsLoaded && pill.Current.Value.Stars == 3);
            AddAssert("placement restored", () => pill.Position == new Vector2(320, 240) && pill.Scale == new Vector2(1.5f) && pill.Rotation == 12);
        }

        private partial class ProbeCache : BeatmapDifficultyCache
        {
            public int PerformanceRequests;

            protected override Task<StarDifficulty?> ComputeValueAsync(DifficultyCacheLookup lookup, CancellationToken cancellationToken = default)
            {
                if (lookup.CalculatePerformance) Interlocked.Increment(ref PerformanceRequests);
                var dt = lookup.OrderedMods.OfType<OsuModDoubleTime>().SingleOrDefault();
                double stars = dt == null ? 3 : dt.SpeedChange.Value * 4;
                return Task.FromResult<StarDifficulty?>(new StarDifficulty(stars + lookup.BeatmapInfo.StarRating, 0));
            }
        }
    }
}
