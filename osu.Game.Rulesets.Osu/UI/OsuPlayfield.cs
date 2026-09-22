// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Primitives;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Osu.Beatmaps;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables.Connections;
using osu.Game.Rulesets.Osu.UI.Cursor;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    [Cached]
    public partial class OsuPlayfield : Playfield
    {
        private const int max_concurrent_performance_basic_judgements = 8;
        private const int max_concurrent_performance_tick_judgements = 1;
        private const float gameplay_render_buffer_padding = 96f;
        private readonly Container borderContainer;
        private readonly PlayfieldBorder playfieldBorder;
        private readonly ProxyContainer approachCircles;
        private readonly ProxyContainer spinnerProxies;
        private readonly JudgementContainer<DrawableOsuJudgement> judgementLayer;
        private readonly AimAssistController aimAssistController;
        private readonly RelaxController relaxController;
        private readonly ObservedHitObjectGraph? observedHitObjectGraph;
        private bool directChildrenLifeStable;
        private readonly List<DrawableOsuJudgement> activePerformanceBasicJudgements = new List<DrawableOsuJudgement>(max_concurrent_performance_basic_judgements + 2);
        private readonly List<DrawableOsuJudgement> activePerformanceTickJudgements = new List<DrawableOsuJudgement>(max_concurrent_performance_tick_judgements + 2);

        private readonly JudgementPooler<DrawableOsuJudgement> judgementPooler;

        // For osu! gameplay, everything is always on screen.
        // Skipping masking calculations improves performance in intense beatmaps (ie. https://osu.ppy.sh/beatmapsets/150945#osu/372245)
        public override bool UpdateSubTreeMasking() => false;

        public SmokeContainer Smoke { get; }
        public FollowPointRenderer FollowPoints { get; }

        public static readonly Vector2 BASE_SIZE = new Vector2(512, 384);

        protected override GameplayCursorContainer? CreateCursor() => new OsuCursorContainer();

        public override Quad SkinnableComponentScreenSpaceDrawQuad => playfieldBorder.ScreenSpaceDrawQuad;

        public AimAssistController AimAssistController => aimAssistController;

        public RelaxController RelaxController => relaxController;

        /// <summary>
        /// The observed-hit-object anti-cheat monitor, or <c>null</c> while
        /// <see cref="UI.ObservedHitObjectGraph.FEATURE_ENABLED"/> is <c>false</c>.
        /// </summary>
        public ObservedHitObjectGraph? ObservedHitObjectGraph => observedHitObjectGraph;

        [Resolved]
        private OsuConfigManager config { get; set; } = null!;

        private readonly Container judgementAboveHitObjectLayer;
        private readonly GameplayRenderBuffer gameplayRenderBuffer;
        private readonly Bindable<float> gameplayRenderScale = new Bindable<float>();

        public OsuPlayfield()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            HitObjectContainer.Name = nameof(HitObjectContainer);

            var children = new List<Drawable>();

            children.Add(borderContainer = new Container
            {
                Name = nameof(borderContainer),
                RelativeSizeAxes = Axes.Both,
                Child = playfieldBorder = new PlayfieldBorder { RelativeSizeAxes = Axes.Both },
            });
            children.Add(new OriginalInputDisplay { RelativeSizeAxes = Axes.Both });
            children.Add(Smoke = new SmokeContainer { RelativeSizeAxes = Axes.Both });
            children.Add(spinnerProxies = new ProxyContainer
            {
                Name = nameof(spinnerProxies),
                RelativeSizeAxes = Axes.Both
            });
            children.Add(FollowPoints = new FollowPointRenderer { RelativeSizeAxes = Axes.Both });
            children.Add(judgementLayer = new JudgementContainer<DrawableOsuJudgement>
            {
                Name = nameof(judgementLayer),
                RelativeSizeAxes = Axes.Both
            });
            children.Add(HitObjectContainer);

            // Keep the dead anti-cheat monitor out of the scene entirely: while disabled it contributes scene
            // nodes and a per-frame Update() callback that can never do anything useful.
#pragma warning disable CS0162 // Unreachable code -- FEATURE_ENABLED is a compile-time master switch.
            if (UI.ObservedHitObjectGraph.FEATURE_ENABLED)
            {
                children.Add(observedHitObjectGraph = new ObservedHitObjectGraph
                {
                    RelativeSizeAxes = Axes.Both,
                    Name = nameof(observedHitObjectGraph),
                });
            }
#pragma warning restore CS0162

            children.Add(aimAssistController = new AimAssistController { RelativeSizeAxes = Axes.Both });
            children.Add(relaxController = new RelaxController { RelativeSizeAxes = Axes.Both });
            children.Add(new MorasoomaEndTagController { RelativeSizeAxes = Axes.Both });
            children.Add(judgementAboveHitObjectLayer = new Container
            {
                Name = nameof(judgementAboveHitObjectLayer),
                RelativeSizeAxes = Axes.Both
            });
            children.Add(approachCircles = new ProxyContainer
            {
                Name = nameof(approachCircles),
                RelativeSizeAxes = Axes.Both
            });
            // Debug display placed LAST with high Depth so raw/assisted markers, offset line and radius ring
            // are drawn on top of hitobjects, approach circles, judgements, etc.
            children.Add(new AimAssistTargetDisplay(aimAssistController)
            {
                RelativeSizeAxes = Axes.Both,
                Depth = -9999,
            });

            InternalChild = gameplayRenderBuffer = new GameplayRenderBuffer
            {
                Name = nameof(gameplayRenderBuffer),
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = BASE_SIZE + new Vector2(gameplay_render_buffer_padding * 2),
                Padding = new MarginPadding(gameplay_render_buffer_padding),

                // Preserve the original direct draw path unless reduced resolution is explicitly requested.
                BufferingEnabled = false,
                Children = children.ToArray(),
            };

            HitPolicy = new StartTimeOrderedHitPolicy();

            AddInternal(judgementPooler = new JudgementPooler<DrawableOsuJudgement>(new[]
            {
                HitResult.Great,
                HitResult.Ok,
                HitResult.Meh,
                HitResult.Miss,
                HitResult.LargeTickHit,
                HitResult.SliderTailHit,
                HitResult.LargeTickMiss,
                HitResult.IgnoreMiss,
            }));

            NewResult += onNewResult;
        }

        private IHitPolicy hitPolicy;

        public IHitPolicy HitPolicy
        {
            get => hitPolicy;
            [MemberNotNull(nameof(hitPolicy))]
            set
            {
                hitPolicy = value ?? throw new ArgumentNullException(nameof(value));
                hitPolicy.HitObjectContainer = HitObjectContainer;
            }
        }

        protected override void OnNewDrawableHitObject(DrawableHitObject drawable)
        {
            ((DrawableOsuHitObject)drawable).CheckHittable = hitPolicy.CheckHittable;

            Debug.Assert(!drawable.IsLoaded, $"Already loaded {nameof(DrawableHitObject)} is added to {nameof(OsuPlayfield)}");
            drawable.OnLoadComplete += onDrawableHitObjectLoaded;
        }

        private void onDrawableHitObjectLoaded(Drawable drawable)
        {
            // note: `Slider`'s `ProxiedLayer` is added when its nested `DrawableHitCircle` is loaded.
            switch (drawable)
            {
                case DrawableSpinner:
                    spinnerProxies.Add(drawable.CreateProxy());
                    break;

                case DrawableHitCircle hitCircle:
                    approachCircles.Add(hitCircle.ProxiedLayer.CreateProxy());
                    break;
            }
        }

        [BackgroundDependencyLoader]
        private void load(OsuRulesetConfigManager? config, IBeatmap? beatmap)
        {
            config?.BindWith(OsuRulesetSetting.PlayfieldBorderStyle, playfieldBorder.PlayfieldBorderStyle);

            this.config.BindWith(OsuSetting.ForkGameplayRenderScale, gameplayRenderScale);
            gameplayRenderScale.BindValueChanged(scale =>
            {
                gameplayRenderBuffer.FrameBufferScale = new Vector2(scale.NewValue);

                // At exactly 100%, BufferedContainer draws its children directly and allocates/uses no framebuffer.
                gameplayRenderBuffer.BufferingEnabled = scale.NewValue < 1f;
            }, true);

            var osuBeatmap = (OsuBeatmap?)beatmap;

            RegisterPool<HitCircle, DrawableHitCircle>(20, 100);

            // handle edge cases where a beatmap has a slider with many repeats.
            int maxRepeatsOnOneSlider = 0;
            int maxTicksOnOneSlider = 0;

            if (osuBeatmap != null)
            {
                foreach (var slider in osuBeatmap.HitObjects.OfType<Slider>())
                {
                    maxRepeatsOnOneSlider = Math.Max(maxRepeatsOnOneSlider, slider.RepeatCount);
                    maxTicksOnOneSlider = Math.Max(maxTicksOnOneSlider, slider.NestedHitObjects.OfType<SliderTick>().Count());
                }
            }

            RegisterPool<Slider, DrawableSlider>(20, 100);
            RegisterPool<SliderHeadCircle, DrawableSliderHead>(20, 100);
            RegisterPool<SliderTailCircle, DrawableSliderTail>(20, 100);
            RegisterPool<SliderTick, DrawableSliderTick>(Math.Max(maxTicksOnOneSlider, 20), Math.Max(maxTicksOnOneSlider, 200));
            RegisterPool<SliderRepeat, DrawableSliderRepeat>(Math.Max(maxRepeatsOnOneSlider, 20), Math.Max(maxRepeatsOnOneSlider, 200));

            RegisterPool<Spinner, DrawableSpinner>(2, 20);
            RegisterPool<SpinnerTick, DrawableSpinnerTick>(10, 200);
            RegisterPool<SpinnerBonusTick, DrawableSpinnerBonusTick>(10, 200);

            if (beatmap != null)
                ApplyCircleSizeToPlayfieldBorder(beatmap);
        }

        protected void ApplyCircleSizeToPlayfieldBorder(IBeatmap beatmap)
        {
            borderContainer.Padding = new MarginPadding(OsuHitObject.OBJECT_RADIUS * -LegacyRulesetExtensions.CalculateScaleFromCircleSize(beatmap.Difficulty.CircleSize, true));
        }

        protected override HitObjectLifetimeEntry CreateLifetimeEntry(HitObject hitObject) => new OsuHitObjectLifetimeEntry(hitObject);

        protected override bool CheckChildrenLife()
        {
            if (directChildrenLifeStable)
                return false;

            bool aliveChanged = base.CheckChildrenLife();
            directChildrenLifeStable = InternalChildren.Count == AliveInternalChildren.Count;
            return aliveChanged;
        }

        protected override void AddInternal(Drawable drawable)
        {
            directChildrenLifeStable = false;
            base.AddInternal(drawable);
        }

        protected override bool RemoveInternal(Drawable drawable, bool disposeImmediately)
        {
            directChildrenLifeStable = false;
            return base.RemoveInternal(drawable, disposeImmediately);
        }

        protected override void ClearInternal(bool disposeChildren = true)
        {
            directChildrenLifeStable = false;
            base.ClearInternal(disposeChildren);
        }

        protected override void OnHitObjectAdded(HitObject hitObject)
        {
            base.OnHitObjectAdded(hitObject);
            FollowPoints.AddFollowPoints((OsuHitObject)hitObject);
        }

        protected override void OnHitObjectRemoved(HitObject hitObject)
        {
            base.OnHitObjectRemoved(hitObject);
            FollowPoints.RemoveFollowPoints((OsuHitObject)hitObject);
        }

        private void onNewResult(DrawableHitObject judgedObject, JudgementResult result)
        {
            // Hitobjects that block future hits should miss previous hitobjects if they're hit out-of-order.
            hitPolicy.HandleHit(judgedObject);

            var visualType = result.VisualType ?? result.Type;

            if (!judgedObject.DisplayResult || !DisplayJudgements.Value || (SkinPerformanceMode.Enabled && visualType == HitResult.Great))
                return;

            // Fork setting: hide visual crosses (miss judgements) for slider ticks/tails/repeats.
            // Slider head misses are still shown so the player can see approach misses.
            if (config?.Get<bool>(OsuSetting.ForkHideVisualSliderMisses) == true)
            {
                var actualResult = result.VisualType ?? result.Type;
                if (!actualResult.IsHit() && (judgedObject is DrawableSliderTick || judgedObject is DrawableSliderTail || judgedObject is DrawableSliderRepeat))
                    return;
            }

            var explosion = judgementPooler.Get(visualType, doj => doj.Apply(result, judgedObject));

            if (explosion == null)
                return;

            judgementLayer.Add(explosion);

            if (SkinPerformanceMode.Enabled)
                trimPerformanceJudgements(explosion, visualType);

            if (explosion.ProxiedAboveHitObjectsContent is Drawable proxiedAboveHitObjectsContent)
            {
                if (proxiedAboveHitObjectsContent.Parent == null)
                    judgementAboveHitObjectLayer.Add(proxiedAboveHitObjectsContent);

                // the proxied content is added to judgementAboveHitObjectLayer lazily, and never removed from it.
                // ensure that ordering is consistent with expectations (latest judgement should be front-most).
                judgementAboveHitObjectLayer.ChangeChildDepth(proxiedAboveHitObjectsContent, (float)-result.TimeAbsolute);
            }
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => HitObjectContainer.ReceivePositionalInputAt(screenSpacePos);

        private void trimPerformanceJudgements(DrawableOsuJudgement latestJudgement, HitResult latestResult)
        {
            var bucket = getPerformanceJudgementBucket(latestResult);

            if (bucket == PerformanceJudgementBucket.None)
                return;

            removeTrackedPerformanceJudgement(latestJudgement);

            var trackedJudgements = getTrackedPerformanceJudgements(bucket);

            for (int i = trackedJudgements.Count - 1; i >= 0; i--)
            {
                if (!isTrackedPerformanceJudgementActive(trackedJudgements[i], bucket))
                    trackedJudgements.RemoveAt(i);
            }

            trackedJudgements.Add(latestJudgement);

            int maxConcurrent = bucket == PerformanceJudgementBucket.Tick
                ? max_concurrent_performance_tick_judgements
                : max_concurrent_performance_basic_judgements;

            while (trackedJudgements.Count > maxConcurrent)
            {
                var staleJudgement = trackedJudgements[0];
                trackedJudgements.RemoveAt(0);

                if (!isTrackedPerformanceJudgementActive(staleJudgement, bucket))
                    continue;

                staleJudgement.ClearTransforms();
                staleJudgement.FadeOut(40);
                staleJudgement.Expire();
            }
        }

        private void removeTrackedPerformanceJudgement(DrawableOsuJudgement judgement)
        {
            activePerformanceBasicJudgements.Remove(judgement);
            activePerformanceTickJudgements.Remove(judgement);
        }

        private List<DrawableOsuJudgement> getTrackedPerformanceJudgements(PerformanceJudgementBucket bucket) =>
            bucket == PerformanceJudgementBucket.Tick
                ? activePerformanceTickJudgements
                : activePerformanceBasicJudgements;

        private bool isTrackedPerformanceJudgementActive(DrawableOsuJudgement judgement, PerformanceJudgementBucket expectedBucket) =>
            judgement.Result != null
            && getPerformanceJudgementBucket(judgement.Result.Type) == expectedBucket
            && judgement.LifetimeEnd > Time.Current;

        private static PerformanceJudgementBucket getPerformanceJudgementBucket(HitResult result)
        {
            if (result.IsTick() || result == HitResult.IgnoreMiss)
                return PerformanceJudgementBucket.Tick;

            if (result.IsHit())
                return PerformanceJudgementBucket.Basic;

            return PerformanceJudgementBucket.None;
        }

        private enum PerformanceJudgementBucket
        {
            None,
            Basic,
            Tick,
        }

        /// <summary>
        /// Keeps the fixed gameplay layer list out of per-frame lifetime checks while providing an optional
        /// low-resolution framebuffer. Its framebuffer path remains disabled at the default 100% scale.
        /// </summary>
        private partial class GameplayRenderBuffer : BufferedContainer
        {
            private bool directChildrenLifeStable;

            // Standalone public build note: BufferingEnabled is an optional Mosu framework extension on BufferedContainer.
            public bool BufferingEnabled { get; set; }

            public GameplayRenderBuffer()
            {
                // Children are blended into a transparent target, leaving premultiplied RGB in the framebuffer.
                // Use premultiplied blending for the final upscale to avoid applying alpha a second time, which
                // otherwise darkens and discolours anti-aliased edges around hit objects.
                EffectBlending = new BlendingParameters
                {
                    Source = BlendingType.One,
                    Destination = BlendingType.OneMinusSrcAlpha,
                    SourceAlpha = BlendingType.One,
                    DestinationAlpha = BlendingType.One,
                    RGBEquation = BlendingEquation.Add,
                    AlphaEquation = BlendingEquation.Add,
                };
            }

            protected override bool CheckChildrenLife()
            {
                if (directChildrenLifeStable)
                    return false;

                bool aliveChanged = base.CheckChildrenLife();
                directChildrenLifeStable = InternalChildren.Count == AliveInternalChildren.Count;
                return aliveChanged;
            }

            protected override void AddInternal(Drawable drawable)
            {
                directChildrenLifeStable = false;
                base.AddInternal(drawable);
            }

            protected override bool RemoveInternal(Drawable drawable, bool disposeImmediately)
            {
                directChildrenLifeStable = false;
                return base.RemoveInternal(drawable, disposeImmediately);
            }

            protected override void ClearInternal(bool disposeChildren = true)
            {
                directChildrenLifeStable = false;
                base.ClearInternal(disposeChildren);
            }
        }

        private OsuResumeOverlay.OsuResumeOverlayInputBlocker? resumeInputBlocker;

        public void AttachResumeOverlayInputBlocker(OsuResumeOverlay.OsuResumeOverlayInputBlocker resumeInputBlocker)
        {
            Debug.Assert(this.resumeInputBlocker == null);
            this.resumeInputBlocker = resumeInputBlocker;
            AddInternal(resumeInputBlocker);
        }

        private partial class ProxyContainer : LifetimeManagementContainer
        {
            public void Add(Drawable proxy) => AddInternal(proxy);
        }

        private class OsuHitObjectLifetimeEntry : HitObjectLifetimeEntry
        {
            public OsuHitObjectLifetimeEntry(HitObject hitObject)
                : base(hitObject)
            {
                // Prevent past objects in idles states from remaining alive as their end times are skipped in non-frame-stable contexts.
                LifetimeEnd = HitObject.GetEndTime() + HitObject.HitWindows.WindowFor(HitResult.Miss);
            }

            protected override double InitialLifetimeOffset => ((OsuHitObject)HitObject).TimePreempt;
        }
    }
}
