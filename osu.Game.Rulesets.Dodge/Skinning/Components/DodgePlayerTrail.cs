// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Skinning.Components
{
    /// <summary>
    /// A bounded, skinnable after-image trail for the Dodge player.
    /// It uses a fixed number of reusable drawables and stores visual positions
    /// independently from the player's collision geometry.
    /// </summary>
    public partial class DodgePlayerTrail : CompositeDrawable
    {
        public const int MAX_SEGMENTS = 12;
        public const double SEGMENT_DURATION = 180;
        public const double SAMPLE_INTERVAL = 22;
        public const double SEEK_RESET_THRESHOLD = 250;
        public const float MINIMUM_SAMPLE_DISTANCE = 1.25f;

        private readonly TrailSegment[] segments;

        private int nextSegment;
        private bool hasPosition;
        private Vector2 lastSamplePosition;
        private double lastUpdateTime;
        private double lastSampleTime;
        private float segmentSize;

        public DodgeSkinComponents Component => DodgeSkinComponents.PlayerTrail;

        public bool HasVisual => segments.Length > 0 && segments[0].Drawable.HasVisual;

        public int EmittedSegmentCount { get; private set; }

        public float SegmentSize
        {
            get => segmentSize;
            set
            {
                segmentSize = Math.Max(0, value);

                foreach (TrailSegment segment in segments)
                    segment.Drawable.Size = new Vector2(segmentSize);
            }
        }

        public DodgePlayerTrail(float segmentSize)
        {
            RelativeSizeAxes = Axes.Both;

            segments = Enumerable.Range(0, MAX_SEGMENTS)
                                 .Select(_ => new TrailSegment(createSegment(segmentSize)))
                                 .ToArray();
            InternalChildren = segments.Select(segment => segment.Drawable).ToArray();

            this.segmentSize = Math.Max(0, segmentSize);
        }

        /// <summary>
        /// Updates the trail against a position in the player's parent coordinate space.
        /// Large clock jumps reset the trail so seeking cannot leave a line across the arena.
        /// </summary>
        public void UpdatePosition(Vector2 position, double time)
        {
            if (!HasVisual)
            {
                updateSamplingState(position, time);
                return;
            }

            if (!hasPosition || time < lastUpdateTime || time - lastUpdateTime > SEEK_RESET_THRESHOLD)
            {
                Reset(position, time);
                return;
            }

            foreach (TrailSegment segment in segments)
            {
                if (segment.ExpiresAt <= time)
                    continue;

                segment.Drawable.Position = segment.Position - position;
            }

            if (time - lastSampleTime >= SAMPLE_INTERVAL
                && Vector2.DistanceSquared(position, lastSamplePosition) >= MINIMUM_SAMPLE_DISTANCE * MINIMUM_SAMPLE_DISTANCE)
            {
                emit(lastSamplePosition, position, time);
                lastSamplePosition = position;
                lastSampleTime = time;
            }

            lastUpdateTime = time;
        }

        private void updateSamplingState(Vector2 position, double time)
        {
            hasPosition = true;
            lastSamplePosition = position;
            lastUpdateTime = time;
            lastSampleTime = time;
        }

        public void Reset(Vector2 position, double time)
        {
            foreach (TrailSegment segment in segments)
            {
                segment.ExpiresAt = double.NegativeInfinity;
                segment.Drawable.ClearTransforms();
                segment.Drawable.Hide();
            }

            nextSegment = 0;
            updateSamplingState(position, time);
        }

        private void emit(Vector2 samplePosition, Vector2 currentPosition, double time)
        {
            TrailSegment segment = segments[nextSegment];
            nextSegment = (nextSegment + 1) % segments.Length;

            segment.Position = samplePosition;
            segment.ExpiresAt = time + SEGMENT_DURATION;
            segment.Drawable.FinishTransforms(true);
            segment.Drawable.Position = samplePosition - currentPosition;
            segment.Drawable.Scale = Vector2.One;
            segment.Drawable.Alpha = 0.34f;
            segment.Drawable.FadeOut(SEGMENT_DURATION, Easing.OutQuint);

            EmittedSegmentCount++;
        }

        private static OptionalDodgeSkinDrawable createSegment(float size) => new OptionalDodgeSkinDrawable(DodgeSkinComponents.PlayerTrail)
        {
            Anchor = Anchor.Centre,
            Origin = Anchor.Centre,
            RelativeSizeAxes = Axes.None,
            Size = new Vector2(Math.Max(0, size)),
        };

        private sealed class TrailSegment
        {
            public readonly OptionalDodgeSkinDrawable Drawable;

            public Vector2 Position;
            public double ExpiresAt = double.NegativeInfinity;

            public TrailSegment(OptionalDodgeSkinDrawable drawable)
            {
                Drawable = drawable;
            }
        }
    }
}
