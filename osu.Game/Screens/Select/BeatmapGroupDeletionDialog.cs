using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Overlays.Notifications;
using osuTK;

namespace osu.Game.Screens.Select
{
    public partial class BeatmapGroupDeletionDialog : PopupDialog
    {
        public override float PreferredWidth => 760;
        protected override bool CompactAppearance => true;
        protected override bool HorizontalButtons => true;
        private readonly LocalisableString groupTitle;
        private readonly BeatmapGroupDeletion selection;
        private readonly Func<IReadOnlySet<Guid>> currentGroup;
        private PopupDialogOkButton next = null!;
        private OsuTextFlowContainer summary = null!;

        [Cached]
        private readonly OverlayColourProvider colours = new OverlayColourProvider(OverlayColourScheme.Purple);

        [Resolved]
        private IDialogOverlay dialogs { get; set; } = null!;

        public BeatmapGroupDeletionDialog(LocalisableString groupTitle, BeatmapGroupDeletion selection, Func<IReadOnlySet<Guid>> currentGroup)
        {
            this.groupTitle = groupTitle;
            this.selection = selection;
            this.currentGroup = currentGroup;
            HeaderText = BeatmapGroupDeletionStrings.ConfirmHeader;
            BodyText = groupTitle;
            Icon = FontAwesome.Solid.TrashAlt;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            var list = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Padding = new MarginPadding { Right = 12, Bottom = 8 },
            };
            foreach (var entry in selection.Entries)
            {
                list.Add(new BeatmapGroupDeletionRow(entry, list.Count + 1));
                entry.Keep.BindValueChanged(_ => updateSummary());
            }
            MainContent.Padding = new MarginPadding { Horizontal = 24 };
            MainContent.Child = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 12),
                Children = new Drawable[]
                {
                    new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y, Direction = FillDirection.Horizontal, Spacing = new Vector2(8, 0),
                        Children = new Drawable[]
                        {
                            new RoundedButton { RelativeSizeAxes = Axes.X, Width = 0.48f, Height = 26, Text = BeatmapGroupDeletionStrings.KeepAll, Action = () => setKeep(true) },
                            new RoundedButton { RelativeSizeAxes = Axes.X, Width = 0.48f, Height = 26, Text = BeatmapGroupDeletionStrings.ClearKept, Action = () => setKeep(false) },
                        },
                    },
                    new OsuScrollContainer { RelativeSizeAxes = Axes.X, Height = Math.Min(320, selection.Entries.Count * (BeatmapGroupDeletionRow.HEIGHT * BeatmapGroupDeletionRow.PANEL_SCALE + 8)), Child = list },
                    summary = new OsuTextFlowContainer(t => t.Font = OsuFont.GetFont(size: 16))
                    {
                        RelativeSizeAxes = Axes.X, AutoSizeAxes = Axes.Y,
                    },
                },
            };
            Buttons = new PopupDialogButton[]
            {
                next = new PopupDialogOkButton
                {
                    Text = BeatmapGroupDeletionStrings.Continue,
                    Action = () =>
                    {
                        var chosen = selection.Entries.Where(e => e.CanDelete && !e.Keep.Value).ToArray();
                        if (chosen.Length > 0)
                            dialogs.Push(new Confirmation(groupTitle, chosen, currentGroup));
                    },
                },
                new PopupDialogCancelButton { Text = BeatmapGroupDeletionStrings.Cancel },
            };
            updateSummary();
        }

        private void setKeep(bool keep)
        {
            foreach (var entry in selection.Entries.Where(e => e.CanDelete))
                entry.Keep.Value = keep;
        }

        private void updateSummary()
        {
            if (summary == null || next == null)
                return;
            var chosen = selection.Entries.Where(e => e.CanDelete && !e.Keep.Value).ToArray();
            summary.Text = BeatmapGroupDeletionStrings.Summary(chosen.Length, chosen.Sum(e => e.Set.Beatmaps.Count), selection.Entries.Count - chosen.Length);
            next.Enabled.Value = chosen.Length > 0;
        }

        private partial class Confirmation : DeletionDialog
        {
            private readonly BeatmapGroupDeletion.Entry[] entries;
            private readonly Func<IReadOnlySet<Guid>> currentGroup;

            public Confirmation(LocalisableString title, BeatmapGroupDeletion.Entry[] entries, Func<IReadOnlySet<Guid>> currentGroup)
            {
                this.entries = entries;
                this.currentGroup = currentGroup;
                HeaderText = BeatmapGroupDeletionStrings.ConfirmHeader;
                BodyText = LocalisableString.Interpolate($"{title}\n{BeatmapGroupDeletionStrings.ConfirmBody(entries.Length, entries.Sum(e => e.Set.Beatmaps.Count))}");
                Buttons.OfType<PopupDialogCancelButton>().Single().Text = BeatmapGroupDeletionStrings.Cancel;
            }

            [BackgroundDependencyLoader]
            private void load(BeatmapManager manager, INotificationOverlay notifications)
            {
                DangerousAction = () =>
                {
                    int count = BeatmapGroupDeletion.Delete(manager, entries, currentGroup());
                    notifications.Post(new ProgressCompletionNotification { Text = BeatmapGroupDeletionStrings.Completed(count, entries.Length - count) });
                };
            }
        }
    }
}
