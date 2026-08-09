// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Rulesets.Dodge.Judgements;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Types;
using Newtonsoft.Json;
using osu.Framework.Graphics;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects
{
    /// <summary>
    /// Changes the playable arena to a target rectangle.
    /// The earliest change (or any change at or before time zero) is applied immediately;
    /// subsequent changes interpolate over <see cref="Duration"/>.
    /// </summary>
    public class DodgeArenaChange : DodgeHitObject, IHasDuration, IHasPosition
    {
        public const float MIN_SIZE = 32;

        /// <summary>
        /// Opacity of the arena border, independently of the background opacity.
        /// </summary>
        public float BorderOpacity { get; set; } = 1;

        public DodgeArenaChange()
        {
            Colour = Colour4.Black;
            OutlineColour = Colour4.White;
            Opacity = 1;
        }

        public Vector2 TargetPosition { get; set; }

        public Vector2 Position
        {
            get => TargetPosition;
            set => TargetPosition = value;
        }

        public float X
        {
            get => TargetPosition.X;
            set => TargetPosition = new Vector2(value, TargetPosition.Y);
        }

        public float Y
        {
            get => TargetPosition.Y;
            set => TargetPosition = new Vector2(TargetPosition.X, value);
        }

        public Vector2 TargetSize { get; set; } = UI.DodgePlayfield.BASE_SIZE;

        /// <summary>
        /// Rotation angle of the target arena rectangle in degrees.
        /// </summary>
        public float TargetRotation { get; set; }

        /// <summary>
        /// Amplitude in degrees of the pendulum shake applied to the arena on each kiai beat.
        /// Zero (default) disables the effect entirely for this keyframe.
        /// </summary>
        public float KiaiShakeAngle { get; set; }

        public double Duration { get; set; } = 500;

        [JsonIgnore]
        public double EndTime
        {
            get => StartTime + Duration;
            set => Duration = value - StartTime;
        }

        public override Judgement CreateJudgement() => new DodgeArenaChangeJudgement();

        public void ClampToBaseBounds()
        {
            TargetSize = new Vector2(
                System.Math.Clamp(TargetSize.X, MIN_SIZE, UI.DodgePlayfield.WIDTH),
                System.Math.Clamp(TargetSize.Y, MIN_SIZE, UI.DodgePlayfield.HEIGHT));

            TargetPosition = new Vector2(
                System.Math.Clamp(TargetPosition.X, 0, UI.DodgePlayfield.WIDTH - TargetSize.X),
                System.Math.Clamp(TargetPosition.Y, 0, UI.DodgePlayfield.HEIGHT - TargetSize.Y));
        }
    }
}
