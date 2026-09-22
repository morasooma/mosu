// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

#nullable disable

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Framework.Input.Handlers;
using osu.Framework.Platform;
using osu.Game.Configuration;
using osu.Game.Online.Spectator;
using osu.Game.Rulesets.Replays;
using osu.Game.Scoring;
using osuTK;

namespace osu.Game.Rulesets.UI
{
    public abstract partial class ReplayRecorder<T> : ReplayRecorder, IKeyBindingHandler<T>
        where T : struct
    {
        private readonly Score target;

        private readonly List<T> pressedActions = new List<T>();

        private InputManager inputManager;
        private GameHost host;
        // Standalone public build note: Input timing capture is an optional Mosu framework extension
        // used for hardware input diagnostics. When building against upstream ppy.osu.Framework,
        // timing capture is unavailable and InputSources remains empty.
        private double clockRate = 1;
        private double lastIntegrityReportUpdate;
        private long replayActionPressCount;
        private long replayActionReleaseCount;

#if DEBUG
        private GameplayIntegrityDebugScenario gameplayIntegrityDebugScenario;
#endif

        [Resolved(CanBeNull = true)]
        private GameplayIntegrityTracker gameplayIntegrityTracker { get; set; }

        /// <summary>
        /// The frame rate to record replays at.
        /// </summary>
        public int RecordFrameRate { get; set; } = 60;

        [Resolved]
        private SpectatorClient spectatorClient { get; set; }

        private readonly BindableBool onlineRecordSendingDisabled = new BindableBool();

        protected ReplayRecorder(Score target)
        {
            this.target = target;

            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, GameHost host)
        {
            this.host = host;
            config.BindWith(OsuSetting.ForkDisableOnlineRecordSending, onlineRecordSendingDisabled);
#if DEBUG
            Bindable<GameplayIntegrityDebugScenario> debugScenario = config.GetBindable<GameplayIntegrityDebugScenario>(OsuSetting.ForkGameplayIntegrityDebugScenario);
            gameplayIntegrityDebugScenario = debugScenario.Value;
            debugScenario.Value = GameplayIntegrityDebugScenario.None;
#endif
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            inputManager = GetContainingInputManager();

            clockRate = Math.Abs(Clock.Rate);
            if (!double.IsFinite(clockRate) || clockRate < 0.01)
                clockRate = 1;

            target.ScoreInfo.GameplayIntegrityReportProvider = createGameplayIntegrityReport;
            target.ScoreInfo.GameplayIntegrityReport = createGameplayIntegrityReport();
        }

        protected override void Update()
        {
            base.Update();
            RecordFrame(false);

            if (Time.Current - lastIntegrityReportUpdate >= 250)
            {
                target.ScoreInfo.GameplayIntegrityReport = createGameplayIntegrityReport();
                lastIntegrityReportUpdate = Time.Current;
            }
        }

        protected override bool OnMouseMove(MouseMoveEvent e)
        {
            RecordFrame(false);
            return base.OnMouseMove(e);
        }

        public bool OnPressed(KeyBindingPressEvent<T> e)
        {
            pressedActions.Add(e.Action);
            replayActionPressCount++;
            RecordFrame(true);
            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<T> e)
        {
            pressedActions.Remove(e.Action);
            replayActionReleaseCount++;
            RecordFrame(true);
        }

        public override void RecordFrame(bool important)
        {
            inputManager ??= GetContainingInputManager();

            if (inputManager == null)
                return;

            var last = target.Replay.Frames.LastOrDefault();

            if (!important && last != null && Time.Current - last.Time < (1000d / RecordFrameRate) * Clock.Rate)
                return;

            var position = ScreenSpaceToGamefield?.Invoke(inputManager.CurrentState.Mouse.Position) ?? inputManager.CurrentState.Mouse.Position;

            var frame = HandleFrame(position, pressedActions, last);

            if (frame != null)
            {
                // this reduces redundancy of frames in the resulting replay.
                if (last?.IsEquivalentTo(frame) == true)
                    target.Replay.Frames[^1] = frame;
                else
                    target.Replay.Frames.Add(frame);

                // the above de-duplication is done at `FrameDataBundle` level in `SpectatorClient`.
                // it's not 100% matching because of the possibility of duplicated frames crossing a bundle boundary, but it's close and simple enough.
                if (!onlineRecordSendingDisabled.Value)
                    spectatorClient?.HandleFrame(frame);
            }
        }

        protected abstract ReplayFrame HandleFrame(Vector2 mousePosition, List<T> actions, ReplayFrame previousFrame);

        private GameplayIntegrityReport createGameplayIntegrityReport()
        {
            var report = new GameplayIntegrityReport
            {
                ReplayFrameRate = RecordFrameRate,
                ClockRate = clockRate,
                ReplayFrameCount = target.Replay.Frames.Count,
                ReplayActionPressCount = replayActionPressCount,
                ReplayActionReleaseCount = replayActionReleaseCount,
                Clock = gameplayIntegrityTracker?.CreateClockReport() ?? new GameplayClockIntegrityReport(),
                Difficulty = gameplayIntegrityTracker?.CreateDifficultyReport() ?? new GameplayDifficultyIntegrityReport(),
                Assistance = gameplayIntegrityTracker?.CreateAssistanceReport() ?? new GameplayAssistanceIntegrityReport(),
                InputSources = Array.Empty<GameplayInputTimingReport>(),
            };

#if DEBUG
            GameplayIntegrityDebugInjector.Apply(report, gameplayIntegrityDebugScenario);
#endif

            return report;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing)
            {
                target.ScoreInfo.GameplayIntegrityReport = createGameplayIntegrityReport();
                target.ScoreInfo.GameplayIntegrityReportProvider = null;
            }

            base.Dispose(isDisposing);
        }
    }

    public abstract partial class ReplayRecorder : Component
    {
        public Func<Vector2, Vector2> ScreenSpaceToGamefield;

        public abstract void RecordFrame(bool important);
    }
}
