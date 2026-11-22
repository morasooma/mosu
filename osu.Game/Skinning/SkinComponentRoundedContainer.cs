using System;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;

namespace osu.Game.Skinning
{
    /// <summary>A capsule by default, with editable corners inherited from its skin component.</summary>
    public partial class SkinComponentRoundedContainer : Container
    {
        public SkinComponentRoundedContainer()
        {
            Masking = true;
            Child = new Box { RelativeSizeAxes = Axes.Both };
        }

        protected override void UpdateAfterChildren()
        {
            base.UpdateAfterChildren();
            CornerRadius = SkinComponentGeometry.GetInheritedOverride(this) ?? Math.Min(DrawWidth, DrawHeight) / 2;
        }
    }
}
