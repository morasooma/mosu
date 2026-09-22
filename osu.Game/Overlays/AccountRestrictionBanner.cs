// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osuTK;
using osuTK.Graphics;
using GameToolbar = osu.Game.Overlays.Toolbar.Toolbar;

namespace osu.Game.Overlays
{
    /// <summary>
    /// A non-dismissible, global warning shown while the current account is restricted.
    /// </summary>
    public partial class AccountRestrictionBanner : CompositeDrawable
    {
        private const double refresh_interval = 30_000;
        private const string fallback_discord_username = "xtillius1";
        private const string fallback_discord_url = "https://discord.gg/bRSDssrgUE";

        private readonly bool pollApi;
        private OsuTextFlowContainer message = null!;
        private FillFlowContainer<RoundedButton> actions = null!;
        private RoundedButton copyButton = null!;
        private RoundedButton discordButton = null!;
        private bool requestPending;
        private IBindable<APIState>? apiState;
        private IBindable<APIUser>? localUser;
        private AccountRestrictionStatusResponse? currentStatus;
        private int accountGeneration;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved(canBeNull: true)]
        private OsuGame? game { get; set; }

        public AccountRestrictionBanner(bool pollApi = true)
        {
            this.pollApi = pollApi;
            RelativeSizeAxes = Axes.X;
            Height = 94;
            Y = GameToolbar.HEIGHT;
            Alpha = 0;
            Depth = -1;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            InternalChildren = new Drawable[]
            {
                new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = ColourInfo.GradientHorizontal(
                        new Color4(118, 18, 39, 255),
                        new Color4(201, 31, 62, 255)),
                },
                message = new OsuTextFlowContainer(text =>
                {
                    text.Font = OsuFont.Default.With(size: 15);
                    text.Colour = Color4.White;
                })
                {
                    RelativeSizeAxes = Axes.X,
                    Height = 58,
                    Padding = new MarginPadding { Horizontal = 20, Top = 8 },
                    TextAnchor = Anchor.TopCentre,
                },
                actions = new FillFlowContainer<RoundedButton>
                {
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    AutoSizeAxes = Axes.X,
                    Height = 30,
                    Y = -7,
                    Direction = FillDirection.Horizontal,
                    Spacing = new Vector2(8, 0),
                    Children = new[]
                    {
                        copyButton = new RoundedButton
                        {
                            Width = 170,
                            Height = 30,
                            Text = ForkSettingsStrings.RestrictionCopyDiscord,
                            BackgroundColour = new Color4(88, 13, 29, 255),
                            Action = copyDiscordUsername,
                        },
                        discordButton = new RoundedButton
                        {
                            Width = 145,
                            Height = 30,
                            Text = ForkSettingsStrings.RestrictionOpenDiscord,
                            BackgroundColour = new Color4(88, 13, 29, 255),
                            Action = openDiscord,
                        },
                    },
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            if (!pollApi)
                return;

            apiState = api.State.GetBoundCopy();
            localUser = api.LocalUser.GetBoundCopy();
            apiState.BindValueChanged(_ => Scheduler.AddOnce(refreshForConnection), true);
            localUser.BindValueChanged(_ => Scheduler.AddOnce(resetForAccountChange));
            Scheduler.AddDelayed(refresh, refresh_interval, true);
        }

        private void refreshForConnection()
        {
            if (api.IsLoggedIn)
                refresh();
            else
                resetForAccountChange();
        }

        private void resetForAccountChange()
        {
            accountGeneration++;
            requestPending = false;
            currentStatus = null;
            Hide();

            if (api.IsLoggedIn)
                refresh();
        }

        private void refresh()
        {
            if (!pollApi || requestPending || !api.IsLoggedIn)
                return;

            requestPending = true;
            int userId = api.LocalUser.Value.Id;
            int generation = accountGeneration;
            var request = new GetAccountRestrictionStatusRequest();
            request.Success += status => Schedule(() =>
            {
                if (generation != accountGeneration)
                    return;

                requestPending = false;
                if (api.IsLoggedIn && api.LocalUser.Value.Id == userId)
                    DisplayStatus(status);
            });
            request.Failure += _ => Schedule(() =>
            {
                if (generation == accountGeneration)
                    requestPending = false;
            });
            api.Queue(request);
        }

        /// <summary>
        /// Updates the visible warning. Public for visual testing.
        /// </summary>
        public void DisplayStatus(AccountRestrictionStatusResponse status)
        {
            currentStatus = status;

            if (!status.IsRestricted)
            {
                Hide();
                return;
            }

            bool cheating = string.Equals(status.Category, "cheating", StringComparison.OrdinalIgnoreCase);
            string username = status.DiscordUsername ?? fallback_discord_username;

            message.Clear();
            message.AddText(ForkSettingsStrings.RestrictionTitle, text =>
            {
                text.Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold);
                text.Colour = Color4.White;
            });
            message.AddText("\n");
            message.AddText(cheating
                ? ForkSettingsStrings.RestrictionCheatingBody(username)
                : ForkSettingsStrings.RestrictionGenericBody);

            actions.Alpha = cheating ? 1 : 0;
            this.FadeIn(180);
        }

        private void copyDiscordUsername() =>
            game?.CopyToClipboard(currentStatus?.DiscordUsername ?? fallback_discord_username);

        private void openDiscord() =>
            game?.OpenUrlExternally(currentStatus?.DiscordUrl ?? fallback_discord_url);
    }
}
