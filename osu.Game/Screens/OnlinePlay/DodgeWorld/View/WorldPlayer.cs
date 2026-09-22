// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal partial class WorldPlayer : CompositeDrawable
    {
        private readonly Container body;
        private readonly Box fill;
        private readonly Box healthFill;
        private readonly OsuSpriteText username;
        private double movementAnimation;
        private bool isMoving;
        private int? lastHealth;

        public Color4 AccentColour { get; private set; }

        public WorldPlayer(OsuColour colours)
        {
            AccentColour = colours.Pink1;
            Anchor = Anchor.Centre;
            Origin = Anchor.BottomCentre;
            Size = new Vector2(120, 80);
            InternalChildren = new Drawable[]
            {
                new CircularContainer
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.Centre,
                    Y = -3,
                    Size = new Vector2(45, 13),
                    Scale = new Vector2(1, 0.6f),
                    Alpha = 0.4f,
                    Masking = true,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black },
                },
                body = new Container
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -10,
                    Size = new Vector2(32),
                    Masking = true,
                    CornerRadius = 5,
                    BorderThickness = 2,
                    BorderColour = colours.Pink0,
                    Child = fill = new Box
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = colours.Pink1,
                    },
                },
                username = new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.BottomCentre,
                    Y = 5,
                    Text = "PLAYER",
                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                    Shadow = true,
                },
                new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 24,
                    Size = new Vector2(52, 7),
                    Masking = true,
                    CornerRadius = 3,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray0 },
                        healthFill = new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Width = 1,
                            Colour = colours.Green1,
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Mirrors the simulated player: position, walk cycle and health bar.
        /// </summary>
        public void SyncFrom(PlayerState player) =>
            SyncFrom(player.Position, player.Facing, player.Moving, player.StepDistance, player.Health);

        /// <summary>
        /// Mirrors a player from values rather than from the simulation, which is how a player on
        /// another client is drawn: their position arrives over the network, not from local physics.
        /// </summary>
        public void SyncFrom(Vector2 position, Vector2 facing, bool moving, float stepDistance, int health)
        {
            Position = position;

            if (moving)
                stepTo(facing, stepDistance);
            else
                settle();

            if (lastHealth == health)
                return;

            bool damaged = lastHealth.HasValue && health < lastHealth.Value;
            lastHealth = health;

            healthFill.ResizeWidthTo((float)health / PlayerState.MAXIMUM_HEALTH, damaged ? 120 : 160, Easing.OutQuint);

            if (damaged)
                body.FlashColour(Color4.White, 180);
        }

        public void SetUser(APIUser user, OsuColour colours)
        {
            username.Text = string.IsNullOrWhiteSpace(user.Username) ? "PLAYER" : user.Username;

            Color4 accent = colours.Pink1;
            if (!string.IsNullOrWhiteSpace(user.Colour))
            {
                try
                {
                    accent = Color4Extensions.FromHex(user.Colour);
                }
                catch (ArgumentException)
                {
                }
            }

            fill.Colour = accent;
            body.BorderColour = accent.Lighten(0.45f);
            AccentColour = accent;
        }

        private void stepTo(Vector2 direction, float distance)
        {
            if (!isMoving)
            {
                isMoving = true;
                // Clears the settle transforms, which would otherwise keep overwriting body.Y below.
                body.ClearTransforms();
            }

            movementAnimation += distance * 0.1;
            body.Y = -10 + (float)Math.Sin(movementAnimation) * 1.6f;
            body.Rotation = direction.X * 3;
        }

        private void settle()
        {
            if (!isMoving)
                return;

            isMoving = false;
            movementAnimation = 0;
            body.MoveToY(-10, 180, Easing.OutQuint);
            body.RotateTo(0, 180, Easing.OutQuint);
        }
    }
}
