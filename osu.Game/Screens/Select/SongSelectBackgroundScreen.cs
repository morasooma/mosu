// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics.Backgrounds;
using osu.Game.Screens.Backgrounds;

namespace osu.Game.Screens.Select
{
    public partial class SongSelectBackgroundScreen : BackgroundScreenBeatmap
    {
        private readonly BindableBool storyboardBackgroundEnabled = new BindableBool();

        public SongSelectBackgroundScreen(WorkingBeatmap? beatmap = null)
            : base(beatmap)
        {
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ForkSongSelectStoryboardBackground, storyboardBackgroundEnabled);
        }

        protected override BeatmapBackground CreateBackgroundDrawable(WorkingBeatmap beatmap)
            => canUseStoryboardBackground(beatmap)
                ? new BeatmapBackgroundWithStoryboard(beatmap)
                : base.CreateBackgroundDrawable(beatmap);

        protected override bool CanReuseCurrentBackground(WorkingBeatmap newBeatmap)
        {
            if (canUseStoryboardBackground(newBeatmap))
                return Background is BeatmapBackgroundWithStoryboard && (Background as BeatmapBackground)?.Beatmap == newBeatmap;

            return base.CanReuseCurrentBackground(newBeatmap);
        }

        public void ApplyStoryboardSettingChange(WorkingBeatmap beatmap)
        {
            Beatmap = beatmap;
            ReloadBackground();
        }

        private bool canUseStoryboardBackground(WorkingBeatmap? beatmap)
            => storyboardBackgroundEnabled.Value && beatmap != null && beatmap is not DummyWorkingBeatmap;
    }
}
