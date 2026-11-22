using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.UI;
using osuTK;

namespace osu.Game.Rulesets.Catch.UI
{
    public partial class FruitNinjaPlayfieldAdjustmentContainer : PlayfieldAdjustmentContainer
    {
        private readonly Container content;
        protected override Container<Drawable> Content => content;

        public FruitNinjaPlayfieldAdjustmentContainer()
        {
            InternalChild = content = new Container
            {
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Size = new Vector2(CatchPlayfield.WIDTH, CatchPlayfield.HEIGHT),
            };
        }

        protected override void Update()
        {
            base.Update();
            content.Scale = new Vector2(Math.Min(DrawWidth / CatchPlayfield.WIDTH, DrawHeight / 440) * 0.9f);
        }
    }
}
