// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Game.Rulesets.Dodge.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.UI
{
    public readonly struct DodgeArenaState
    {
        public readonly Vector2 Position;
        public readonly Vector2 Size;
        public readonly float Rotation;
        public readonly Colour4 BackgroundColour;
        public readonly float BackgroundOpacity;
        public readonly Colour4 BorderColour;
        public readonly float BorderOpacity;

        /// <summary>Amplitude of the kiai beat shake in degrees. Zero means no effect.</summary>
        public readonly float KiaiShakeAngle;

        public DodgeArenaState(
            Vector2 position,
            Vector2 size,
            float rotation,
            Colour4 backgroundColour,
            float backgroundOpacity,
            Colour4 borderColour,
            float borderOpacity,
            float kiaiShakeAngle = 0)
        {
            Position = position;
            Size = size;
            Rotation = rotation;
            BackgroundColour = backgroundColour;
            BackgroundOpacity = backgroundOpacity;
            BorderColour = borderColour;
            BorderOpacity = borderOpacity;
            KiaiShakeAngle = kiaiShakeAngle;
        }
    }

    public class DodgeArenaStateEvaluator
    {
        public static readonly DodgeArenaState DEFAULT = new DodgeArenaState(
            Vector2.Zero,
            DodgePlayfield.BASE_SIZE,
            0,
            Colour4.Black,
            1,
            Colour4.White,
            1);

        private readonly DodgeArenaChange[] changes;
        private readonly DodgeArenaState[] startStates;

        public DodgeArenaStateEvaluator(IEnumerable<DodgeArenaChange> changes)
        {
            this.changes = changes.OrderBy(change => change.StartTime).ToArray();
            startStates = new DodgeArenaState[this.changes.Length];

            for (int i = 0; i < this.changes.Length; i++)
                startStates[i] = evaluate(this.changes[i].StartTime, i);
        }

        public DodgeArenaState Evaluate(double time) => evaluate(time, changes.Length);

        private DodgeArenaState evaluate(double time, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                DodgeArenaChange change = changes[i];

                if (change.StartTime > time)
                    continue;

                DodgeArenaState target = getConstrainedTarget(change);

                // The earliest arena object defines the initial arena regardless of its timestamp.
                // This also keeps older maps working when their first timing point prevented placing it at zero.
                if (i == 0 || change.StartTime <= 0 || change.Duration <= 0)
                    return target;

                float progress = (float)Math.Clamp((time - change.StartTime) / change.Duration, 0, 1);
                progress = change.Easing.Apply(progress);
                Vector2 size = new Vector2(
                    Math.Clamp(lerp(startStates[i].Size.X, target.Size.X, progress), DodgeArenaChange.MIN_SIZE, DodgePlayfield.WIDTH),
                    Math.Clamp(lerp(startStates[i].Size.Y, target.Size.Y, progress), DodgeArenaChange.MIN_SIZE, DodgePlayfield.HEIGHT));
                Vector2 position = new Vector2(
                    Math.Clamp(lerp(startStates[i].Position.X, target.Position.X, progress), 0, DodgePlayfield.WIDTH - size.X),
                    Math.Clamp(lerp(startStates[i].Position.Y, target.Position.Y, progress), 0, DodgePlayfield.HEIGHT - size.Y));

                return new DodgeArenaState(
                    position,
                    size,
                    startStates[i].Rotation + (target.Rotation - startStates[i].Rotation) * progress,
                    interpolate(startStates[i].BackgroundColour, target.BackgroundColour, progress),
                    interpolate(startStates[i].BackgroundOpacity, target.BackgroundOpacity, progress),
                    interpolate(startStates[i].BorderColour, target.BorderColour, progress),
                    interpolate(startStates[i].BorderOpacity, target.BorderOpacity, progress),
                    startStates[i].KiaiShakeAngle + (target.KiaiShakeAngle - startStates[i].KiaiShakeAngle) * progress);
            }

            // The first change is the initial state and must already be visible during pre-game,
            // including maps whose timing begins after zero.
            if (count > 0)
                return getConstrainedTarget(changes[0]);

            return DEFAULT;
        }

        private static DodgeArenaState getConstrainedTarget(DodgeArenaChange change)
        {
            Vector2 targetSize = new Vector2(
                Math.Clamp(change.TargetSize.X, DodgeArenaChange.MIN_SIZE, DodgePlayfield.WIDTH),
                Math.Clamp(change.TargetSize.Y, DodgeArenaChange.MIN_SIZE, DodgePlayfield.HEIGHT));
            Vector2 targetPosition = new Vector2(
                Math.Clamp(change.TargetPosition.X, 0, DodgePlayfield.WIDTH - targetSize.X),
                Math.Clamp(change.TargetPosition.Y, 0, DodgePlayfield.HEIGHT - targetSize.Y));

            return new DodgeArenaState(
                targetPosition,
                targetSize,
                change.TargetRotation,
                change.Colour,
                Math.Clamp(change.Opacity, 0, 1),
                change.OutlineColour,
                Math.Clamp(change.BorderOpacity, 0, 1),
                Math.Max(0, change.KiaiShakeAngle));
        }

        private static Colour4 interpolate(Colour4 from, Colour4 to, float progress)
            => new Colour4(
                Math.Clamp(from.R + (to.R - from.R) * progress, 0, 1),
                Math.Clamp(from.G + (to.G - from.G) * progress, 0, 1),
                Math.Clamp(from.B + (to.B - from.B) * progress, 0, 1),
                Math.Clamp(from.A + (to.A - from.A) * progress, 0, 1));

        private static float interpolate(float from, float to, float progress)
            => Math.Clamp(lerp(from, to, progress), 0, 1);

        private static float lerp(float from, float to, float progress)
            => from + (to - from) * progress;
    }
}
