// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Online.Multiplayer;
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
        private readonly Bindable<string> text = new Bindable<string>();
        private MorasoomaEndTagPath path = null!;

        [Resolved]
        private DrawableOsuRuleset drawableRuleset { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private ScoreProcessor? scoreProcessor { get; set; }

        [Resolved(canBeNull: true)]
        private IGameplayClock? gameplayClock { get; set; }

        [Resolved(canBeNull: true)]
        private MultiplayerClient? multiplayerClient { get; set; }

        private bool drawing;
        private bool smokePressed;
        private double elapsedRealTime;
        private int currentStroke = -1;
        private OsuReplayRecorder? replayRecorder;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            config.BindWith(OsuSetting.ForkMorasoomaEndTag, enabled);
            config.BindWith(OsuSetting.ForkMorasoomaEndTagText, text);
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
                moveTo(path.GetPosition(path.StrokeCount - 1, 1));
                finishTag();
                return;
            }

            double strokeProgress = elapsedRealTime / DRAW_DURATION * path.StrokeCount;
            int stroke = Math.Min((int)strokeProgress, path.StrokeCount - 1);
            float progress = (float)(strokeProgress - stroke);

            if (stroke != currentStroke)
                beginStroke(stroke);

            moveTo(path.GetPosition(stroke, progress));
        }

        private void beginTag()
        {
            OsuInputManager inputManager = drawableRuleset.KeyBindingInputManager;

            // Replays already contain their own smoke input. Only author a tag while a local replay recorder is active.
            // The tag appends synthetic replay frames after the final judgement. In multiplayer the
            // result must be submitted immediately, so never extend the recorded input there.
            if (!enabled.Value || multiplayerClient?.Room != null || drawableRuleset.ReplayScore != null || inputManager.Recorder is not OsuReplayRecorder recorder)
                return;

            // Never release a smoke press owned by the player.
            if (inputManager.PressedActions.Contains(OsuAction.Smoke))
                return;

            // Release the assist before the first smoke press, not on its next Update():
            // resetting its virtual cursor after drawing starts would also release the tag's cursor.
            drawableRuleset.Playfield.AimAssistController.StopCursorTracking();

            path = new MorasoomaEndTagPath(text.Value);
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
            moveTo(path.GetPosition(stroke, 0));
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

    internal sealed class MorasoomaEndTagPath
    {
        private const float letter_width = 42;
        private const float letter_height = 86;
        private const float letter_gap = 7;
        private const float top = 149;

        private static readonly Vector2[][] legacyStrokes =
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

        internal const int MAX_LENGTH = 16;
        private readonly Vector2[][] strokes;
        private readonly Vector2[] offsets;
        private readonly float scale;

        // Single-line stroke font. Separate paths in a glyph never leave connecting smoke trails.
        private static readonly Dictionary<char, string> glyphs = new Dictionary<char, string>
        {
            ['B'] = "09 00 70 92 74 04|74 97 79 09", ['C'] = "91 60 21 05 29 69 98",
            ['D'] = "09 00 60 92 97 69 09", ['E'] = "90 00 09 99|05 75",
            ['F'] = "09 00 90|05 75", ['G'] = "91 60 21 05 29 69 98 95 65",
            ['H'] = "00 09|90 99|05 95", ['I'] = "00 90|50 59|09 99",
            ['J'] = "00 90 97 69 29 07", ['K'] = "00 09|90 05 99", ['L'] = "00 09 99",
            ['N'] = "09 00 99 90", ['P'] = "09 00 70 92 74 04", ['Q'] = "50 11 05 19 59 98 95 81 50|66 99",
            ['T'] = "00 90|50 59", ['U'] = "00 07 29 79 97 90", ['V'] = "00 59 90",
            ['W'] = "00 29 55 79 90", ['X'] = "00 99|90 09", ['Y'] = "00 55 90|55 59",
            ['Z'] = "00 90 09 99",
            ['0'] = "50 11 05 19 59 98 95 81 50|18 81", ['1'] = "13 50 59|19 99",
            ['2'] = "02 20 70 92 94 09 99", ['3'] = "00 90 55 95 97 79 29 07",
            ['4'] = "70 06 96|70 79", ['5'] = "90 00 04 74 96 98 79 19 08",
            ['6'] = "91 60 20 05 08 29 79 98 96 74 04", ['7'] = "00 90 39",
            ['8'] = "50 10 02 14 85 97 89 19 07 15 84 92 80 50", ['9'] = "95 25 03 01 20 70 91 97 79 29 08",
            ['Б'] = "90 00 09 79 97 75 05", ['Г'] = "09 00 90", ['Д'] = "09 29 40 80 89 99|07 99",
            ['Ж'] = "00 55 09|50 59|90 55 99", ['З'] = "01 30 80 92 74 44|74 96 89 29 08",
            ['И'] = "00 09 90 99", ['Й'] = "02 09 92 99|30 51 70", ['Л'] = "09 30 90 99",
            ['П'] = "09 00 90 99", ['У'] = "00 55 90|55 39 09", ['Ф'] = "50 59|52 12 04 16 86 94 82 52",
            ['Ц'] = "00 08 88 90|88 99", ['Ч'] = "00 04 94|90 99",
            ['Ш'] = "00 09 99 90|50 59", ['Щ'] = "00 08 88 90|50 58|88 99",
            ['Ъ'] = "00 30 39 79 97 75 35", ['Ы'] = "00 09 59 77 55 05|90 99",
            ['Ь'] = "00 09 79 97 75 05", ['Э'] = "01 30 70 94 98 79 29 08|45 95",
            ['Ю'] = "00 09|05 35|60 31 35 39 69 98 95 91 60", ['Я'] = "99 90 20 02 24 94|54 09",
            ['-'] = "15 85", ['_'] = "09 99", ['.'] = "48 59", [','] = "58 49",
            ['!'] = "50 56|58 59", ['?'] = "02 20 70 92 74 55 56|58 59",
            [':'] = "42 53|47 58", ['/'] = "09 90", ['\\'] = "00 99",
            ['('] = "80 32 37 89", [')'] = "10 62 67 19", ['+'] = "05 95|50 59",
            ['='] = "03 93|07 97", ['<'] = "91 05 99", ['>'] = "01 95 09",
            ['['] = "80 20 29 89", [']'] = "10 70 79 19", ['#'] = "30 19|80 69|03 93|07 97",
            ['@'] = "97 99 19 00 90 96 46 43 73 76", ['*'] = "15 85|22 78|28 72",
            ['\''] = "40 42", ['\"'] = "30 32|70 72", [';'] = "42 53|58 49",
        };

        public MorasoomaEndTagPath(string text)
        {
            string value = (text ?? string.Empty).Trim().ToUpperInvariant();
            value = new string(value.Where(c => c == ' ' || getGlyph(c).Length > 0).Take(MAX_LENGTH).ToArray()).Trim();
            if (value.Length == 0)
                value = "MORASOOMA";

            scale = Math.Min(1, (OsuPlayfield.BASE_SIZE.X - 40) / (value.Length * letter_width + (value.Length - 1) * letter_gap));
            float totalWidth = (value.Length * letter_width + (value.Length - 1) * letter_gap) * scale;
            var paths = new List<Vector2[]>();
            var positions = new List<Vector2>();
            for (int i = 0; i < value.Length; i++)
            {
                foreach (Vector2[] stroke in getGlyph(value[i]))
                {
                    paths.Add(stroke);
                    positions.Add(new Vector2((OsuPlayfield.BASE_SIZE.X - totalWidth) / 2 + i * (letter_width + letter_gap) * scale,
                        top + letter_height * (1 - scale) / 2));
                }
            }
            strokes = paths.ToArray();
            offsets = positions.ToArray();
        }

        private static Vector2[][] getGlyph(char c)
        {
            c = c switch
            {
                'А' => 'A', 'В' => 'B', 'Е' or 'Ё' => 'E', 'К' => 'K', 'М' => 'M',
                'Н' => 'H', 'О' => 'O', 'Р' => 'P', 'С' => 'C', 'Т' => 'T', 'Х' => 'X', _ => c,
            };
            int legacyIndex = c switch { 'M' => 0, 'O' => 1, 'R' => 2, 'A' => 3, 'S' => 4, _ => -1 };
            if (legacyIndex >= 0)
                return new[] { legacyStrokes[legacyIndex] };
            if (!glyphs.TryGetValue(c, out string? encoded))
                return Array.Empty<Vector2[]>();
            return encoded.Split('|').Select(stroke => stroke.Split(' ').Select(point => p((point[0] - '0') / 9f, (point[1] - '0') / 9f)).ToArray()).ToArray();
        }

        public int StrokeCount => strokes.Length;

        public Vector2 GetPosition(int stroke, float progress)
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
                    return offsets[stroke] + Vector2.Lerp(start, end, segmentProgress) * scale;
                }

                targetLength -= segmentLength;
            }

            return offsets[stroke] + points[^1] * scale;
        }

        private static Vector2 p(float x, float y) => new Vector2(x * letter_width, y * letter_height);

    }
}
