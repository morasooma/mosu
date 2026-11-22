using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play.HUD
{
    /// <summary>
    /// Gameplay keys with a scrolling history of their press durations.
    /// Available in the skin editor, without changing existing skin layouts.
    /// </summary>
    public partial class ScrollingKeyCounterDisplay : KeyCounterDisplay, IMorasoomaSkinComponent
    {
        protected override FillFlowContainer<KeyCounter> KeyFlow { get; }
        protected override Axes DisplayAutoSizeAxes => Axes.X;

        [SettingSource(typeof(MorasoomaSkinEditorStrings), nameof(MorasoomaSkinEditorStrings.KeyScrollSpeed))]
        public BindableFloat ScrollSpeed { get; } = new BindableFloat(90) { MinValue = 10, MaxValue = 1000, Precision = 1 };

        public ScrollingKeyCounterDisplay()
        {
            AutoSizeAxes = Axes.X;
            Height = 225;
            Content.AutoSizeAxes = Axes.X;
            Content.RelativeSizeAxes = Axes.Y;
            Child = KeyFlow = new FillFlowContainer<KeyCounter>
            {
                Direction = FillDirection.Horizontal,
                AutoSizeAxes = Axes.X,
                RelativeSizeAxes = Axes.Y,
                Spacing = new Vector2(2),
            };
        }

        protected override KeyCounter CreateCounter(InputTrigger trigger)
        {
            var counter = new ScrollingKeyCounter(trigger) { RelativeSizeAxes = Axes.Y, Height = 1 };
            counter.ScrollSpeed.BindTo(ScrollSpeed);
            return counter;
        }
    }
}
