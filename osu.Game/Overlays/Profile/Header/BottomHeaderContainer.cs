// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using Humanizer;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Resources.Localisation.Web;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Profile.Header
{
    public partial class BottomHeaderContainer : CompositeDrawable
    {
        public readonly Bindable<UserProfileData?> User = new Bindable<UserProfileData?>();

        private LinkFlowContainer topLinkContainer = null!;
        private LinkFlowContainer bottomLinkContainer = null!;

        private Color4 iconColour;
        private Color4 textColour;
        private IBindable<Colour4> themeColour = null!;
        private Box background = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        public BottomHeaderContainer()
        {
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider)
        {
            InternalChildren = new Drawable[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background4
                },
                new FillFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Direction = FillDirection.Vertical,
                    Padding = new MarginPadding { Horizontal = WaveOverlayContainer.HORIZONTAL_PADDING, Vertical = 10 },
                    Spacing = new Vector2(0, 10),
                    Children = new Drawable[]
                    {
                        topLinkContainer = new LinkFlowContainer(text =>
                        {
                            text.Font = text.Font.With(size: 12);
                            text.Colour = textColour;
                        })
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                        bottomLinkContainer = new LinkFlowContainer(text =>
                        {
                            text.Font = text.Font.With(size: 12);
                            text.Colour = textColour;
                        })
                        {
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        }
                    }
                }
            };

            User.BindValueChanged(user => updateDisplay(user.NewValue?.User));

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateThemeColours(colourProvider), true);
        }

        private void updateThemeColours(OverlayColourProvider colourProvider)
        {
            background.Colour = colourProvider.Background4;
            iconColour = colourProvider.Foreground1;
            textColour = OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Color4.White;
            updateDisplay(User.Value?.User);
        }

        private void updateDisplay(APIUser? user)
        {
            topLinkContainer.Clear();
            bottomLinkContainer.Clear();

            if (user == null) return;

            if (user.JoinDate.ToUniversalTime().Year < 2008)
                topLinkContainer.AddText(UsersStrings.ShowFirstMembers);
            else
            {
                topLinkContainer.AddText("Joined ");
                topLinkContainer.AddText(new DrawableDate(user.JoinDate, italic: false), embolden);
            }

            addSpacer(topLinkContainer);

            if (user.WasRecentlyOnline)
            {
                topLinkContainer.AddText(UsersStrings.ShowLastvisitOnline);
                addSpacer(topLinkContainer);
            }
            else if (user.LastVisit.HasValue)
            {
                topLinkContainer.AddText("Last seen ");
                topLinkContainer.AddText(new DrawableDate(user.LastVisit.Value, italic: false), embolden);

                addSpacer(topLinkContainer);
            }

            if (user.PlayStyles?.Length > 0)
            {
                topLinkContainer.AddText("Plays with ");

                LocalisableString playStylesString = user.PlayStyles[0].GetLocalisableDescription();

                for (int i = 1; i < user.PlayStyles.Length; i++)
                {
                    playStylesString = new TranslatableString(@"_", @"{0}{1}", playStylesString, CommonStrings.ArrayAndWordsConnector);
                    playStylesString = new TranslatableString(@"_", @"{0}{1}", playStylesString, user.PlayStyles[i].GetLocalisableDescription());
                }

                topLinkContainer.AddText(playStylesString, embolden);

                addSpacer(topLinkContainer);
            }

            topLinkContainer.AddText("Contributed ");
            topLinkContainer.AddLink("forum post".ToQuantity(user.PostCount, "#,##0"), $"{api.Endpoints.WebsiteUrl}/users/{user.Id}/posts", creationParameters: embolden);

            addSpacer(topLinkContainer);

            topLinkContainer.AddText("Posted ");
            topLinkContainer.AddLink("comment".ToQuantity(user.CommentsCount, "#,##0"), $"{api.Endpoints.WebsiteUrl}/comments?user_id={user.Id}", creationParameters: embolden);

            string websiteWithoutProtocol = user.Website;

            if (!string.IsNullOrEmpty(websiteWithoutProtocol))
            {
                if (Uri.TryCreate(websiteWithoutProtocol, UriKind.Absolute, out var uri))
                {
                    websiteWithoutProtocol = uri.Host + uri.PathAndQuery + uri.Fragment;
                    websiteWithoutProtocol = websiteWithoutProtocol.TrimEnd('/');
                }
            }

            bool anyInfoAdded = false;

            anyInfoAdded |= tryAddInfo(FontAwesome.Solid.MapMarkerAlt, user.Location);
            anyInfoAdded |= tryAddInfo(FontAwesome.Regular.Heart, user.Interests);
            anyInfoAdded |= tryAddInfo(FontAwesome.Solid.Suitcase, user.Occupation);

            if (anyInfoAdded)
                bottomLinkContainer.NewLine();

            if (!string.IsNullOrEmpty(user.Twitter))
                anyInfoAdded |= tryAddInfo(FontAwesome.Brands.Twitter, "@" + user.Twitter, $@"https://twitter.com/{user.Twitter}");
            anyInfoAdded |= tryAddTelegramInfo(user.TelegramProfile);
            anyInfoAdded |= tryAddInfo(FontAwesome.Brands.Discord, user.Discord);
            anyInfoAdded |= tryAddInfo(FontAwesome.Solid.Link, websiteWithoutProtocol, user.Website);

            // If no information was added to the bottomLinkContainer, hide it to avoid unwanted padding
            bottomLinkContainer.Alpha = anyInfoAdded ? 1 : 0;
        }

        private void addSpacer(OsuTextFlowContainer textFlow) => textFlow.AddArbitraryDrawable(new Container { Width = 15 });

        private bool tryAddInfo(IconUsage icon, string content, string? link = null)
        {
            if (string.IsNullOrEmpty(content)) return false;

            // newlines could be contained in API returned user content.
            content = content.Replace('\n', ' ');

            bottomLinkContainer.AddIcon(icon, text =>
            {
                text.Font = text.Font.With(icon.Family, 10, icon.Weight);
                text.Colour = iconColour;
            });

            if (link != null)
                bottomLinkContainer.AddLink(" " + content, link, creationParameters: embolden);
            else
                bottomLinkContainer.AddText(" " + content, embolden);

            addSpacer(bottomLinkContainer);
            return true;
        }

        private bool tryAddTelegramInfo(APIUser.TelegramProfileLink? profile)
        {
            if (profile == null)
                return false;

            string? username = profile.Username?.Trim().TrimStart('@');
            string? url = profile.Url?.Trim();
            string content = !string.IsNullOrEmpty(username) ? "@" + username : "Telegram";

            if (string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(username))
                url = $@"https://t.me/{username}";

            if (!isWebUrl(url))
                url = null;

            return tryAddInfo(FontAwesome.Brands.Telegram, content, url);
        }

        private static bool isWebUrl(string? url)
            => Uri.TryCreate(url, UriKind.Absolute, out var uri)
               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

        private void embolden(SpriteText text) => text.Font = text.Font.With(weight: FontWeight.Bold);
    }
}
