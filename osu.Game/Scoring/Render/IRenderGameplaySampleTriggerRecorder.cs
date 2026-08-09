// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Scoring.Render
{
    internal interface IRenderGameplaySampleTriggerRecorder
    {
        double? FirstTriggerTime { get; }

        double? LastTriggerTime { get; }

        int TriggerCount { get; }

        void RecordTrigger(double gameplayTime);
    }
}
