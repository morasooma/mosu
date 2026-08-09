// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#if DEBUG

using JetBrains.Annotations;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Screens.Ranking
{
    public partial class ReplayBotLoadButton : CompositeDrawable
    {
        public readonly Bindable<ScoreInfo?> Score = new Bindable<ScoreInfo?>();

        private readonly Bindable<DownloadState> state = new Bindable<DownloadState>();

        private RoundedButton button = null!;
        private ScoreDownloadTracker? downloadTracker;

        [Resolved]
        private ScoreManager scoreManager { get; set; } = null!;

        [Resolved]
        private SessionStatics sessionStatics { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private INotificationOverlay? notifications { get; set; }

        public ReplayBotLoadButton(ScoreInfo? score)
        {
            Score.Value = score;
            Size = new Vector2(50, 40);
        }

        [BackgroundDependencyLoader]
        private void load(OsuColour colours)
        {
            InternalChild = button = new RoundedButton
            {
                RelativeSizeAxes = Axes.Both,
                BackgroundColour = colours.Gray4,
                Text = "Load replay bot (debug)",
                Action = loadReplayBot
            };

            Score.BindValueChanged(score =>
            {
                downloadTracker?.RemoveAndDisposeImmediately();
                downloadTracker = null;
                state.SetDefault();

                if (score.NewValue != null)
                {
                    AddInternal(downloadTracker = new ScoreDownloadTracker(score.NewValue)
                    {
                        State = { BindTarget = state }
                    });
                }

                updateState();
            }, true);

            state.BindValueChanged(_ => updateState(), true);
        }

        private void loadReplayBot()
        {
            if (Score.Value == null)
                return;

            var score = scoreManager.GetScore(Score.Value);

            if (score == null || score.Replay.Frames.Count == 0)
            {
                notifications?.Post(new SimpleNotification
                {
                    Text = "Replay is not available locally."
                });
                return;
            }

            sessionStatics.SetValue(Static.LoadedReplayBotScore, score.DeepClone());

            notifications?.Post(new SimpleNotification
            {
                Text = "Replay loaded for replay bot."
            });
        }

        private void updateState()
        {
            bool locallyAvailable = state.Value == DownloadState.LocallyAvailable;

            button.Enabled.Value = locallyAvailable;
            button.TooltipText = locallyAvailable
                ? "load replay for replay bot"
                : "download replay first";
        }
    }
}

#endif
