// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal partial class MobAttackTelegraph : CompositeDrawable
    {
        public MobAttackTelegraph(OsuColour colours, IReadOnlyList<Vector2> directions, float range)
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Size = new Vector2(range * 2);
            Alpha = 0;

            foreach (Vector2 direction in directions)
            {
                AddInternal(new Box
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.CentreLeft,
                    Size = new Vector2(range, 2),
                    Rotation = MathF.Atan2(direction.Y, direction.X) * 180 / MathF.PI,
                    Colour = colours.Red1,
                });
            }

            AddInternal(new CircularContainer
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(18),
                Masking = true,
                BorderThickness = 3,
                BorderColour = colours.Red0,
                Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Red3.Opacity(0.55f) },
            });
        }

        public void Play()
        {
            this.FadeTo(0.28f, 100, Easing.OutQuint)
                .Then().FadeTo(0.7f, 170, Easing.InOutSine)
                .Then().FadeTo(0.34f, 170, Easing.InOutSine)
                .Then().FadeTo(0.9f, 170, Easing.InQuint);
        }
    }
}
