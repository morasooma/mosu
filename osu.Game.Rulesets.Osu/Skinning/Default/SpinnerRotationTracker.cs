// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Utils;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Osu.Skinning.Default
{
    public partial class SpinnerRotationTracker : CircularContainer
    {
        public override bool IsPresent => true; // handle input when hidden

        private readonly DrawableSpinner drawableSpinner;

        private Vector2? mousePosition;
        private float? lastAngle;

        private float currentRotation;
        private bool rotationTransferred;
        private osu.Game.Rulesets.Osu.Scoring.StableSpinnerInput stableInput = new osu.Game.Rulesets.Osu.Scoring.StableSpinnerInput();
        private double stablePendingAngle;
        private double stablePendingElapsed;
        private double stableReplayTime = double.NegativeInfinity;
        private double? stableReplayAngle;

        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        public SpinnerRotationTracker(DrawableSpinner drawableSpinner)
        {
            this.drawableSpinner = drawableSpinner;
            drawableSpinner.HitObjectApplied += resetState;

            RelativeSizeAxes = Axes.Both;
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true;

        public bool Tracking { get; set; }

        /// <summary>
        /// Whether the spinning is spinning at a reasonable speed to be considered visually spinning.
        /// </summary>
        public readonly BindableBool IsSpinning = new BindableBool();

        /// <summary>
        /// Whether currently in the correct time range to allow spinning.
        /// </summary>
        public bool IsSpinnableTime => drawableSpinner.HitObject.StartTime <= Time.Current && drawableSpinner.HitObject.EndTime > Time.Current;

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            mousePosition = Parent!.ToLocalSpace(e.ScreenSpaceMousePosition);
            return base.OnMouseMove(e);
        }

        protected override void Update()
        {
            base.Update();

            if (mousePosition is Vector2 pos)
            {
                float thisAngle = -float.RadiansToDegrees(MathF.Atan2(pos.X - DrawSize.X / 2, pos.Y - DrawSize.Y / 2));
                float delta = lastAngle == null ? 0 : thisAngle - lastAngle.Value;

                // Normalise the delta to -180 .. 180
                if (delta > 180) delta -= 360;
                if (delta < -180) delta += 360;

                if (drawableSpinner.Result.StableState != null)
                {
                    double rate = Math.Abs(gameplayClock?.GetTrueGameplayRate() ?? Clock.Rate);
                    // Stable's normal-input branch assumes ~60 Hz samples. Lazer
                    // polls this tracker every update (often 1000 Hz): dividing each
                    // tiny delta by 16.67 ms makes rotation almost disappear. Batch
                    // input in wall-clock time before feeding that branch.
                    var replay = drawableSpinner.OsuActionInputManager?.ReplayInputHandler as osu.Game.Rulesets.Osu.Replays.OsuFramedReplayInputHandler;
                    if (replay?.CurrentFrame is { } frame)
                    {
                        // Stable processes the recorded samples, not lazer's
                        // interpolated cursor on every update. Do not resample .osr.
                        if (frame.Time < stableReplayTime)
                        {
                            stableReplayAngle = null;
                            stableReplayTime = double.NegativeInfinity;
                            stableInput = new osu.Game.Rulesets.Osu.Scoring.StableSpinnerInput();
                        }
                        if (frame.Time != stableReplayTime)
                        {
                            double angle = Math.Atan2(frame.Position.Y - 192, frame.Position.X - 256);
                            double angleDelta = stableReplayAngle.HasValue ? angle - stableReplayAngle.Value : 0;
                            if (angleDelta > Math.PI) angleDelta -= Math.PI * 2;
                            if (angleDelta < -Math.PI) angleDelta += Math.PI * 2;
                            stableInput.Sample(angleDelta, double.IsNegativeInfinity(stableReplayTime) ? 1000d / 60 : frame.Time - stableReplayTime,
                                rate, Tracking || drawableSpinner.StableRelaxTracking && IsSpinnableTime);
                            stableReplayTime = frame.Time;
                            stableReplayAngle = angle;
                        }
                    }
                    else
                    {
                        stablePendingAngle += delta;
                        stablePendingElapsed += rate > 0 ? Math.Max(0, Time.Elapsed) / rate : 0;
                        if (stablePendingElapsed >= 16.666666666666668)
                        {
                            delta = (float)stablePendingAngle;
                            double sampleElapsed = stablePendingElapsed;
                            stablePendingAngle = 0;
                            stablePendingElapsed = 0;
                            stableInput.Sample(float.DegreesToRadians(delta), sampleElapsed * rate, rate,
                                Tracking || drawableSpinner.StableRelaxTracking && IsSpinnableTime);
                        }
                    }
                }
                else if (Tracking)
                    AddRotation(delta);

                lastAngle = thisAngle;
            }

            if (drawableSpinner.Result.StableState is { } stable && IsSpinnableTime && !drawableSpinner.Result.HasResult)
            {
                double rate = Math.Abs(gameplayClock?.GetTrueGameplayRate() ?? Clock.Rate);
                float applied = stable.Advance(Time.Current, Time.Elapsed, stableInput.TargetVelocity, rate,
                    !drawableSpinner.HandleUserInput, drawableSpinner.StableRelaxTracking);
                currentRotation += applied;
                drawableSpinner.Result.History.ReportDelta(Time.Current, applied);
            }

            IsSpinning.Value = IsSpinnableTime && Math.Abs(currentRotation - Rotation) > 10f;
            Rotation = (float)Interpolation.Damp(Rotation, currentRotation, 0.99, Math.Abs(Time.Elapsed));
        }

        /// <summary>
        /// Rotate the disc by the provided angle (in addition to any existing rotation).
        /// </summary>
        /// <remarks>
        /// Will be a no-op if not a valid time to spin.
        /// </remarks>
        /// <param name="delta">The delta angle.</param>
        public void AddRotation(float delta)
        {
            if (!IsSpinnableTime || drawableSpinner.Result.StableState != null)
                return;

            if (!rotationTransferred)
            {
                currentRotation = Rotation;
                rotationTransferred = true;
            }

            Debug.Assert(Math.Abs(delta) <= 180);

            double rate = gameplayClock?.GetTrueGameplayRate() ?? Clock.Rate;
            delta = (float)(delta * Math.Abs(rate));

            currentRotation += delta;
            drawableSpinner.Result.History.ReportDelta(Time.Current, delta);
        }

        private void resetState(DrawableHitObject obj)
        {
            Tracking = false;
            IsSpinning.Value = false;
            mousePosition = null;
            lastAngle = null;
            currentRotation = 0;
            Rotation = 0;
            rotationTransferred = false;
            stableInput = new osu.Game.Rulesets.Osu.Scoring.StableSpinnerInput();
            stablePendingAngle = 0;
            stablePendingElapsed = 0;
            stableReplayTime = double.NegativeInfinity;
            stableReplayAngle = null;
            drawableSpinner.Result.StableState = osu.Game.Online.MosuServerEnvironment.UsesStableProtocol
                ? new osu.Game.Rulesets.Osu.Scoring.StableSpinnerState(drawableSpinner.HitObject.Duration, drawableSpinner.HitObject.StableOverallDifficulty)
                : null;
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (drawableSpinner.IsNotNull())
                drawableSpinner.HitObjectApplied -= resetState;
        }
    }
}
