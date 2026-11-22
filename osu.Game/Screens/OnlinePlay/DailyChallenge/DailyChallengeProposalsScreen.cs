using System;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Screens.OnlinePlay.Match.Components;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.DailyChallenge
{
    public partial class DailyChallengeProposalsScreen : OsuScreen
    {
        [Cached] private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Plum);
        [Resolved] private IAPIProvider api { get; set; } = null!;
        [Resolved] private BeatmapManager beatmaps { get; set; } = null!;

        private ProposalList queue = null!;
        private ProposalList pending = null!;
        private DailyChallengeProposal? selected;
        private OsuSpriteText status = null!;
        private OsuSpriteText details = null!;
        private PurpleRoundedButton approve = null!, edit = null!, reject = null!, up = null!, down = null!;
        private MutateDailyChallengeProposalRequest? mutation;
        private GetDailyChallengeProposalStatusRequest? permissionRequest;
        private bool busy;
        private bool allowed;
        private int permissionGeneration;

        public override string Title => "Daily challenge proposals";

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.Both,
                Children = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colourProvider.Background5 },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Padding = new MarginPadding { Horizontal = 45, Top = 20, Bottom = 40 },
                        RowDimensions = new[] { new Dimension(GridSizeMode.Absolute, 50), new Dimension(), new Dimension(GridSizeMode.Absolute, 190) },
                        Content = new[]
                        {
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both, Direction = FillDirection.Horizontal, Spacing = new Vector2(20),
                                    Children = new Drawable[]
                                    {
                                        new OsuSpriteText { Text = DailyChallengeProposalStrings.Requests, Font = osu.Game.Graphics.OsuFont.TorusAlternate.With(size: 28) },
                                        button(DailyChallengeProposalStrings.Add, () => this.Push(new DailyChallengeProposalSongSelect(true)), 200),
                                        button(DailyChallengeProposalStrings.Refresh, reload, 140),
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new GridContainer
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    RowDimensions = new[] { new Dimension(GridSizeMode.Relative, 0.43f), new Dimension(GridSizeMode.Absolute, 15), new Dimension() },
                                    Content = new[]
                                    {
                                        new Drawable[] { queue = new ProposalList("approved", DailyChallengeProposalStrings.Queue, onSelected) },
                                        Array.Empty<Drawable>(),
                                        new Drawable[] { pending = new ProposalList("pending", DailyChallengeProposalStrings.Pending, onSelected) }
                                    }
                                }
                            },
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    RelativeSizeAxes = Axes.Both, Direction = FillDirection.Vertical, Spacing = new Vector2(0, 8),
                                    Padding = new MarginPadding { Top = 12 },
                                    Children = new Drawable[]
                                    {
                                        details = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Text = DailyChallengeProposalStrings.Choose },
                                        new FillFlowContainer
                                        {
                                            AutoSizeAxes = Axes.Both, Direction = FillDirection.Horizontal, Spacing = new Vector2(10),
                                            Children = new Drawable[]
                                            {
                                                approve = button(DailyChallengeProposalStrings.AddToQueue, () => act("approve"), 180),
                                                edit = button(DailyChallengeProposalStrings.Edit, editSelected, 180),
                                                reject = button(DailyChallengeProposalStrings.Reject, () => act("reject"), 180),
                                                up = button(DailyChallengeProposalStrings.Up, () => act("up"), 150),
                                                down = button(DailyChallengeProposalStrings.Down, () => act("down"), 150),
                                            }
                                        },
                                        status = new TruncatingSpriteText { RelativeSizeAxes = Axes.X, Text = DailyChallengeProposalStrings.Loading },
                                        new OsuSpriteText { Text = DailyChallengeProposalStrings.QueueHint },
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        private static PurpleRoundedButton button(osu.Framework.Localisation.LocalisableString text, Action action, float width) =>
            new PurpleRoundedButton { Text = text, Action = action, Width = width, Height = 35 };

        protected override void LoadComplete()
        {
            base.LoadComplete();
            updateButtons();
            reload();
        }

        public override void OnResuming(ScreenTransitionEvent e)
        {
            base.OnResuming(e);
            reload();
        }

        private void reload()
        {
            if (busy) return;
            int generation = ++permissionGeneration;
            permissionRequest?.Cancel();
            status.Text = DailyChallengeProposalStrings.Loading;
            permissionRequest = new GetDailyChallengeProposalStatusRequest();
            permissionRequest.Success += overview => Schedule(() =>
            {
                if (generation != permissionGeneration) return;
                allowed = overview.CanManage;
                if (!allowed)
                {
                    queue.Clear();
                    pending.Clear();
                    this.Exit();
                    return;
                }
                selected = null;
                details.Text = DailyChallengeProposalStrings.Choose;
                updateButtons();
                queue.Reload();
                pending.Reload();
                status.Text = string.Empty;
            });
            permissionRequest.Failure += exception => Schedule(() =>
            {
                if (generation == permissionGeneration && exception is not OperationCanceledException)
                    status.Text = DailyChallengeProposalStrings.Failed;
            });
            api.Queue(permissionRequest);
        }

        private void onSelected(DailyChallengeProposal value)
        {
            if (busy || !allowed) return;
            selected = value;
            details.Text = DailyChallengeProposalStrings.ModSummary(value.ID,
                string.Join(", ", value.RequiredMods.Select(m => m.ToString())),
                string.Join(", ", value.AllowedMods.Select(m => m.ToString())));
            updateButtons();
        }

        private void updateButtons()
        {
            bool valid = allowed && !busy && selected != null;
            approve.Enabled.Value = valid && selected?.Status == "pending";
            edit.Enabled.Value = reject.Enabled.Value = valid;
            up.Enabled.Value = down.Enabled.Value = valid && selected?.Status == "approved";
        }

        private void editSelected()
        {
            if (selected == null || busy || !allowed) return;
            if (beatmaps.QueryOnlineBeatmapId(selected.BeatmapID) == null)
            {
                status.Text = DailyChallengeProposalStrings.DownloadToEdit;
                return;
            }
            this.Push(new DailyChallengeProposalSongSelect(editing: selected));
        }

        private void act(string action)
        {
            if (selected == null || busy || !allowed) return;
            busy = true;
            updateButtons();
            status.Text = DailyChallengeProposalStrings.Saving;
            mutation = new MutateDailyChallengeProposalRequest(selected, action);
            mutation.Success += _ => Schedule(() => { busy = false; reload(); });
            mutation.Failure += exception => Schedule(() =>
            {
                busy = false;
                status.Text = DailyChallengeProposalSongSelect.ProposalError(exception.Message);
                // Do not reuse the stale selected revision after a conflict.
                selected = null;
                updateButtons();
            });
            api.Queue(mutation);
        }

        public override bool OnExiting(ScreenExitEvent e) => busy || base.OnExiting(e);

        protected override void Dispose(bool isDisposing)
        {
            permissionRequest?.Cancel();
            mutation?.Cancel();
            base.Dispose(isDisposing);
        }

        private partial class ProposalList : CompositeDrawable
        {
            private readonly string state;
            private readonly osu.Framework.Localisation.LocalisableString title;
            private readonly Action<DailyChallengeProposal> select;
            private DailyChallengeProposal[] proposals = Array.Empty<DailyChallengeProposal>();
            private DrawableRoomPlaylist list = null!;
            private OsuSpriteText label = null!;
            private PurpleRoundedButton previous = null!, next = null!;
            private GetDailyChallengeProposalsRequest? request;
            private int offset;
            private int total;
            private int generation;
            [Resolved] private IAPIProvider api { get; set; } = null!;

            public ProposalList(string state, osu.Framework.Localisation.LocalisableString title, Action<DailyChallengeProposal> select)
            {
                this.state = state; this.title = title; this.select = select;
                RelativeSizeAxes = Axes.Both;
            }

            [BackgroundDependencyLoader]
            private void load(OverlayColourProvider colours)
            {
                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background4 },
                    new GridContainer
                    {
                        RelativeSizeAxes = Axes.Both, Padding = new MarginPadding(10),
                        RowDimensions = new[] { new Dimension(GridSizeMode.Absolute, 30), new Dimension(), new Dimension(GridSizeMode.Absolute, 35) },
                        Content = new[]
                        {
                            new Drawable[] { new OsuSpriteText { Text = title, Font = osu.Game.Graphics.OsuFont.TorusAlternate.With(size: 21) } },
                            new Drawable[] { list = new DrawableRoomPlaylist { RelativeSizeAxes = Axes.Both, AllowSelection = true, ShowItemOwners = true } },
                            new Drawable[]
                            {
                                new FillFlowContainer
                                {
                                    AutoSizeAxes = Axes.Both, Direction = FillDirection.Horizontal, Spacing = new Vector2(12),
                                    Children = new Drawable[]
                                    {
                                        previous = button(DailyChallengeProposalStrings.Previous, () => { offset = Math.Max(0, offset - 50); Reload(); }, 155),
                                        next = button(DailyChallengeProposalStrings.More, () => { offset += 50; Reload(); }, 155),
                                        label = new OsuSpriteText { Anchor = Anchor.CentreLeft, Origin = Anchor.CentreLeft },
                                    }
                                }
                            }
                        }
                    }
                };
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                list.SelectedItem.BindValueChanged(change =>
                {
                    var value = proposals.FirstOrDefault(p => p.ID == change.NewValue?.ID);
                    if (value != null) select(value);
                });
            }

            public void Clear() { proposals = Array.Empty<DailyChallengeProposal>(); list.Items.Clear(); }

            public void Reload()
            {
                int currentGeneration = ++generation;
                request?.Cancel();
                previous.Enabled.Value = next.Enabled.Value = false;
                label.Text = DailyChallengeProposalStrings.Loading;
                request = new GetDailyChallengeProposalsRequest(state, offset);
                request.Success += response => Schedule(() =>
                {
                    if (currentGeneration != generation) return;
                    total = response.Total;
                    if (offset >= total && offset > 0) { offset = Math.Max(0, ((total - 1) / 50) * 50); Reload(); return; }
                    proposals = response.Items;
                    list.SelectedItem.Value = null;
                    list.ReplaceItemsBatched(proposals.Select(p => p.ToPlaylistItem()));
                    label.Text = total == 0 ? DailyChallengeProposalStrings.Empty : DailyChallengeProposalStrings.Page(offset / 50 + 1, total);
                    previous.Enabled.Value = offset > 0;
                    next.Enabled.Value = offset + 50 < total;
                });
                request.Failure += exception => Schedule(() =>
                {
                    if (currentGeneration != generation || exception is OperationCanceledException) return;
                    label.Text = DailyChallengeProposalStrings.Failed;
                    previous.Enabled.Value = offset > 0;
                    next.Enabled.Value = offset + 50 < total;
                });
                api.Queue(request);
            }

            protected override void Dispose(bool isDisposing) { request?.Cancel(); base.Dispose(isDisposing); }
        }
    }
}
