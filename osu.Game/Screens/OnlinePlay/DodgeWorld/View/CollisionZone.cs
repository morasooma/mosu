// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal partial class CollisionZone : EditableWorldEntity
    {
        public override Vector2 CollisionSize => Size;
        public override Vector2 CollisionCentreOffset => Vector2.Zero;
        public override float? FixedDepth => -9000;
        public override string LayoutKind => "collision";
        public override bool CanResize => true;
        public override bool CanScale => false;

        public CollisionZone(OsuColour colours, Func<bool> editing, Action<EditableWorldEntity> select, Vector2 size)
            : base(editing, select, colours.Red1)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Size = size;
            Alpha = 0;
            AddRangeInternal(new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colours.Red3.Opacity(0.42f),
                },
                new OsuSpriteText
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Text = "COLLISION",
                    Font = OsuFont.Default.With(size: 14, weight: FontWeight.Bold),
                    Colour = colours.Red0,
                },
            });
        }

        public override void SetEditing(bool value)
        {
            Alpha = value ? 0.82f : 0;
            base.SetEditing(value);
        }
    }
}
