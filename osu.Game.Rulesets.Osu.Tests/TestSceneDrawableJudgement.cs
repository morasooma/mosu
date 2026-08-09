// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Testing;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneDrawableJudgement : OsuSkinnableTestScene
    {
        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private readonly List<DrawablePool<TestDrawableOsuJudgement>> pools = new List<DrawablePool<TestDrawableOsuJudgement>>();

        [TestCaseSource(nameof(validResults))]
        public void Test(HitResult result)
        {
            showResult(result);
        }

        private static IEnumerable<HitResult> validResults => Enum.GetValues<HitResult>().Skip(1);

        [Test]
        public void TestHitLightingDisabled()
        {
            AddStep("hit lighting disabled", () => config.SetValue(OsuSetting.HitLighting, false));
            AddStep("skin perf disabled", () => config.SetValue(OsuSetting.ForkSkinPerformanceMode, false));
            AddStep("apply skin perf disabled", () => SkinPerformanceMode.Enabled = false);

            showResult(HitResult.Great);

            AddUntilStep("judgements shown", () => activeJudgements().Any());
            AddAssert("hit lighting has no transforms", () => activeJudgements().All(judgement => judgement.Lighting == null || !judgement.Lighting.Transforms.Any()));
            AddAssert("hit lighting hidden", () => activeJudgements().All(judgement => judgement.Lighting == null || judgement.Lighting.Alpha == 0));
        }

        [Test]
        public void TestHitLightingEnabled()
        {
            AddStep("hit lighting enabled", () => config.SetValue(OsuSetting.HitLighting, true));
            AddStep("skin perf disabled", () => config.SetValue(OsuSetting.ForkSkinPerformanceMode, false));
            AddStep("apply skin perf disabled", () => SkinPerformanceMode.Enabled = false);

            showResult(HitResult.Great);

            AddUntilStep("judgements shown", () => activeJudgements().Any());
            AddAssert("hit lighting created", () => activeJudgements().Any(judgement => judgement.Lighting != null));
            AddAssert("hit lighting has transforms", () => activeJudgements().Any(judgement => judgement.Lighting?.Transforms.Any() == true));
        }

        [Test]
        public void TestSkinPerformanceModeSkipsHitLighting()
        {
            AddStep("hit lighting enabled", () => config.SetValue(OsuSetting.HitLighting, true));
            AddStep("skin perf enabled", () => config.SetValue(OsuSetting.ForkSkinPerformanceMode, true));
            AddStep("apply skin perf enabled", () => SkinPerformanceMode.Enabled = true);

            showResult(HitResult.Ok);

            AddUntilStep("judgements shown", () => activeJudgements().Any());
            AddAssert("hit lighting has no transforms", () => activeJudgements().All(judgement => judgement.Lighting == null || !judgement.Lighting.Transforms.Any()));
            AddAssert("hit lighting hidden", () => activeJudgements().All(judgement => judgement.Lighting == null || judgement.Lighting.Alpha == 0));
        }

        private void showResult(HitResult result)
        {
            AddStep("Show " + result.GetDescription(), () =>
            {
                int poolIndex = 0;

                SetContents(_ =>
                {
                    DrawablePool<TestDrawableOsuJudgement> pool;

                    if (poolIndex >= pools.Count)
                        pools.Add(pool = new DrawablePool<TestDrawableOsuJudgement>(1));
                    else
                    {
                        // We need to make sure neither the pool nor the judgement get disposed when new content is set, and they both share the same parent.
                        pool = pools[poolIndex];
                        ((Container)pool.Parent!).Clear(false);
                    }

                    var container = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = pool,
                    };

                    // Must be scheduled so the pool is loaded before we try and retrieve from it.
                    Schedule(() =>
                    {
                        container.Add(pool.Get(j => j.Apply(new JudgementResult(new HitObject
                        {
                            StartTime = Time.Current
                        }, new Judgement())
                        {
                            Type = result,
                        }, null)).With(j =>
                        {
                            j.Anchor = Anchor.Centre;
                            j.Origin = Anchor.Centre;
                        }));
                    });

                    poolIndex++;
                    return container;
                });
            });
        }

        private IEnumerable<TestDrawableOsuJudgement> activeJudgements() => this.ChildrenOfType<TestDrawableOsuJudgement>().Where(judgement => judgement.IsPresent);

        private partial class TestDrawableOsuJudgement : DrawableOsuJudgement
        {
            public new SkinnableLighting? Lighting => base.Lighting;
            public new SkinnableDrawable? JudgementBody => base.JudgementBody;
        }
    }
}
