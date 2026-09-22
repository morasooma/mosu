// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// The list of warps the player has opened, and what travelling to each one costs.
    /// </summary>
    /// <remarks>
    /// Only opened warps are listed: a warp is discovered by walking to it, not by reading a menu.
    /// </remarks>
    internal partial class WarpMenu : CompositeDrawable
    {
        private readonly OsuColour colours;
        private readonly FillFlowContainer rows;
        private readonly OsuSpriteText balance;
        private readonly OsuTextFlowContainer empty;

        public bool IsOpen { get; private set; }

        /// <summary>
        /// Chosen destination. The screen decides whether the player can afford it.
        /// </summary>
        public Action<WarpPoint>? Travel { get; set; }

        public WarpMenu(OsuColour colours)
        {
            this.colours = colours;

            RelativeSizeAxes = Axes.Both;
            Alpha = 0;
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray0.Opacity(0.82f) },
                new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Width = 520,
                    AutoSizeAxes = Axes.Y,
                    Masking = true,
                    CornerRadius = 12,
                    BorderThickness = 2,
                    BorderColour = colours.Blue1,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1 },
                        new FillFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Direction = FillDirection.Vertical,
                            Padding = new MarginPadding(18),
                            Spacing = new Vector2(0, 10),
                            Children = new Drawable[]
                            {
                                new OsuSpriteText
                                {
                                    Text = "СЕТЬ ВАРПОВ",
                                    Font = OsuFont.Default.With(size: 22, weight: FontWeight.Bold),
                                    Colour = colours.Blue0,
                                },
                                balance = new OsuSpriteText
                                {
                                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                                    Colour = colours.YellowLight,
                                },
                                empty = WrappedText.Paragraph(
                                    "Открытых варпов пока нет. Найди их в мире и открой на месте.", colours.GrayA, 13),
                                rows = new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.X,
                                    AutoSizeAxes = Axes.Y,
                                    Direction = FillDirection.Vertical,
                                    Spacing = new Vector2(0, 6),
                                },
                                new OsuSpriteText
                                {
                                    Text = "E или Esc — закрыть",
                                    Font = OsuFont.Default.With(size: 11),
                                    Colour = colours.GrayA,
                                },
                            },
                        },
                    },
                },
            };
        }

        /// <summary>
        /// Shows the warps <paramref name="isOpened"/> accepts, leaving out the one being stood on.
        /// </summary>
        /// <param name="destinations">Every warp in the world.</param>
        /// <param name="isOpened">Whether the player has paid to open a given warp.</param>
        /// <param name="coins">The player's balance, or null when it is unknown.</param>
        /// <param name="standingOn">The warp the menu was opened from.</param>
        public void Open(IEnumerable<WarpPoint> destinations, Func<WarpPoint, bool> isOpened, long? coins, WarpPoint standingOn)
        {
            rows.Clear();

            WarpPoint[] available = destinations
                                   .Where(point => isOpened(point))
                                   .Where(point => point.RoomId != standingOn.RoomId || point.EntityId != standingOn.EntityId)
                                   .ToArray();

            balance.Text = coins == null
                ? "Баланс недоступен"
                : $"Монет: {coins.Value.ToString("N0", CultureInfo.CurrentCulture)}";

            empty.Alpha = available.Length == 0 ? 1 : 0;

            foreach (WarpPoint point in available)
            {
                bool affordable = coins == null || coins.Value >= point.TravelCost;

                rows.Add(new OsuAnimatedButton
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 40,
                    Enabled = { Value = affordable },
                    Action = () => Travel?.Invoke(point),
                    Child = new Container
                    {
                        RelativeSizeAxes = Axes.Both,
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = affordable ? colours.Gray3 : colours.Gray2,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                X = 14,
                                Text = $"{point.Name}  ·  {point.RoomName}",
                                Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                                Colour = affordable ? colours.Blue0 : colours.Gray7,
                            },
                            new OsuSpriteText
                            {
                                Anchor = Anchor.CentreRight,
                                Origin = Anchor.CentreRight,
                                X = -14,
                                Text = point.TravelCost > 0 ? $"{point.TravelCost} монет" : "бесплатно",
                                Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                                Colour = affordable ? colours.YellowLight : colours.Red1,
                            },
                        },
                    },
                });
            }

            IsOpen = true;
            this.FadeIn(140, Easing.OutQuint);
        }

        public void Close()
        {
            IsOpen = false;
            this.FadeOut(120, Easing.OutQuint);
        }

        /// <summary>
        /// The destination rows, for tests to assert on what was offered and to pick one.
        /// </summary>
        internal IReadOnlyList<OsuAnimatedButton> Destinations => rows.Children.OfType<OsuAnimatedButton>().ToArray();
    }
}
