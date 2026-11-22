// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Input.StateChanges;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Online;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Osu.UI;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Osu.Mods
{
    public class OsuModAutopilot : Mod, IUpdatableByPlayfield, IApplicableToDrawableRuleset<OsuHitObject>
    {
        public override string Name => "Autopilot";
        public override string Acronym => "AP";
        public override IconUsage? Icon => OsuIcon.ModAutopilot;
        public override ModType Type => ModType.Automation;
        public override LocalisableString Description => global::osu.Game.Localisation.ClientTranslationStrings.J3664;

        public override Type[] IncompatibleMods => new[]
        {
            typeof(OsuModSpunOut),
            typeof(ModRelax),
            typeof(ModAutoplay),
            typeof(OsuModMagnetised),
            typeof(OsuModRepel),
            typeof(ModTouchDevice)
        };

        private OsuInputManager inputManager = null!;

        private List<OsuReplayFrame> replayFrames = null!;

        private int currentFrame;
        private bool useSmoothMovement;

        public void Update(Playfield playfield)
        {
            if (GetCursorPosition(playfield.Clock.CurrentTime) is not Vector2 position)
                return;

            if (useSmoothMovement)
                inputManager.MoveVirtualCursorTo(playfield.ToScreenSpace(position));
            else
                new MousePositionAbsoluteInput { Position = playfield.ToScreenSpace(position) }.Apply(inputManager.CurrentState, inputManager);
        }

        internal Vector2? GetCursorPosition(double time)
        {
            if (!useSmoothMovement)
            {
                // Preserve the original lazer frame selection outside Mosu, including advancing
                // at most one frame per update and only moving when that frame changes.
                if (currentFrame == replayFrames.Count - 1)
                    return null;

                if (currentFrame < 0 || Math.Abs(replayFrames[currentFrame + 1].Time - time) <= Math.Abs(replayFrames[currentFrame].Time - time))
                    return replayFrames[++currentFrame].Position;

                return null;
            }

            if (replayFrames.Count == 0)
                return null;

            // Follow the gameplay clock, including seeks and frames skipped during a stall.
            while (currentFrame > 0 && replayFrames[currentFrame].Time > time)
                currentFrame--;

            while (currentFrame < replayFrames.Count - 1 && replayFrames[currentFrame + 1].Time <= time)
                currentFrame++;

            OsuReplayFrame start = replayFrames[currentFrame];

            if (time <= start.Time || currentFrame == replayFrames.Count - 1)
                return start.Position;

            OsuReplayFrame end = replayFrames[currentFrame + 1];

            // Sample on every gameplay update, rather than snapping to the nearest generated frame.
            // The generator already applies easing, so interpolation between its samples is linear.
            // TODO: pause at unjudged objects based on user interactions, as stable does.
            return Interpolation.ValueAt(time, start.Position, end.Position, start.Time, end.Time);
        }

        public void ApplyToDrawableRuleset(DrawableRuleset<OsuHitObject> drawableRuleset)
        {
            // Grab the input manager to disable the user's cursor, and for future use
            inputManager = ((DrawableOsuRuleset)drawableRuleset).KeyBindingInputManager;
            inputManager.AllowUserCursorMovement = false;

            // Capture the server once for this play, not on every cursor update.
            useSmoothMovement = MosuServerEnvironment.IsMosuServerUrl(MosuServerEnvironment.ActiveApiUrl);

            // Generate the replay frames the cursor should follow
            replayFrames = new OsuAutoGenerator(drawableRuleset.Beatmap, drawableRuleset.Mods)
            {
                DelayedMovements = useSmoothMovement
            }.Generate().Frames.Cast<OsuReplayFrame>().ToList();
            currentFrame = useSmoothMovement ? 0 : -1;
        }
    }
}
