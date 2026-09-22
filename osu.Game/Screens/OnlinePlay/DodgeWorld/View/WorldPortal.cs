// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal partial class WorldPortal : WorldPassage
    {
        private readonly SpriteIcon spinner;
        private readonly OsuSpriteText nameText;
        public override float TriggerRadius => 62;
        public override string LayoutKind => "portal";

        public WorldPortal(OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select)
            : base(editing, select, colours.Orange1)
        {
            Size = new Vector2(220, 168);
            AddRangeInternal(new Drawable[]
            {
                new CircularContainer
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -34,
                    Size = new Vector2(94),
                    Masking = true,
                    BorderThickness = 5,
                    BorderColour = colours.Pink1,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1 },
                        spinner = new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Icon = FontAwesome.Solid.CircleNotch,
                            Size = new Vector2(44),
                            Colour = colours.Pink0,
                        },
                    },
                },
                nameText = new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -7,
                    Text = "DODGE PORTAL",
                    Font = OsuFont.Default.With(size: 15, weight: FontWeight.Bold),
                },
            });
        }

        public override void SetDisplayName(string value)
        {
            base.SetDisplayName(value);
            nameText.Text = value.ToUpperInvariant();
        }

        protected override void Update()
        {
            base.Update();
            spinner.Rotation = (float)(Time.Current / 22 % 360);
        }
    }
}
