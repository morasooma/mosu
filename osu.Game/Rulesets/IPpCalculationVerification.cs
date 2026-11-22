using System;
using System.Threading;
using osu.Game.Beatmaps;

namespace osu.Game.Rulesets
{
    /// <summary>
    /// Explicit, local-only developer verification. Never used by gameplay or rating caches.
    /// </summary>
    public interface IPpCalculationVerification
    {
        string VerifyPpCalculation(IWorkingBeatmap beatmap, Action<string, float> progress, CancellationToken cancellationToken);
    }
}
