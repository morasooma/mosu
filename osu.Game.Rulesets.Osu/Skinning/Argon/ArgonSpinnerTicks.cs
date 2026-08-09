// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Effects;
using osu.Framework.Graphics.Shapes;
using osu.Game.Performance;
using osu.Game.Skinning;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Osu.Skinning.Argon
{
    public partial class ArgonSpinnerTicks : CompositeDrawable
    {
        [BackgroundDependencyLoader]
        private void load()
        {
            Origin = Anchor.Centre;
            Anchor = Anchor.Centre;
            RelativeSizeAxes = Axes.Both;

            const float count = 25;

            // The soft white shadow around every tick is purely decorative — the tick bodies below carry
            // the spinner's rotation feedback and are untouched. Each shadow is a blurred 90x65 quad
            // (tick size inflated by the radius-30 blur), so the 25 of them rasterise ~146k spinner-local
            // px every frame of the spinner's lifetime.
            bool dropTickShadows = SkinPerformanceMode.ShouldSimplifyEffects && MosuOptimisationToggles.PerfSpinnerLite;

            for (float i = 0; i < count; i++)
            {
                var tick = new CircularContainer
                {
                    RelativePositionAxes = Axes.Both,
                    Masking = true,
                    CornerRadius = 5,
                    BorderColour = Color4.White,
                    BorderThickness = 2f,
                    Size = new Vector2(30, 5),
                    Origin = Anchor.Centre,
                    Position = new Vector2(
                        0.5f + MathF.Sin(i / count * 2 * MathF.PI) / 2 * 0.75f,
                        0.5f + MathF.Cos(i / count * 2 * MathF.PI) / 2 * 0.75f
                    ),
                    Rotation = -i / count * 360 - 120,
                    Children = new[]
                    {
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Alpha = 0,
                            AlwaysPresent = true,
                        }
                    }
                };

                if (!dropTickShadows)
                {
                    tick.EdgeEffect = new EdgeEffectParameters
                    {
                        Type = EdgeEffectType.Shadow,
                        Colour = Colour4.White.Opacity(0.2f),
                        Radius = 30,
                    };
                }

                AddInternal(tick);
            }
        }
    }
}
