using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Input;
using osu.Game.Input.Bindings;

namespace osu.Game.Overlays.Settings
{
    public partial class FavouriteSettingsPanel : SettingsSubPanel
    {
        private readonly SettingsOverlay owner;
        private readonly FavouriteSection section;
        private readonly List<FavouriteSettingEntry> borrowed = new List<FavouriteSettingEntry>();
        private SettingsButtonV2? removeButton;
        private bool removing;
        private OsuSpriteText shortcutHint = null!;

        [Resolved]
        private RealmKeyBindingStore keyBindings { get; set; } = null!;

        public FavouriteSettingsPanel(SettingsOverlay owner)
        {
            this.owner = owner;
            AddSection(section = new FavouriteSection());
            SectionsLoaded += () =>
            {
                if (State.Value == Visibility.Visible)
                    Populate();
            };
        }

        protected override Drawable CreateHeader() => new Container
        {
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Padding = new MarginPadding { Top = 16, Bottom = 16, Left = SettingsPanel.CONTENT_PADDING.Left, Right = SettingsPanel.CONTENT_PADDING.Right },
            Child = new OsuSpriteText
            {
                Text = FavouriteSettingsStrings.Title,
                Font = OsuFont.TorusAlternate.With(size: 28),
            },
        };

        protected override Drawable CreateSelectionControls() => new FillFlowContainer
        {
            Direction = FillDirection.Vertical,
            RelativeSizeAxes = Axes.X,
            AutoSizeAxes = Axes.Y,
            Margin = new MarginPadding { Top = 10, Bottom = 6 },
            Spacing = new osuTK.Vector2(0, 8),
            Children = new Drawable[]
            {
                shortcutHint = new OsuSpriteText
                {
                    Font = OsuFont.Default.With(size: 14),
                    Margin = SettingsPanel.CONTENT_PADDING,
                    Alpha = 0.7f,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 36,
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            Padding = new MarginPadding { Right = 4 },
                            Child = new SettingsButtonV2
                            {
                                Text = FavouriteSettingsStrings.Add,
                                Height = 36,
                                Padding = new MarginPadding(),
                                Action = owner.BeginFavouriteSelection,
                            },
                        },
                        new Container
                        {
                            Anchor = Anchor.TopRight,
                            Origin = Anchor.TopRight,
                            RelativeSizeAxes = Axes.Both,
                            Width = 0.5f,
                            Padding = new MarginPadding { Left = 4 },
                            Child = removeButton = new SettingsButtonV2
                            {
                                Text = FavouriteSettingsStrings.Remove,
                                Height = 36,
                                Padding = new MarginPadding(),
                                Action = toggleRemoving,
                            },
                        },
                    },
                },
            },
        };

        private void toggleRemoving()
        {
            removing = !removing;
            removeButton!.Text = removing ? FavouriteSettingsStrings.FinishRemoving : FavouriteSettingsStrings.Remove;
        }

        protected override void PopIn()
        {
            // Returning from selection must show the complete saved list, not just matches
            // for the previous favourite search (which may hide newly selected settings).
            SearchTextBox.Current.SetDefault();
            shortcutHint.Text = FavouriteSettingsStrings.Shortcut(keyBindings.GetBindingsStringFor(GlobalAction.ToggleFavouriteSettings));
            base.PopIn();
            if (SectionsReady)
                Populate();
        }

        protected override void PopOut()
        {
            Restore();
            removing = false;
            if (removeButton != null)
                removeButton.Text = FavouriteSettingsStrings.Remove;
            base.PopOut();
        }

        internal void Populate()
        {
            Restore();
            if (!SectionsReady || owner.Favourites == null)
                return;

            var entries = owner.FavouriteEntries.ToLookup(e => e.Item.FavouriteId!);
            foreach (string id in owner.Favourites.Ids)
            {
                var entry = entries[id].FirstOrDefault();
                if (entry == null)
                    continue;
                var host = new Container { RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y };
                var row = new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Children = new Drawable[]
                    {
                        new OsuSpriteText
                        {
                            Text = entry.Path,
                            Font = OsuFont.Default.With(size: 12),
                            Alpha = 0.6f,
                            Margin = SettingsPanel.CONTENT_PADDING,
                        },
                        host,
                    },
                };
                section.Add(row);
                entry.Borrow(host);
                borrowed.Add(entry);
                entry.Item.FavouriteSelectionOverlay.Add(new FavouriteSelectionTarget(
                    () => Schedule(() => { owner.Favourites.Toggle(id); Populate(); }),
                    () => true, () => removing));
            }

            if (borrowed.Count == 0)
                section.Add(new SettingsNote
                {
                    RelativeSizeAxes = Axes.X,
                    Current = { Value = new SettingsNote.Data(FavouriteSettingsStrings.Empty, SettingsNote.Type.Informational) },
                });
            removeButton!.Enabled.Value = owner.Favourites.Ids.Count > 0;
            SectionsContainer.RefreshSearch();
        }

        internal void Restore()
        {
            foreach (var entry in borrowed)
            {
                entry.Item.FavouriteSelectionOverlay.Clear();
                entry.Return();
            }
            borrowed.Clear();
            section.Clear();
        }

        private partial class FavouriteSection : SettingsSection
        {
            public override LocalisableString Header => FavouriteSettingsStrings.Title;
            public override Drawable CreateIcon() => new SpriteIcon { Icon = FontAwesome.Solid.Star };
        }
    }
}
