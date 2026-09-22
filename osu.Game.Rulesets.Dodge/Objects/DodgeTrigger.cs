// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using Newtonsoft.Json;
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
    }

    /// <summary>
    /// A time-based, non-threatening map event. Triggers have no position gameplay meaning,
    /// never take part in collision or graze, and are judged purely by clock time:
    /// the player never has to touch anything for them to fire.
    /// </summary>
    public class DodgeTrigger : DodgeHitObject, IHasDuration, IHasPosition
    {
        public const double MAX_DURATION = 5000;

        /// <summary>The state change applied when this trigger's time is reached.</summary>
        public DodgeTriggerAction Action { get; set; }

        /// <summary>
        /// Length of the effect in milliseconds. Only meaningful for timed effects
        /// (<see cref="DodgeTriggerAction.ScreenShake"/>, <see cref="DodgeTriggerAction.FlashEffect"/>);
        /// ignored by instant actions such as <see cref="DodgeTriggerAction.ClearBullets"/>.
        /// </summary>
        public double Duration { get; set; } = 300;

        /// <summary>
        /// Effect intensity in the 0..1 range. Only meaningful for parameterised effects
        /// (<see cref="DodgeTriggerAction.ScreenShake"/>, <see cref="DodgeTriggerAction.FlashEffect"/>).
        /// </summary>
        public float Strength { get; set; } = 0.5f;

        /// <summary>
        /// Editor-only canvas position for the trigger marker. It has no gameplay or collision meaning.
        /// </summary>
        public Vector2 Position { get; set; } = new Vector2(256, 32);

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
        public bool IsTimedEffect => Action is DodgeTriggerAction.ScreenShake or DodgeTriggerAction.FlashEffect;

        public override Judgement CreateJudgement() => new DodgeTriggerJudgement();

        protected override HitWindows CreateHitWindows() => HitWindows.Empty;
    }
}
