// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Scoring.Legacy
{
    public interface ILegacyScoreV1Processor
    {
        long TotalScore { get; }

        void ApplyResult(JudgementResult result);

        void RevertResult(JudgementResult result);
    }

    public interface IHasLegacyScoreV1Processor
    {
        ILegacyScoreV1Processor CreateLegacyScoreV1Processor(IWorkingBeatmap workingBeatmap, IBeatmap playableBeatmap, IReadOnlyList<Mod> mods);
    }
}
