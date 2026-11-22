// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Sprites;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD;
using osu.Game.Skinning;
using osu.Game.Utils;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneSkinnableAccuracyCounter : SkinnableHUDComponentTestScene
    {
        [Cached]
        private ScoreProcessor scoreProcessor = new ScoreProcessor(new OsuRuleset());

        protected override Drawable CreateArgonImplementation() => new ArgonAccuracyCounter();
        protected override Drawable CreateDefaultImplementation() => new DefaultAccuracyCounter();
        protected override Drawable CreateLegacyImplementation() => new LegacyAccuracyCounter();

        public override void SetUpSteps()
        {
            AddStep("Set initial accuracy", () => scoreProcessor.Accuracy.Value = 1);

            base.SetUpSteps();
        }

        [Test]
        public void TestFormattedAccuracyAfterRollingInBothDirections()
        {
            foreach (double accuracy in new[] { 0.989999, 0.99, 0.876543, 1.0 })
            {
                AddStep($"set accuracy to {accuracy}", () => scoreProcessor.Accuracy.Value = accuracy);
                AddUntilStep("all skins finish rolling", () => CreatedDrawables.Cast<GameplayAccuracyCounter>()
                    .All(counter => counter.DisplayedCount == accuracy));
                AddAssert("all skins retain complete formatted accuracy", () => CreatedDrawables.Cast<GameplayAccuracyCounter>()
                    .All(counter => ((IHasText)counter.DrawableCount).Text.Equals(accuracy.FormatAccuracy())));
            }
        }

        [Test]
        public void TestChangingAccuracy()
        {
            AddStep(@"Reset all", () => scoreProcessor.Accuracy.Value = 1);

            AddStep(@"Miss :(", () => scoreProcessor.Accuracy.Value -= 0.023);
        }
    }
}
