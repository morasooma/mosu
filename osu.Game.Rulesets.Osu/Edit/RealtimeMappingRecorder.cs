using System;
using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Framework.Input.Events;
using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Screens.Edit;
using osuTK;

namespace osu.Game.Rulesets.Osu.Edit
{
    /// <summary>
    /// Records editor-clock timestamps, not wall-clock time, so tempo changes do not alter the map.
    /// </summary>
    public partial class RealtimeMappingRecorder : Component, IKeyBindingHandler<OsuAction>
    {
        private readonly EditorBeatmap beatmap;
        private readonly EditorClock clock;
        private readonly Func<Vector2?> cursorPosition;
        private readonly Action<HitObject?> preview;
        private readonly List<HitObject> recorded = new List<HitObject>();
        private readonly List<Vector2> points = new List<Vector2>();
        private OsuAction? heldKey;
        private double rawStartTime;
        private double lastTime;
        private Vector2 origin;

        public bool Recording { get; private set; }
        public double Offset { get; set; }
        public Action? StateChanged { get; set; }
        public Action? NotePressed { get; set; }
        public IReadOnlyList<HitObject> RecordedObjects => recorded;

        public RealtimeMappingRecorder(EditorBeatmap beatmap, EditorClock clock, Func<Vector2?> cursorPosition, Action<HitObject?> preview)
        {
            this.beatmap = beatmap;
            this.clock = clock;
            this.cursorPosition = cursorPosition;
            this.preview = preview;
        }

        public void Toggle()
        {
            if (Recording)
            {
                Stop();
                clock.Stop();
                return;
            }

            recorded.Clear();
            Recording = true;
            lastTime = clock.CurrentTime;
            clock.Start();
            StateChanged?.Invoke();
        }

        public void Stop()
        {
            if (heldKey.HasValue)
                finish();
            Recording = false;
            StateChanged?.Invoke();
        }

        public bool OnPressed(KeyBindingPressEvent<OsuAction> e)
        {
            if (!Recording)
                return false;

            if (e.Action != OsuAction.LeftButton && e.Action != OsuAction.RightButton)
                return false;

            if (e.Repeat || !clock.IsRunning)
                return true;

            var position = cursorPosition();
            if (!position.HasValue)
                return true;

            // Alternating keys may overlap slightly; finish the previous object on the new press.
            if (heldKey.HasValue)
                finish();

            double snappedPress = beatmap.SnapTime(clock.CurrentTime - Offset, null);
            if (IsTimeSlotOccupied(recorded, snappedPress) || IsTimeSlotOccupied(beatmap.HitObjects, snappedPress))
                return true;

            heldKey = e.Action;
            origin = position.Value;
            // Capture geometry now. Offset and beat snapping must never select a past cursor position.
            rawStartTime = clock.CurrentTime;
            points.Clear();
            points.Add(Vector2.Zero);
            var circle = new HitCircle { StartTime = rawStartTime, Position = origin };
            circle.Samples.Add(new HitSampleInfo(HitSampleInfo.HIT_NORMAL));
            preview(circle);
            NotePressed?.Invoke();
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<OsuAction> e)
        {
            if (Recording && heldKey == e.Action)
                finish();
        }

        protected override void Update()
        {
            base.Update();
            if (!Recording)
                return;

            if (clock.CurrentTime < lastTime)
            {
                cancelPending();
                Stop();
                return;
            }

            if (!clock.IsRunning)
            {
                Stop();
                return;
            }

            lastTime = clock.CurrentTime;
            if (heldKey.HasValue && cursorPosition() is Vector2 position)
            {
                Vector2 relative = position - origin;
                if (Vector2.DistanceSquared(points[^1], relative) >= 9 && points.Count < 4096)
                {
                    points.Add(relative);
                    if (clock.CurrentTime - rawStartTime >= 100)
                        preview(CreateRecordedObject(beatmap, rawStartTime, clock.CurrentTime,
                            clock.CurrentTime - rawStartTime, origin, points));
                }
            }
        }

        private void finish()
        {
            if (!heldKey.HasValue)
                return;

            if (cursorPosition() is Vector2 position && Vector2.DistanceSquared(points[^1], position - origin) > 0.01f)
                points.Add(position - origin);

            HitObject obj = CreateSnappedRecordedObject(beatmap, rawStartTime, clock.CurrentTime, Offset, origin, points);
            cancelPending();
            recorded.Add(obj);
        }

        internal static bool IsTimeSlotOccupied(IReadOnlyList<HitObject> objects, double snappedTime)
        {
            foreach (var obj in objects)
            {
                if (Math.Abs(obj.StartTime - snappedTime) < 0.01)
                    return true;
            }
            return false;
        }

        internal static HitObject CreateSnappedRecordedObject(EditorBeatmap beatmap, double pressTime, double releaseTime,
                                                              double offset, Vector2 capturedPosition, IReadOnlyList<Vector2> capturedPath)
        {
            double startTime = beatmap.SnapTime(pressTime - offset, null);
            double endTime = beatmap.SnapTime(releaseTime - offset, startTime);
            return CreateRecordedObject(beatmap, startTime, endTime, releaseTime - pressTime, capturedPosition, capturedPath);
        }

        internal static HitObject CreateRecordedObject(EditorBeatmap beatmap, double startTime, double endTime,
                                                       double heldDuration, Vector2 origin, IReadOnlyList<Vector2> points)
        {
            double duration = endTime - startTime;
            HitObject obj = new HitCircle { StartTime = startTime, Position = origin };
            obj.Samples.Add(new HitSampleInfo(HitSampleInfo.HIT_NORMAL));

            // Short taps, or holding without a usable path, remain circles.
            if (heldDuration >= 100 && duration > 0 && points.Count > 1)
            {
                var slider = new Slider { StartTime = startTime, Position = origin };
                var builder = new IncrementalBSplineBuilder { Degree = 4, Tolerance = 1.8f, CornerThreshold = 0.4f };
                foreach (var point in points)
                    builder.AddLinearPoint(point);
                builder.Finish();
                foreach (var segment in builder.ControlPoints)
                {
                    if (segment.Count == 0)
                        continue;
                    slider.Path.ControlPoints.Add(new PathControlPoint(segment[0], PathType.BSpline(4)));
                    for (int i = 1; i < segment.Count - 1; i++)
                        slider.Path.ControlPoints.Add(new PathControlPoint(segment[i]));
                }
                // Keep the actual release position as the tail, even when the spline fitter
                // approximates its last sample slightly inside the captured path.
                slider.Path.ControlPoints.Add(new PathControlPoint(points[^1]));
                if (slider.Path.CalculatedDistance < 0.01)
                {
                    slider.Path.ControlPoints.Clear();
                    slider.Path.ControlPoints.Add(new PathControlPoint(Vector2.Zero, PathType.LINEAR));
                    slider.Path.ControlPoints.Add(new PathControlPoint(points[^1]));
                }
                slider.Samples.Add(new HitSampleInfo(HitSampleInfo.HIT_NORMAL));

                slider.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                double multiplier = slider.Path.CalculatedDistance / (duration * slider.Velocity);
                slider.SliderVelocityMultiplier = Math.Clamp(multiplier, 0.1, 10);
                slider.ApplyDefaults(beatmap.ControlPointInfo, beatmap.Difficulty);
                // At extreme velocities, trim/extend the path rather than lose the snapped end time.
                slider.Path.ExpectedDistance.Value = duration * slider.Velocity;
                obj = slider;
            }

            return obj;
        }

        private void cancelPending()
        {
            heldKey = null;
            points.Clear();
            preview(null);
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                StateChanged = null;
                cancelPending();
                Recording = false;
            }
            base.Dispose(isDisposing);
        }
    }
}
