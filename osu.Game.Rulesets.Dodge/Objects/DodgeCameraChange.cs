// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
using osu.Game.Rulesets.Dodge.Judgements;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects.Types;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects
{
    /// <summary>
    /// Scrolls the playfield contents to imitate field movement.
    /// The arrow from <see cref="Position"/> to <see cref="EndPosition"/> defines a vector:
    /// with <see cref="Continuous"/> off it is the total displacement applied over
    /// <see cref="Duration"/>; with <see cref="Continuous"/> on it is the scroll velocity
    /// in pixels per second, applied from the change's start time until the next
    /// camera change (i.e. the field can scroll indefinitely).
    /// Offsets accumulate across changes. Live objects drift with the scroll from
    /// their own spawn time, so newly-spawned objects always appear where they were placed.
    /// </summary>
    public class DodgeCameraChange : DodgeHitObject, IHasDuration, IHasPosition
    {
        public DodgeCameraChange()
        {
            Colour = osu.Framework.Graphics.Colour4.Transparent;
            OutlineColour = osu.Framework.Graphics.Colour4.Transparent;
            Opacity = 1;
        }

        /// <summary>
        /// Editor anchor of the scroll arrow. Not an absolute field offset.
        /// </summary>
        public Vector2 Position { get; set; }

        /// <summary>
        /// End of the scroll arrow. <see cref="EndPosition"/> - <see cref="Position"/>
        /// is the scroll velocity (continuous) or total displacement.
        /// </summary>
        public Vector2 EndPosition { get; set; }

        /// <summary>
        /// When true, the field keeps scrolling at the arrow's velocity (px/s)
        /// from the change's start time until the next camera change.
        /// </summary>
        public bool Continuous { get; set; }

        public float X
        {
            get => Position.X;
            set => Position = new Vector2(value, Y);
        }

        public float Y
        {
            get => Position.Y;
            set => Position = new Vector2(X, value);
        }

        public double Duration { get; set; }

        [JsonIgnore]
        public double EndTime
        {
            get => StartTime + Duration;
            set => Duration = value - StartTime;
        }

        // Camera changes are non-judged state transitions, same as arena changes.
        public override Judgement CreateJudgement() => new DodgeArenaChangeJudgement();
    }
}
