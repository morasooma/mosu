// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Scoring.Render
{
    internal sealed class RenderGameplaySampleTriggerRecorder : IRenderGameplaySampleTriggerRecorder
    {
        public double? FirstTriggerTime { get; private set; }
        public double? LastTriggerTime { get; private set; }
        public int TriggerCount { get; private set; }

        public void RecordTrigger(double gameplayTime)
        {
            if (!FirstTriggerTime.HasValue)
                FirstTriggerTime = gameplayTime;

            LastTriggerTime = gameplayTime;
            TriggerCount++;
        }
    }
}
