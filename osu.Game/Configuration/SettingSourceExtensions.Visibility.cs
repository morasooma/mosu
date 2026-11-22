using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;

namespace osu.Game.Configuration
{
    public static partial class SettingSourceExtensions
    {
        private static Drawable applySettingVisibility(object source, string propertyName, Drawable control)
        {
            if (source is not IHasConditionalSettings conditional || conditional.GetSettingVisibility(propertyName) is not { } visibility)
                return control;

            return new ConditionalSettingContainer(control, visibility);
        }

        private partial class ConditionalSettingContainer : Container
        {
            private readonly IBindable<bool> visibility;

            public ConditionalSettingContainer(Drawable control, IBindable<bool> visibility)
            {
                RelativeSizeAxes = Axes.X;
                AutoSizeAxes = Axes.Y;
                Child = control;
                this.visibility = visibility.GetBoundCopy();
                this.visibility.BindValueChanged(value => Alpha = value.NewValue ? 1 : 0, true);
            }

            protected override void Dispose(bool isDisposing)
            {
                visibility.UnbindAll();
                base.Dispose(isDisposing);
            }
        }
    }
}
