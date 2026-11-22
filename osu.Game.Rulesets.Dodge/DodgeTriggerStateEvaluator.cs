// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Dodge.Objects;

namespace osu.Game.Rulesets.Dodge
{
    /// <summary>
    /// Deterministic, rewind-friendly evaluation of <see cref="DodgeTrigger"/> events.
    /// The state at any time T is a pure function of the trigger list and T, so seeking
    /// backwards or replaying always produces identical results.
    /// </summary>
    public class DodgeTriggerStateEvaluator
    {
        private readonly List<DodgeTrigger> triggers;

        public DodgeTriggerStateEvaluator(IEnumerable<DodgeTrigger> triggers)
        {
            // Stable order: time, then authoring action so equal-time triggers apply predictably.
            this.triggers = triggers.OrderBy(t => t.StartTime).ThenBy(t => t.Action).ToList();
        }

        /// <summary>
        /// All triggers that have fired by <paramref name="time"/>, in stable application order.
        /// </summary>
        public IEnumerable<DodgeTrigger> TriggersUpTo(double time)
            => triggers.Where(t => t.StartTime <= time);

        /// <summary>
        /// The latest ClearBullets trigger fired at or before <paramref name="time"/>, if any.
        /// Projectiles whose movement started strictly before this trigger's time are removed.
        /// </summary>
        public DodgeTrigger? ClearBulletsAt(double time)
            => triggers.LastOrDefault(t => t.Action == DodgeTriggerAction.ClearBullets && t.StartTime <= time);

        /// <summary>
        /// The HUD state implied by map triggers at <paramref name="time"/>:
        /// null when no trigger has touched the HUD, otherwise true for visible.
        /// </summary>
        public bool? HudVisibleAt(double time)
        {
            bool? visible = null;

            foreach (DodgeTrigger trigger in triggers)
            {
                if (trigger.StartTime > time)
                    break;

                switch (trigger.Action)
                {
                    case DodgeTriggerAction.ToggleHud:
                        visible = !(visible ?? true);
                        break;

                    case DodgeTriggerAction.HideHud:
                        visible = false;
                        break;

                    case DodgeTriggerAction.ShowHud:
                        visible = true;
                        break;
                }
            }

            return visible;
        }

        /// <summary>
        /// Whether the trail is enabled by map triggers at <paramref name="time"/>.
        /// Null means the trigger list does not control the trail.
        /// </summary>
        public bool? TrailEnabledAt(double time)
        {
            bool? enabled = null;

            foreach (DodgeTrigger trigger in triggers)
            {
                if (trigger.StartTime > time)
                    break;

                switch (trigger.Action)
                {
                    case DodgeTriggerAction.TrailDisable:
                        enabled = false;
                        break;

                    case DodgeTriggerAction.TrailEnable:
                        enabled = true;
                        break;
                }
            }

            return enabled;
        }

        /// <summary>
        /// The screen-shake effect active at <paramref name="time"/>, if any.
        /// A later trigger replaces a still-running earlier one.
        /// </summary>
        public DodgeTrigger? ScreenShakeAt(double time)
            => timedEffectAt(time, DodgeTriggerAction.ScreenShake);

        /// <summary>The flash effect active at <paramref name="time"/>, if any.</summary>
        public DodgeTrigger? FlashEffectAt(double time)
            => timedEffectAt(time, DodgeTriggerAction.FlashEffect);

        public DodgeTrigger? PlayerMoveAt(double time)
            => triggers.LastOrDefault(t => t.Action == DodgeTriggerAction.MovePlayer && t.StartTime <= time);

        public DodgeTrigger? CameraModeAt(double time)
            => triggers.LastOrDefault(t => t.StartTime <= time
                && t.Action is DodgeTriggerAction.CameraFollow or DodgeTriggerAction.CameraFixed);

        public bool CameraFollowsAt(double time) => CameraModeAt(time)?.Action == DodgeTriggerAction.CameraFollow;

        public static osuTK.Vector2 PlayerPositionAt(DodgeTrigger move, double time)
            => PlayerPositionAt(move, time, move.SourcePosition);

        public static osuTK.Vector2 PlayerPositionAt(DodgeTrigger move, double time, osuTK.Vector2 source)
        {
            float progress = move.Duration <= 0 ? 1 : (float)Math.Clamp((time - move.StartTime) / move.Duration, 0, 1);
            return source + (move.TargetPosition - source) * move.Easing.Apply(progress);
        }

        private DodgeTrigger? timedEffectAt(double time, DodgeTriggerAction action)
        {
            DodgeTrigger? match = null;

            foreach (DodgeTrigger trigger in triggers)
            {
                if (trigger.StartTime > time)
                    break;

                if (trigger.Action == action)
                    match = trigger;
            }

            // The newest effect replaces its predecessor permanently. An older,
            // longer effect must not resume when the replacement finishes.
            return match != null && time <= match.EndTime ? match : null;
        }

        /// <summary>
        /// Whether a projectile/emitter with the given movement start time exists on the
        /// field at <paramref name="currentTime"/> according to ClearBullets triggers.
        /// </summary>
        public bool ProjectileExists(double projectileStartTime, double currentTime)
        {
            DodgeTrigger? clear = ClearBulletsAt(currentTime);
            return clear == null || projectileStartTime >= clear.StartTime;
        }
    }
}
