// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Rulesets.Dodge.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.Objects.Drawables
{
    /// <summary>
    /// A trajectory guide with a cheap unbuffered fast path for linear movement.
    /// </summary>
    public partial class DodgeTrajectoryGuide : CompositeDrawable
    {
        private Box? line;
        private DodgeUnbufferedPath? path;

        public Vector2 StartPosition { get; private set; }

        public Vector2 EndPosition { get; private set; }

        internal bool UsesBufferedPath => false;

        internal bool UsesUnbufferedPath => path != null;

        internal int BufferedGeometryRebuildCount { get; private set; }

        public void SetGeometry(
            Vector2 start,
            Vector2 controlEnd,
            float minimumProgress,
            float maximumProgress,
            DodgeMovementType movementType,
            float waveAmplitude,
            int waveCycles,
            float wavePhase,
            DodgeMovementEasing movementEasing = DodgeMovementEasing.Linear)
        {
            minimumProgress = Math.Max(0, minimumProgress);
            maximumProgress = Math.Max(minimumProgress, maximumProgress);
            StartPosition = DodgeTrajectory.PositionAtProgress(
                start,
                controlEnd,
                minimumProgress,
                movementType,
                waveAmplitude,
                waveCycles,
                wavePhase,
                movementEasing);
            EndPosition = DodgeTrajectory.PositionAtProgress(
                start,
                controlEnd,
                maximumProgress,
                movementType,
                waveAmplitude,
                waveCycles,
                wavePhase,
                movementEasing);

            if (movementType == DodgeMovementType.Linear || waveAmplitude == 0 || start == controlEnd)
            {
                syncLinearGuide();

                Vector2 displacement = EndPosition - StartPosition;
                line!.Position = StartPosition;
                line.Width = displacement.Length;
                line.Rotation = displacement.LengthSquared == 0
                    ? 0
                    : MathHelper.RadiansToDegrees(MathF.Atan2(displacement.Y, displacement.X));
                return;
            }

            syncBufferedGuide();
            path!.SetGeometry(
                start,
                controlEnd,
                minimumProgress,
                maximumProgress,
                movementType,
                waveAmplitude,
                waveCycles,
                wavePhase,
                movementEasing);
            BufferedGeometryRebuildCount++;
        }

        private void syncLinearGuide()
        {
            if (line != null)
                return;

            ClearInternal(true);
            path = null;
            line = new Box
            {
                Origin = Anchor.CentreLeft,
                Height = 1,
                Colour = Color4.White,
            };
            AddInternal(line);
        }

        private void syncBufferedGuide()
        {
            if (path != null)
                return;

            ClearInternal(true);
            line = null;
            path = new DodgeUnbufferedPath
            {
                RelativeSizeAxes = Axes.Both,
                Colour = Color4.White,
            };
            AddInternal(path);
        }
    }
}
