using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Bindings;
using osu.Framework.Input.Events;
using osu.Game.Input.Bindings;
using osu.Game.Localisation;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Overlays.Settings
{
    internal partial class FavouriteSelectionControls : FillFlowContainer, IKeyBindingHandler<GlobalAction>
    {
        [Resolved(CanBeNull = true)]
        private SettingsOverlay? settings { get; set; }

        public FavouriteSelectionControls()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Alpha = 0;
            AlwaysPresent = false;
            Children = new Drawable[]
            {
                new OsuSpriteText
                {
                    Text = FavouriteSettingsStrings.SelectionTitle,
                    Font = OsuFont.Default.With(size: 16),
                    Margin = new MarginPadding { Vertical = 6 },
                },
                new DoneButton
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 36,
                    Text = FavouriteSettingsStrings.Done,
                    Action = () => settings?.FinishFavouriteSelection(),
                },
            };
        }

        private partial class DoneButton : OsuButton { }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (settings != null)
                settings.FavouriteSelectionChanged += refresh;
            refresh();
        }

        private void refresh() => Alpha = settings?.SelectingFavourites == true ? 1 : 0;

        public bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (settings?.SelectingFavourites == true && e.Action == GlobalAction.Back && !e.Repeat)
            {
                settings.FinishFavouriteSelection();
                return true;
            }
            return false;
        }

        public void OnReleased(KeyBindingReleaseEvent<GlobalAction> e) { }

        protected override void Dispose(bool isDisposing)
        {
            if (settings != null)
                settings.FavouriteSelectionChanged -= refresh;
            base.Dispose(isDisposing);
        }
    }
}
