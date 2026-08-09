// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Mania.Objects;
using osu.Game.Rulesets.Objects;

namespace osu.Game.Rulesets.Mania.Difficulty.Preprocessing
{
    public class ManiaDifficultyHitObject : DifficultyHitObject
    {
        public new ManiaHitObject BaseObject => (ManiaHitObject)base.BaseObject;

        private readonly List<DifficultyHitObject>[] perColumnObjects;

        private readonly int columnIndex;

        public readonly int Column;

        // The hit object earlier in time than this note in each column
        public readonly ManiaDifficultyHitObject?[] PreviousHitObjects;

        public readonly double ColumnStrainTime;

        /// <summary>
        /// Duration of consecutive generated-vibro row transitions. The value is final once
        /// the whole row is known. Difficulty-reduction rates use gameplay time while faster
        /// rates use source time, so a rate mod can never delay the anti-abuse duration gates.
        /// </summary>
        public readonly double VibroDuration;

        public readonly int CurrentRowNoteCount;
        public readonly int CurrentRowColumnMask;

        /// <summary>
        /// Object count of the previous completed row when that row continued a detected
        /// vibro sequence. Reported once, on the first object of the next row.
        /// </summary>
        public readonly int CompletedVibroRowObjectCount;

        private readonly int previousRowNoteCount;
        private readonly int previousRowColumnMask;
        private readonly double currentRowInterval;
        private readonly double currentRowDurationIncrement;
        private readonly double completedVibroDurationBeforeCurrentRow;
        private readonly bool currentRowContainsHold;
        private readonly bool previousRowContainsHold;
        private readonly double currentRowLongestHoldDuration;
        private readonly double previousRowLongestHoldDuration;

        /// <summary>
        /// Progressive strain multiplier for sustained vibro patterns.
        /// Short bursts are unaffected and sustained generated vibro is reduced heavily.
        /// </summary>
        public double VibroPenalty => CalculateVibroPenalty(completedVibroDurationBeforeCurrentRow);

        public ManiaDifficultyHitObject(HitObject hitObject, HitObject lastObject, double clockRate, List<DifficultyHitObject> objects, List<DifficultyHitObject>[] perColumnObjects, int index)
            : base(hitObject, lastObject, clockRate, objects, index)
        {
            int totalColumns = perColumnObjects.Length;
            this.perColumnObjects = perColumnObjects;
            Column = BaseObject.Column;
            columnIndex = perColumnObjects[Column].Count;
            PreviousHitObjects = new ManiaDifficultyHitObject[totalColumns];
            ColumnStrainTime = StartTime - PrevInColumn(0)?.StartTime ?? StartTime;

            // A difficulty-reduction rate mod must not turn off anti-abuse detection. Faster
            // rates are still allowed to make a previously-safe pattern qualify as vibro.
            double detectionClockRate = Math.Max(1, clockRate);
            double rawDeltaTime = hitObject.StartTime - lastObject.StartTime;
            double detectionDeltaTime = rawDeltaTime / detectionClockRate;
            double vibroDurationIncrement = rawDeltaTime / Math.Min(1, clockRate);

            if (index > 0)
            {
                ManiaDifficultyHitObject prevNote = (ManiaDifficultyHitObject)objects[index - 1];

                for (int i = 0; i < prevNote.PreviousHitObjects.Length; i++)
                    PreviousHitObjects[i] = prevNote.PreviousHitObjects[i];

                // intentionally depends on processing order to match live.
                PreviousHitObjects[prevNote.Column] = prevNote;

                if (isSameRow(rawDeltaTime))
                {
                    CurrentRowNoteCount = prevNote.CurrentRowNoteCount + 1;
                    CurrentRowColumnMask = prevNote.CurrentRowColumnMask | 1 << Column;
                    previousRowNoteCount = prevNote.previousRowNoteCount;
                    previousRowColumnMask = prevNote.previousRowColumnMask;
                    currentRowInterval = prevNote.currentRowInterval;
                    currentRowDurationIncrement = prevNote.currentRowDurationIncrement;
                    completedVibroDurationBeforeCurrentRow = prevNote.completedVibroDurationBeforeCurrentRow;
                    currentRowContainsHold = prevNote.currentRowContainsHold || BaseObject is HoldNote;
                    previousRowContainsHold = prevNote.previousRowContainsHold;
                    currentRowLongestHoldDuration = Math.Max(prevNote.currentRowLongestHoldDuration, getHoldDuration(BaseObject, detectionClockRate));
                    previousRowLongestHoldDuration = prevNote.previousRowLongestHoldDuration;
                }
                else
                {
                    CompletedVibroRowObjectCount = prevNote.VibroDuration > 0 ? prevNote.CurrentRowNoteCount : 0;
                    CurrentRowNoteCount = 1;
                    CurrentRowColumnMask = 1 << Column;
                    previousRowNoteCount = prevNote.CurrentRowNoteCount;
                    previousRowColumnMask = prevNote.CurrentRowColumnMask;
                    currentRowInterval = detectionDeltaTime;
                    currentRowDurationIncrement = vibroDurationIncrement;
                    completedVibroDurationBeforeCurrentRow = prevNote.VibroDuration;
                    currentRowContainsHold = BaseObject is HoldNote;
                    previousRowContainsHold = prevNote.currentRowContainsHold;
                    currentRowLongestHoldDuration = getHoldDuration(BaseObject, detectionClockRate);
                    previousRowLongestHoldDuration = prevNote.currentRowLongestHoldDuration;
                }
            }
            else
            {
                CurrentRowNoteCount = 1;
                CurrentRowColumnMask = 1 << Column;
                currentRowInterval = double.PositiveInfinity;
                currentRowDurationIncrement = double.PositiveInfinity;
                currentRowContainsHold = BaseObject is HoldNote;
                currentRowLongestHoldDuration = getHoldDuration(BaseObject, detectionClockRate);
            }

            VibroDuration = CalculateNextVibroDuration(
                completedVibroDurationBeforeCurrentRow,
                currentRowInterval, currentRowDurationIncrement,
                previousRowNoteCount, previousRowColumnMask, previousRowContainsHold, previousRowLongestHoldDuration,
                CurrentRowNoteCount, CurrentRowColumnMask, currentRowContainsHold, currentRowLongestHoldDuration);
        }

        internal static double CalculateNextVibroDuration(
            double previousDuration, double rowInterval,
            int previousNoteCount, int previousColumnMask, bool previousContainsHold, double previousLongestHoldDuration,
            int currentNoteCount, int currentColumnMask, bool currentContainsHold, double currentLongestHoldDuration)
            => CalculateNextVibroDuration(
                previousDuration, rowInterval, rowInterval,
                previousNoteCount, previousColumnMask, previousContainsHold, previousLongestHoldDuration,
                currentNoteCount, currentColumnMask, currentContainsHold, currentLongestHoldDuration);

        internal static double CalculateNextVibroDuration(
            double previousDuration, double rowInterval, double durationIncrement,
            int previousNoteCount, int previousColumnMask, bool previousContainsHold, double previousLongestHoldDuration,
            int currentNoteCount, int currentColumnMask, bool currentContainsHold, double currentLongestHoldDuration)
        {
            bool isVibro = IsVibroRowTransition(
                rowInterval,
                previousNoteCount, previousColumnMask, previousContainsHold, previousLongestHoldDuration,
                currentNoteCount, currentColumnMask, currentContainsHold, currentLongestHoldDuration);

            return isVibro ? previousDuration + durationIncrement : 0;
        }

        internal static bool IsVibroRowTransition(
            double rowInterval,
            int previousNoteCount, int previousColumnMask, bool previousContainsHold, double previousLongestHoldDuration,
            int currentNoteCount, int currentColumnMask, bool currentContainsHold, double currentLongestHoldDuration)
        {
            if (rowInterval <= 1)
                return false;

            bool masksRepeatOrAlternate = previousColumnMask == currentColumnMask || (previousColumnMask & currentColumnMask) == 0;
            if (!masksRepeatOrAlternate)
                return false;

            double notesPerSecond = Math.Min(previousNoteCount, currentNoteCount) * 1000 / rowInterval;

            // Chord vibro is inflated by treating many simultaneous keys as independent strain.
            bool containsHold = previousContainsHold || currentContainsHold;
            bool chordVibro = previousNoteCount >= 2 && currentNoteCount >= 2 && notesPerSecond >= 24 && !containsHold;

            // Some generated packs encode the same abuse as alternating micro-LNs. Require an
            // LN on the transition and reject normal-length LNs. Singleton rows use a lower
            // threshold for separately mapped slow variants; chord-LNs retain the stricter one.
            double maximumMicroHoldDuration = Math.Max(80, rowInterval * 2.25);
            bool holdsAreAbsentOrMicro = (!previousContainsHold || previousLongestHoldDuration <= maximumMicroHoldDuration)
                                         && (!currentContainsHold || currentLongestHoldDuration <= maximumMicroHoldDuration);
            if (!holdsAreAbsentOrMicro)
                return false;

            // The map-wide and sustained-pattern gates provide the main false-positive safety.
            // Keeping this threshold below the old 36 NPS prevents a separately mapped 0.8x
            // difficulty from bypassing the detector while ordinary 75ms LN streams stay out.
            double microLnThreshold = previousNoteCount == 1 && currentNoteCount == 1 ? 24 : 36;
            bool microLnVibro = notesPerSecond >= microLnThreshold && containsHold;
            return chordVibro || microLnVibro;
        }

        internal static double CalculateVibroPenalty(double duration)
        {
            const double grace_duration = 500;
            const double full_penalty_duration = 1500;
            const double maximum_reduction = 0.90;

            if (duration <= grace_duration)
                return 1;

            double progress = Math.Clamp((duration - grace_duration) / (full_penalty_duration - grace_duration), 0, 1);
            double smoothProgress = progress * progress * (3 - 2 * progress);
            return 1 - maximum_reduction * smoothProgress;
        }

        private static double getHoldDuration(ManiaHitObject hitObject, double detectionClockRate) =>
            hitObject is HoldNote hold ? hold.Duration / detectionClockRate : 0;

        private static bool isSameRow(double deltaTime) => deltaTime >= 0 && deltaTime <= 1;

        /// <summary>
        /// The previous object in the same column as this <see cref="ManiaDifficultyHitObject"/>, exclusive of Long Note tails.
        /// </summary>
        /// <param name="backwardsIndex">The number of notes to go back.</param>
        /// <returns>The object in this column <paramref name="backwardsIndex"/> notes back, or null if this is the first note in the column.</returns>
        public ManiaDifficultyHitObject? PrevInColumn(int backwardsIndex)
        {
            int index = columnIndex - (backwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? (ManiaDifficultyHitObject)perColumnObjects[Column][index] : null;
        }

        /// <summary>
        /// The next object in the same column as this <see cref="ManiaDifficultyHitObject"/>, exclusive of Long Note tails.
        /// </summary>
        /// <param name="forwardsIndex">The number of notes to go forward.</param>
        /// <returns>The object in this column <paramref name="forwardsIndex"/> notes forward, or null if this is the last note in the column.</returns>
        public ManiaDifficultyHitObject? NextInColumn(int forwardsIndex)
        {
            int index = columnIndex + (forwardsIndex + 1);
            return index >= 0 && index < perColumnObjects[Column].Count ? (ManiaDifficultyHitObject)perColumnObjects[Column][index] : null;
        }
    }
}
