// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Judgements;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.Tests
{
    public partial class TestSceneDrawableJudgementSliderTicks : OsuSkinnableTestScene
    {
        private bool classic;
        private readonly JudgementPooler<DrawableOsuJudgement>[] judgementPools;

        public TestSceneDrawableJudgementSliderTicks()
        {
            judgementPools = new JudgementPooler<DrawableOsuJudgement>[Rows * Cols];
        }

        [Test]
        public void TestSkinPerformanceModeKeepsSliderTickSkinnable()
        {
            DrawableSlider? slider = null;

            AddStep("enable skin performance mode", () => SkinPerformanceMode.Enabled = true);
            AddStep("load slider", () => SetContents(_ =>
            {
                var hitObject = new Slider
                {
                    StartTime = Time.Current,
                    Path = new SliderPath(PathType.LINEAR, new[]
                    {
                        Vector2.Zero,
                        new Vector2(200, 0),
                    }),
                };

                hitObject.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty
                {
                    SliderMultiplier = 1,
                    SliderTickRate = 3,
                });

                return slider = new DrawableSlider(hitObject);
            }));
            AddUntilStep("slider tick loaded", () => slider?.NestedHitObjects.OfType<DrawableSliderTick>().Any(t => t.IsLoaded) == true);
            AddAssert("slider tick uses skinnable drawable", () => slider!.NestedHitObjects.OfType<DrawableSliderTick>().All(t => t.ChildrenOfType<SkinnableDrawable>().Any()));
            AddStep("disable skin performance mode", () => SkinPerformanceMode.Enabled = false);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            int cellIndex = 0;

            SetContents(_ =>
            {
                var container = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Children = new Drawable[]
                    {
                        judgementPools[cellIndex] = new JudgementPooler<DrawableOsuJudgement>(new[]
                        {
                            HitResult.Great,
                            HitResult.Miss,
                            HitResult.LargeTickHit,
                            HitResult.SliderTailHit,
                            HitResult.LargeTickMiss,
                            HitResult.IgnoreMiss,
                        }),
                        new GridContainer
                        {
                            Padding = new MarginPadding { Top = 26f },
                            RelativeSizeAxes = Axes.Both,
                            RowDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            ColumnDimensions = new[] { new Dimension(GridSizeMode.AutoSize) },
                            Content =
                                new[]
                                {
                                    new[]
                                    {
                                        Empty(),
                                        new OsuSpriteText
                                        {
                                            Text = "hit",
                                            Anchor = Anchor.BottomCentre,
                                            Origin = Anchor.BottomCentre,
                                        },
                                        new OsuSpriteText
                                        {
                                            Text = "miss",
                                            Anchor = Anchor.BottomCentre,
                                            Origin = Anchor.BottomCentre,
                                        },
                                    },
                                }.Concat(new[]
                                {
                                    "head",
                                    "tick",
                                    "repeat",
                                    "tail",
                                    "slider",
                                }.Select(label => new Drawable[]
                                {
                                    new OsuSpriteText
                                    {
                                        Text = label,
                                        Anchor = Anchor.CentreRight,
                                        Origin = Anchor.CentreRight,
                                    },
                                    new Container<DrawableOsuJudgement> { RelativeSizeAxes = Axes.Both },
                                    new Container<DrawableOsuJudgement> { RelativeSizeAxes = Axes.Both },
                                })).ToArray(),
                        },
                    },
                };

                cellIndex++;

                return container;
            });

            AddToggleStep("Toggle classic behaviour", c => classic = c);

            AddStep("Show judgements", createAllJudgements);
        }

        private void createAllJudgements()
        {
            for (int cellIndex = 0; cellIndex < Rows * Cols; cellIndex++)
            {
                var slider = new Slider { StartTime = Time.Current, ClassicSliderBehaviour = classic };
                slider.ApplyDefaults(new ControlPointInfo(), new BeatmapDifficulty());

                var drawableHitObjects = new DrawableOsuHitObject[]
                {
                    new DrawableSliderHead(new SliderHeadCircle { StartTime = Time.Current, ClassicSliderBehaviour = classic }),
                    new DrawableSliderTick(new SliderTick { StartTime = Time.Current }),
                    new DrawableSliderRepeat(new SliderRepeat(slider) { StartTime = Time.Current }),
                    new DrawableSliderTail(new SliderTailCircle(slider) { StartTime = Time.Current, ClassicSliderBehaviour = classic }),
                    new DrawableSlider(slider),
                };

                var containers = Cell(cellIndex).ChildrenOfType<Container<DrawableOsuJudgement>>().ToArray();

                for (int i = 0; i < drawableHitObjects.Length; i++)
                {
                    createJudgement(judgementPools[cellIndex], containers[i * 2], drawableHitObjects[i], true);
                    createJudgement(judgementPools[cellIndex], containers[i * 2 + 1], drawableHitObjects[i], false);
                }
            }
        }

        private void createJudgement(JudgementPooler<DrawableOsuJudgement> pool, Container<DrawableOsuJudgement> container, DrawableOsuHitObject drawableHitObject, bool hit)
        {
            container.Clear(false);

            if (!drawableHitObject.DisplayResult)
                return;

            var hitObject = drawableHitObject.HitObject;
            var result = new OsuJudgementResult(hitObject, hitObject.Judgement)
            {
                Type = hit ? hitObject.Judgement.MaxResult : hitObject.Judgement.MinResult,
            };

            var judgement = pool.Get(result.Type, d =>
            {
                d.Anchor = Anchor.Centre;
                d.Origin = Anchor.Centre;
                d.Scale = new Vector2(0.7f);
                d.Apply(result, null);
            });

            if (judgement != null)
                container.Add(judgement);
        }
    }
}
