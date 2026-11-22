using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Input.Events;
using osu.Framework.Localisation;
using osu.Game.Input.Bindings;
using osu.Game.Overlays.Settings;

namespace osu.Game.Overlays
{
    public partial class SettingsOverlay
    {
        internal FavouriteSettingsStore? Favourites { get; private set; }
        internal readonly List<FavouriteSettingEntry> FavouriteEntries = new List<FavouriteSettingEntry>();
        private readonly HashSet<Drawable> registeredFavouriteRows = new HashSet<Drawable>();
        private FavouriteSettingsPanel favouritePanel = null!;
        private bool pendingFavourites;
        private bool selectionDirty;
        public bool SelectingFavourites { get; private set; }
        internal event Action? FavouriteSelectionChanged;

        protected override Drawable CreateSelectionControls() => new FavouriteSelectionControls();

        public void ToggleFavouriteSettings()
        {
            if (OverlayActivationMode.Value == OverlayActivation.Disabled)
                return;
            if (State.Value == Visibility.Visible && (pendingFavourites || favouritePanel.State.Value == Visibility.Visible))
            {
                pendingFavourites = false;
                Hide();
                return;
            }
            ShowFavouriteSettings();
        }

        public void ShowFavouriteSettings()
        {
            if (OverlayActivationMode.Value == OverlayActivation.Disabled)
                return;
            SelectingFavourites = false;
            FavouriteSelectionChanged?.Invoke();
            closeSubPanels();
            pendingFavourites = true;
            Show();
            EnsureSectionsLoaded();
            foreach (var panel in subPanels.Where(p => p != favouritePanel))
                panel.EnsureSectionsLoaded(this);
        }

        public void BeginFavouriteSelection()
        {
            pendingFavourites = false;
            favouritePanel.Hide();
            SelectingFavourites = true;
            FavouriteSelectionChanged?.Invoke();
            installSelectionTargets();
            SearchTextBox.Current.Value = string.Empty;
        }

        public void FinishFavouriteSelection()
        {
            if (SelectingFavourites)
                ShowFavouriteSettings();
        }

        private void closeSubPanels()
        {
            foreach (var panel in subPanels)
                panel.Hide();
        }

        private void registerFavouriteRows(SettingsPanel panel)
        {
            foreach (var section in panel.AvailableSections)
                visit(section, new List<LocalisableString>());
            selectionDirty = true;

            void visit(Drawable drawable, List<LocalisableString> path)
            {
                if (drawable is SettingsSection section)
                    path = path.Append(section.Header).ToList();
                else if (drawable is SettingsSubsection subsection)
                    path = path.Append(subsection.SidebarHeader).ToList();

                if (drawable is IFavouriteSettingsItem item && item.FavouriteId != null && registeredFavouriteRows.Add(drawable)
                    && drawable.Parent is Container<Drawable> parent)
                {
                    var entry = new FavouriteSettingEntry(drawable, item, parent,
                        LocalisableString.Format(string.Join(" → ", path.Select((_, i) => "{" + i + "}")), path.Cast<object>().ToArray()));
                    FavouriteEntries.Add(entry);
                    return;
                }
                if (drawable is Container<Drawable> container)
                {
                    foreach (var child in container.Children)
                        visit(child, path);
                }
            }
        }

        private void installSelectionTargets()
        {
            foreach (var entry in FavouriteEntries)
            {
                entry.Item.FavouriteSelectionOverlay.Clear();
                entry.Item.FavouriteSelectionOverlay.Add(new FavouriteSelectionTarget(
                    () => Favourites!.Toggle(entry.Item.FavouriteId!),
                    () => Favourites?.Contains(entry.Item.FavouriteId!) == true,
                    () => SelectingFavourites,
                    () => SelectingFavourites));
            }
        }

        protected override void Update()
        {
            base.Update();
            if (selectionDirty)
            {
                selectionDirty = false;
                if (SelectingFavourites)
                    installSelectionTargets();
            }
            if (pendingFavourites && SectionsReady && subPanels.Where(p => p != favouritePanel).All(p => p.SectionsReady))
            {
                // Rows are registered after load completion on the update thread; no background tree mutations.
                registerFavouriteRows(this);
                foreach (var panel in subPanels.Where(p => p != favouritePanel))
                    registerFavouriteRows(panel);
                pendingFavourites = false;
                favouritePanel.Show();
            }
        }

        public override bool OnPressed(KeyBindingPressEvent<GlobalAction> e)
        {
            if (!e.Repeat)
            {
                if (e.Action == GlobalAction.ToggleFavouriteSettings)
                {
                    ToggleFavouriteSettings();
                    return true;
                }
                if (e.Action == GlobalAction.Back && SelectingFavourites)
                {
                    FinishFavouriteSelection();
                    return true;
                }
            }
            return base.OnPressed(e);
        }

        protected override void PopOut()
        {
            pendingFavourites = false;
            SelectingFavourites = false;
            FavouriteSelectionChanged?.Invoke();
            closeSubPanels();
            base.PopOut();
        }

        protected override void Dispose(bool isDisposing)
        {
            // Disposal may run on AsyncDisposalQueue, where scene graph mutations are forbidden.
            // Borrowed rows are already children of this overlay and are disposed recursively.
            Favourites?.Dispose();
            foreach (var entry in FavouriteEntries)
                entry.DisposePlaceholder();
            base.Dispose(isDisposing);
        }
    }
}
