using System;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Catch.Scoring;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Catch.Objects
{
    public class FruitNinjaFruit : Fruit
    {
        public const float SLICE_Y = 120;

        public bool IsBomb { get; set; }
        public Vector2? SlicePosition { get; set; }
        public float? ThrowDrift { get; set; }
        public int PatternIndex { get; set; }

        // Retain the original playable objects for Catch star calculation, not the flattened ninja targets.
        public PalpableCatchHitObject? SourceObject { get; set; }

        protected override HitWindows CreateHitWindows() => new FruitNinjaHitWindows();

        public float Radius => OBJECT_RADIUS * Scale;

        public override Judgement CreateJudgement() => IsBomb ? new BombJudgement() : base.CreateJudgement();

        public double LaunchTime => StartTime - TimePreempt;

        // The musical note is on the ascent, not at the turnaround. Even at high AR,
        // reaching the apex must already be outside the late 100 window.
        public double ApexTime => StartTime + Math.Max(TimePreempt / 2, HitWindows.WindowFor(HitResult.Ok) + 1);
        public double FlightEndTime => ApexTime + TimePreempt / 2;

        public Vector2 PositionAt(double time)
        {
            double duration = time < ApexTime ? ApexTime - LaunchTime : FlightEndTime - ApexTime;
            float phase = (float)((time - ApexTime) / duration);
            Vector2 apex = SlicePosition ?? new Vector2(Math.Clamp(EffectiveX, 48, 464), SLICE_Y);
            float drift = ThrowDrift ?? (IsBomb ? (apex.X < 256 ? -28 : 28) : (256 - apex.X) * 0.24f);
            return new Vector2(apex.X + phase * drift, apex.Y + (420 - apex.Y) * phase * phase);
        }

        public bool IsInFlight(double time) => time >= LaunchTime && time < FlightEndTime;

        public HitResult SliceResultAt(double time)
        {
            if (!IsInFlight(time))
                return HitResult.Miss;

            // A connected cut is always a hit. Timing still controls accuracy, but must not
            // turn a visibly sliced fruit into a miss outside the narrow timing windows.
            return HitWindows.ResultFor(time - StartTime) switch
            {
                HitResult.Great => HitResult.Great,
                HitResult.Ok => HitResult.Ok,
                _ => HitResult.Meh,
            };
        }

        /// <summary>
        /// Swept blade against a moving fruit, in relative coordinates. Fast swipes cannot tunnel through it.
        /// A stationary blade must not cut fruit just because fruit moves into it.
        /// </summary>
        public static bool IntersectsSwipe(Vector2 bladeStart, Vector2 bladeEnd, Vector2 fruitStart, Vector2 fruitEnd, float radius)
        {
            if (Vector2.DistanceSquared(bladeStart, bladeEnd) < 0.25f)
                return false;

            Vector2 start = bladeStart - fruitStart;
            Vector2 delta = (bladeEnd - fruitEnd) - start;
            float progress = delta.LengthSquared > 0 ? Math.Clamp(-Vector2.Dot(start, delta) / delta.LengthSquared, 0, 1) : 0;
            return (start + progress * delta).LengthSquared <= radius * radius;
        }

        private class BombJudgement : Judgement
        {
            public override HitResult MaxResult => HitResult.IgnoreHit;
            public override HitResult MinResult => HitResult.ComboBreak;

            protected override double HealthIncreaseFor(HitResult result) => result == HitResult.ComboBreak ? -0.15 : 0;
        }
    }
}
