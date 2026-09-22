// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Input;
using osu.Framework.Input.StateChanges;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Input.Handlers;
using osu.Game.Replays;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Configuration;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.UI;
using osu.Game.Scoring;
using osu.Game.Scoring.Render;
using osu.Game.Screens.Play;
using osu.Game.Screens.Play.HUD.HitErrorMeters;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    [Cached(typeof(DrawableOsuRuleset))]
    public partial class DrawableOsuRuleset : DrawableRuleset<OsuHitObject>, IHasReplayBotPlayback, IPositionalMissProvider
    {
        private Bindable<bool>? cursorHideEnabled;
        private IBindable<bool>? forkRelaxEnabled;
        private ReplayBotController? replayBotController;
        private RenderReplayPlaybackController? renderReplayPlaybackController;

        public new OsuInputManager KeyBindingInputManager => (OsuInputManager)base.KeyBindingInputManager;

        public event Action<double> NewPositionalMiss
        {
            add => KeyBindingInputManager.NewPositionalMiss += value;
            remove => KeyBindingInputManager.NewPositionalMiss -= value;
        }

        public new OsuPlayfield Playfield => (OsuPlayfield)base.Playfield;

        protected new OsuRulesetConfigManager Config => (OsuRulesetConfigManager)base.Config;

        public DrawableOsuRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
            : base(ruleset, beatmap, mods)
        {
        }

        [Resolved]
        private osu.Game.Configuration.OsuConfigManager myOsuConfig { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load(ReplayPlayer? replayPlayer)
        {
            forkRelaxEnabled = myOsuConfig.GetBindable<bool>(OsuSetting.ForkRelaxEnabled);

            if (replayPlayer != null)
            {
                bool renderReplay = replayPlayer is RenderReplayPlayer;

                if (!renderReplay)
                {
                    ReplayAnalysisOverlay analysisOverlay;
                    PlayfieldAdjustmentContainer.Add(analysisOverlay = new ReplayAnalysisOverlay(replayPlayer.Score.Replay));
                    Overlays.Add(analysisOverlay.CreateProxy().With(p => p.Depth = float.NegativeInfinity));
                    replayPlayer.AddSettings(new ReplayAnalysisSettings(Config));
                }
                else
                {
                    HasReplayLoaded.BindValueChanged(_ => attachRenderReplayController(), true);
                }

                cursorHideEnabled = Config.GetBindable<bool>(OsuRulesetSetting.ReplayCursorHideEnabled);

                // I have little faith in this working (other things touch cursor visibility) but haven't broken it yet.
                // Let's wait for someone to report an issue before spending too much time on it.
                cursorHideEnabled.BindValueChanged(enabled => Playfield.Cursor.FadeTo(enabled.NewValue ? 0 : 1), true);
            }
        }

        [Resolved(CanBeNull = true)]
        private osu.Game.Screens.Play.Player? player { get; set; }

        [Resolved(CanBeNull = true)]
        private GameplayIntegrityTracker gameplayIntegrityTracker { get; set; } = null!;

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (gameplayIntegrityTracker == null)
                return;

            gameplayIntegrityTracker.VisualOD11Provider = () => IsVisualOD11;
            gameplayIntegrityTracker.AssistanceReportProvider = () => new GameplayAssistanceIntegrityReport
            {
                AimAssistEnabled = Playfield.AimAssistController.IsAimAssistEnabled,
                AimAssistDeclaredMod = Playfield.AimAssistController.IsAimAssistDeclaredMod,
                AimAssistAdjustedFrameCount = Playfield.AimAssistController.AdjustedFrameCount,
                AimAssistMaxAdjustment = Playfield.AimAssistController.MaxAdjustmentMagnitude,
                RelaxEnabled = Playfield.RelaxController.IsEnabled,
                RelaxDeclaredMod = Playfield.RelaxController.IsDeclaredMod,
                RelaxGeneratedPressCount = Playfield.RelaxController.GeneratedPressCount,
                RelaxGeneratedReleaseCount = Playfield.RelaxController.GeneratedReleaseCount,
            };
        }

        public override DrawableHitObject<OsuHitObject>? CreateDrawableRepresentation(OsuHitObject h) => null;

        public bool IsVisualOD11 => !(player is osu.Game.Screens.Play.ReplayPlayer) && !HasReplayLoaded.Value && myOsuConfig?.Get<bool>(OsuSetting.ForkVisualOD11) == true;

        public override osu.Game.Rulesets.Scoring.HitWindows? FirstAvailableVisualHitWindows
        {
            get
            {
                var original = base.FirstAvailableVisualHitWindows;

                if (original != null && IsVisualOD11)
                {
                    double originalGreat = original.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Great);
                    double od11Great = 13.5 * original.CustomSpeedMultiplier;

                    if (originalGreat >= od11Great)
                    {
                        var visualHitWindows = new OsuVisualHitWindows(original.WindowFor(osu.Game.Rulesets.Scoring.HitResult.Meh));
                        visualHitWindows.CustomSpeedMultiplier = original.CustomSpeedMultiplier;
                        return visualHitWindows;
                    }
                }

                return original;
            }
        }

        private class OsuVisualHitWindows : osu.Game.Rulesets.Osu.Scoring.OsuHitWindows
        {
            private readonly double originalMeh;

            public OsuVisualHitWindows(double originalMeh)
            {
                this.originalMeh = originalMeh;
                SetDifficulty(11);
            }

            public override double WindowFor(osu.Game.Rulesets.Scoring.HitResult result)
            {
                if (result == osu.Game.Rulesets.Scoring.HitResult.Meh)
                    return originalMeh;
                return base.WindowFor(result);
            }
        }

        public override bool ReceivePositionalInputAt(Vector2 screenSpacePos) => true; // always show the gameplay cursor

        protected override Playfield CreatePlayfield() => new OsuPlayfield();

        protected override PassThroughInputManager CreateInputManager() => new OsuInputManager(Ruleset.RulesetInfo);

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer() => new OsuPlayfieldAdjustmentContainer { AlignWithStoryboard = true };

        protected override ResumeOverlay CreateResumeOverlay()
        {
            if (Mods.Any(m => m is OsuModAutopilot or OsuModTouchDevice))
                return new DelayedResumeOverlay { Scale = new Vector2(0.65f) };

            return new OsuResumeOverlay();
        }

        protected override ReplayInputHandler CreateReplayInputHandler(Replay replay) => new OsuFramedReplayInputHandler(replay);

        protected override ReplayRecorder CreateReplayRecorder(Score score) => new OsuReplayRecorder(score);

        public void SetReplayBotScore(Score replayBotScore)
        {
            replayBotController?.Expire();
            replayBotController = null;

            if (replayBotScore == null)
                return;

            var handler = new OsuFramedReplayInputHandler(replayBotScore.Replay, forkRelaxEnabled, ignoreReplayActionsWhenRelaxEnabled: true)
            {
                GamefieldToScreenSpace = Playfield.GamefieldToScreenSpace
            };

            FrameStableComponents.Add(replayBotController = new ReplayBotController(KeyBindingInputManager, handler));
        }

        public override double GameplayStartTime
        {
            get
            {
                if (Objects.FirstOrDefault() is OsuHitObject first)
                    return first.StartTime - Math.Max(2000, first.TimePreempt);

                return 0;
            }
        }

        private partial class ReplayBotController : Drawable
        {
            private readonly OsuInputManager inputManager;
            private readonly OsuFramedReplayInputHandler handler;
            private readonly List<IInput> pendingInputs = new List<IInput>();

            public ReplayBotController(OsuInputManager inputManager, OsuFramedReplayInputHandler handler)
            {
                this.inputManager = inputManager;
                this.handler = handler;
                Alpha = 0;
                AlwaysPresent = true;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                inputManager.AllowUserCursorMovement = false;
                inputManager.SetReplayBotActive(true);
            }

            protected override void Update()
            {
                base.Update();

                if (handler.SetFrameFromTime(Clock.CurrentTime) == null)
                    return;

                pendingInputs.Clear();
                handler.CollectPendingInputs(pendingInputs);

                foreach (IInput input in pendingInputs)
                    input.Apply(inputManager.CurrentState, inputManager);
            }

            // Do not propagate synthetic key releases from Dispose(). Drawable disposal may run
            // after the update thread has stopped (notably in the replay-render worker), while a
            // release mutates cursor transforms. The input manager is disposed with this ruleset,
            // so resetting its transient replay state here is unnecessary.
        }

        private void attachRenderReplayController()
        {
            if (renderReplayPlaybackController != null || !HasReplayLoaded.Value)
                return;

            if (KeyBindingInputManager.ReplayInputHandler is not OsuFramedReplayInputHandler replayHandler)
                return;

            replayHandler.GamefieldToScreenSpace = Playfield.GamefieldToScreenSpace;
            FrameStableComponents.Add(renderReplayPlaybackController = new RenderReplayPlaybackController(KeyBindingInputManager));
        }

        private partial class RenderReplayPlaybackController : Drawable
        {
            private readonly OsuInputManager inputManager;

            public RenderReplayPlaybackController(OsuInputManager inputManager)
            {
                this.inputManager = inputManager;
                Alpha = 0;
                AlwaysPresent = true;
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();

                inputManager.UseParentInput = false;
                inputManager.AllowUserCursorMovement = false;
                inputManager.SetReplayBotActive(true);
            }

            // See ReplayBotController above. Restoring UseParentInput or applying releases here
            // can dispatch cursor events from the host disposal thread.
        }
    }
}
