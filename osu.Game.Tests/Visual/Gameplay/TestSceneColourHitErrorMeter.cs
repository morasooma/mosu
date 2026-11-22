// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Testing;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osuTK;

namespace osu.Game.Tests.Visual.Gameplay
{
    public partial class TestSceneColourHitErrorMeter : OsuTestScene
    {
        private DependencyProvidingContainer dependencyContainer = null!;

        private readonly Bindable<JudgementResult> lastJudgementResult = new Bindable<JudgementResult>();
        private ScoreProcessor scoreProcessor = null!;

        private int iteration;

        private ColourHitErrorMeter colourHitErrorMeter = null!;

        public TestSceneColourHitErrorMeter()
        {
            AddSliderStep("Judgement spacing", 0, 10, 2, spacing =>
            {
                if (colourHitErrorMeter.IsNotNull())
                    colourHitErrorMeter.JudgementSpacing.Value = spacing;
            });

            AddSliderStep("Judgement count", 1, 50, 5, spacing =>
            {
                if (colourHitErrorMeter.IsNotNull())
                    colourHitErrorMeter.JudgementCount.Value = spacing;
            });
        }

        [SetUpSteps]
        public void SetupSteps() => AddStep("Create components", () =>
        {
            var ruleset = CreateRuleset();

            Debug.Assert(ruleset != null);

            scoreProcessor = new ScoreProcessor(ruleset);
            Child = dependencyContainer = new DependencyProvidingContainer
            {
                RelativeSizeAxes = Axes.Both,
                CachedDependencies = new (Type, object)[]
                {
                    (typeof(ScoreProcessor), scoreProcessor)
                }
            };
            dependencyContainer.Child = colourHitErrorMeter = new ColourHitErrorMeter
            {
                Margin = new MarginPadding
                {
                    Top = 100
                },
                Anchor = Anchor.TopCentre,
                Origin = Anchor.TopCentre,
                Scale = new Vector2(2),
            };
        });

        protected override Ruleset CreateRuleset() => new OsuRuleset();

        [Test]
        public void TestSpacingChange()
        {
            AddRepeatStep("Add judgement", applyOneJudgement, 5);
            AddStep("Change spacing", () => colourHitErrorMeter.JudgementSpacing.Value = 10);
            AddRepeatStep("Add judgement", applyOneJudgement, 5);
        }

        [Test]
        public void TestJudgementAmountChange()
        {
            AddRepeatStep("Add judgement", applyOneJudgement, 10);
            AddStep("Judgement count change to 4", () => colourHitErrorMeter.JudgementCount.Value = 4);
            AddRepeatStep("Add judgement", applyOneJudgement, 8);
        }

        [Test]
        public void TestHitErrorShapeChange()
        {
            AddRepeatStep("Add judgement", applyOneJudgement, 8);
            AddStep("Change shape square", () => colourHitErrorMeter.JudgementShape.Value = ColourHitErrorMeter.ShapeStyle.Square);
            AddRepeatStep("Add judgement", applyOneJudgement, 10);
            AddStep("Change shape circle", () => colourHitErrorMeter.JudgementShape.Value = ColourHitErrorMeter.ShapeStyle.Circle);
        }

        [TestCase(ColourHitErrorMeter.ShapeStyle.Circle)]
        [TestCase(ColourHitErrorMeter.ShapeStyle.Square)]
        public void TestNewestFirstLayoutAndPoolReuse(ColourHitErrorMeter.ShapeStyle shape)
        {
            AddStep("configure", () =>
            {
                colourHitErrorMeter.JudgementCount.Value = 5;
                colourHitErrorMeter.JudgementShape.Value = shape;
            });
            AddStep("add burst", () =>
            {
                for (int i = 0; i < 12; i++)
                    applyOneJudgement();
            });
            AddUntilStep("only five remain", () => shapes().Length == 5);
            AddUntilStep("newest first at original spacing", () => positionsMatch(10));
            AddStep("change spacing", () => colourHitErrorMeter.JudgementSpacing.Value = 7);
            AddUntilStep("relayout at new spacing", () => positionsMatch(15));
            AddStep("reduce count", () => colourHitErrorMeter.JudgementCount.Value = 2);
            AddUntilStep("oldest removed", () => shapes().Length == 2 && positionsMatch(15));
            AddStep("clear", () => colourHitErrorMeter.Clear());
            AddUntilStep("no shapes retained", () => shapes().Length == 0);
            AddStep("reuse pooled shapes", () =>
            {
                for (int i = 0; i < 4; i++)
                    applyOneJudgement();
            });
            AddUntilStep("reuse starts with correct layout", () => shapes().Length == 2 && positionsMatch(15));
        }

        [Test]
        public void TestSettledLayoutDoesNotAllocate()
        {
            Action updateLayout = null!;
            AddStep("add full history", () =>
            {
                for (int i = 0; i < 20; i++)
                    applyOneJudgement();
            });
            AddUntilStep("layout settled", () => shapes().Length == 20 && positionsMatch(10));
            AddStep("bind layout update", () =>
            {
                var flow = shapes()[0].Parent!;
                var method = flow.GetType().GetMethod("UpdateAfterChildren", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
                updateLayout = (Action)method.CreateDelegate(typeof(Action), flow);
                for (int i = 0; i < 100; i++)
                    updateLayout();
            });
            AddStep("steady layout is allocation-free", () =>
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++)
                    updateLayout();
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Assert.That(allocated, Is.Zero, "unchanged history must not sort, enumerate LINQ, or recreate position transforms");
            });
        }

        private ColourHitErrorMeter.HitErrorShape[] shapes() => colourHitErrorMeter.ChildrenOfType<ColourHitErrorMeter.HitErrorShape>().ToArray();

        private bool positionsMatch(float stride)
        {
            var displayed = shapes();
            return displayed.Length > 0 && displayed.Reverse().Select((shape, index) => Math.Abs(shape.Y - index * stride) < 0.01f).All(matches => matches);
        }

        private void applyOneJudgement()
        {
            lastJudgementResult.Value = new OsuJudgementResult(new HitObject
            {
                StartTime = iteration * 10000,
            }, new OsuJudgement())
            {
                Type = HitResult.Great,
            };
            scoreProcessor.ApplyResult(lastJudgementResult.Value);

            iteration++;
        }
    }
}
