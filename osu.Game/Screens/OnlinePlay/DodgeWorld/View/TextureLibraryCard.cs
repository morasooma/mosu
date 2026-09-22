// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal partial class TextureLibraryCard : ClickableContainer
    {
        private readonly OsuColour colours;
        private readonly Sprite preview;
        private bool selected;

        public string AssetId { get; }

        public bool Selected
        {
            get => selected;
            set
            {
                selected = value;
                BorderThickness = value ? 3 : 1;
                BorderColour = value ? colours.Pink1 : colours.Gray4;
            }
        }

        public TextureLibraryCard(string assetId, string name, OsuColour colours, Action action)
        {
            AssetId = assetId;
            this.colours = colours;
            Action = action;
            Size = new Vector2(108, 102);
            Masking = true;
            CornerRadius = 9;
            BorderThickness = 1;
            BorderColour = colours.Gray4;
            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray2 },
                new SpriteIcon
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.Centre,
                    Y = 34,
                    Icon = FontAwesome.Solid.Image,
                    Size = new Vector2(22),
                    Colour = colours.Gray6,
                },
                preview = new Sprite
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Y = 5,
                    Size = new Vector2(98, 62),
                    FillMode = FillMode.Fit,
                },
                new Box
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 30,
                    Anchor = Anchor.BottomLeft,
                    Origin = Anchor.BottomLeft,
                    Colour = colours.Gray0.Opacity(0.88f),
                },
                new TruncatingSpriteText
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -8,
                    Width = 96,
                    Text = name,
                    Font = OsuFont.Default.With(size: 10, weight: FontWeight.Bold),
                },
            };
        }

        public void SetTexture(Texture? texture) => preview.Texture = texture;

        protected override bool OnHover(HoverEvent e)
        {
            this.FadeTo(1, 100);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            this.FadeTo(0.82f, 160);
            base.OnHoverLost(e);
        }
    }
}
