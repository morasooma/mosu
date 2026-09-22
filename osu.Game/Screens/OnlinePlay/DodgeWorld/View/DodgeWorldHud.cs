// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Globalization;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Online.API.Requests;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Net;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Simulation;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// The status chips along the top of the screen.
    /// </summary>
    /// <remarks>
    /// Progression and player count are bound straight to the session, so the screen no longer has to
    /// remember to push them into the display after every request that might have changed them.
    /// </remarks>
    internal partial class DodgeWorldHud : CompositeDrawable
    {
        private readonly HudChip room;
        private readonly HudChip health;
        private readonly HudChip progression;
        private readonly HudChip currency;
        private readonly HudChip online;

        public DodgeWorldHud(OsuColour colours, DodgeWorldSession session)
        {
            RelativeSizeAxes = Axes.Both;

            InternalChildren = new Drawable[]
            {
                room = new HudChip(colours, FontAwesome.Solid.MapMarkerAlt, "LOADING WORLD…", colours.Pink1)
                {
                    Position = new Vector2(16, 12),
                    Width = 205,
                },
                health = new HudChip(colours, FontAwesome.Solid.Heart, "HP —", colours.Red1)
                {
                    Position = new Vector2(16, 56),
                    Width = 205,
                },
                progression = new HudChip(colours, FontAwesome.Solid.Star, "LV —  •  XP —", colours.Yellow)
                {
                    Position = new Vector2(16, 100),
                    Width = 205,
                },
                new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.TopRight,
                    Origin = Anchor.TopRight,
                    Position = new Vector2(-16, 12),
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Children = new Drawable[]
                    {
                        currency = new HudChip(colours, FontAwesome.Solid.Gem, "—", colours.Purple1) { Width = 112 },
                        online = new HudChip(colours, FontAwesome.Solid.Users, "ONLINE —", colours.Green1) { Width = 135 },
                    },
                },
            };

            session.Progression.BindValueChanged(value => setProgression(value.NewValue), true);
            session.OnlineUsers.BindValueChanged(value => online.SetText($"ONLINE {value.NewValue:#,0}"), true);
        }

        public void SetRoom(string name) => room.SetText(name.ToUpperInvariant());

        public void SetHealth(int current) => health.SetText($"HP {current} / {PlayerState.MAXIMUM_HEALTH}");

        /// <summary>
        /// Flashes the progression chip to acknowledge a reward attempt.
        /// </summary>
        public void FlashProgression(osuTK.Graphics.Color4 colour) => progression.FlashColour(colour, 450);

        private void setProgression(DodgeWorldProgressionResponse? value)
        {
            if (value == null || value.Level < 1 || value.ExperienceForNextLevel < 1)
            {
                progression.SetText("LV —  •  XP —");
                currency.SetText("—");
                return;
            }

            progression.SetText($"LV {value.Level:#,0}  •  XP {value.Experience:#,0}/{value.ExperienceForNextLevel:#,0}");
            currency.SetText(value.Coins.ToString("#,0", CultureInfo.InvariantCulture));
        }
    }
}
