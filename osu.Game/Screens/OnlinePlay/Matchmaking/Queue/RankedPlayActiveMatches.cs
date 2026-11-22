using System;
using System.Linq;
using System.Threading;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Database;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Matchmaking;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.Queue
{
    public partial class RankedPlayActiveMatches : CompositeDrawable
    {
        [Resolved]
        private UserLookupCache users { get; set; } = null!;

        public Action<RankedPlayActiveMatch>? Spectate;
        public readonly OsuSpriteText EmptyMessage;
        private readonly FillFlowContainer rows;
        private CancellationTokenSource? lookup;
        private int version;
        private string? renderKey;

        public RankedPlayActiveMatches()
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            InternalChild = rows = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
            };
            EmptyMessage = new OsuSpriteText { Text = RankedPlayLobbyStrings.NoActiveMatches, Font = OsuFont.GetFont(size: 16) };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            SetMatches([]);
        }

        public async void SetMatches(RankedPlayActiveMatch[] matches)
        {
            var valid = matches.Where(match => match.RoomId > 0 && match.UserIds.Length == 2
                                                && match.UserIds.Distinct().Count() == 2 && match.Stage != RankedPlayStage.Ended)
                               .GroupBy(match => match.RoomId).Select(group => group.First()).OrderByDescending(match => match.RoomId).ToArray();
            string key = string.Join("|", valid.Select(match => $"{match.RoomId}:{string.Join(',', match.UserIds)}:{match.CurrentRound}"));
            if (key == renderKey) return;
            renderKey = key;
            int loadVersion = ++version;
            lookup?.Cancel();
            lookup?.Dispose();
            lookup = new CancellationTokenSource();
            var token = lookup.Token;
            try
            {
                await users.GetUsersAsync(valid.SelectMany(match => match.UserIds).Distinct().ToArray(), token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception)
            {
                // The lookup cache may be unavailable; rows still show participant IDs and remain usable.
            }
            Schedule(() =>
            {
                if (loadVersion != version || token.IsCancellationRequested) return;
                rows.Remove(EmptyMessage, false);
                rows.Clear();
                if (valid.Length == 0)
                {
                    rows.Add(EmptyMessage);
                    return;
                }
                foreach (var match in valid)
                    rows.Add(new RankedPlayActiveMatchRow(match) { Action = () => Spectate?.Invoke(match) });
            });
        }

        protected override void Dispose(bool isDisposing)
        {
            lookup?.Cancel();
            lookup?.Dispose();
            base.Dispose(isDisposing);
        }
    }

    public partial class RankedPlayActiveMatchRow(RankedPlayActiveMatch match) : CompositeDrawable
    {
        [Resolved]
        private UserLookupCache users { get; set; } = null!;

        public long RoomId => match.RoomId;
        public Action? Action;
        public RoundedButton WatchButton { get; private set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.X;
            Height = 68;
            InternalChildren = new Drawable[]
            {
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Right = 150 },
                    Child = new FillFlowContainer
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Direction = FillDirection.Vertical,
                        Children = new Drawable[]
                        {
                            new TruncatingSpriteText
                            {
                                RelativeSizeAxes = Axes.X,
                                Text = global::osu.Game.Localisation.ClientTranslationStrings.FormatJ4839(users.GetUserAsync(match.UserIds[0]).GetAwaiter().GetResult()?.Username ?? match.UserIds[0].ToString(), users.GetUserAsync(match.UserIds[1]).GetAwaiter().GetResult()?.Username ?? match.UserIds[1].ToString()),
                                Font = OsuFont.GetFont(size: 20, weight: FontWeight.SemiBold),
                            },
                            new OsuSpriteText { Text = RankedPlayLobbyStrings.Round(match.CurrentRound), Font = OsuFont.GetFont(size: 16) },
                        }
                    }
                },
                WatchButton = new RoundedButton
                {
                    Anchor = Anchor.CentreRight,
                    Origin = Anchor.CentreRight,
                    Size = new Vector2(135, 40),
                    Text = RankedPlayLobbyStrings.Spectate,
                    Action = () => Action?.Invoke(),
                },
            };
        }
    }
}
