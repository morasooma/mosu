// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Rulesets.Judgements;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Objects.Drawables;
using osu.Game.Rulesets.Osu.Objects;
using osu.Game.Rulesets.Osu.Objects.Drawables;
using osu.Game.Rulesets.Osu.Replays;
using osu.Game.Rulesets.Replays;
using osu.Game.Rulesets.Scoring;
using osu.Game.Rulesets.TagCoop;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.UI.TagCoop
{
    /// <summary>
    /// Splits osu!standard combos between Tag Co-op players and mirrors the owner's judgements.
    /// </summary>
    public partial class OsuTagCoopGameplayController : TagCoopGameplayController
    {
        private const double remote_feedback_delay = 250;
        private const double remote_judgement_grace = 1000;
        private static readonly Color4 remote_object_colour = new Color4(105, 105, 105, 255);

        private readonly DrawableRuleset drawableRuleset;
        private readonly TagCoopGameplayConfiguration configuration;
        private readonly Dictionary<HitObject, int> objectIndices;
        private readonly Dictionary<int, HitObject> objectsByIndex;
        private readonly Dictionary<HitObject, int> objectOwners = new Dictionary<HitObject, int>();
        private readonly Dictionary<int, (int UserId, HitResult Result)> pendingRemoteFeedback = new Dictionary<int, (int, HitResult)>();
        private readonly OsuHitObject[] topLevelObjects;
        private int activeObjectIndex;
        private double lastTurnStateTime = double.NegativeInfinity;
        private OsuHitObject? activeObject;
        private int? activeUserId;
        private int? nextUserId;
        private double millisecondsUntilNextTurn = double.PositiveInfinity;
        private bool allPlayersActive;
        private bool allPlayersNext;

        public override int? ActiveUserId => activeUserId;

        public override int? NextUserId => nextUserId;

        public override double MillisecondsUntilNextTurn => millisecondsUntilNextTurn;

        public override bool AllPlayersActive => allPlayersActive;

        public override bool AllPlayersNext => allPlayersNext;

        public override Vector2? AutomatedCursorPosition => getAutomatedCursorPosition();

        public override byte LocalButtonState
        {
            get
            {
                if (configuration.AutoplayLocalTurns && (allPlayersActive || activeUserId == configuration.LocalUserId))
                    return 1;

                if (drawableRuleset is not DrawableOsuRuleset osuRuleset)
                    return 0;

                byte state = 0;

                if (osuRuleset.KeyBindingInputManager.PressedActions.Contains(OsuAction.LeftButton))
                    state |= 1;
                if (osuRuleset.KeyBindingInputManager.PressedActions.Contains(OsuAction.RightButton))
                    state |= 2;

                return state;
            }
        }

        public override ReplayFrame? CreateCompatibilityReplayFrame(TagCoopReplayFrame frame)
            => createCompatibilityReplayFrame(frame, topLevelObjects, configuration.PlayerOrder);

        public override IReadOnlyList<ReplayFrame> CreateCompatibilityReplayFrames(TagCoopReplayMetadata metadata)
            => createCompatibilityReplayFrames(metadata.Frames, topLevelObjects, configuration.PlayerOrder);

        public static IReadOnlyList<ReplayFrame> CreateCompatibilityReplayFrames(DrawableRuleset drawableRuleset, TagCoopReplayMetadata metadata)
        {
            OsuHitObject[] objects = drawableRuleset.Objects.OfType<OsuHitObject>().OrderBy(hitObject => hitObject.StartTime).ToArray();
            int[] playerOrder = metadata.Players.Select(player => player.UserID).Distinct().ToArray();

            return createCompatibilityReplayFrames(metadata.Frames, objects, playerOrder);
        }

        private static IReadOnlyList<ReplayFrame> createCompatibilityReplayFrames(
            IEnumerable<TagCoopReplayFrame> source,
            OsuHitObject[] objects,
            IReadOnlyList<int> playerOrder)
        {
            var combined = new List<ReplayFrame>();
            int? previousOwner = null;
            byte previousButtons = 0;
            Vector2 previousPosition = Vector2.Zero;

            foreach (TagCoopReplayFrame frame in source.DistinctBy(f => (f.UserID, f.Sequence))
                                                               .OrderBy(f => f.GameplayTime)
                                                               .ThenBy(f => f.Sequence))
            {
                if (objects.Length == 0 || playerOrder.Count == 0 || ownerAt(frame.GameplayTime, objects, playerOrder) != frame.UserID)
                    continue;

                Vector2 position = new Vector2(frame.X * 512, frame.Y * 384);

                // Two players may both be holding the same physical action when ownership
                // changes. The ordinary replay is a single input stream, so it needs an explicit
                // release between owners or the next player's first press disappears.
                if (previousOwner != null && previousOwner != frame.UserID && previousButtons != 0)
                    combined.Add(new OsuReplayFrame(frame.GameplayTime, previousPosition));

                combined.Add(createCompatibilityReplayFrame(frame, position));
                previousOwner = frame.UserID;
                previousButtons = (byte)(frame.ButtonState & 0b11);
                previousPosition = position;
            }

            return combined;
        }

        private static ReplayFrame? createCompatibilityReplayFrame(TagCoopReplayFrame frame, OsuHitObject[] objects, IReadOnlyList<int> playerOrder)
        {
            if (objects.Length == 0 || playerOrder.Count == 0 || ownerAt(frame.GameplayTime, objects, playerOrder) != frame.UserID)
                return null;

            return createCompatibilityReplayFrame(frame, new Vector2(frame.X * 512, frame.Y * 384));
        }

        private static OsuReplayFrame createCompatibilityReplayFrame(TagCoopReplayFrame frame, Vector2 position)
        {
            var actions = new List<OsuAction>(2);

            if ((frame.ButtonState & 1) != 0)
                actions.Add(OsuAction.LeftButton);
            if ((frame.ButtonState & 2) != 0)
                actions.Add(OsuAction.RightButton);

            return new OsuReplayFrame(frame.GameplayTime, position, actions.ToArray());
        }

        public OsuTagCoopGameplayController(DrawableRuleset drawableRuleset, TagCoopGameplayConfiguration configuration)
        {
            this.drawableRuleset = drawableRuleset;
            this.configuration = configuration;

            HitObject[] allObjects = flatten(drawableRuleset.Objects).ToArray();
            objectIndices = allObjects.Select((hitObject, index) => (hitObject, index))
                                      .ToDictionary(pair => pair.hitObject, pair => pair.index);
            objectsByIndex = objectIndices.ToDictionary(pair => pair.Value, pair => pair.Key);
            topLevelObjects = drawableRuleset.Objects.OfType<OsuHitObject>().OrderBy(h => h.StartTime).ToArray();

            foreach (OsuHitObject topLevel in topLevelObjects)
            {
                int owner = ownerFromCombo(topLevel);

                foreach (HitObject hitObject in flatten(new[] { topLevel }))
                    objectOwners[hitObject] = owner;
            }

            drawableRuleset.NewResult += onNewResult;
        }

        protected override void Update()
        {
            base.Update();

            double currentTime = drawableRuleset.Playfield.Clock.CurrentTime;
            updateTurnState(currentTime);
            dispatchRemoteFeedback(currentTime);

            foreach (DrawableHitObject topLevel in drawableRuleset.Playfield.AllHitObjects)
                updateDrawable(topLevel, currentTime);
        }

        public override void ApplyRemoteJudgement(int userId, int objectIndex, HitResult result)
        {
            if (objectIndex < 0 || result == HitResult.None
                || !objectsByIndex.TryGetValue(objectIndex, out HitObject? hitObject)
                || hitObject is not OsuHitObject osuHitObject
                || ownerFor(osuHitObject) != userId)
                return;

            DrawableOsuHitObject? drawable = findDrawable(hitObject);
            if (drawable == null || drawable.Judged)
                return;

            // The owner judgement is part of the one shared score. Previously remote objects
            // were awarded a placeholder maximum result, causing every client to calculate a
            // different score and producing one leaderboard row per personal segment.
            drawable.ApplyTagCoopResult(result, playSamples: false);

            if (!result.IsHit() && result != HitResult.IgnoreMiss)
                pendingRemoteFeedback[objectIndex] = (userId, result);
        }

        private void updateDrawable(DrawableHitObject drawable, double currentTime)
        {
            if (drawable is DrawableOsuHitObject osuDrawable)
            {
                OsuHitObject hitObject = osuDrawable.HitObject;
                int owner = ownerFor(hitObject);
                bool locallyOwned = owner == configuration.LocalUserId;
                bool ownerPresent = locallyOwned || configuration.IsPlayerPresent(owner);
                bool autoplay = (configuration.AutoplayLocalTurns && locallyOwned) || !ownerPresent;
                bool waitingForRemote = !locallyOwned && ownerPresent;

                osuDrawable.HandleUserInput = locallyOwned && !autoplay;
                osuDrawable.SuppressAutomaticJudgement = autoplay || waitingForRemote;
                osuDrawable.AccentColour.Value = locallyOwned ? colourFor(configuration.LocalUserId) : remote_object_colour;

                if (!osuDrawable.Judged && waitingForRemote && currentTime >= automatedJudgementTime(osuDrawable))
                    osuDrawable.PlayTagCoopSamplesAhead();
            }

            // Slider children must be judged before their parent. In particular, the tail has
            // the same end time as the slider and the slider result depends on all nested results.
            foreach (DrawableHitObject nested in drawable.NestedHitObjects)
                updateDrawable(nested, currentTime);

            if (drawable is DrawableOsuHitObject osuDrawableToJudge)
            {
                OsuHitObject hitObject = osuDrawableToJudge.HitObject;
                int owner = ownerFor(hitObject);
                bool locallyOwned = owner == configuration.LocalUserId;
                bool ownerPresent = locallyOwned || configuration.IsPlayerPresent(owner);
                bool autoplay = (configuration.AutoplayLocalTurns && locallyOwned) || !ownerPresent;
                bool waitingForRemote = !locallyOwned && ownerPresent;

                if (!osuDrawableToJudge.Judged && objectIndices.TryGetValue(hitObject, out int index))
                {
                    bool canJudge = osuDrawableToJudge is not DrawableHitCircle || !hasEarlierUnjudgedHitCircle(hitObject.StartTime);

                    if (canJudge && waitingForRemote && currentTime >= automatedJudgementTime(osuDrawableToJudge) + remote_judgement_grace)
                        osuDrawableToJudge.MissForcefully();
                    else if (canJudge && autoplay && currentTime >= automatedJudgementTime(osuDrawableToJudge))
                        osuDrawableToJudge.HitForcefully();
                }
            }
        }

        private static double automatedJudgementTime(DrawableOsuHitObject drawable)
            => drawable is DrawableSlider or DrawableSpinner ? drawable.HitObject.GetEndTime() : drawable.HitObject.StartTime;

        private bool hasEarlierUnjudgedHitCircle(double startTime)
        {
            foreach (DrawableHitObject drawable in drawableRuleset.Playfield.AllHitObjects)
            {
                if (containsEarlierUnjudgedHitCircle(drawable, startTime))
                    return true;
            }

            return false;
        }

        private static bool containsEarlierUnjudgedHitCircle(DrawableHitObject drawable, double startTime)
        {
            if (drawable is DrawableHitCircle && !drawable.Judged && drawable.HitObject.StartTime < startTime)
                return true;

            foreach (DrawableHitObject nested in drawable.NestedHitObjects)
            {
                if (containsEarlierUnjudgedHitCircle(nested, startTime))
                    return true;
            }

            return false;
        }

        private void dispatchRemoteFeedback(double currentTime)
        {
            if (pendingRemoteFeedback.Count == 0)
                return;

            foreach (int index in pendingRemoteFeedback.Keys.ToArray())
            {
                if (!objectsByIndex.TryGetValue(index, out HitObject? hitObject)
                    || hitObject is not OsuHitObject osuHitObject
                    || currentTime < hitObject.StartTime + remote_feedback_delay)
                    continue;

                (int userId, HitResult result) = pendingRemoteFeedback[index];
                pendingRemoteFeedback.Remove(index);

                if (!result.IsHit() && result != HitResult.IgnoreMiss)
                    NotifyRemoteJudgementFeedback(userId, osuHitObject.StackedPosition, result);
            }
        }

        private int ownerFor(OsuHitObject hitObject)
            => objectOwners.TryGetValue(hitObject, out int owner) ? owner : ownerFromCombo(hitObject);

        private int ownerFromCombo(OsuHitObject hitObject)
        {
            if (configuration.PlayerOrder.Count == 0)
                return configuration.LocalUserId;

            int index = Math.Abs(hitObject.ComboIndex % configuration.PlayerOrder.Count);
            return configuration.PlayerOrder[index];
        }

        private static int ownerAt(double time, OsuHitObject[] objects, IReadOnlyList<int> playerOrder)
        {
            int left = 0;
            int right = objects.Length - 1;
            int lastStarted = -1;

            while (left <= right)
            {
                int middle = left + (right - left) / 2;

                if (objects[middle].StartTime <= time)
                {
                    lastStarted = middle;
                    left = middle + 1;
                }
                else
                    right = middle - 1;
            }

            if (lastStarted < 0)
                return ownerFromCombo(objects[0], playerOrder);

            OsuHitObject current = objects[lastStarted];

            // During a gap, switch to the next player early so the compatibility cursor can
            // travel towards their first object. While an object is active (notably a slider),
            // keep its owner's continuous position and button stream.
            if (time > current.GetEndTime() && lastStarted + 1 < objects.Length)
                return ownerFromCombo(objects[lastStarted + 1], playerOrder);

            return ownerFromCombo(current, playerOrder);
        }

        private static int ownerFromCombo(OsuHitObject hitObject, IReadOnlyList<int> playerOrder)
            => playerOrder[Math.Abs(hitObject.ComboIndex % playerOrder.Count)];

        private void onNewResult(JudgementResult result)
        {
            if (result.HitObject is not OsuHitObject osuHitObject
                || ownerFor(osuHitObject) != configuration.LocalUserId
                || !objectIndices.TryGetValue(result.HitObject, out int index))
                return;

            configuration.SendJudgement(index, result.Type);
        }

        private void updateTurnState(double currentTime)
        {
            if (topLevelObjects.Length == 0 || configuration.PlayerOrder.Count == 0)
            {
                activeObject = null;
                activeUserId = null;
                nextUserId = null;
                millisecondsUntilNextTurn = double.PositiveInfinity;
                allPlayersActive = false;
                allPlayersNext = false;
                return;
            }

            if (currentTime < lastTurnStateTime)
                activeObjectIndex = 0;

            lastTurnStateTime = currentTime;

            while (activeObjectIndex < topLevelObjects.Length - 1 && topLevelObjects[activeObjectIndex].GetEndTime() < currentTime)
                activeObjectIndex++;

            activeObject = topLevelObjects[activeObjectIndex];
            bool hasStarted = currentTime >= activeObject.StartTime;
            bool continuesPreviousCombo = !hasStarted
                                           && activeObjectIndex > 0
                                           && activeObject is not Spinner
                                           && topLevelObjects[activeObjectIndex - 1] is not Spinner
                                           && topLevelObjects[activeObjectIndex - 1].ComboIndex == activeObject.ComboIndex;

            allPlayersActive = false;
            activeUserId = !hasStarted && !continuesPreviousCombo ? null : ownerFor(activeObject);
            nextUserId = null;
            millisecondsUntilNextTurn = double.PositiveInfinity;
            allPlayersNext = false;

            if (!hasStarted && !continuesPreviousCombo)
            {
                millisecondsUntilNextTurn = Math.Max(0, activeObject.StartTime - currentTime);

                nextUserId = ownerFor(activeObject);

                return;
            }

            int activeOwner = ownerFor(activeObject);

            for (int i = activeObjectIndex + 1; i < topLevelObjects.Length; i++)
            {
                OsuHitObject candidate = topLevelObjects[i];
                int candidateOwner = ownerFor(candidate);

                if (candidate is not Spinner && candidateOwner == activeOwner && activeObject is not Spinner)
                    continue;

                millisecondsUntilNextTurn = Math.Max(0, candidate.StartTime - currentTime);

                nextUserId = candidateOwner;

                break;
            }
        }

        private Vector2? getAutomatedCursorPosition()
        {
            if (!configuration.AutoplayLocalTurns || activeObject == null)
                return null;

            double currentTime = drawableRuleset.Playfield.Clock.CurrentTime;

            if (activeObject is Spinner spinner)
            {
                if (ownerFor(activeObject) != configuration.LocalUserId)
                    return null;

                double phase = (currentTime - spinner.StartTime) / 1000 * Math.PI * 4;
                return new Vector2(256 + (float)Math.Cos(phase) * 90, 192 + (float)Math.Sin(phase) * 90);
            }

            if (ownerFor(activeObject) != configuration.LocalUserId)
                return null;

            if (activeObject is Slider slider)
            {
                double progress = Math.Clamp((currentTime - slider.StartTime) / Math.Max(1, slider.Duration), 0, 1);
                return slider.StackedPositionAt(progress);
            }

            return activeObject.StackedPosition;
        }

        private Color4 colourFor(int userId)
            => configuration.PlayerColours.TryGetValue(userId, out Color4 colour) ? colour : Color4.White;

        private DrawableOsuHitObject? findDrawable(HitObject hitObject)
        {
            foreach (DrawableHitObject drawable in drawableRuleset.Playfield.AllHitObjects)
            {
                DrawableOsuHitObject? result = findDrawable(drawable, hitObject);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static DrawableOsuHitObject? findDrawable(DrawableHitObject drawable, HitObject hitObject)
        {
            if (ReferenceEquals(drawable.HitObject, hitObject))
                return drawable as DrawableOsuHitObject;

            foreach (DrawableHitObject nested in drawable.NestedHitObjects)
            {
                DrawableOsuHitObject? result = findDrawable(nested, hitObject);
                if (result != null)
                    return result;
            }

            return null;
        }

        private static IEnumerable<HitObject> flatten(IEnumerable<HitObject> objects)
        {
            foreach (HitObject hitObject in objects)
            {
                yield return hitObject;

                foreach (HitObject nested in flatten(hitObject.NestedHitObjects))
                    yield return nested;
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            drawableRuleset.NewResult -= onNewResult;
            base.Dispose(isDisposing);
        }
    }
}
