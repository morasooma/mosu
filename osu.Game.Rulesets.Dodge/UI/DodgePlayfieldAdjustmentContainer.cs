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

        protected virtual float ViewZoom => 1;
        protected virtual Vector2 ViewCentre => DodgePlayfield.BASE_SIZE / 2;

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
                Child = content = new ScalingContainer(this)
                {
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
            };
        }

        private partial class ScalingContainer : Container
        {
            private readonly DodgePlayfieldAdjustmentContainer owner;

            public ScalingContainer(DodgePlayfieldAdjustmentContainer owner) => this.owner = owner;

            protected override void Update()
            {
                base.Update();

                float baseScale = Parent!.ChildSize.X / DodgePlayfield.WIDTH;
                Scale = new Vector2(baseScale * owner.ViewZoom);
                Size = new Vector2(1 / baseScale);
                Position = (DodgePlayfield.BASE_SIZE / 2 - owner.ViewCentre) * baseScale * owner.ViewZoom;
            }
        }
    }
}
