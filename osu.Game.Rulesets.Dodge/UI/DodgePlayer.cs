// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Rulesets.Dodge.Replays;
using osu.Game.Rulesets.Dodge.Skinning;
using osu.Game.Rulesets.Dodge.Skinning.Components;
using osu.Game.Rulesets.UI;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.UI
{
    public partial class DodgePlayer : CompositeDrawable, IKeyBindingHandler<DodgeAction>
    {
        public const float SIZE = 16;
        public const float BASE_SPEED = 0.24f;
        public const float SLOW_MULTIPLIER = 0.4f;

        [Resolved(canBeNull: true)]
        private DodgePlayfield? playfield { get; set; }

        private bool leftPressed;
        private bool rightPressed;
        private bool upPressed;
        private bool downPressed;

        public bool SlowActive { get; private set; }

        public int MissFlashCount { get; private set; }

        public DodgePlayerTrail Trail { get; }

        public DodgeSkinEffect GrazeEffect { get; }

        public DodgeSkinEffect CollisionEffect { get; }

        public float MovementSpeed { get; set; } = BASE_SPEED;

        public float PlayerSize
        {
            get => Size.X;
            set
            {
                Size = new Vector2(Math.Max(0, value));
                Trail.SegmentSize = Size.X;
            }
        }

        public Vector2 ArenaPosition { get; set; }

        public Vector2 ArenaSize { get; set; } = DodgePlayfield.BASE_SIZE;

        public float ArenaRotation { get; set; }

        private Vector2 previousCameraOffset;
        private bool cameraOffsetInitialised;

        public DodgePlayer()
        {
            Size = new Vector2(SIZE);
            Origin = Anchor.Centre;
            Position = DodgePlayfield.BASE_SIZE / 2;
            Colour = Color4.White;

            InternalChildren = new Drawable[]
            {
                Trail = new DodgePlayerTrail(SIZE),
                new SkinnableDrawable(
                    new DodgeSkinComponentLookup(DodgeSkinComponents.Player),
                    _ => new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = Color4.White,
                    },
                    ConfineMode.ScaleToFit)
                {
                    RelativeSizeAxes = Axes.Both,
                },
                GrazeEffect = new DodgeSkinEffect(DodgeSkinComponents.GrazeEffect),
                CollisionEffect = new DodgeSkinEffect(DodgeSkinComponents.CollisionEffect),
            };
        }

        public void TriggerMissFlash()
        {
            MissFlashCount++;

            FinishTransforms(false, nameof(Colour));
            this.FlashColour(new Color4(1f, 0.25f, 0.35f, 1f), 180, Easing.OutQuint);
            CollisionEffect.Play();
        }

        public void TriggerGrazeEffect() => GrazeEffect.Play();

        protected override void Update()
        {
            base.Update();

            // Camera scroll drift: the player is carried by the world scroll exactly
            // like live bullets, so that the coordinate spaces stay consistent for
            // collision detection and the gameplay feels like a true bullet-hell scroller.
            Vector2 currentCameraOffset = playfield?.CameraOffsetAt(Time.Current) ?? Vector2.Zero;
            Vector2 cameraDelta = cameraOffsetInitialised
                ? currentCameraOffset - previousCameraOffset
                : Vector2.Zero;
            previousCameraOffset = currentCameraOffset;
            cameraOffsetInitialised = true;

            var replayState = (GetContainingInputManager()?.CurrentState as RulesetInputManagerInputState<DodgeAction>)?.LastReplayState
                              as DodgeFramedReplayInputHandler.DodgeReplayState;

            if (replayState?.PlayerPosition is Vector2 replayPosition)
            {
                Position = ClampToArena(replayPosition, ArenaPosition, ArenaSize, PlayerSize, ArenaRotation);
                Trail.UpdatePosition(Position, Time.Current);
                return;
            }

            var direction = new Vector2(
                (rightPressed ? 1 : 0) - (leftPressed ? 1 : 0),
                (downPressed ? 1 : 0) - (upPressed ? 1 : 0));

            // Apply camera drift first so input acts on the already-scrolled position.
            Position = CalculatePosition(Position + cameraDelta, direction, SlowActive, Clock.ElapsedFrameTime, ArenaPosition, ArenaSize, MovementSpeed, PlayerSize, ArenaRotation);
            Trail.UpdatePosition(Position, Time.Current);
        }

        public static Vector2 CalculatePosition(Vector2 currentPosition, Vector2 direction, bool slow, double elapsedMilliseconds)
            => CalculatePosition(currentPosition, direction, slow, elapsedMilliseconds, Vector2.Zero, DodgePlayfield.BASE_SIZE);

        public static Vector2 CalculatePosition(Vector2 currentPosition, Vector2 direction, bool slow, double elapsedMilliseconds, Vector2 arenaPosition, Vector2 arenaSize)
            => CalculatePosition(currentPosition, direction, slow, elapsedMilliseconds, arenaPosition, arenaSize, BASE_SPEED);

        public static Vector2 CalculatePosition(
            Vector2 currentPosition,
            Vector2 direction,
            bool slow,
            double elapsedMilliseconds,
            Vector2 arenaPosition,
            Vector2 arenaSize,
            float movementSpeed,
            float playerSize = SIZE,
            float arenaRotation = 0)
        {
            if (direction.LengthSquared > 1)
                direction.Normalize();

            float distance = movementSpeed * (slow ? SLOW_MULTIPLIER : 1) * (float)Math.Max(0, elapsedMilliseconds);
            Vector2 next = currentPosition + direction * distance;
            return ClampToArena(next, arenaPosition, arenaSize, playerSize, arenaRotation);
        }

        public static Vector2 ClampToArena(Vector2 position, Vector2 arenaPosition, Vector2 arenaSize, float playerSize = SIZE, float arenaRotation = 0)
        {
            float halfSize = Math.Max(0, playerSize) / 2;

            if (Math.Abs(arenaRotation) < 0.001f)
            {
                position.X = Math.Clamp(position.X, arenaPosition.X + halfSize, arenaPosition.X + arenaSize.X - halfSize);
                position.Y = Math.Clamp(position.Y, arenaPosition.Y + halfSize, arenaPosition.Y + arenaSize.Y - halfSize);
                return position;
            }

            Vector2 center = arenaPosition + arenaSize / 2;
            float rad = MathHelper.DegreesToRadians(-arenaRotation);
            float cos = MathF.Cos(rad);
            float sin = MathF.Sin(rad);

            Vector2 rel = position - center;
            Vector2 local = new Vector2(
                rel.X * cos - rel.Y * sin,
                rel.X * sin + rel.Y * cos);

            float halfExtentX = Math.Max(0, arenaSize.X / 2 - halfSize);
            float halfExtentY = Math.Max(0, arenaSize.Y / 2 - halfSize);

            local.X = Math.Clamp(local.X, -halfExtentX, halfExtentX);
            local.Y = Math.Clamp(local.Y, -halfExtentY, halfExtentY);

            float backRad = MathHelper.DegreesToRadians(arenaRotation);
            float backCos = MathF.Cos(backRad);
            float backSin = MathF.Sin(backRad);

            return center + new Vector2(
                local.X * backCos - local.Y * backSin,
                local.X * backSin + local.Y * backCos);
        }

        public bool OnPressed(KeyBindingPressEvent<DodgeAction> e)
        {
            setPressed(e.Action, true);
            return true;
        }

        public void OnReleased(KeyBindingReleaseEvent<DodgeAction> e) => setPressed(e.Action, false);

        private void setPressed(DodgeAction action, bool pressed)
        {
            switch (action)
            {
                case DodgeAction.MoveLeft:
                    leftPressed = pressed;
                    break;

                case DodgeAction.MoveRight:
                    rightPressed = pressed;
                    break;

                case DodgeAction.MoveUp:
                    upPressed = pressed;
                    break;

                case DodgeAction.MoveDown:
                    downPressed = pressed;
                    break;

                case DodgeAction.Slow:
                    SlowActive = pressed;
                    break;
            }
        }
    }
}
