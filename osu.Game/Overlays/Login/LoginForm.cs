// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.LocalisationExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.UserInterface;
using osu.Framework.Input;
using osu.Framework.Input.Events;
using osu.Framework.Extensions;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online;
using osu.Game.Online.Legacy;
using osu.Game.Overlays.Settings;
using osu.Game.Resources.Localisation.Web;
using osuTK;
using osu.Game.Localisation;

namespace osu.Game.Overlays.Login
{
    public partial class LoginForm : FillFlowContainer
    {
        private TextBox username = null!;
        private TextBox password = null!;
        private ShakeContainer shakeSignIn = null!;
        private ErrorTextFlowContainer errorText = null!;
        private OsuSpriteText accountTitleText = null!;
        private IBindable<Colour4>? themeColour;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private ServerProfileManager profileManager { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private StableBanchoSession? stableBanchoSession { get; set; }

        [Resolved]
        private osu.Framework.Platform.GameHost? host { get; set; }

        public Action? RequestHide;

        public override bool AcceptsFocus => true;

        [BackgroundDependencyLoader(permitNulls: true)]
        private void load(OsuConfigManager config, OverlayColourProvider? colourProvider = null)
        {
            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
            Direction = FillDirection.Vertical;
            Spacing = new Vector2(0, SettingsSection.ITEM_SPACING);

            LinkFlowContainer forgottenPasswordLink;

            Children = new Drawable[]
            {
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS },
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(0f, SettingsSection.ITEM_SPACING),
                    Children = new Drawable[]
                    {
                        accountTitleText = new OsuSpriteText
                        {
                            Text = LoginPanelStrings.Account.ToUpper(),
                            Font = OsuFont.GetFont(weight: FontWeight.Bold),
                        },
                        username = new OsuTextBox
                        {
                            InputProperties = new TextInputProperties(TextInputType.Username, false),
                            PlaceholderText = UsersStrings.LoginUsername.ToLower(),
                            RelativeSizeAxes = Axes.X,
                            Text = api.ProvidedUsername,
                            TabbableContentContainer = this
                        },
                        password = new OsuPasswordTextBox
                        {
                            PlaceholderText = UsersStrings.LoginPassword.ToLower(),
                            RelativeSizeAxes = Axes.X,
                            TabbableContentContainer = this,
                        },
                        errorText = new ErrorTextFlowContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Alpha = 0,
                        },
                    },
                },
                new SettingsCheckbox
                {
                    LabelText = LoginPanelStrings.RememberUsername,
                    Current = config.GetBindable<bool>(OsuSetting.SaveUsername),
                },
                new SettingsCheckbox
                {
                    LabelText = LoginPanelStrings.StaySignedIn,
                    Current = config.GetBindable<bool>(OsuSetting.SavePassword),
                },
                forgottenPasswordLink = new LinkFlowContainer
                {
                    Padding = new MarginPadding { Horizontal = SettingsPanel.CONTENT_MARGINS },
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                },
                new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Children = new Drawable[]
                    {
                        shakeSignIn = new ShakeContainer
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                            Child = new SettingsButton
                            {
                                Text = UsersStrings.LoginButton,
                                Action = performLogin
                            },
                        }
                    }
                }
            };

            string websiteUrl = MosuServerEnvironment.UsesStableProtocol
                ? profileManager.ActiveProfile.WebsiteUrl
                : api.Endpoints.WebsiteUrl;
            forgottenPasswordLink.AddLink(LayoutStrings.PopupLoginLoginForgot, $"{websiteUrl.TrimEnd('/')}/home/password-reset");

            password.OnCommit += (_, _) => performLogin();

            if (api.LastLoginError?.Message is string error)
            {
                errorText.Alpha = 1;
                errorText.AddErrors(new[] { error });
            }

            if (colourProvider != null)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ => accountTitleText.Colour = colourProvider.Content1, true);
            }
        }

        private async void performLogin()
        {
            if (string.IsNullOrEmpty(username.Text) || string.IsNullOrEmpty(password.Text))
            {
                shakeSignIn.Shake();
                return;
            }

            if (!MosuServerEnvironment.UsesStableProtocol)
            {
                api.Login(username.Text, password.Text);
                return;
            }

            if (stableBanchoSession == null)
                return;

            var profile = profileManager.ActiveProfile;
            profile.Username = username.Text;
            profile.StablePasswordHash = password.Text.ComputeMD5Hash();
            profileManager.SaveProfiles();

            try
            {
                await stableBanchoSession.LoginNowAsync().ConfigureAwait(false);
                Schedule(() => RequestHide?.Invoke());
            }
            catch (Exception exception)
            {
                Schedule(() =>
                {
                    errorText.Alpha = 1;
                    errorText.AddErrors(new[] { exception.Message });
                    shakeSignIn.Shake();
                });
            }
        }

        protected override bool OnClick(ClickEvent e) => true;

        protected override void OnFocus(FocusEvent e)
        {
            Schedule(() => { GetContainingFocusManager()!.ChangeFocus(string.IsNullOrEmpty(username.Text) ? username : password); });
        }
    }
}
