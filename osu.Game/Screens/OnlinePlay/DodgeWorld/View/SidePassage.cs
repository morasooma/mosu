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
    internal partial class SidePassage : WorldPassage
    {
        private readonly OsuSpriteText nameText;
        private readonly Container door;
        private readonly SpriteIcon chevron;

        public override float TriggerRadius => 76;
        public override string LayoutKind => "passage";
        public override bool CanRotate => true;

        private bool pointsRight;

        /// <summary>
        /// Which way the doorway faces, and therefore which side of it somebody arriving stands on.
        /// </summary>
        public bool PointsRight
        {
            get => pointsRight;
            set
            {
                pointsRight = value;
                chevron.Icon = value ? FontAwesome.Solid.ChevronRight : FontAwesome.Solid.ChevronLeft;
            }
        }

        /// <summary>
        /// The way the chevron points, turned by however the author turned the doorway.
        /// </summary>
        public override Vector2? ExitDirection
        {
            get
            {
                float radians = FacingDegrees * MathF.PI / 180;
                float sign = PointsRight ? 1 : -1;

                return new Vector2(sign * MathF.Cos(radians), sign * MathF.Sin(radians));
            }
        }

        public SidePassage(OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select, bool pointsRight)
            : base(editing, select, colours.Orange1)
        {
            this.pointsRight = pointsRight;
            Size = new Vector2(160, 130);
            AddRangeInternal(new Drawable[]
            {
                door = new Container
                {
                    // Centred with an offset rather than anchored to the bottom, so that turning it
                    // spins the doorway in place instead of swinging it out of the object's own box.
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Y = 19,
                    Size = new Vector2(140, 92),
                    Masking = true,
                    CornerRadius = 18,
                    BorderThickness = 3,
                    BorderColour = colours.Gray4,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray2 },
                        chevron = new SpriteIcon
                        {
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                            Icon = pointsRight ? FontAwesome.Solid.ChevronRight : FontAwesome.Solid.ChevronLeft,
                            Size = new Vector2(38),
                            Colour = colours.GrayD,
                        },
                    },
                },
                nameText = new OsuSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.TopCentre,
                    Y = 5,
                    Text = "PASSAGE",
                    Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                    Colour = colours.GrayA,
                },
            });
        }

        public override void SetDisplayName(string value)
        {
            base.SetDisplayName(value);
            nameText.Text = value.ToUpperInvariant();
        }

        /// <summary>
        /// Turns the doorway only. The caption stays upright, because a sideways label is not a label.
        /// </summary>
        protected override void ApplyFacing(float degrees) => door.Rotation = degrees;
    }
}
