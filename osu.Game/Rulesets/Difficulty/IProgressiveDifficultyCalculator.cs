using System;
using System.Threading;

namespace osu.Game.Rulesets.Difficulty
{
    /// <summary>
    /// A single-reader, calculation-local cursor over the existing timed difficulty path.
    /// Forward queries process skipped objects but aggregate only the requested prefix.
    /// Backward queries rebuild skill state. Never share a cursor across threads concurrently.
    /// </summary>
    public interface IProgressiveDifficultyCalculator : IDisposable
    {
        DifficultyAttributes? GetAttributesAtTime(double time, CancellationToken cancellationToken = default);
    }
}
