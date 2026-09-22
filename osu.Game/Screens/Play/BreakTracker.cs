// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Beatmaps.Timing;
using osu.Game.Rulesets.Scoring;
using osu.Game.Utils;

namespace osu.Game.Screens.Play
{
    public partial class BreakTracker : Component
    {
        private readonly ScoreProcessor scoreProcessor;
        private readonly double gameplayStartTime;

        private PeriodTracker breaks = new PeriodTracker(Enumerable.Empty<Period>());
        private Dictionary<(double Start, double ActiveEnd), BreakPeriod> breakPeriods = new Dictionary<(double, double), BreakPeriod>();
        private Dictionary<BreakPeriod, int> breakIndices = new Dictionary<BreakPeriod, int>();

        /// <summary>
        /// Whether the gameplay is currently in a break.
        /// </summary>
        public IBindable<bool> IsBreakTime => isBreakTime;

        private readonly BindableBool isBreakTime = new BindableBool(true);

        public readonly Bindable<Period?> CurrentPeriod = new Bindable<Period?>();

        /// <summary>
        /// The beatmap break which is currently active, including its unmodified end time.
        /// </summary>
        public readonly Bindable<BreakPeriod?> CurrentBreak = new Bindable<BreakPeriod?>();

        /// <summary>
        /// Returns the stable zero-based index of a break in the effective beatmap break list.
        /// </summary>
        public int GetBreakIndex(BreakPeriod breakPeriod) => breakIndices.TryGetValue(breakPeriod, out int index) ? index : -1;

        public IReadOnlyList<BreakPeriod> Breaks
        {
            set
            {
                BreakPeriod[] effectiveBreaks = value.Where(b => b.HasEffect).Distinct().OrderBy(b => b.StartTime).ToArray();

                breakPeriods = effectiveBreaks.ToDictionary(b => (b.StartTime, b.EndTime - BreakOverlay.BREAK_FADE_DURATION));
                breakIndices = effectiveBreaks.Select((b, index) => (b, index)).ToDictionary(pair => pair.b, pair => pair.index);
                breaks = new PeriodTracker(effectiveBreaks.Select(b => new Period(b.StartTime, b.EndTime - BreakOverlay.BREAK_FADE_DURATION)));

                if (IsLoaded)
                    updateBreakTime();
            }
        }

        public BreakTracker(double gameplayStartTime, ScoreProcessor scoreProcessor)
        {
            this.gameplayStartTime = gameplayStartTime;
            this.scoreProcessor = scoreProcessor;
        }

        protected override void Update()
        {
            base.Update();
            updateBreakTime();
        }

        private void updateBreakTime()
        {
            double time = Clock.CurrentTime;

            if (breaks.IsInAny(time, out var currentBreak))
            {
                CurrentPeriod.Value = currentBreak;
                CurrentBreak.Value = breakPeriods[(currentBreak.Value.Start, currentBreak.Value.End)];
                isBreakTime.Value = true;
            }
            else
            {
                CurrentPeriod.Value = null;
                CurrentBreak.Value = null;
                isBreakTime.Value = time < gameplayStartTime || scoreProcessor.HasCompleted.Value;
            }
        }
    }
}
