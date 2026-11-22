using System.Linq;
using osu.Game.Scoring;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// Per-HUD score template for the ordinary progressive path. Its map/mod configuration
    /// is fixed at load, just like the difficulty cursor. Only the worker reads the private
    /// cloned configuration; no gameplay objects or ever-growing hit event list are copied.
    /// </summary>
    internal sealed class LivePerformanceScoreSnapshot
    {
        private readonly ScoreInfo template;

        public LivePerformanceScoreSnapshot(ScoreInfo source)
        {
            template = new ScoreInfo(source.BeatmapInfo?.Clone(), source.Ruleset.Clone())
            {
                Mods = source.Mods.Select(mod => mod.DeepClone()).ToArray()
            };
        }

        // Avoid reflection, settings Bindable construction and JSON serialisation per judgement.
        public ScoreInfo Create(ScoreInfo source) => template.CreateLivePerformanceSnapshot(source);
    }
}
