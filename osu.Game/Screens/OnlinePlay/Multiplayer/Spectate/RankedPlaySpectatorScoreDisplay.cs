using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Components;
using osu.Game.Users.Drawables;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.Multiplayer.Spectate
{
    public partial class RankedPlaySpectatorScoreDisplay : CompositeDrawable
    {
        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        private readonly int[] users;
        private readonly MultiSpectatorLeaderboardProvider leaderboard;
        private readonly OsuSpriteText[] labels;
        private readonly UpdateableAvatar[] avatars;
        private readonly RankedPlayUserDisplay.HealthBar[] healthBars;

        public RankedPlaySpectatorScoreDisplay(int[] users, MultiSpectatorLeaderboardProvider leaderboard)
        {
            this.users = users;
            this.leaderboard = leaderboard;
            labels = new OsuSpriteText[users.Length];
            avatars = new UpdateableAvatar[users.Length];
            healthBars = new RankedPlayUserDisplay.HealthBar[users.Length];
            RelativeSizeAxes = Axes.X;
            Height = 90;
            Padding = new MarginPadding { Horizontal = 24, Vertical = 8 };

            for (int i = 0; i < users.Length; i++)
            {
                var colours = i == 0 ? RankedPlayColourScheme.BLUE : RankedPlayColourScheme.RED;
                AddInternal(new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    RelativePositionAxes = Axes.X,
                    Width = 1f / users.Length,
                    X = (float)i / users.Length,
                    Padding = new MarginPadding { Horizontal = 12 },
                    Children = new Drawable[]
                    {
                        new Container
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 32,
                            Children = new Drawable[]
                            {
                                avatars[i] = new UpdateableAvatar(isInteractive: false)
                                {
                                    Size = new Vector2(32),
                                    Masking = true,
                                    CornerRadius = 4,
                                },
                                new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Padding = new MarginPadding { Left = 40 },
                                    Child = labels[i] = new TruncatingSpriteText
                                    {
                                        Name = "Participant username",
                                        Anchor = Anchor.CentreLeft,
                                        Origin = Anchor.CentreLeft,
                                        RelativeSizeAxes = Axes.X,
                                        Colour = colours.Primary,
                                    },
                                },
                            },
                        },
                        healthBars[i] = new RankedPlayUserDisplay.HealthBar(colours, i == 0, Vector2.Zero)
                        {
                            RelativeSizeAxes = Axes.X,
                            Height = 28,
                            Y = 42,
                        },
                    },
                });
            }
        }

        protected override void Update()
        {
            base.Update();
            if (client.Room?.MatchState is not RankedPlayRoomState state)
                return;

            for (int i = 0; i < users.Length; i++)
            {
                var score = leaderboard.Scores.FirstOrDefault(s => s.User.OnlineID == users[i]);
                var participant = score?.User as APIUser ?? client.Room.Users.FirstOrDefault(u => u.UserID == users[i])?.User;
                labels[i].Text = score?.User.Username ?? participant?.Username ?? users[i].ToString();
                avatars[i].User = participant;
                if (state.Users.TryGetValue(users[i], out var user))
                    healthBars[i].Health.Value = user.Life;
            }
        }
    }
}
