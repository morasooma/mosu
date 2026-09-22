// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play;
using osuTK;

namespace osu.Game.Rulesets.Osu.UI
{
    /// <summary>
    /// Draws a short Morasooma signature after a locally-played map has completed.
    ///
    /// Cursor movement and <see cref="OsuAction.Smoke"/> are routed through the normal input manager so the
    /// signature is captured by <see cref="OsuReplayRecorder"/> and survives replay export and playback.
    /// </summary>
    internal partial class MorasoomaEndTagController : Drawable
    {
        internal const double DRAW_DURATION = 680;

        private readonly BindableBool enabled = new BindableBool();

        [Resolved]
        private DrawableOsuRuleset drawableRuleset { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private ScoreProcessor? scoreProcessor { get; set; }

        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        private bool drawing;
        private bool smokePressed;
        private double elapsedRealTime;
        private int currentStroke = -1;
        private OsuReplayRecorder? replayRecorder;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ForkMorasoomaEndTag, enabled);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            // The osu! playfield is also used by the editor, where gameplay-only dependencies are absent.
            if (scoreProcessor == null || gameplayClock == null)
                return;

            scoreProcessor.HasCompleted.BindValueChanged(completed =>
            {
                if (completed.NewValue)
                    beginTag();
                else
                    cancelTag();
            });
        }

        protected override void Update()
        {
            base.Update();

            if (!drawing || gameplayClock == null)
                return;

            if (drawableRuleset.KeyBindingInputManager.Recorder != replayRecorder)
            {
                cancelTag();
                return;
            }

            double gameplayRate = Math.Abs(gameplayClock.GetTrueGameplayRate());

            if (!double.IsFinite(gameplayRate) || gameplayRate < 0.01)
            {
                cancelTag();
                return;
            }

            elapsedRealTime += Math.Abs(gameplayClock.ElapsedFrameTime) / gameplayRate;

            if (elapsedRealTime >= DRAW_DURATION)
            {
                moveTo(MorasoomaEndTagPath.GetPosition(MorasoomaEndTagPath.StrokeCount - 1, 1));
                finishTag();
                return;
            }

            double strokeProgress = elapsedRealTime / DRAW_DURATION * MorasoomaEndTagPath.StrokeCount;
            int stroke = Math.Min((int)strokeProgress, MorasoomaEndTagPath.StrokeCount - 1);
            float progress = (float)(strokeProgress - stroke);

            if (stroke != currentStroke)
                beginStroke(stroke);

            moveTo(MorasoomaEndTagPath.GetPosition(stroke, progress));
        }

        private void beginTag()
        {
            OsuInputManager inputManager = drawableRuleset.KeyBindingInputManager;

            // Replays already contain their own smoke input. Only author a tag while a local replay recorder is active.
            if (!enabled.Value || drawableRuleset.ReplayScore != null || inputManager.Recorder is not OsuReplayRecorder recorder)
                return;

            // Never release a smoke press owned by the player.
            if (inputManager.PressedActions.Contains(OsuAction.Smoke))
                return;

            drawing = true;
            replayRecorder = recorder;
            elapsedRealTime = 0;
            currentStroke = -1;
            beginStroke(0);
        }

        private void beginStroke(int stroke)
        {
            releaseSmoke();
            currentStroke = stroke;
            moveTo(MorasoomaEndTagPath.GetPosition(stroke, 0));
            drawableRuleset.KeyBindingInputManager.KeyBindingContainer.TriggerPressed(OsuAction.Smoke);
            smokePressed = true;
        }

        private void finishTag()
        {
            releaseSmoke();
            drawing = false;
            drawableRuleset.KeyBindingInputManager.ResetVirtualCursor(restoreOriginalPosition: false);
            replayRecorder = null;
        }

        private void cancelTag()
        {
            if (!drawing)
                return;

            releaseSmoke();
            drawing = false;
            drawableRuleset.KeyBindingInputManager.ResetVirtualCursor();
            replayRecorder = null;
        }

        private void releaseSmoke()
        {
            if (!smokePressed)
                return;

            drawableRuleset.KeyBindingInputManager.KeyBindingContainer.TriggerReleased(OsuAction.Smoke);
            smokePressed = false;
        }

        private void moveTo(Vector2 gamefieldPosition)
        {
            drawableRuleset.KeyBindingInputManager.MoveVirtualCursorTo(drawableRuleset.Playfield.GamefieldToScreenSpace(gamefieldPosition));
            replayRecorder?.RecordFrame(true);
        }
    }

    internal static class MorasoomaEndTagPath
    {
        private const float letter_width = 42;
        private const float letter_height = 86;
        private const float letter_gap = 7;
        private const float top = 149;

        private static readonly Vector2[][] strokes =
        {
            // M
            new[] { p(0, 1), p(0, 0), p(0.5f, 0.55f), p(1, 0), p(1, 1) },
            // O
            new[] { p(0.5f, 0), p(0.12f, 0.08f), p(0, 0.5f), p(0.12f, 0.92f), p(0.5f, 1), p(0.88f, 0.92f), p(1, 0.5f), p(0.88f, 0.08f), p(0.5f, 0) },
            // R
            new[] { p(0, 1), p(0, 0), p(0.7f, 0), p(1, 0.2f), p(0.7f, 0.46f), p(0, 0.46f), p(0.62f, 0.46f), p(1, 1) },
            // A
            new[] { p(0, 1), p(0.5f, 0), p(1, 1), p(0.78f, 0.56f), p(0.22f, 0.56f) },
            // S
            new[] { p(0.95f, 0.08f), p(0.65f, 0), p(0.18f, 0.06f), p(0, 0.3f), p(0.2f, 0.48f), p(0.8f, 0.53f), p(1, 0.75f), p(0.82f, 0.97f), p(0.25f, 1), p(0.03f, 0.9f) },
            // O
            new[] { p(0.5f, 0), p(0.12f, 0.08f), p(0, 0.5f), p(0.12f, 0.92f), p(0.5f, 1), p(0.88f, 0.92f), p(1, 0.5f), p(0.88f, 0.08f), p(0.5f, 0) },
            // O
            new[] { p(0.5f, 0), p(0.12f, 0.08f), p(0, 0.5f), p(0.12f, 0.92f), p(0.5f, 1), p(0.88f, 0.92f), p(1, 0.5f), p(0.88f, 0.08f), p(0.5f, 0) },
            // M
            new[] { p(0, 1), p(0, 0), p(0.5f, 0.55f), p(1, 0), p(1, 1) },
            // A
            new[] { p(0, 1), p(0.5f, 0), p(1, 1), p(0.78f, 0.56f), p(0.22f, 0.56f) },
        };

        public static int StrokeCount => strokes.Length;

        public static Vector2 GetPosition(int stroke, float progress)
        {
            Vector2[] points = strokes[stroke];
            progress = Math.Clamp(progress, 0, 1);

            float totalLength = 0;
            for (int i = 1; i < points.Length; i++)
                totalLength += (points[i] - points[i - 1]).Length;

            float targetLength = totalLength * progress;

            for (int i = 1; i < points.Length; i++)
            {
                Vector2 start = points[i - 1];
                Vector2 end = points[i];
                float segmentLength = (end - start).Length;

                if (targetLength <= segmentLength)
                {
                    float segmentProgress = segmentLength == 0 ? 0 : targetLength / segmentLength;
                    return offset(stroke) + Vector2.Lerp(start, end, segmentProgress);
                }

                targetLength -= segmentLength;
            }

            return offset(stroke) + points[^1];
        }

        private static Vector2 p(float x, float y) => new Vector2(x * letter_width, y * letter_height);

        private static Vector2 offset(int stroke)
        {
            float totalWidth = StrokeCount * letter_width + (StrokeCount - 1) * letter_gap;
            return new Vector2((OsuPlayfield.BASE_SIZE.X - totalWidth) / 2 + stroke * (letter_width + letter_gap), top);
        }
    }
}
