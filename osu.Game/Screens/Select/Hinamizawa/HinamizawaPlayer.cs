using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Play;

namespace osu.Game.Screens.Select.Hinamizawa
{
    /// <summary>Only mirror sessions need a score metadata anchor without importing a beatmapset into the library.</summary>
    public partial class HinamizawaPlayer : SoloPlayer
    {
        [Resolved]
        private RealmAccess realm { get; set; } = null!;

        protected override async Task ImportScore(Score score)
        {
            if (DrawableRuleset.ReplayScore != null) return;
            await EnsureScoreBeatmapAsync(realm, score.ScoreInfo.BeatmapInfo!).ConfigureAwait(false);
            await base.ImportScore(score).ConfigureAwait(false);
        }

        internal static Task EnsureScoreBeatmapAsync(RealmAccess realm, BeatmapInfo source) => realm.WriteAsync(r =>
            {
                if (r.Find<BeatmapInfo>(source.ID) != null) return;
                var copy = source.Clone();
                copy.Metadata = source.Metadata.DeepClone();
                copy.Difficulty = source.Difficulty.Clone();
                copy.BeatmapSet = null;
                copy.Ruleset = r.All<RulesetInfo>().First(info => info.ShortName == source.Ruleset.ShortName);
                // No BeatmapSet is inserted: neither regular carousel can pick this metadata-only score anchor.
                r.Add(copy);
            });
    }
}
