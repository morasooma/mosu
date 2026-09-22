// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    /// <summary>
    /// A travel point. Unlike a passage, walking onto a warp does nothing: it is opened and used with
    /// <c>E</c>, because both cost coins and neither should happen by accident.
    /// </summary>
    internal partial class WorldWarp : EditableWorldEntity
    {
        private readonly OsuColour colours;
        private readonly Container pad;
        private readonly SpriteIcon glyph;
        private readonly OsuSpriteText nameText;
        private readonly OsuSpriteText priceText;
        private readonly Container prompt;

        public override bool BlocksMovement => false;
        public override string LayoutKind => "warp";

        /// <summary>Coins charged once to open this warp.</summary>
        public int UnlockCost { get; set; }

        /// <summary>Coins charged each time a player travels here.</summary>
        public int TravelCost { get; set; }

        /// <summary>
        /// Whether the local player has opened this warp. Server state, not part of the world document.
        /// </summary>
        public bool Unlocked { get; private set; }

        public WorldWarp(OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select)
            : base(editing, select, colours.Orange1)
        {
            this.colours = colours;
            Size = new Vector2(150, 130);
            AddRangeInternal(new Drawable[]
            {
                pad = new Container
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -18,
                    Size = new Vector2(104, 52),
                    Masking = true,
                    CornerRadius = 26,
                    BorderThickness = 3,
                    BorderColour = colours.Blue1,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Blue4 },
                        glyph = new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Icon = FontAwesome.Solid.Ankh,
                            Size = new Vector2(26),
                            Colour = colours.Blue0,
                        },
                    },
                },
                nameText = new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -2,
                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                },
                priceText = new OsuSpriteText
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 4,
                    Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                    Colour = colours.YellowLight,
                },
                prompt = new Container
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.BottomCentre,
                    Y = 0,
                    Size = new Vector2(104, 28),
                    Alpha = 0,
                    Masking = true,
                    CornerRadius = 8,
                    BorderThickness = 2,
                    BorderColour = colours.Blue1,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1.Opacity(0.96f) },
                        new OsuSpriteText
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Text = "E   ВАРП",
                            Font = OsuFont.Default.With(size: 12, weight: FontWeight.Bold),
                        },
                    },
                },
            });

            updateAppearance();
        }

        public override void SetDisplayName(string value)
        {
            base.SetDisplayName(value);
            nameText.Text = value.ToUpperInvariant();
        }

        /// <summary>
        /// Applies whether the local player has opened this warp, which changes both what it says and
        /// what pressing <c>E</c> will do.
        /// </summary>
        public void SetUnlocked(bool value)
        {
            Unlocked = value;
            updateAppearance();
        }

        public void SetPrompt(bool available) => prompt.FadeTo(available ? 1 : 0, 140, Easing.OutQuint);

        public void Pulse()
        {
            pad.FlashColour(Color4.White, 260, Easing.OutQuint);
            pad.ScaleTo(1.12f, 70).Then().ScaleTo(1, 180, Easing.OutQuint);
        }

        protected override void Update()
        {
            base.Update();

            // A slow breath, so an open warp reads as active without demanding attention.
            float pulse = (float)Math.Sin(Time.Current / 900) * 0.5f + 0.5f;
            glyph.Alpha = Unlocked ? 0.55f + pulse * 0.45f : 0.4f;
        }

        private void updateAppearance()
        {
            pad.BorderColour = Unlocked ? colours.Blue1 : colours.Gray5;
            glyph.Colour = Unlocked ? colours.Blue0 : colours.Gray7;

            priceText.Text = Unlocked
                ? TravelCost > 0 ? $"ПЕРЕХОД {TravelCost}" : "ПЕРЕХОД БЕСПЛАТНО"
                : UnlockCost > 0 ? $"ОТКРЫТЬ {UnlockCost}" : "ОТКРЫТЬ БЕСПЛАТНО";

            priceText.Colour = Unlocked ? colours.Blue0 : colours.YellowLight;
        }

        /// <summary>
        /// Refreshes the caption after the editor changed a price.
        /// </summary>
        public void PricesChanged() => updateAppearance();
    }
}
