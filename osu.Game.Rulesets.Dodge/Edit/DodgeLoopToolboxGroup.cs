using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Edit;
using osu.Game.Screens.Edit;
using osu.Game.Rulesets.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DodgeLoopToolboxGroup : EditorToolboxGroup
    {
        public enum LoopShape
        {
            [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.MovementPingPong))]
            PingPong,
            [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.MovementOrbitClockwise))]
            Clockwise,
            [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.MovementOrbitCounterclockwise))]
            Counterclockwise,
        }

        public enum LoopScope
        {
            [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.LoopIndividual))]
            Individual,
            [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.LoopTranslatePattern))]
            TranslatePattern,
            [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.LoopRotatePattern))]
            RotatePattern,
        }

        [Resolved]
        private EditorBeatmap beatmap { get; set; } = null!;

        public readonly Bindable<LoopShape> Shape = new Bindable<LoopShape>();
        public readonly Bindable<LoopScope> Scope = new Bindable<LoopScope>();
        public readonly BindableFloat PeriodBeats = new BindableFloat(4) { MinValue = 0.25f, MaxValue = 64, Precision = 0.25f };
        public readonly BindableInt Repetitions = new BindableInt(4) { MinValue = 1, MaxValue = 32 };
        public readonly BindableFloat OffsetX = new BindableFloat(128) { MinValue = -4096, MaxValue = 4096, Precision = 1 };
        public readonly BindableFloat OffsetY = new BindableFloat(0) { MinValue = -4096, MaxValue = 4096, Precision = 1 };
        private ExpandableButton apply = null!;

        public DodgeLoopToolboxGroup() : base(DodgeEditorStrings.LoopingMotion)
        {
            Expanded.BindValueChanged(value =>
            {
                if (!value.NewValue)
                    Expanded.Value = true;
            });
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Spacing = new Vector2(5);
            Children = new Drawable[]
            {
                new SettingsEnumDropdown<LoopShape> { LabelText = DodgeEditorStrings.MovementType, Current = Shape },
                new SettingsEnumDropdown<LoopScope> { LabelText = DodgeEditorStrings.LoopScope, Current = Scope },
                new ExpandableSlider<float> { ExpandedLabelText = DodgeEditorStrings.LoopPeriodBeats, Current = PeriodBeats, KeyboardStep = 0.25f },
                new ExpandableSlider<int> { ExpandedLabelText = DodgeEditorStrings.LoopRepetitions, Current = Repetitions, KeyboardStep = 1 },
                new ExpandableSlider<float> { ExpandedLabelText = DodgeEditorStrings.LoopOffsetX, Current = OffsetX, KeyboardStep = 16 },
                new ExpandableSlider<float> { ExpandedLabelText = DodgeEditorStrings.LoopOffsetY, Current = OffsetY, KeyboardStep = 16 },
                new osu.Game.Graphics.Containers.OsuTextFlowContainer(t => t.Font = osu.Game.Graphics.OsuFont.Default.With(size: 11))
                {
                    Text = DodgeEditorStrings.LoopHint,
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
                apply = new ExpandableButton
                {
                    RelativeSizeAxes = Axes.X,
                    ContractedLabelText = DodgeEditorStrings.ApplyLoop,
                    ExpandedLabelText = DodgeEditorStrings.ApplyLoop,
                    Action = ApplyLoop,
                },
            };
            Scope.BindValueChanged(value =>
            {
                if (value.NewValue == LoopScope.RotatePattern && Shape.Value == LoopShape.PingPong)
                    Shape.Value = LoopShape.Clockwise;
            });
            Shape.BindValueChanged(value =>
            {
                if (value.NewValue == LoopShape.PingPong && Scope.Value == LoopScope.RotatePattern)
                    Scope.Value = LoopScope.TranslatePattern;
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            beatmap.SelectedHitObjects.CollectionChanged += selectionChanged;
            beatmap.HitObjectUpdated += objectUpdated;
            readEmitterSelection();
        }

        private void selectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
            => Scheduler.AddOnce(readEmitterSelection);

        private void objectUpdated(HitObject item)
        {
            if (beatmap.SelectedHitObjects.Contains(item))
                Scheduler.AddOnce(readEmitterSelection);
        }

        private void readEmitterSelection()
        {
            var emitter = beatmap.SelectedHitObjects.OfType<DodgeEmitter>().FirstOrDefault();
            if (emitter == null || !emitter.MovementType.IsLooping())
                return;
            Shape.Value = emitter.MovementType switch
            {
                DodgeMovementType.OrbitClockwise => LoopShape.Clockwise,
                DodgeMovementType.OrbitCounterclockwise => LoopShape.Counterclockwise,
                _ => LoopShape.PingPong,
            };
            Scope.Value = emitter.LoopScope switch
            {
                DodgeEmitterLoopScope.TranslatePattern => LoopScope.TranslatePattern,
                DodgeEmitterLoopScope.RotatePattern => LoopScope.RotatePattern,
                _ => LoopScope.Individual,
            };
            Repetitions.Value = emitter.WaveCycles;
            PeriodBeats.Value = (float)(emitter.Duration / Math.Max(1, emitter.WaveCycles)
                / beatmap.ControlPointInfo.TimingPointAt(emitter.StartTime).BeatLength);
            OffsetX.Value = emitter.LoopOffset.X;
            OffsetY.Value = emitter.LoopOffset.Y;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (beatmap != null)
            {
                beatmap.SelectedHitObjects.CollectionChanged -= selectionChanged;
                beatmap.HitObjectUpdated -= objectUpdated;
            }
            base.Dispose(isDisposing);
        }

        protected override void Update()
        {
            base.Update();
            apply.Enabled.Value = CanApply;
        }

        public bool CanApply => beatmap.SelectedHitObjects.Count > 0
            && beatmap.SelectedHitObjects.All(item => item is DodgeBullet or DodgeEmitter)
            && (Scope.Value != LoopScope.RotatePattern || Shape.Value != LoopShape.PingPong);

        public void ApplyLoop()
        {
            if (!CanApply)
                return;
            DodgeHitObject[] objects = beatmap.SelectedHitObjects.Cast<DodgeHitObject>().ToArray();
            double originTime = objects.Min(item => item.StartTime);
            double groupPeriod = beatmap.ControlPointInfo.TimingPointAt(originTime).BeatLength * PeriodBeats.Value;
            Vector2 centre = objects.OfType<DodgeBullet>().Select(b => b.Position).Aggregate(Vector2.Zero, (sum, p) => sum + p)
                             / Math.Max(1, objects.OfType<DodgeBullet>().Count());
            var movement = Shape.Value switch
            {
                LoopShape.Clockwise => DodgeMovementType.OrbitClockwise,
                LoopShape.Counterclockwise => DodgeMovementType.OrbitCounterclockwise,
                _ => DodgeMovementType.PingPong,
            };
            beatmap.BeginChange();
            try
            {
                foreach (DodgeHitObject item in objects)
                {
                    double period = Scope.Value == LoopScope.Individual
                        ? beatmap.ControlPointInfo.TimingPointAt(item.StartTime).BeatLength * PeriodBeats.Value : groupPeriod;
                    item.MovementType = movement;
                    if (Scope.Value != LoopScope.Individual)
                        item.MovementEasing = DodgeMovementEasing.Linear;
                    item.WaveCycles = Repetitions.Value;
                    item.WavePhase = Scope.Value == LoopScope.Individual ? 0
                        : (float)((((item.StartTime - originTime) / period + 0.5) % 1 - 0.5) * 360);
                    if (item is DodgeBullet bullet)
                    {
                        bullet.Duration = period * Repetitions.Value;
                        bullet.ContinueUntilExit = false;
                        if (Scope.Value == LoopScope.TranslatePattern)
                            bullet.EndPosition = bullet.Position + new Vector2(OffsetX.Value, OffsetY.Value);
                        else if (Scope.Value == LoopScope.RotatePattern)
                            bullet.EndPosition = centre * 2 - bullet.Position;
                    }
                    else if (item is DodgeEmitter emitter)
                    {
                        emitter.Duration = period * Repetitions.Value;
                        emitter.ContinueUntilExit = false;
                        emitter.LoopScope = Scope.Value switch
                        {
                            LoopScope.TranslatePattern => DodgeEmitterLoopScope.TranslatePattern,
                            LoopScope.RotatePattern => DodgeEmitterLoopScope.RotatePattern,
                            _ => DodgeEmitterLoopScope.Individual,
                        };
                        emitter.LoopOffset = new Vector2(OffsetX.Value, OffsetY.Value);
                    }
                    beatmap.Update(item);
                }
            }
            finally
            {
                beatmap.EndChange();
            }
        }
    }
}
