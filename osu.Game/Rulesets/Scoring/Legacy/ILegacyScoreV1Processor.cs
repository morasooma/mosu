// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Mods;
using osu.Game.Scoring;

namespace osu.Game.Rulesets.Scoring.Legacy
{
    public interface ILegacyScoreV1Processor
    {
        long TotalScore { get; }

        int? CurrentCombo => null;
        int? MaximumCombo => null;

        void ApplyResult(JudgementResult result);

        void RevertResult(JudgementResult result);

        /// <summary>
        /// Adjust a detached submission copy when stable counts judgements differently
        /// from lazer (for example, mania ScoreV1 hold notes).
        /// </summary>
        void PrepareScoreForSubmission(ScoreInfo score)
        {
        }
    }

    public interface IHasLegacyScoreV1Processor
    {
        ILegacyScoreV1Processor CreateLegacyScoreV1Processor(IWorkingBeatmap workingBeatmap, IBeatmap playableBeatmap, IReadOnlyList<Mod> mods);
    }
}
