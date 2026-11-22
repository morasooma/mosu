// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Testing;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Online.API.Requests.Responses;
using osuTK;

namespace osu.Game.Tests.Visual.Beatmaps
{
    public partial class TestSceneDifficultySpectrumDisplay : OsuTestScene
    {
        private DifficultySpectrumDisplay display = null!;

        [SetUpSteps]
        public void SetUpSteps()
        {
            AddStep("create spectrum display", () => Child = display = new DifficultySpectrumDisplay
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Scale = new Vector2(3)
            });
        }

        [Test]
        public void TestSingleRuleset()
        {
            var beatmapSet = createBeatmapSetWith(
                (rulesetId: 0, stars: 2.0),
                (rulesetId: 0, stars: 3.2),
                (rulesetId: 0, stars: 5.6));

            AddStep("set beatmap to display", () => display.BeatmapSet = beatmapSet);
        }

        [Test]
        public void TestMultipleRulesets()
        {
            var beatmapSet = createBeatmapSetWith(
                (rulesetId: 0, stars: 2.0),
                (rulesetId: 3, stars: 2.3),
                (rulesetId: 0, stars: 3.2),
                (rulesetId: 1, stars: 4.3),
                (rulesetId: 0, stars: 5.6));

            AddStep("set beatmap to display", () => display.BeatmapSet = beatmapSet);
        }

        [Test]
        public void TestUnknownRuleset()
        {
            var beatmapSet = createBeatmapSetWith(
                (rulesetId: 0, stars: 2.0),
                (rulesetId: 3, stars: 2.3),
                (rulesetId: 0, stars: 3.2),
                (rulesetId: 1, stars: 4.3),
                (rulesetId: 0, stars: 5.6),
                (rulesetId: 15, stars: 7.8));

            AddStep("set beatmap to display", () => display.BeatmapSet = beatmapSet);
        }

        [Test]
        public void TestMaximumUncollapsed()
        {
            var beatmapSet = createBeatmapSetWith(Enumerable.Range(0, 12).Select(i => (rulesetId: i % 4, stars: 2.5 + i * 0.25)).ToArray());
            AddStep("set beatmap to display", () => display.BeatmapSet = beatmapSet);
        }

        [Test]
        public void TestMinimumCollapsed()
        {
            var beatmapSet = createBeatmapSetWith(Enumerable.Range(0, 13).Select(i => (rulesetId: i % 4, stars: 2.5 + i * 0.25)).ToArray());
            AddStep("set beatmap to display", () => display.BeatmapSet = beatmapSet);
        }

        [Test]
        public void TestInitialGroupsArePreparedBeforePublication()
        {
            DifficultySpectrumDisplay prepared = null!;
            bool loaded = false;
            AddStep("load pre-populated spectrum", () => LoadComponentAsync(prepared = new DifficultySpectrumDisplay
            {
                BeatmapSet = createBeatmapSetWith((0, 2.0), (0, 5.0), (3, 4.0)),
            }, _ => loaded = true));
            AddUntilStep("background loading finished", () => loaded);
            AddAssert("icons exist before attachment", () => prepared.ChildrenOfType<SpriteIcon>().Count(), () => Is.EqualTo(2));
            AddAssert("icons already dependency-loaded", () => prepared.ChildrenOfType<SpriteIcon>().All(i => i.LoadState >= LoadState.Ready));
            AddAssert("initial dots already configured", () => prepared.ChildrenOfType<Circle>().Count(c => c.Alpha > 0), () => Is.EqualTo(3));
            AddStep("replace data before publication", () => prepared.BeatmapSet = createBeatmapSetWith((0, 3.0), (3, 4.0)));
            AddAssert("ready but unattached display reflects latest data", () => prepared.ChildrenOfType<Circle>().Count(c => c.Alpha > 0), () => Is.EqualTo(2));
            AddStep("publish spectrum", () => Child = prepared);
            AddWaitStep("first layout", 2);
            AddAssert("publication does not duplicate groups", () => prepared.ChildrenOfType<SpriteIcon>().Count(), () => Is.EqualTo(2));
            AddStep("clear beatmaps", () => prepared.BeatmapSet = null);
            AddAssert("all dots hidden", () => prepared.ChildrenOfType<Circle>().All(c => c.Alpha == 0));
        }

        private static APIBeatmapSet createBeatmapSetWith(params (int rulesetId, double stars)[] difficulties) => new APIBeatmapSet
        {
            Beatmaps = difficulties.Select(difficulty => new APIBeatmap
            {
                RulesetID = difficulty.rulesetId,
                StarRating = difficulty.stars
            }).ToArray()
        };
    }
}
