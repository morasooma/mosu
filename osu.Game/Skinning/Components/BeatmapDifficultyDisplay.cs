using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Beatmaps.Drawables;
using osu.Game.Localisation;
using osu.Game.Screens.Select;

namespace osu.Game.Skinning.Components
{
    /// <summary>The carousel's compact star-rating pill, available as an optional skin component.</summary>
    [LocalisableDescription(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.BeatmapDifficultyDisplay))]
    public partial class BeatmapDifficultyDisplay : StarRatingDisplay, ISerialisableDrawable, IMorasoomaSkinComponent
    {
        public bool UsesFixedAnchor { get; set; }

        [Resolved]
        private IBindable<WorkingBeatmap> beatmap { get; set; } = null!;

        [Resolved]
        private BeatmapDifficultyCache difficultyCache { get; set; } = null!;

        [Resolved]
        private ISongSelect? songSelect { get; set; }

        private IBindable<WorkingBeatmap>? boundBeatmap;
        private IBindable<StarDifficulty>? difficulty;
        private CancellationTokenSource? cancellationSource;

        public BeatmapDifficultyDisplay()
            : base(default, StarRatingDisplaySize.Small)
        {
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            boundBeatmap = beatmap.GetBoundCopy();
            boundBeatmap.BindValueChanged(_ => updateDifficulty(), true);
        }

        private void updateDifficulty()
        {
            cancelDifficulty();
            cancellationSource = new CancellationTokenSource();
            // The cache tracks ruleset, mod selection and mod-setting changes itself.
            // Song select retains the same admission delay as the carousel's stars.
            difficulty = difficultyCache.GetBindableDifficulty(beatmap.Value.BeatmapInfo, cancellationSource.Token,
                computationDelay: songSelect == null ? 0 : SongSelect.DIFFICULTY_CALCULATION_DEBOUNCE,
                calculatePerformance: false);
            difficulty.BindValueChanged(value => Current.Value = value.NewValue, true);
        }

        private void cancelDifficulty()
        {
            cancellationSource?.Cancel();
            cancellationSource?.Dispose();
            cancellationSource = null;
            difficulty?.UnbindAll();
            difficulty = null;
        }

        protected override void Dispose(bool isDisposing)
        {
            boundBeatmap?.UnbindAll();
            cancelDifficulty();
            base.Dispose(isDisposing);
        }
    }
}
