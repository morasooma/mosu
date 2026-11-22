// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.ControlPoints;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Legacy;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Scoring;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Osu.Objects
{
    public abstract class OsuHitObject : HitObject, IHasComboInformation, IHasPosition, IHasTimePreempt
    {
        /// <summary>
        /// The radius of hit objects (ie. the radius of a <see cref="HitCircle"/>).
        /// </summary>
        public const float OBJECT_RADIUS = 64;

        /// <summary>
        /// The width and height any element participating in display of a hitcircle (or similarly sized object) should be.
        /// </summary>
        public static readonly Vector2 OBJECT_DIMENSIONS = new Vector2(OBJECT_RADIUS * 2);

        /// <summary>
        /// Scoring distance with a speed-adjusted beat length of 1 second (ie. the speed slider balls move through their track).
        /// </summary>
        internal const float BASE_SCORING_DISTANCE = 100;

        /// <summary>
        /// Minimum preempt time at AR=10.
        /// </summary>
        public const double PREEMPT_MIN = 450;

        /// <summary>
        /// Median preempt time at AR=5.
        /// </summary>
        public const double PREEMPT_MID = 1200;

        /// <summary>
        /// Maximum preempt time at AR=0.
        /// </summary>
        public const double PREEMPT_MAX = 1800;

        public static readonly DifficultyRange PREEMPT_RANGE = new DifficultyRange(PREEMPT_MAX, PREEMPT_MID, PREEMPT_MIN);

        public double TimePreempt { get; set; } = 600;
        public double TimeFadeIn = 400;

        /// <summary>
        /// The extra preempt added by Freeze Frame. Hidden excludes this from its fade-in duration
        /// to preserve the HD-before-FR behaviour regardless of beatmap mod application order.
        /// </summary>
        internal double FreezeFramePreemptAdjustment { get; set; }

        private HitObjectProperty<Vector2> position;

        public Bindable<Vector2> PositionBindable => position.Bindable;

        public virtual Vector2 Position
        {
            get => position.Value;
            set => position.Value = value;
        }

        public float X
        {
            get => Position.X;
            set => Position = new Vector2(value, Position.Y);
        }

        public float Y
        {
            get => Position.Y;
            set => Position = new Vector2(Position.X, value);
        }

        public Vector2 StackedPosition => Position + StackOffset;

        public virtual Vector2 EndPosition => Position;

        public Vector2 StackedEndPosition => EndPosition + StackOffset;

        private HitObjectProperty<int> stackHeight;

        private bool stackHeightBindableInitialised;

        public Bindable<int> StackHeightBindable
        {
            get
            {
                var bindable = stackHeight.Bindable;
                if (!stackHeightBindableInitialised)
                {
                    stackHeightBindableInitialised = true;
                    bindable.BindValueChanged(height => updateNestedStackHeights(height.NewValue));
                }
                return bindable;
            }
        }

        public int StackHeight
        {
            get => stackHeight.Value;
            set
            {
                if (stackHeightBindableInitialised)
                    stackHeight.Value = value; // existing bindable propagates changes/disabled checks
                else if (stackHeight.Value != value)
                {
                    stackHeight.Value = value;
                    updateNestedStackHeights(value);
                }
            }
        }

        public virtual Vector2 StackOffset => new Vector2(StackHeight * Scale * -6.4f);

        public double Radius => OBJECT_RADIUS * LegacyRulesetExtensions.SanitizeScale(Scale);

        private HitObjectProperty<float> scale = new HitObjectProperty<float>(1);

        public Bindable<float> ScaleBindable => scale.Bindable;

        public float Scale
        {
            get => scale.Value;
            set => scale.Value = value;
        }

        public virtual bool NewCombo { get; set; }

        private HitObjectProperty<int> comboOffset;

        public Bindable<int> ComboOffsetBindable => comboOffset.Bindable;

        public int ComboOffset
        {
            get => comboOffset.Value;
            set => comboOffset.Value = value;
        }

        private HitObjectProperty<int> indexInCurrentCombo;

        private ComboInformation? sharedComboInformation;
        private bool ownsSharedComboInformation;
        private bool indexInCurrentComboBound;
        private bool comboIndexBound;
        private bool comboIndexWithOffsetsBound;
        private bool comboBindableExposed;

        // Built-in slider children need the same combo values during calculation, but
        // do not need a bidirectional bindable graph until someone observes a bindable.
        // The compact shared state has no parent reference, so a retained old child
        // does not keep the whole slider/playable beatmap alive.
        private sealed class ComboInformation
        {
            public HitObjectProperty<int> IndexInCurrentCombo;
            public HitObjectProperty<int> ComboIndex;
            public HitObjectProperty<int> ComboIndexWithOffsets;
            public readonly List<WeakReference<OsuHitObject>> Children = new List<WeakReference<OsuHitObject>>();
            public bool BindablesMaterialised;

            public void MaterialiseBindings()
            {
                if (BindablesMaterialised) return;
                BindablesMaterialised = true;
                foreach (var reference in Children)
                {
                    if (reference.TryGetTarget(out var child)) child.bindSharedComboInformation();
                }
                Children.Clear();
            }
        }

        public Bindable<int> IndexInCurrentComboBindable
        {
            get
            {
                comboBindableExposed = true;
                if (sharedComboInformation == null) return indexInCurrentCombo.Bindable;
                var shared = sharedComboInformation;
                shared.MaterialiseBindings();
                return ownsSharedComboInformation ? shared.IndexInCurrentCombo.Bindable : indexInCurrentCombo.Bindable;
            }
        }

        public virtual int IndexInCurrentCombo
        {
            get => sharedComboInformation != null && (ownsSharedComboInformation || !indexInCurrentComboBound)
                ? sharedComboInformation.IndexInCurrentCombo.Value : indexInCurrentCombo.Value;
            set
            {
                if (sharedComboInformation != null && (ownsSharedComboInformation || !indexInCurrentComboBound))
                    sharedComboInformation.IndexInCurrentCombo.Value = value;
                else
                    indexInCurrentCombo.Value = value;
            }
        }

        private HitObjectProperty<int> comboIndex;

        public Bindable<int> ComboIndexBindable
        {
            get
            {
                comboBindableExposed = true;
                if (sharedComboInformation == null) return comboIndex.Bindable;
                var shared = sharedComboInformation;
                shared.MaterialiseBindings();
                return ownsSharedComboInformation ? shared.ComboIndex.Bindable : comboIndex.Bindable;
            }
        }

        public virtual int ComboIndex
        {
            get => sharedComboInformation != null && (ownsSharedComboInformation || !comboIndexBound)
                ? sharedComboInformation.ComboIndex.Value : comboIndex.Value;
            set
            {
                if (sharedComboInformation != null && (ownsSharedComboInformation || !comboIndexBound))
                    sharedComboInformation.ComboIndex.Value = value;
                else
                    comboIndex.Value = value;
            }
        }

        private HitObjectProperty<int> comboIndexWithOffsets;

        public Bindable<int> ComboIndexWithOffsetsBindable
        {
            get
            {
                comboBindableExposed = true;
                if (sharedComboInformation == null) return comboIndexWithOffsets.Bindable;
                var shared = sharedComboInformation;
                shared.MaterialiseBindings();
                return ownsSharedComboInformation ? shared.ComboIndexWithOffsets.Bindable : comboIndexWithOffsets.Bindable;
            }
        }

        public int ComboIndexWithOffsets
        {
            get => sharedComboInformation != null && (ownsSharedComboInformation || !comboIndexWithOffsetsBound)
                ? sharedComboInformation.ComboIndexWithOffsets.Value : comboIndexWithOffsets.Value;
            set
            {
                if (sharedComboInformation != null && (ownsSharedComboInformation || !comboIndexWithOffsetsBound))
                    sharedComboInformation.ComboIndexWithOffsets.Value = value;
                else
                    comboIndexWithOffsets.Value = value;
            }
        }

        private HitObjectProperty<bool> lastInCombo;

        public Bindable<bool> LastInComboBindable => lastInCombo.Bindable;

        public bool LastInCombo
        {
            get => lastInCombo.Value;
            set => lastInCombo.Value = value;
        }

        protected override void BindNestedComboInformation(IHasComboInformation nested, IHasComboInformation parent)
        {
            // Arbitrary ruleset/plugin subclasses retain the original eager binding
            // path, including their virtual scalar accessors and custom nested objects.
            Type nestedType = nested.GetType();
            if (GetType() != typeof(Slider) || !ReferenceEquals(parent, this)
                || (nestedType != typeof(SliderHeadCircle) && nestedType != typeof(SliderTailCircle)
                    && nestedType != typeof(SliderRepeat) && nestedType != typeof(SliderTick)))
            {
                base.BindNestedComboInformation(nested, parent);
                return;
            }

            if (sharedComboInformation == null)
            {
                sharedComboInformation = new ComboInformation
                {
                    ComboIndex = comboIndex,
                    ComboIndexWithOffsets = comboIndexWithOffsets,
                    IndexInCurrentCombo = indexInCurrentCombo,
                    BindablesMaterialised = comboBindableExposed,
                };
                ownsSharedComboInformation = true;
            }
            // Drop dead generations on reapplication, without keeping replaced children
            // alive. Public bindable exposure materialises the original graph for *all*
            // live children, so parent UnbindBindings/UnbindAll preserve their semantics.
            if (ReferenceEquals(nested, NestedHitObjects[0]))
                sharedComboInformation.Children.RemoveAll(reference => !reference.TryGetTarget(out _));
            var child = (OsuHitObject)nested;
            child.sharedComboInformation = sharedComboInformation;
            if (sharedComboInformation.BindablesMaterialised)
                child.bindSharedComboInformation();
            else
                sharedComboInformation.Children.Add(new WeakReference<OsuHitObject>(child));
        }

        private void bindSharedComboInformation()
        {
            if (sharedComboInformation == null || ownsSharedComboInformation) return;
            if (!comboIndexBound)
            {
                comboIndex.Bindable.BindTo(sharedComboInformation.ComboIndex.Bindable);
                comboIndexBound = true;
            }
            if (!comboIndexWithOffsetsBound)
            {
                comboIndexWithOffsets.Bindable.BindTo(sharedComboInformation.ComboIndexWithOffsets.Bindable);
                comboIndexWithOffsetsBound = true;
            }
            if (!indexInCurrentComboBound)
            {
                indexInCurrentCombo.Bindable.BindTo(sharedComboInformation.IndexInCurrentCombo.Bindable);
                indexInCurrentComboBound = true;
            }
            // Once the original weak binding graph exists, the child must not retain
            // the shared holder (and therefore the parent's bindables) strongly.
            sharedComboInformation = null;
        }

        internal void PrepareGameplayBindings()
        {
            // Calculation-only beatmaps never need this graph. Gameplay constructs
            // its drawable ruleset during background loading: restore the graph there,
            // not on the first Update frame that acquires a pooled drawable.
            _ = StackHeightBindable;
            sharedComboInformation?.MaterialiseBindings();
            foreach (var nested in NestedHitObjects)
            {
                if (nested is OsuHitObject osuHitObject) osuHitObject.PrepareGameplayBindings();
            }
        }

        private void updateNestedStackHeights(int height)
        {
            foreach (var nested in NestedHitObjects)
            {
                if (nested is OsuHitObject osuHitObject)
                    osuHitObject.StackHeight = height;
            }
        }

        protected override void ApplyDefaultsToSelf(ControlPointInfo controlPointInfo, IBeatmapDifficultyInfo difficulty)
        {
            base.ApplyDefaultsToSelf(controlPointInfo, difficulty);

            FreezeFramePreemptAdjustment = 0;
            TimePreempt = IBeatmapDifficultyInfo.DifficultyRangeInt(difficulty.ApproachRate, PREEMPT_RANGE);

            // Preempt time can go below 450ms. Normally, this is achieved via the DT mod which uniformly speeds up all animations game wide regardless of AR.
            // This uniform speedup is hard to match 1:1, however we can at least make AR>10 (via mods) feel good by extending the upper linear function above.
            // Note that this doesn't exactly match the AR>10 visuals as they're classically known, but it feels good.
            // This adjustment is necessary for AR>10, otherwise TimePreempt can become smaller leading to hitcircles not fully fading in.
            TimeFadeIn = 400 * Math.Min(1, TimePreempt / PREEMPT_MIN);

            Scale = LegacyRulesetExtensions.CalculateSignedScaleFromCircleSize(difficulty.CircleSize, true);
        }

        public void UpdateComboInformation(IHasComboInformation? lastObj)
        {
            // Note that this implementation is shared with the osu!catch ruleset's implementation.
            // If a change is made here, CatchHitObject.cs should also be updated.
            int index = lastObj?.ComboIndex ?? 0;
            int indexWithOffsets = lastObj?.ComboIndexWithOffsets ?? 0;
            int inCurrentCombo = (lastObj?.IndexInCurrentCombo + 1) ?? 0;

            // - For the purpose of combo colours, spinners never start a new combo even if they are flagged as doing so.
            // - At decode time, the first hitobject in the beatmap and the first hitobject after a spinner are both enforced to be a new combo,
            //   but this isn't directly enforced by the editor so the extra checks against the last hitobject are duplicated here.
            if (this is not Spinner && (NewCombo || lastObj == null || lastObj is Spinner))
            {
                inCurrentCombo = 0;
                index++;
                indexWithOffsets += ComboOffset + 1;

                lastObj?.LastInCombo = true;
            }

            ComboIndex = index;
            ComboIndexWithOffsets = indexWithOffsets;
            IndexInCurrentCombo = inCurrentCombo;
        }

        protected override HitWindows CreateHitWindows() => new OsuHitWindows();
    }
}
