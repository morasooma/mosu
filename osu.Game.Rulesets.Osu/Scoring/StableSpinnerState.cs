using System;
using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Rulesets.Osu.Scoring
{
    /// <summary>
    /// Stable-profile spinner physics and ScoreV1 accounting. Independent of lazer's
    /// finite nested ticks. Units are radians/ms and half-turns. Based on observed
    /// arithmetic in the locally installed stable client; no obfuscated code is embedded.
    /// </summary>
    public sealed class StableSpinnerState
    {
        private readonly double acceleration;
        private readonly Stack<Snapshot> history = new Stack<Snapshot>();
        private double velocity;
        private float rotation;
        private int awardedHalfTurns;
        private double lastTime = double.NegativeInfinity;

        public double SpinsPerMinute { get; private set; }
        public double AngularVelocity => velocity;
        public int RequiredHalfTurns { get; }
        public int HalfTurns => (int)rotation;
        public long SpinScore { get; private set; }
        public int BonusCount { get; private set; }

        public StableSpinnerState(double duration, double overallDifficulty)
        {
            RequiredHalfTurns = (int)((float)duration / 1000f * IBeatmapDifficultyInfo.DifficultyRange(overallDifficulty, 3, 5, 7.5));
            acceleration = 0.00008 + Math.Max(0, (5000 - duration) / 1000 / 2000);
        }

        public float Advance(double time, double elapsed, double targetVelocity, double rate = 1, bool spunOut = false, bool relax = false)
        {
            if (time < lastTime)
            {
                while (history.TryPeek(out var previous) && previous.Time > time)
                {
                    var state = history.Pop();
                    velocity = state.Velocity;
                    rotation = state.Rotation;
                    awardedHalfTurns = state.Awarded;
                    SpinScore = state.Score;
                    BonusCount = state.Bonuses;
                    SpinsPerMinute = state.Rpm;
                }
                lastTime = time;
                return 0;
            }
            if (elapsed <= 0 || rate <= 0 || time == lastTime)
                return 0;

            history.Push(new Snapshot(time, velocity, rotation, awardedHalfTurns, SpinScore, BonusCount, SpinsPerMinute));
            // Stable updates the RPM display from the previous physics velocity.
            double rpmDamping = Math.Pow(0.9, elapsed / (1000d / 60));
            SpinsPerMinute = SpinsPerMinute * rpmDamping + (1 - rpmDamping) * Math.Abs(velocity) * 60000 / (Math.PI * 2);
            lastTime = time;
            double maxChange = acceleration * elapsed / rate;
            if (relax && velocity * (targetVelocity - velocity) < 0)
                maxChange /= 4;
            velocity = spunOut ? 0.03 : velocity + Math.Clamp(targetVelocity - velocity, -maxChange, maxChange);
            velocity = Math.Clamp(velocity, -0.05, 0.05);
            float delta = (float)(velocity * elapsed);
            rotation += Math.Min(1f, Math.Abs(delta / (float)Math.PI));

            // Stable emits at most one half-turn event per update, even when a
            // frame crosses multiple integer boundaries. Do not synthesise a while loop.
            if (HalfTurns > awardedHalfTurns)
            {
                awardedHalfTurns++;
                int value = ScoreForHalfTurn(awardedHalfTurns, RequiredHalfTurns);
                SpinScore += value;
                if (value == 1100)
                    BonusCount++;
            }
            return float.RadiansToDegrees(delta);
        }

        public static int ScoreForHalfTurn(int halfTurn, int required)
        {
            if (halfTurn > required + 3 && (halfTurn - (required + 3)) % 2 == 0)
                return 1100;
            return halfTurn > 1 && halfTurn % 2 == 0 ? 100 : 0;
        }

        public HitResult GetJudgement()
        {
            if (RequiredHalfTurns == 0 || awardedHalfTurns > RequiredHalfTurns)
                return HitResult.Great;
            if (awardedHalfTurns < RequiredHalfTurns / 4)
                return HitResult.Miss;
            return awardedHalfTurns > RequiredHalfTurns - 2 ? HitResult.Ok : HitResult.Meh;
        }

        private readonly record struct Snapshot(double Time, double Velocity, float Rotation, int Awarded, long Score, int Bonuses, double Rpm);
    }
}
