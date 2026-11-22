// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Graphics;

namespace osu.Game.Overlays
{
    public abstract partial class SettingsSubPanel : SettingsPanel
    {
        protected SettingsSubPanel()
            : base(true)
        {
        }

        [BackgroundDependencyLoader]
        private void load()
        {
        }

        protected override osu.Framework.Graphics.Drawable CreateSelectionControls() => new Settings.FavouriteSelectionControls();

        public override bool OnPressed(osu.Framework.Input.Events.KeyBindingPressEvent<osu.Game.Input.Bindings.GlobalAction> e)
        {
            var settings = this.FindClosestParent<SettingsOverlay>();
            if (!e.Repeat && settings != null)
            {
                if (e.Action == osu.Game.Input.Bindings.GlobalAction.ToggleFavouriteSettings)
                {
                    settings.ToggleFavouriteSettings();
                    return true;
                }
                if (e.Action == osu.Game.Input.Bindings.GlobalAction.Back && settings.SelectingFavourites)
                {
                    settings.FinishFavouriteSelection();
                    return true;
                }
            }
            return base.OnPressed(e);
        }

        protected override bool DimMainContent => false; // dimming is handled by main overlay
    }
}
