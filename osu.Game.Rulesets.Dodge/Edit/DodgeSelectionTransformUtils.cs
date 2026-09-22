// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics.Primitives;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Objects;
using osuTK;

namespace osu.Game.Rulesets.Dodge.Edit
{
    internal static class DodgeSelectionTransformUtils
    {
        public static HitObject[] Transformable(IEnumerable<HitObject> items, bool includeArena = true)
            => items.Where(item => item is DodgeBullet or DodgeEmitter or DodgeBeam or DodgeCameraChange or DodgeTrigger || includeArena && item is DodgeArenaChange).ToArray();

        public static (Vector2 Start, Vector2 End) GetPoints(HitObject hitObject) => hitObject switch
        {
            DodgeBullet bullet => (bullet.Position, bullet.EndPosition),
            DodgeEmitter emitter => (emitter.Position, emitter.AimPosition),
            DodgeArenaChange arena => (arena.TargetPosition, arena.TargetPosition + arena.TargetSize),
            DodgeBeam beam => (beam.Position, beam.EndPosition),
            DodgeCameraChange camera => (camera.Position, camera.EndPosition),
            DodgeTrigger trigger => (trigger.Position, trigger.Position),
            _ => throw new ArgumentException($"Unsupported transform object {hitObject.GetType().Name}.", nameof(hitObject)),
        };

        public static void SetPoints(HitObject hitObject, Vector2 start, Vector2 end)
        {
            switch (hitObject)
            {
                case DodgeBullet bullet:
                    bullet.Position = start;
                    bullet.EndPosition = end;
                    break;

                case DodgeBeam beam:
                    beam.Position = start;
                    beam.EndPosition = end;
                    break;

                case DodgeEmitter emitter:
                    Vector2 oldDirection = emitter.AimPosition - emitter.Position;
                    Vector2 newDirection = end - start;
                    Vector2 movementOffset = emitter.MovementEndPosition - emitter.Position;

                    if (emitter.MoveSource && emitter.EffectiveBurstCount > 1 && oldDirection.LengthSquared > 0)
                    {
                        float scale = newDirection.Length / oldDirection.Length;
                        float rotation = MathF.Atan2(newDirection.Y, newDirection.X) - MathF.Atan2(oldDirection.Y, oldDirection.X);
                        float sin = MathF.Sin(rotation);
                        float cos = MathF.Cos(rotation);
                        movementOffset = new Vector2(
                            movementOffset.X * cos - movementOffset.Y * sin,
                            movementOffset.X * sin + movementOffset.Y * cos) * scale;
                    }

                    emitter.Position = start;
                    emitter.AimPosition = end;

                    if (emitter.MoveSource && emitter.EffectiveBurstCount > 1)
                        emitter.MovementEndPosition = start + movementOffset;

                    break;

                case DodgeCameraChange camera:
                    camera.Position = start;
                    camera.EndPosition = end;
                    break;

                case DodgeArenaChange arena:
                    arena.TargetPosition = Vector2.ComponentMin(start, end);
                    arena.TargetSize = Vector2.ComponentMax(start, end) - arena.TargetPosition;
                    arena.ClampToBaseBounds();
                    break;

                case DodgeTrigger trigger:
                    trigger.Position = start;
                    break;
            }
        }

        public static Quad GetSurroundingQuad(IEnumerable<HitObject> items)
        {
            Vector2[] points = items.SelectMany(item =>
            {
                var pair = GetPoints(item);
                return item is DodgeEmitter emitter && emitter.MoveSource && emitter.EffectiveBurstCount > 1
                    ? new[] { pair.Start, pair.End, emitter.MovementEndPosition }
                    : new[] { pair.Start, pair.End };
            }).ToArray();

            if (points.Length == 0)
                return new Quad();

            float minX = points.Min(point => point.X);
            float minY = points.Min(point => point.Y);
            float maxX = points.Max(point => point.X);
            float maxY = points.Max(point => point.Y);
            return new Quad(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
