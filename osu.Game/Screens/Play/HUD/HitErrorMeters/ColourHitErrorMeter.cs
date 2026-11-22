// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Pooling;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Localisation.HUD;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Screens.Play.HUD.HitErrorMeters
{
    [Cached]
    public partial class ColourHitErrorMeter : HitErrorMeter
    {
        private const int animation_duration = 200;
        private const int drawable_judgement_size = 8;

        [SettingSource(typeof(ColourHitErrorMeterStrings), nameof(ColourHitErrorMeterStrings.JudgementCount), nameof(ColourHitErrorMeterStrings.JudgementCountDescription))]
        public BindableNumber<int> JudgementCount { get; } = new BindableNumber<int>(20)
        {
            MinValue = 1,
            MaxValue = 50,
        };

        [SettingSource(typeof(ColourHitErrorMeterStrings), nameof(ColourHitErrorMeterStrings.JudgementSpacing), nameof(ColourHitErrorMeterStrings.JudgementSpacingDescription))]
        public BindableNumber<float> JudgementSpacing { get; } = new BindableNumber<float>(2)
        {
            MinValue = 0,
            MaxValue = 10,
        };

        [SettingSource(typeof(ColourHitErrorMeterStrings), nameof(ColourHitErrorMeterStrings.JudgementShape), nameof(ColourHitErrorMeterStrings.JudgementShapeDescription))]
        public Bindable<ShapeStyle> JudgementShape { get; } = new Bindable<ShapeStyle>();

        private readonly DrawablePool<HitErrorShape> judgementShapePool;
        private readonly JudgementFlow judgementsFlow;

        public ColourHitErrorMeter()
        {
            AutoSizeAxes = Axes.Both;
            InternalChildren = new Drawable[]
            {
                judgementShapePool = new DrawablePool<HitErrorShape>(50),
                judgementsFlow = new JudgementFlow
                {
                    JudgementShape = { BindTarget = JudgementShape },
                    JudgementSpacing = { BindTarget = JudgementSpacing },
                    JudgementCount = { BindTarget = JudgementCount }
                }
            };
        }

        protected override void OnNewJudgement(JudgementResult judgement)
        {
            var visualType = judgement.VisualType ?? judgement.Type;
            if (!visualType.IsScorable() || visualType.IsBonus())
                return;

            judgementsFlow.Push(judgementShapePool.Get(shape => shape.Colour = GetColourForHitResult(visualType)));
        }

        public override void Clear()
        {
            foreach (var j in judgementsFlow)
            {
                j.ClearTransforms();
                j.Expire();
            }
        }

        private partial class JudgementFlow : Container<HitErrorShape>
        {
            // The icons have a fixed size. FlowContainer would invalidate its layout on every
            // frame of their position animations, sorting and allocating an unchanged layout.
            private readonly List<HitErrorShape> layoutChildren = new List<HitErrorShape>();
            private bool layoutInvalid = true;

            public readonly Bindable<ShapeStyle> JudgementShape = new Bindable<ShapeStyle>();

            public readonly Bindable<float> JudgementSpacing = new Bindable<float>();

            public readonly Bindable<int> JudgementCount = new Bindable<int>();

            public JudgementFlow()
            {
                Width = drawable_judgement_size;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                JudgementCount.BindValueChanged(_ =>
                {
                    removeExtraJudgements();
                    updateMetrics();
                });

                JudgementSpacing.BindValueChanged(_ => updateMetrics(), true);
            }

            public void Push(HitErrorShape shape)
            {
                layoutInvalid = true;
                Add(shape);
                removeExtraJudgements();
            }

            private void removeExtraJudgements()
            {
                int remaining = 0;
                for (int i = 0; i < Children.Count; i++)
                {
                    if (!Children[i].IsRemoved)
                        remaining++;
                }

                for (int i = 0; remaining > JudgementCount.Value && i < Children.Count; i++)
                {
                    if (Children[i].IsRemoved)
                        continue;

                    Children[i].Remove();
                    remaining--;
                }
            }

            private void updateMetrics()
            {
                Height = JudgementCount.Value * (drawable_judgement_size + JudgementSpacing.Value) - JudgementSpacing.Value;
                layoutInvalid = true;
            }

            protected override void UpdateAfterChildren()
            {
                base.UpdateAfterChildren();

                // Match the original newest-first flow, including fading-out icons until
                // they cease to be present. Position/colour animations do not change membership.
                int index = 0;
                for (int i = AliveChildren.Count - 1; i >= 0; i--)
                {
                    var shape = AliveChildren[i];
                    if (!shape.IsPresent)
                        continue;

                    if (index == layoutChildren.Count)
                    {
                        layoutChildren.Add(shape);
                        layoutInvalid = true;
                    }
                    else if (layoutChildren[index] != shape)
                    {
                        layoutChildren[index] = shape;
                        layoutInvalid = true;
                    }

                    index++;
                }

                if (index < layoutChildren.Count)
                {
                    layoutChildren.RemoveRange(index, layoutChildren.Count - index);
                    layoutInvalid = true;
                }

                if (!layoutInvalid)
                    return;

                for (int i = 0; i < layoutChildren.Count; i++)
                    layoutChildren[i].MoveToLayoutPosition(new Vector2(0, i * (drawable_judgement_size + JudgementSpacing.Value)));

                layoutInvalid = false;
            }
        }

        public partial class HitErrorShape : PoolableDrawable
        {
            public bool IsRemoved { get; private set; }

            public readonly Bindable<ShapeStyle> Shape = new Bindable<ShapeStyle>();

            [Resolved]
            private ColourHitErrorMeter hitErrorMeter { get; set; } = null!;

            private Container content = null!;
            private Vector2 layoutPosition;

            internal void MoveToLayoutPosition(Vector2 position)
            {
                if (layoutPosition == position)
                    return;

                layoutPosition = position;
                this.MoveTo(position, animation_duration, Easing.OutQuint);
            }

            public HitErrorShape()
            {
                Size = new Vector2(drawable_judgement_size);
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                InternalChild = content = new Container
                {
                    RelativeSizeAxes = Axes.Both,
                };

                Shape.BindTo(hitErrorMeter.JudgementShape);
                Shape.BindValueChanged(shape =>
                {
                    switch (shape.NewValue)
                    {
                        case ShapeStyle.Circle:
                            content.Child = new FastCircle { RelativeSizeAxes = Axes.Both };
                            break;

                        case ShapeStyle.Square:
                            content.Child = new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                // somewhat improves the appearance of uneven spacing on certain `JudgementSpacing` specifications
                                EdgeSmoothness = new Vector2(0.5f),
                            };
                            break;
                    }
                }, true);
            }

            protected override void PrepareForUse()
            {
                base.PrepareForUse();

                layoutPosition = Vector2.Zero;
                this.FadeInFromZero(animation_duration, Easing.OutQuint)
                    // On pool re-use, start flow animation from (0,0).
                    .MoveTo(Vector2.Zero);

                content.MoveToY(-DrawSize.Y)
                       .MoveToY(0, animation_duration, Easing.OutQuint);
            }

            protected override void FreeAfterUse()
            {
                base.FreeAfterUse();
                IsRemoved = false;
            }

            public void Remove()
            {
                IsRemoved = true;

                this.FadeOut(animation_duration, Easing.OutQuint)
                    .Expire();
            }
        }

        public enum ShapeStyle
        {
            [LocalisableDescription(typeof(ColourHitErrorMeterStrings), nameof(ColourHitErrorMeterStrings.ShapeStyleCircle))]
            Circle,

            [LocalisableDescription(typeof(ColourHitErrorMeterStrings), nameof(ColourHitErrorMeterStrings.ShapeStyleSquare))]
            Square
        }
    }
}
