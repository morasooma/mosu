// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Audio;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Objects.Drawables;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    public partial class DrawableDodgeBeam : DrawableHitObject<DodgeHitObject>, IDodgeCollisionSource
    {
        private DodgeBeam Beam => (DodgeBeam)HitObject;

        private Container beamBody = null!;
        private Box telegraphFill = null!;
        private Box activeFill = null!;

        [Resolved]
        private DodgePlayfield playfield { get; set; } = null!;

        private bool hasProcessedHit;
        private bool grazeActive;
        private double lastGrazeTime = double.NegativeInfinity;
        private Vector2 spawnCameraAnchor;
        private int cameraAnchorVersion = -1;

        /// <summary>
        /// Scroll drift: the beam rides the field scroll accumulated since its own
        /// start time, so it always appears where it was placed and then drifts
        /// with the field. Applied identically to the visual body and collisions.
        /// </summary>
        private Vector2 driftOffset(double time)
        {
            if (cameraAnchorVersion != playfield.CameraStateVersion)
            {
                spawnCameraAnchor = playfield.CameraOffsetAt(HitObject.StartTime);
                cameraAnchorVersion = playfield.CameraStateVersion;
            }

            return playfield.CameraOffsetAt(time) - spawnCameraAnchor;
        }

        public DrawableDodgeBeam(DodgeBeam hitObject)
            : base(hitObject)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Anchor = Anchor.TopLeft;
            Origin = Anchor.Centre;
            Size = Vector2.One;

            AddInternal(beamBody = new Container
            {
                Anchor = Anchor.TopLeft,
                Origin = Anchor.Centre,
                Masking = true,
                Children = new Drawable[]
                {
                    telegraphFill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.Gray,
                        Alpha = 0,
                    },
                    activeFill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Colour4.White,
                        Alpha = 0,
                    },
                },
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            playfield.RegisterCollisionSource(this);
        }

        protected override double InitialLifetimeOffset => Beam.TimePreempt;

        protected override void Update()
        {
            base.Update();
            updateAppearance();
        }

        private void updateAppearance()
        {
            float beamLength = Beam.BeamLength;
            float beamWidth = Beam.BeamWidth;
            Vector2 center = Beam.BeamCenter + driftOffset(Time.Current);
            float rotation = Beam.BeamRotation;

            beamBody.Position = center;
            beamBody.Size = new Vector2(beamLength, beamWidth);
            beamBody.Rotation = rotation;
            beamBody.BorderThickness = Beam.OutlineThickness;
            beamBody.BorderColour = Beam.OutlineColour;

            double currentTime = Time.Current;
            double preemptStart = Beam.StartTime - Beam.TimePreempt;

            if (currentTime < preemptStart)
            {
                telegraphFill.Alpha = 0;
                activeFill.Alpha = 0;
                return;
            }

            if (currentTime < Beam.StartTime)
            {
                // Preempt / telegraph phase
                float progress = (float)((currentTime - preemptStart) / Beam.TimePreempt);
                telegraphFill.Alpha = progress * 0.25f * Beam.Opacity;
                activeFill.Alpha = 0;
                return;
            }

            if (currentTime <= Beam.EndTime)
            {
                // Active phase
                telegraphFill.Alpha = 0;
                activeFill.Colour = Beam.Colour;
                activeFill.Alpha = Beam.Opacity;
                return;
            }

            // Past end time — handled by hit state transforms
        }

        public override IEnumerable<HitSampleInfo> GetSamples() => Array.Empty<HitSampleInfo>();

        public override void PlaySamples()
        {
        }

        protected override void CheckForResult(bool userTriggered, double timeOffset)
        {
            if (timeOffset >= 0 && !hasProcessedHit && !AllJudged && !Result.HasResult)
                ApplyMaxResult();
        }

        protected override void UpdateHitStateTransforms(ArmedState state)
        {
            switch (state)
            {
                case ArmedState.Hit:
                    this.FadeOut(120).Expire();
                    break;

                case ArmedState.Miss:
                    using (BeginAbsoluteSequence(Beam.EndTime))
                        this.FadeOut(160).Expire();
                    break;
            }
        }

        #region IDodgeCollisionSource

        public double CollisionStartTime => Beam.StartTime;

        public double CollisionEndTime => Beam.EndTime;

        public bool CollisionProcessingComplete => hasProcessedHit || AllJudged || Result.HasResult;

        public void ProcessCollisions(
            double currentTime,
            Vector2 previousPlayerPosition,
            Vector2 currentPlayerPosition,
            bool allowSweptCollision,
            bool isRewind)
        {
            if (isRewind)
            {
                rewindState(currentTime);
                return;
            }

            if (hasProcessedHit || AllJudged || Result.HasResult || currentTime < Beam.StartTime || currentTime > Beam.EndTime)
                return;

            Vector2 center = Beam.BeamCenter + driftOffset(currentTime);
            Vector2 beamDir = Beam.BeamDirection;
            Vector2 perpDir = Beam.PerpendicularDirection;
            float beamLength = Beam.BeamLength;
            float beamWidth = Beam.BeamWidth;
            float playerSize = playfield.PlayerSize;
            float grazeDistance = playfield.GrazeDistance;

            // Check graze first
            bool withinGraze;

            if (allowSweptCollision)
            {
                withinGraze = DodgeBeam.IsWithinGrazeDistanceSwept(
                    center, beamDir, perpDir, beamLength, beamWidth,
                    previousPlayerPosition, currentPlayerPosition,
                    playerSize, grazeDistance);
            }
            else
            {
                withinGraze = DodgeBeam.IsWithinGrazeDistance(
                    center, beamDir, perpDir, beamLength, beamWidth,
                    currentPlayerPosition, playerSize, grazeDistance);
            }

            if (withinGraze && !grazeActive)
            {
                grazeActive = true;
                lastGrazeTime = currentTime;
                playfield.RegisterGraze(currentTime, true);
            }
            else if (!withinGraze && grazeActive)
            {
                grazeActive = false;
            }

            // Check collision
            bool intersects;

            if (allowSweptCollision)
            {
                intersects = DodgeBeam.IntersectsPlayerSwept(
                    center, beamDir, perpDir, beamLength, beamWidth,
                    previousPlayerPosition, currentPlayerPosition,
                    playerSize);
            }
            else
            {
                intersects = DodgeBeam.IntersectsPlayer(
                    center, beamDir, perpDir, beamLength, beamWidth,
                    currentPlayerPosition, playerSize);
            }

            if (intersects && !AllJudged && !Result.HasResult)
            {
                hasProcessedHit = true;

                if (grazeActive)
                {
                    playfield.RegisterGraze(currentTime, false);
                    grazeActive = false;
                }

                playfield.TriggerMissFeedback();
                ApplyMinResult();
            }
        }

        private void rewindState(double currentTime)
        {
            if (currentTime < Beam.StartTime)
            {
                hasProcessedHit = false;
                grazeActive = false;
            }
        }

        #endregion

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (playfield != null)
                playfield.UnregisterCollisionSource(this);
        }
    }
}
