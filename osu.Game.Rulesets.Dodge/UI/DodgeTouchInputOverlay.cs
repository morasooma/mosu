// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge.UI
{
    /// <summary>
    /// Mobile controls for Dodge gameplay. The left stick emits the four movement actions while
    /// the right button holds <see cref="DodgeAction.Slow"/>.
    /// </summary>
    public partial class DodgeTouchInputOverlay : CompositeDrawable
    {
        private const float joystick_size = 176;
        private const float joystick_knob_size = 68;
        private const float slow_button_size = 116;
        private const float deadzone = 0.18f;

        public override bool PropagatePositionalInputSubTree => true;
        public override bool PropagateNonPositionalInputSubTree => true;

        private readonly HashSet<DodgeAction> pressedMovementActions = new HashSet<DodgeAction>();

        private DodgeInputManager inputManager = null!;
        private Container joystick = null!;
        private Circle joystickBackground = null!;
        private Circle joystickKnob = null!;
        private Container slowButton = null!;
        private Circle slowButtonBackground = null!;

        private TouchSource? joystickSource;
        private TouchSource? slowSource;

        public DodgeTouchInputOverlay()
        {
            RelativeSizeAxes = Axes.Both;
        }

        [BackgroundDependencyLoader]
        private void load(DodgeInputManager inputManager)
        {
            this.inputManager = inputManager;

            InternalChildren = new Drawable[]
            {
                joystick = new Container
                {
                    Name = "Movement joystick",
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Position = new Vector2(48, -48),
                    Size = new Vector2(joystick_size),
                    Children = new Drawable[]
                    {
                        joystickBackground = new Circle
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(0.46f, 0.46f, 0.46f, 0.24f),
                        },
                        new Circle
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(joystick_size - 12),
                            Colour = new Color4(0.72f, 0.72f, 0.72f, 0.08f),
                        },
                        joystickKnob = new Circle
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(joystick_knob_size),
                            Colour = new Color4(0.72f, 0.72f, 0.72f, 0.48f),
                        },
                    },
                },
                slowButton = new Container
                {
                    Name = "Slow movement button",
                    Anchor = Anchor.BottomRight,
                    Origin = Anchor.BottomRight,
                    Position = new Vector2(-60, -70),
                    Size = new Vector2(slow_button_size),
                    Children = new Drawable[]
                    {
                        slowButtonBackground = new Circle
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = new Color4(0.46f, 0.46f, 0.46f, 0.28f),
                        },
                        new Circle
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Size = new Vector2(slow_button_size - 12),
                            Colour = new Color4(0.72f, 0.72f, 0.72f, 0.08f),
                        },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = "SLOW",
                            Font = OsuFont.Torus.With(size: 20, weight: FontWeight.SemiBold),
                            Colour = new Color4(0.9f, 0.9f, 0.9f, 0.72f),
                        },
                    },
                },
            };
        }

        protected override bool OnTouchDown(TouchDownEvent e)
        {
            Vector2 position = e.ScreenSpaceTouchDownPosition;

            if (joystickSource == null && containsCircle(joystick, position))
            {
                joystickSource = e.Touch.Source;
                joystickKnob.ClearTransforms();
                joystickBackground.FadeColour(new Color4(0.56f, 0.56f, 0.56f, 0.34f), 80, Easing.OutQuint);
                updateJoystick(position);
                return true;
            }

            if (slowSource == null && containsCircle(slowButton, position))
            {
                slowSource = e.Touch.Source;
                inputManager.KeyBindingContainer.TriggerPressed(DodgeAction.Slow);
                slowButton.ScaleTo(0.92f, 80, Easing.OutQuint);
                slowButtonBackground.FadeColour(new Color4(0.62f, 0.62f, 0.62f, 0.42f), 80, Easing.OutQuint);
                return true;
            }

            return false;
        }

        protected override void OnTouchMove(TouchMoveEvent e)
        {
            if (e.Touch.Source == joystickSource)
                updateJoystick(e.ScreenSpaceTouch.Position);

            base.OnTouchMove(e);
        }

        protected override void OnTouchUp(TouchUpEvent e)
        {
            if (e.Touch.Source == joystickSource)
                releaseJoystick();

            if (e.Touch.Source == slowSource)
                releaseSlow();

            base.OnTouchUp(e);
        }

        private void updateJoystick(Vector2 screenSpacePosition)
        {
            Vector2 offset = joystick.ToLocalSpace(screenSpacePosition) - joystick.DrawSize / 2;
            float inputRadius = joystick.DrawWidth / 2;
            Vector2 direction = inputRadius > 0 ? offset / inputRadius : Vector2.Zero;

            updateMovementActions(
                direction.X < -deadzone,
                direction.X > deadzone,
                direction.Y < -deadzone,
                direction.Y > deadzone);

            float knobRadius = Math.Max(0, (joystick.DrawWidth - joystickKnob.DrawWidth) / 2);
            if (offset.LengthSquared > knobRadius * knobRadius && offset.LengthSquared > 0)
                offset = offset.Normalized() * knobRadius;

            joystickKnob.Position = offset;
        }

        private void updateMovementActions(bool left, bool right, bool up, bool down)
        {
            updateMovementAction(DodgeAction.MoveLeft, left);
            updateMovementAction(DodgeAction.MoveRight, right);
            updateMovementAction(DodgeAction.MoveUp, up);
            updateMovementAction(DodgeAction.MoveDown, down);
        }

        private void updateMovementAction(DodgeAction action, bool shouldBePressed)
        {
            bool isPressed = pressedMovementActions.Contains(action);

            if (isPressed == shouldBePressed)
                return;

            if (shouldBePressed)
            {
                pressedMovementActions.Add(action);
                inputManager.KeyBindingContainer.TriggerPressed(action);
            }
            else
            {
                pressedMovementActions.Remove(action);
                inputManager.KeyBindingContainer.TriggerReleased(action);
            }
        }

        private void releaseJoystick()
        {
            joystickSource = null;
            updateMovementActions(false, false, false, false);
            joystickKnob.MoveTo(Vector2.Zero, 120, Easing.OutQuint);
            joystickBackground.FadeColour(new Color4(0.46f, 0.46f, 0.46f, 0.24f), 160, Easing.OutQuint);
        }

        private void releaseSlow()
        {
            slowSource = null;
            inputManager.KeyBindingContainer.TriggerReleased(DodgeAction.Slow);
            slowButton.ScaleTo(1, 120, Easing.OutQuint);
            slowButtonBackground.FadeColour(new Color4(0.46f, 0.46f, 0.46f, 0.28f), 160, Easing.OutQuint);
        }

        private static bool containsCircle(Drawable drawable, Vector2 screenSpacePosition)
        {
            Vector2 localPosition = drawable.ToLocalSpace(screenSpacePosition);
            Vector2 centre = drawable.DrawSize / 2;
            float radius = Math.Min(drawable.DrawWidth, drawable.DrawHeight) / 2;
            return (localPosition - centre).LengthSquared <= radius * radius;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (isDisposing && inputManager != null)
            {
                updateMovementActions(false, false, false, false);

                if (slowSource != null)
                    inputManager.KeyBindingContainer.TriggerReleased(DodgeAction.Slow);
            }

            base.Dispose(isDisposing);
        }
    }
}
