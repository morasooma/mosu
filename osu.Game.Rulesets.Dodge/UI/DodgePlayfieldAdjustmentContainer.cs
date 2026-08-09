// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Dodge.UI
{
    public partial class DodgePlayfieldAdjustmentContainer : PlayfieldAdjustmentContainer
    {
        public const float DEFAULT_SCALE = 0.8f;

        protected override Container<Drawable> Content => content;

        private readonly ScalingContainer content;

        public DodgePlayfieldAdjustmentContainer()
        {
            Anchor = Anchor.Centre;
            Origin = Anchor.Centre;
            Size = new Vector2(DEFAULT_SCALE);

            InternalChild = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                RelativeSizeAxes = Axes.Both,
                FillMode = FillMode.Fit,
                FillAspectRatio = DodgePlayfield.WIDTH / DodgePlayfield.HEIGHT,
                Child = content = new ScalingContainer { RelativeSizeAxes = Axes.Both },
            };
        }

        private partial class ScalingContainer : Container
        {
            protected override void Update()
            {
                base.Update();

                Scale = new Vector2(Parent!.ChildSize.X / DodgePlayfield.WIDTH);
                Size = Vector2.Divide(Vector2.One, Scale);
            }
        }
    }
}
