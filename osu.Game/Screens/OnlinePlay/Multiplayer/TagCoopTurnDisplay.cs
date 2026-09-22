// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Rulesets.TagCoop;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.Multiplayer
{
    /// <summary>
    /// Keeps the current and upcoming Tag Co-op turn visible without requiring the player
    /// to infer ownership from rejected input.
    /// </summary>
    public partial class TagCoopTurnDisplay : CompositeDrawable
    {
        private const double warning_window = 2500;

        private readonly int localUserId;
        private readonly TagCoopGameplayController controller;
        private readonly IReadOnlyDictionary<int, string> usernames;
        private readonly IReadOnlyDictionary<int, Color4> playerColours;
        private readonly Func<int, int?> pingForUser;

        private readonly Box accent;
        private readonly OsuSpriteText title;
        private readonly OsuSpriteText subtitle;

        private LocalisableString lastTitle;
        private LocalisableString lastSubtitle;
        private Color4 lastColour;

        public TagCoopTurnDisplay(
            int localUserId,
            TagCoopGameplayController controller,
            IReadOnlyDictionary<int, string> usernames,
            IReadOnlyDictionary<int, Color4> playerColours,
            Func<int, int?> pingForUser)
        {
            this.localUserId = localUserId;
            this.controller = controller;
            this.usernames = usernames;
            this.playerColours = playerColours;
            this.pingForUser = pingForUser;

            Anchor = Anchor.BottomRight;
            Origin = Anchor.BottomRight;
            Size = new Vector2(300, 50);

            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 7,
                    BorderThickness = 1,
                    BorderColour = Color4.White.Opacity(0.1f),
                    Children = new Drawable[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black.Opacity(0.42f),
                        },
                        accent = new Box
                        {
                            RelativeSizeAxes = Axes.Y,
                            Width = 4,
                        },
                    }
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Left = 12, Right = 8, Top = 5, Bottom = 4 },
                    Children = new Drawable[]
                    {
                        title = new OsuSpriteText
                        {
                            Font = OsuFont.Torus.With(size: 15, weight: FontWeight.Bold),
                        },
                        subtitle = new OsuSpriteText
                        {
                            Font = OsuFont.Torus.With(size: 11, weight: FontWeight.SemiBold),
                            Colour = Color4.White.Opacity(0.68f),
                        },
                    }
                }
            };
        }

        protected override void Update()
        {
            base.Update();

            LocalisableString newTitle;
            LocalisableString newSubtitle;
            Color4 newColour;

            if (controller.AllPlayersActive)
            {
                newTitle = TagCoopStrings.AllPlayers;
                newSubtitle = withPing(TagCoopStrings.SpinNow, localUserId);
                newColour = new Color4(255, 202, 40, 255);
            }
            else if (controller.ActiveUserId == localUserId)
            {
                newTitle = TagCoopStrings.YourTurn;
                newSubtitle = withPing(TagCoopStrings.HitThisCombo, localUserId);
                newColour = colourFor(localUserId);
            }
            else if (controller.AllPlayersNext && controller.MillisecondsUntilNextTurn <= warning_window)
            {
                double seconds = Math.Max(0, controller.MillisecondsUntilNextTurn) / 1000;
                newTitle = TagCoopStrings.AllGetReady;
                newSubtitle = withPing(TagCoopStrings.SpinnerIn(seconds), localUserId);
                newColour = new Color4(255, 202, 40, 255);
            }
            else if (controller.NextUserId == localUserId && controller.MillisecondsUntilNextTurn <= warning_window)
            {
                double seconds = Math.Max(0, controller.MillisecondsUntilNextTurn) / 1000;
                newTitle = TagCoopStrings.GetReady;
                newSubtitle = withPing(TagCoopStrings.YourTurnIn(seconds), localUserId);
                newColour = new Color4(255, 202, 40, 255);
            }
            else if (controller.ActiveUserId is int activeUser)
            {
                newTitle = usernameFor(activeUser);
                newSubtitle = withPing(TagCoopStrings.PlayingNow, activeUser);
                newColour = colourFor(activeUser);
            }
            else
            {
                newTitle = TagCoopStrings.TagCoop;
                newSubtitle = withPing(TagCoopStrings.WaitingForTurn, localUserId);
                newColour = Color4.White;
            }

            if (!newTitle.Equals(lastTitle))
            {
                title.Text = lastTitle = newTitle;
                title.FadeOut().FadeIn(120, Easing.OutQuint);
            }

            if (!newSubtitle.Equals(lastSubtitle))
                subtitle.Text = lastSubtitle = newSubtitle;

            if (newColour != lastColour)
            {
                lastColour = newColour;
                accent.FadeColour(newColour, 120, Easing.OutQuint);
                title.FadeColour(newColour, 120, Easing.OutQuint);
            }
        }

        private string usernameFor(int userId)
            => usernames.TryGetValue(userId, out string? username) ? username : $"Player {userId}";

        private LocalisableString withPing(LocalisableString status, int userId)
            => pingForUser(userId) is int ping ? TagCoopStrings.WithPing(status, ping) : status;

        private Color4 colourFor(int userId)
            => playerColours.TryGetValue(userId, out Color4 colour) ? colour : Color4.White;
    }
}
