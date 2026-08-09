// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Utils;
using osu.Game.Storyboards;
using osu.Game.Storyboards.Commands;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit.Design
{
    /// <summary>
    /// Pure storyboard editing operations shared by the design screen and its timeline.
    /// Storyboard commands are immutable, so timing edits replace a sprite with a safe clone.
    /// </summary>
    internal static class DodgeStoryboardEditing
    {
        public const double DEFAULT_VISIBLE_DURATION = 2000;
        public const double MINIMUM_VISIBLE_DURATION = 50;
        public const double DEFAULT_TIMELINE_SPAN = 10000;

        public static void InitialiseVisibleRange(StoryboardSprite sprite, double startTime, double duration)
        {
            startTime = Math.Max(0, startTime);
            duration = Math.Max(MINIMUM_VISIBLE_DURATION, duration);
            double endTime = startTime + duration;

            sprite.Commands.AddAlpha(Easing.None, startTime, endTime, 1, 1);
        }

        public static bool CanRetime(StoryboardSprite sprite)
            => sprite.LoopingGroups.Count == 0 && sprite.TriggerGroups.Count == 0;

        public static StoryboardSprite Clone(StoryboardSprite source, Vector2 offset)
        {
            StoryboardSprite clone = createEmptyClone(source, source.InitialPosition + offset);

            foreach (IStoryboardCommand command in source.Commands.AllCommands)
                CopyCommand(command, clone.Commands);

            copyAuxiliaryGroups(source, clone);
            return clone;
        }

        public static StoryboardVisualState StateAt(StoryboardSprite sprite, double time)
        {
            float uniformScale = valueAt(sprite.Commands.Scale, 1, time);
            Vector2 vectorScale = valueAt(sprite.Commands.VectorScale, Vector2.One, time);

            return new StoryboardVisualState(
                PositionAt(sprite, time),
                uniformScale,
                vectorScale,
                vectorScale * uniformScale,
                valueAt(sprite.Commands.Rotation, 0, time),
                valueAt(sprite.Commands.Alpha, 1, time));
        }

        public static CornerScaleResult CalculateCornerScale(
            Vector2 startEffectiveScale,
            Vector2 startPosition,
            Vector2 pivot,
            Vector2 fixedCorner,
            Vector2 startPointer,
            Vector2 currentPointer,
            float rotation,
            bool lockAspect)
        {
            Vector2 localStartPointer = rotate(startPointer - fixedCorner, -rotation);
            Vector2 localCurrentPointer = rotate(currentPointer - fixedCorner, -rotation);

            float ratioX = Math.Abs(localStartPointer.X) < 0.001f
                ? 1
                : localCurrentPointer.X / localStartPointer.X;
            float ratioY = Math.Abs(localStartPointer.Y) < 0.001f
                ? 1
                : localCurrentPointer.Y / localStartPointer.Y;
            ratioX = Math.Clamp(ratioX, 0.01f, 1000);
            ratioY = Math.Clamp(ratioY, 0.01f, 1000);

            if (lockAspect)
            {
                float ratio = Math.Abs(ratioX - 1) >= Math.Abs(ratioY - 1)
                    ? ratioX
                    : ratioY;
                ratioX = ratioY = ratio;
            }

            Vector2 effectiveScale = new Vector2(
                Math.Clamp(startEffectiveScale.X * ratioX, 0.001f, 1000),
                Math.Clamp(startEffectiveScale.Y * ratioY, 0.001f, 1000));

            Vector2 fixedOffset = rotate(fixedCorner - pivot, -rotation);
            Vector2 resizedFixedOffset = new Vector2(fixedOffset.X * ratioX, fixedOffset.Y * ratioY);
            Vector2 resizedPivot = fixedCorner - rotate(resizedFixedOffset, rotation);
            Vector2 position = startPosition + resizedPivot - pivot;

            return new CornerScaleResult(effectiveScale, position);
        }

        public static StoryboardSprite SetPositionAt(StoryboardSprite source, double time, Vector2 position, Easing easing)
        {
            ensureVisualEditingSupported(source);
            StoryboardSprite edited = source;

            if (edited.Commands.X.Count == 0)
                edited = cloneWithPosition(edited, new Vector2(position.X, edited.InitialPosition.Y));
            else
                edited = replaceFloatValueAt(edited, edited.Commands.X, time, position.X, easing, static (commands, key) =>
                    commands.AddX(key.Easing, key.StartTime, key.EndTime, key.StartValue, key.EndValue));

            if (edited.Commands.Y.Count == 0)
                edited = cloneWithPosition(edited, new Vector2(edited.InitialPosition.X, position.Y));
            else
                edited = replaceFloatValueAt(edited, edited.Commands.Y, time, position.Y, easing, static (commands, key) =>
                    commands.AddY(key.Easing, key.StartTime, key.EndTime, key.StartValue, key.EndValue));

            return edited;
        }

        public static StoryboardSprite SetEffectiveScaleAt(StoryboardSprite source, double time, Vector2 effectiveScale, Easing easing)
        {
            ensureVisualEditingSupported(source);
            float uniformScale = valueAt(source.Commands.Scale, 1, time);
            if (Math.Abs(uniformScale) < 0.0001f)
                uniformScale = 1;

            Vector2 vectorScale = new Vector2(
                Math.Clamp(effectiveScale.X / uniformScale, 0.001f, 1000),
                Math.Clamp(effectiveScale.Y / uniformScale, 0.001f, 1000));

            return replaceVectorValueAt(
                source,
                source.Commands.VectorScale,
                time,
                vectorScale,
                easing,
                static (commands, key) => commands.AddVectorScale(key.Easing, key.StartTime, key.EndTime, key.StartValue, key.EndValue));
        }

        public static StoryboardSprite SetRotationAt(StoryboardSprite source, double time, float rotation, Easing easing)
        {
            ensureVisualEditingSupported(source);
            return replaceFloatValueAt(source, source.Commands.Rotation, time, rotation, easing, static (commands, key) =>
                commands.AddRotation(key.Easing, key.StartTime, key.EndTime, key.StartValue, key.EndValue));
        }

        public static StoryboardSprite SetOpacityAt(StoryboardSprite source, double time, float opacity, Easing easing)
        {
            ensureVisualEditingSupported(source);
            return replaceFloatValueAt(source, source.Commands.Alpha, time, Math.Clamp(opacity, 0, 1), easing, static (commands, key) =>
                commands.AddAlpha(key.Easing, key.StartTime, key.EndTime, key.StartValue, key.EndValue), replaceSingleConstant: true);
        }

        /// <summary>
        /// Moves the complete object, including an existing animated path, without inserting a keyframe.
        /// </summary>
        public static StoryboardSprite TranslateWholeObject(StoryboardSprite source, Vector2 delta)
        {
            ensureVisualEditingSupported(source);
            StoryboardSprite clone = createEmptyClone(source, source.InitialPosition + delta);

            foreach (IStoryboardCommand command in source.Commands.AllCommands)
            {
                switch (command)
                {
                    case StoryboardXCommand x:
                        clone.Commands.AddX(x.Easing, x.StartTime, x.EndTime, x.StartValue + delta.X, x.EndValue + delta.X);
                        break;

                    case StoryboardYCommand y:
                        clone.Commands.AddY(y.Easing, y.StartTime, y.EndTime, y.StartValue + delta.Y, y.EndValue + delta.Y);
                        break;

                    default:
                        CopyCommand(command, clone.Commands);
                        break;
                }
            }

            return clone;
        }

        /// <summary>
        /// Scales every existing vector-scale command by the ratio required to reach
        /// <paramref name="effectiveScale"/> at <paramref name="referenceTime"/>.
        /// </summary>
        public static StoryboardSprite ScaleWholeObjectAt(StoryboardSprite source, double referenceTime, Vector2 effectiveScale)
        {
            ensureVisualEditingSupported(source);
            Vector2 current = StateAt(source, referenceTime).EffectiveScale;
            Vector2 ratio = new Vector2(
                Math.Clamp(effectiveScale.X / Math.Max(0.001f, Math.Abs(current.X)), 0.001f, 1000),
                Math.Clamp(effectiveScale.Y / Math.Max(0.001f, Math.Abs(current.Y)), 0.001f, 1000));
            StoryboardSprite clone = createEmptyClone(source, source.InitialPosition);
            bool replacedVectorScale = false;

            foreach (IStoryboardCommand command in source.Commands.AllCommands)
            {
                if (command is StoryboardVectorScaleCommand scale)
                {
                    replacedVectorScale = true;
                    clone.Commands.AddVectorScale(
                        scale.Easing,
                        scale.StartTime,
                        scale.EndTime,
                        scale.StartValue * ratio,
                        scale.EndValue * ratio);
                }
                else
                {
                    CopyCommand(command, clone.Commands);
                }
            }

            if (!replacedVectorScale)
            {
                (double start, double end) = safeVisibleRange(source, referenceTime);
                clone.Commands.AddVectorScale(Easing.None, start, end, ratio, ratio);
            }

            return clone;
        }

        /// <summary>
        /// Rotates the complete object animation by a constant offset.
        /// </summary>
        public static StoryboardSprite RotateWholeObjectAt(StoryboardSprite source, double referenceTime, float rotation)
        {
            ensureVisualEditingSupported(source);
            float delta = rotation - StateAt(source, referenceTime).Rotation;
            StoryboardSprite clone = createEmptyClone(source, source.InitialPosition);
            bool replacedRotation = false;

            foreach (IStoryboardCommand command in source.Commands.AllCommands)
            {
                if (command is StoryboardRotationCommand rotate)
                {
                    replacedRotation = true;
                    clone.Commands.AddRotation(
                        rotate.Easing,
                        rotate.StartTime,
                        rotate.EndTime,
                        rotate.StartValue + delta,
                        rotate.EndValue + delta);
                }
                else
                {
                    CopyCommand(command, clone.Commands);
                }
            }

            if (!replacedRotation)
            {
                (double start, double end) = safeVisibleRange(source, referenceTime);
                clone.Commands.AddRotation(Easing.None, start, end, rotation, rotation);
            }

            return clone;
        }

        /// <summary>
        /// Applies one opacity multiplier to every alpha command without inserting a playhead key.
        /// </summary>
        public static StoryboardSprite SetWholeObjectOpacityAt(StoryboardSprite source, double referenceTime, float opacity)
        {
            ensureVisualEditingSupported(source);
            opacity = Math.Clamp(opacity, 0, 1);
            float current = StateAt(source, referenceTime).Opacity;
            float ratio = current > 0.001f ? opacity / current : 0;
            StoryboardSprite clone = createEmptyClone(source, source.InitialPosition);
            bool replacedAlpha = false;

            foreach (IStoryboardCommand command in source.Commands.AllCommands)
            {
                if (command is StoryboardAlphaCommand alpha)
                {
                    replacedAlpha = true;
                    float start = current > 0.001f ? Math.Clamp(alpha.StartValue * ratio, 0, 1) : opacity;
                    float end = current > 0.001f ? Math.Clamp(alpha.EndValue * ratio, 0, 1) : opacity;
                    clone.Commands.AddAlpha(alpha.Easing, alpha.StartTime, alpha.EndTime, start, end);
                }
                else
                {
                    CopyCommand(command, clone.Commands);
                }
            }

            if (!replacedAlpha)
            {
                (double start, double end) = safeVisibleRange(source, referenceTime);
                clone.Commands.AddAlpha(Easing.None, start, end, opacity, opacity);
            }

            return clone;
        }

        public static StoryboardSprite RepairVisibleRange(StoryboardSprite source)
        {
            ensureVisualEditingSupported(source);

            double startTime = double.IsFinite(source.StartTime)
                ? Math.Max(0, source.StartTime)
                : 0;
            float opacity = StateAt(source, startTime).Opacity;
            StoryboardSprite clone = createEmptyClone(source, source.InitialPosition);

            foreach (IStoryboardCommand command in source.Commands.AllCommands)
            {
                if (command is not StoryboardAlphaCommand)
                    CopyCommand(command, clone.Commands);
            }

            clone.Commands.AddAlpha(
                Easing.None,
                startTime,
                startTime + DEFAULT_VISIBLE_DURATION,
                opacity,
                opacity);
            return clone;
        }

        public static void AddPositionKey(StoryboardSprite sprite, double time, Vector2 value, Easing easing)
        {
            sprite.Commands.AddX(easing, time, time, value.X, value.X);
            sprite.Commands.AddY(easing, time, time, value.Y, value.Y);
        }

        public static void AddScaleKey(StoryboardSprite sprite, double time, Vector2 value, Easing easing)
            => sprite.Commands.AddVectorScale(easing, time, time, value, value);

        public static void AddRotationKey(StoryboardSprite sprite, double time, float value, Easing easing)
            => sprite.Commands.AddRotation(easing, time, time, value, value);

        public static void AddOpacityKey(StoryboardSprite sprite, double time, float value, Easing easing)
            => sprite.Commands.AddAlpha(easing, time, time, value, value);

        private static void copyAuxiliaryGroups(StoryboardSprite source, StoryboardSprite clone)
        {
            foreach (StoryboardLoopingGroup sourceLoop in source.LoopingGroups)
            {
                IStoryboardLoopingCommand? firstCommand = sourceLoop.AllCommands.OfType<IStoryboardLoopingCommand>().FirstOrDefault();
                double loopStart = firstCommand == null
                    ? sourceLoop.StartTime
                    : firstCommand.StartTime - firstCommand.OriginalCommand.StartTime;
                StoryboardLoopingGroup targetLoop = clone.AddLoopingGroup(loopStart, sourceLoop.TotalIterations - 1);

                foreach (IStoryboardLoopingCommand command in sourceLoop.AllCommands.OfType<IStoryboardLoopingCommand>())
                    CopyCommand(command.OriginalCommand, targetLoop);
            }

            foreach (StoryboardTriggerGroup sourceTrigger in source.TriggerGroups)
            {
                StoryboardTriggerGroup targetTrigger = clone.AddTriggerGroup(
                    sourceTrigger.TriggerName,
                    sourceTrigger.TriggerStartTime,
                    sourceTrigger.TriggerEndTime,
                    sourceTrigger.GroupNumber);

                foreach (IStoryboardCommand command in sourceTrigger.AllCommands)
                    CopyCommand(command, targetTrigger);
            }
        }

        public static StoryboardSprite Retime(StoryboardSprite source, double newStartTime, double newEndTime)
        {
            if (!CanRetime(source))
                throw new InvalidOperationException("Storyboard sprites containing loops or triggers cannot be retimed safely.");

            newStartTime = Math.Max(0, newStartTime);
            newEndTime = Math.Max(newStartTime + MINIMUM_VISIBLE_DURATION, newEndTime);

            double oldStartTime = source.StartTime;
            double oldEndTime = source.EndTimeForDisplay;
            double oldDuration = oldEndTime - oldStartTime;
            double newDuration = newEndTime - newStartTime;

            StoryboardSprite clone = createEmptyClone(source, source.InitialPosition);

            foreach (IStoryboardCommand command in source.Commands.AllCommands)
            {
                double mappedStart = mapTime(command.StartTime);
                double mappedEnd = mapTime(command.EndTime);
                CopyCommand(command, clone.Commands, mappedStart, mappedEnd);
            }

            return clone;

            double mapTime(double time)
            {
                if (oldDuration <= 0 || !double.IsFinite(oldDuration))
                    return newStartTime;

                return newStartTime + (time - oldStartTime) / oldDuration * newDuration;
            }
        }

        public static StoryboardSprite RetimeCommand(
            StoryboardSprite source,
            IStoryboardCommand target,
            double newStartTime,
            double newEndTime)
        {
            if (!CanRetime(source))
                throw new InvalidOperationException("Storyboard sprites containing loops or triggers cannot be retimed safely.");

            if (!source.Commands.AllCommands.Any(command => ReferenceEquals(command, target)))
                throw new ArgumentException("The command does not belong to the supplied storyboard sprite.", nameof(target));

            newStartTime = Math.Max(0, newStartTime);
            newEndTime = Math.Max(newStartTime, newEndTime);
            StoryboardSprite clone = createEmptyClone(source, source.InitialPosition);

            foreach (IStoryboardCommand command in source.Commands.AllCommands)
            {
                if (ReferenceEquals(command, target))
                    CopyCommand(command, clone.Commands, newStartTime, newEndTime);
                else
                    CopyCommand(command, clone.Commands);
            }

            return clone;
        }

        public static Vector2 PositionAt(StoryboardSprite sprite, double time)
            => new Vector2(
                valueAt(sprite.Commands.X, sprite.InitialPosition.X, time),
                valueAt(sprite.Commands.Y, sprite.InitialPosition.Y, time));

        public static (double Start, double End) CalculateTimelineView(StoryboardSprite? sprite, double playhead)
        {
            playhead = Math.Max(0, playhead);

            if (sprite == null || !double.IsFinite(sprite.StartTime) || !double.IsFinite(sprite.EndTimeForDisplay))
                return (Math.Max(0, playhead - DEFAULT_TIMELINE_SPAN / 2), Math.Max(DEFAULT_TIMELINE_SPAN, playhead + DEFAULT_TIMELINE_SPAN / 2));

            double contentStart = Math.Min(playhead, sprite.StartTime);
            double contentEnd = Math.Max(playhead, sprite.EndTimeForDisplay);
            double span = Math.Max(DEFAULT_TIMELINE_SPAN, (contentEnd - contentStart) * 1.35);
            double start = Math.Max(0, (contentStart + contentEnd - span) / 2);
            double end = start + span;

            if (end < contentEnd + span * 0.05)
            {
                end = contentEnd + span * 0.05;
                start = Math.Max(0, end - span);
            }

            return (start, end);
        }

        public static string FormatTime(double time)
        {
            string sign = time < 0 ? "-" : string.Empty;
            TimeSpan value = TimeSpan.FromMilliseconds(Math.Abs(time));
            return string.Create(
                CultureInfo.InvariantCulture,
                $"{sign}{(int)value.TotalMinutes:00}:{value.Seconds:00}.{value.Milliseconds:000}");
        }

        public static string FormatDuration(double duration)
            => string.Create(CultureInfo.InvariantCulture, $"{Math.Max(0, duration) / 1000:0.000} s");

        public static void CopyCommand(IStoryboardCommand command, StoryboardCommandGroup target)
            => CopyCommand(command, target, command.StartTime, command.EndTime);

        public static void CopyCommand(IStoryboardCommand command, StoryboardCommandGroup target, double startTime, double endTime)
        {
            switch (command)
            {
                case StoryboardAlphaCommand c:
                    target.AddAlpha(c.Easing, startTime, endTime, c.StartValue, c.EndValue);
                    break;

                case StoryboardXCommand c:
                    target.AddX(c.Easing, startTime, endTime, c.StartValue, c.EndValue);
                    break;

                case StoryboardYCommand c:
                    target.AddY(c.Easing, startTime, endTime, c.StartValue, c.EndValue);
                    break;

                case StoryboardScaleCommand c:
                    target.AddScale(c.Easing, startTime, endTime, c.StartValue, c.EndValue);
                    break;

                case StoryboardVectorScaleCommand c:
                    target.AddVectorScale(c.Easing, startTime, endTime, c.StartValue, c.EndValue);
                    break;

                case StoryboardRotationCommand c:
                    target.AddRotation(c.Easing, startTime, endTime, c.StartValue, c.EndValue);
                    break;

                case StoryboardColourCommand c:
                    target.AddColour(c.Easing, startTime, endTime, c.StartValue, c.EndValue);
                    break;

                case StoryboardBlendingParametersCommand c:
                    target.AddBlendingParameters(c.Easing, startTime, endTime, c.StartValue, c.EndValue);
                    break;

                case StoryboardFlipHCommand c:
                    target.AddFlipH(c.Easing, startTime, endTime, c.StartValue, c.EndValue);
                    break;

                case StoryboardFlipVCommand c:
                    target.AddFlipV(c.Easing, startTime, endTime, c.StartValue, c.EndValue);
                    break;
            }
        }

        private static StoryboardSprite cloneWithPosition(StoryboardSprite source, Vector2 position)
        {
            StoryboardSprite clone = createEmptyClone(source, position);
            foreach (IStoryboardCommand command in source.Commands.AllCommands)
                CopyCommand(command, clone.Commands);

            copyAuxiliaryGroups(source, clone);
            return clone;
        }

        private static StoryboardSprite replaceFloatValueAt(
            StoryboardSprite source,
            IReadOnlyList<StoryboardCommand<float>> commands,
            double time,
            float value,
            Easing easing,
            Action<StoryboardCommandGroup, EditedKey<float>> add,
            bool replaceSingleConstant = false)
        {
            IReadOnlyList<EditedKey<float>> editedKeys = editKeys(commands, Math.Max(0, time), value, easing, replaceSingleConstant);
            var replacedCommands = commands.Cast<IStoryboardCommand>().ToHashSet();
            StoryboardSprite clone = createEmptyClone(source, source.InitialPosition);

            foreach (IStoryboardCommand command in source.Commands.AllCommands)
            {
                if (!replacedCommands.Contains(command))
                    CopyCommand(command, clone.Commands);
            }

            foreach (EditedKey<float> key in editedKeys)
                add(clone.Commands, key);

            copyAuxiliaryGroups(source, clone);
            return clone;
        }

        private static StoryboardSprite replaceVectorValueAt(
            StoryboardSprite source,
            IReadOnlyList<StoryboardCommand<Vector2>> commands,
            double time,
            Vector2 value,
            Easing easing,
            Action<StoryboardCommandGroup, EditedKey<Vector2>> add)
        {
            IReadOnlyList<EditedKey<Vector2>> editedKeys = editKeys(commands, Math.Max(0, time), value, easing, false);
            var replacedCommands = commands.Cast<IStoryboardCommand>().ToHashSet();
            StoryboardSprite clone = createEmptyClone(source, source.InitialPosition);

            foreach (IStoryboardCommand command in source.Commands.AllCommands)
            {
                if (!replacedCommands.Contains(command))
                    CopyCommand(command, clone.Commands);
            }

            foreach (EditedKey<Vector2> key in editedKeys)
                add(clone.Commands, key);

            copyAuxiliaryGroups(source, clone);
            return clone;
        }

        private static IReadOnlyList<EditedKey<T>> editKeys<T>(
            IReadOnlyList<StoryboardCommand<T>> commands,
            double time,
            T value,
            Easing easing,
            bool replaceSingleConstant)
        {
            if (commands.Count == 0)
                return new[] { new EditedKey<T>(easing, time, time, value, value) };

            if (replaceSingleConstant &&
                commands.Count == 1 &&
                commands[0].Duration > 0 &&
                EqualityComparer<T>.Default.Equals(commands[0].StartValue, commands[0].EndValue))
            {
                StoryboardCommand<T> command = commands[0];
                return new[] { new EditedKey<T>(command.Easing, command.StartTime, command.EndTime, value, value) };
            }

            const double precision = 0.001;
            StoryboardCommand<T>? exactPoint = commands.LastOrDefault(command =>
                command.Duration <= precision && Math.Abs(command.StartTime - time) <= precision);

            if (exactPoint != null)
            {
                return commands.Select(command => ReferenceEquals(command, exactPoint)
                    ? new EditedKey<T>(command.Easing, command.StartTime, command.EndTime, value, value)
                    : keyFrom(command)).ToArray();
            }

            StoryboardCommand<T>? active = commands.LastOrDefault(command =>
                command.Duration > precision &&
                time >= command.StartTime - precision &&
                time <= command.EndTime + precision);

            if (active != null)
            {
                var result = new List<EditedKey<T>>();

                foreach (StoryboardCommand<T> command in commands)
                {
                    if (!ReferenceEquals(command, active))
                    {
                        result.Add(keyFrom(command));
                        continue;
                    }

                    if (Math.Abs(time - command.StartTime) <= precision)
                    {
                        result.Add(new EditedKey<T>(command.Easing, command.StartTime, command.EndTime, value, command.EndValue));
                    }
                    else if (Math.Abs(time - command.EndTime) <= precision)
                    {
                        result.Add(new EditedKey<T>(command.Easing, command.StartTime, command.EndTime, command.StartValue, value));
                    }
                    else
                    {
                        result.Add(new EditedKey<T>(command.Easing, command.StartTime, time, command.StartValue, value));
                        result.Add(new EditedKey<T>(command.Easing, time, command.EndTime, value, command.EndValue));
                    }
                }

                return result;
            }

            StoryboardCommand<T> first = commands[0];
            StoryboardCommand<T> last = commands[^1];

            if (time < first.StartTime)
            {
                return new[] { new EditedKey<T>(easing, time, first.StartTime, value, first.StartValue) }
                       .Concat(commands.Select(keyFrom))
                       .ToArray();
            }

            if (time > last.EndTime)
            {
                return commands.Select(keyFrom)
                               .Append(new EditedKey<T>(easing, last.EndTime, time, last.EndValue, value))
                               .ToArray();
            }

            return commands.Select(keyFrom)
                           .Append(new EditedKey<T>(easing, time, time, value, value))
                           .OrderBy(key => key.StartTime)
                           .ThenBy(key => key.EndTime)
                           .ToArray();

            static EditedKey<T> keyFrom(StoryboardCommand<T> command)
                => new EditedKey<T>(command.Easing, command.StartTime, command.EndTime, command.StartValue, command.EndValue);
        }

        private static void ensureVisualEditingSupported(StoryboardSprite source)
        {
            if (!CanRetime(source))
                throw new InvalidOperationException("Storyboard sprites containing loops or triggers cannot be edited safely.");
        }

        private static (double Start, double End) safeVisibleRange(StoryboardSprite source, double referenceTime)
        {
            double start = double.IsFinite(source.StartTime) ? Math.Max(0, source.StartTime) : Math.Max(0, referenceTime);
            double end = double.IsFinite(source.EndTimeForDisplay)
                ? Math.Max(start + MINIMUM_VISIBLE_DURATION, source.EndTimeForDisplay)
                : start + DEFAULT_VISIBLE_DURATION;
            return (start, end);
        }

        private static StoryboardSprite createEmptyClone(StoryboardSprite source, Vector2 position)
            => source is StoryboardAnimation animation
                ? new StoryboardAnimation(source.Source, source.Path, source.Origin, position, animation.FrameCount, animation.FrameDelay, animation.LoopType)
                : new StoryboardSprite(source.Source, source.Path, source.Origin, position);

        private static float valueAt(System.Collections.Generic.IReadOnlyList<StoryboardCommand<float>> commands, float fallback, double time)
        {
            if (commands.Count == 0)
                return fallback;

            float value = commands[0].StartValue;

            foreach (StoryboardCommand<float> command in commands)
            {
                if (time < command.StartTime)
                    break;

                if (time >= command.EndTime || command.Duration <= 0)
                {
                    value = command.EndValue;
                    continue;
                }

                return Interpolation.ValueAt(time, command.StartValue, command.EndValue, command.StartTime, command.EndTime, command.Easing);
            }

            return value;
        }

        private static Vector2 valueAt(IReadOnlyList<StoryboardCommand<Vector2>> commands, Vector2 fallback, double time)
        {
            if (commands.Count == 0)
                return fallback;

            Vector2 value = commands[0].StartValue;

            foreach (StoryboardCommand<Vector2> command in commands)
            {
                if (time < command.StartTime)
                    break;

                if (time >= command.EndTime || command.Duration <= 0)
                {
                    value = command.EndValue;
                    continue;
                }

                return Interpolation.ValueAt(time, command.StartValue, command.EndValue, command.StartTime, command.EndTime, command.Easing);
            }

            return value;
        }

        private static Vector2 rotate(Vector2 value, float degrees)
        {
            float radians = MathHelper.DegreesToRadians(degrees);
            float sin = MathF.Sin(radians);
            float cos = MathF.Cos(radians);
            return new Vector2(value.X * cos - value.Y * sin, value.X * sin + value.Y * cos);
        }

        public readonly record struct StoryboardVisualState(
            Vector2 Position,
            float UniformScale,
            Vector2 VectorScale,
            Vector2 EffectiveScale,
            float Rotation,
            float Opacity);

        public readonly record struct CornerScaleResult(Vector2 EffectiveScale, Vector2 Position);

        private readonly record struct EditedKey<T>(
            Easing Easing,
            double StartTime,
            double EndTime,
            T StartValue,
            T EndValue);
    }
}
