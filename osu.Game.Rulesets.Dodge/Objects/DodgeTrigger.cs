// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Newtonsoft.Json;
using osu.Game.Rulesets.Edit;
using osu.Framework.Graphics;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Dodge.Judgements;
using osu.Game.Rulesets.Dodge.Localisation;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Types;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Objects
{
    public enum DodgeTriggerAction
    {
        /// <summary>Remove every live projectile from the playfield instantly and fairly.</summary>
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerClearBullets))]
        ClearBullets,

        /// <summary>Toggle the player-facing HUD from its current map-authored state.</summary>
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerToggleHud))]
        ToggleHud,

        /// <summary>Hide the player-facing HUD overlay (score, combo, accuracy, key overlay).</summary>
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerHideHud))]
        HideHud,

        /// <summary>Show the player-facing HUD overlay.</summary>
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerShowHud))]
        ShowHud,

        /// <summary>Shake the arena visually for the configured duration. Purely visual.</summary>
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerScreenShake))]
        ScreenShake,

        /// <summary>Flash the arena with a coloured overlay for the configured duration. Purely visual.</summary>
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerFlashEffect))]
        FlashEffect,

        /// <summary>Enable the player trail for the remainder of the map (unless disabled by the player).</summary>
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerTrailEnable))]
        TrailEnable,

        /// <summary>Disable the player trail for the remainder of the map.</summary>
        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerTrailDisable))]
        TrailDisable,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerResizeArena))]
        ResizeArena,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerCameraFollow))]
        CameraFollow,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerCameraFixed))]
        CameraFixed,

        [LocalisableDescription(typeof(DodgeEditorStrings), nameof(DodgeEditorStrings.TriggerMovePlayer))]
        MovePlayer,
    }

    /// <summary>
    /// A time-based, non-threatening map event. Triggers have no position gameplay meaning,
    /// never take part in collision or graze, and are judged purely by clock time:
    /// the player never has to touch anything for them to fire.
    /// </summary>
    public class DodgeTrigger : DodgeHitObject, IHasDuration, IHasPosition, IEditorTimelineEndTimeAdjustable
    {
        private double duration = 300;

        /// <summary>The state change applied when this trigger's time is reached.</summary>
        public DodgeTriggerAction Action { get; set; }

        /// <summary>
        /// Length of a visual effect, arena transition or player path in milliseconds;
        /// ignored by instant actions such as <see cref="DodgeTriggerAction.ClearBullets"/>.
        /// </summary>
        public double Duration
        {
            get => IsTimedEffect ? duration : 0;
            set => duration = double.IsFinite(value) ? Math.Max(0, value) : 0;
        }

        public bool CanAdjustEditorTimelineEndTime => IsTimedEffect;

        public void SetEditorTimelineEndTime(double endTime) => Duration = endTime - StartTime;

        /// <summary>
        /// Effect intensity in the 0..1 range. Only meaningful for parameterised effects
        /// (<see cref="DodgeTriggerAction.ScreenShake"/>, <see cref="DodgeTriggerAction.FlashEffect"/>).
        /// </summary>
        public float Strength { get; set; } = 0.5f;

        /// <summary>
        /// Editor-only canvas position for the trigger marker. It has no gameplay or collision meaning.
        /// </summary>
        public Vector2 Position { get; set; } = new Vector2(256, 32);

        /// <summary>World-space start and target of an authored player move.</summary>
        public Vector2 SourcePosition { get; set; } = UI.DodgePlayfield.BASE_SIZE / 2;

        public bool MoveFromCurrentPosition { get; set; } = true;

        public Vector2 TargetPosition { get; set; } = UI.DodgePlayfield.BASE_SIZE / 2;

        public Vector2 TargetSize { get; set; } = UI.DodgePlayfield.BASE_SIZE;

        public DodgeCameraEasing Easing { get; set; }

        public float X
        {
            get => Position.X;
            set => Position = new Vector2(value, Y);
        }

        public float Y
        {
            get => Position.Y;
            set => Position = new Vector2(X, value);
        }

        [JsonIgnore]
        public double EndTime
        {
            get => StartTime + Duration;
            set => Duration = value - StartTime;
        }

        /// <summary>
        /// Whether this trigger's effect occupies a time range rather than being instantaneous.
        /// </summary>
        [JsonIgnore]
        public bool IsTimedEffect => Action is DodgeTriggerAction.ScreenShake or DodgeTriggerAction.FlashEffect
                                              or DodgeTriggerAction.ResizeArena or DodgeTriggerAction.MovePlayer;

        public override Judgement CreateJudgement() => new DodgeTriggerJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;
    }
}
