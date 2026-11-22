// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Logging;
using osu.Game.Graphics.Containers;
using osu.Game.Performance.Debug;

namespace osu.Game.Screens.Select
{
    /// <summary>
    /// The left portion of the song select screen which houses the metadata or leaderboards wedge, along with controls
    /// to switch between them and adjust specifics.
    /// </summary>
    public partial class BeatmapDetailsArea : VisibilityContainer
    {
        /// <summary>
        /// When enabled, empty leaderboard space lets positional input reach the presentation behind it.
        /// Actual score panels remain interactive.
        /// </summary>
        public Func<bool> UseScorePanelOnlyInput { get; init; } = () => false;

        private Header header = null!;
        private Container contentContainer = null!;
        private BeatmapMetadataWedge? preloadedMetadata;

        public BeatmapDetailsArea()
        {
            RelativeSizeAxes = Axes.X;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            const float header_height = 35f;

            InternalChildren = new Drawable[]
            {
                new ShearAligningWrapper(header = new Header
                {
                    Shear = -OsuGame.SHEAR,
                    RelativeSizeAxes = Axes.X,
                    Height = header_height,
                }),
                new ShearAligningWrapper(contentContainer = new Container
                {
                    Shear = -OsuGame.SHEAR,
                    Padding = new MarginPadding { Top = header_height },
                    RelativeSizeAxes = Axes.Both,
                })
                {
                    Depth = 1f,
                },
            };

            // The first Details wedge costs tens of milliseconds to load when added on Update.
            // Prepare its dependencies while the song-select screen is loading off-thread;
            // LoadComplete and binding to the selected beatmap still happen when it is shown.
            LoadComponent(preloadedMetadata = new BeatmapMetadataWedge());
        }

        protected override void LoadComplete()
        {
            using var loading = PerformancePhaseLogger.Begin("BeatmapDetailsArea.LoadComplete");
            base.LoadComplete();

            header.Type.BindValueChanged(_ => updateDisplay(), true);
        }

        private int measuredInitialUpdateFrames;

        public override bool UpdateSubTree()
        {
            if (!PerformancePhaseLogger.Enabled || measuredInitialUpdateFrames >= 3)
                return base.UpdateSubTree();

            measuredInitialUpdateFrames++;
            long start = Stopwatch.GetTimestamp();
            try
            {
                return base.UpdateSubTree();
            }
            finally
            {
                Logger.Log($"MOSU_SONG_SELECT_INITIAL_TREE component=BeatmapDetailsArea frame={measuredInitialUpdateFrames} "
                           + $"elapsed_ms={Stopwatch.GetElapsedTime(start).TotalMilliseconds:F2}", LoggingTarget.Performance);
            }
        }

        protected override void PopIn()
        {
            this.MoveToX(0, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeIn(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        protected override void PopOut()
        {
            this.MoveToX(-150, SongSelect.ENTER_DURATION, Easing.OutQuint)
                .FadeOut(SongSelect.ENTER_DURATION / 3, Easing.In);
        }

        private Drawable? currentContent;

        private void updateDisplay()
        {
            using var updating = PerformancePhaseLogger.Begin("BeatmapDetailsArea.UpdateDisplay");
            if (currentContent != null)
            {
                currentContent.Hide();
                currentContent.Expire();
            }

            using (PerformancePhaseLogger.Begin("BeatmapDetailsArea.CreateContent", detail: header.Type.Value.ToString()))
            {
                switch (header.Type.Value)
                {
                    default:
                    case Header.Selection.Details:
                        currentContent = preloadedMetadata ?? new BeatmapMetadataWedge();
                        preloadedMetadata = null;
                        break;

                    case Header.Selection.Ranking:
                        currentContent = new BeatmapLeaderboardWedge
                        {
                            UseScorePanelOnlyInput = UseScorePanelOnlyInput,
                            Scope = { BindTarget = header.Scope },
                            Sorting = { BindTarget = header.Sorting },
                            FilterBySelectedMods = { BindTarget = header.FilterBySelectedMods },
                        };
                        break;
                }
            }

            using (PerformancePhaseLogger.Begin("BeatmapDetailsArea.AttachContent"))
            {
                contentContainer.Add(currentContent);
                currentContent.Show();
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            preloadedMetadata?.Dispose();
            base.Dispose(isDisposing);
        }

        public void Refresh()
        {
            if (currentContent is BeatmapLeaderboardWedge leaderboardWedge)
                leaderboardWedge.RefetchScores();
        }
    }
}
