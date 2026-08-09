// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Cursor;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Framework.Screens;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Online.Multiplayer;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay
{
    public partial class HamburgerMenu : IconButton, IHasPopover
    {
        public HamburgerMenu()
        {
            Icon = FontAwesome.Solid.Bars;
            Action = this.ShowPopover;
        }

        public Framework.Graphics.UserInterface.Popover GetPopover() => new Popover();

        private partial class Popover : OsuPopover
        {
            [Resolved]
            private RankedPlayScreen? rankedPlayScreen { get; set; }

            [Resolved]
            private MultiplayerClient? multiplayerClient { get; set; }

            private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);
            private FillFlowContainer buttonFlow = null!;

            [BackgroundDependencyLoader]
            private void load()
            {
                Content.Padding = new MarginPadding(5);

                Child = buttonFlow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Direction = FillDirection.Vertical,
                    Spacing = new Vector2(3),
                };

                bool matchEnded = rankedPlayScreen?.ActiveSubScreen is EndedScreen;
                addButton(matchEnded ? CommonStrings.Exit : "Give up", FontAwesome.Solid.SignOutAlt, () =>
                {
                    if (matchEnded)
                    {
                        rankedPlayScreen?.Exit();
                    }
                    else
                    {
                        if (multiplayerClient != null)
                        {
                            multiplayerClient.Surrender().ContinueWith(_ =>
                            {
                                Schedule(() =>
                                {
                                    if (rankedPlayScreen.IsCurrentScreen() && (multiplayerClient.Room == null || !multiplayerClient.IsConnected.Value))
                                    {
                                        rankedPlayScreen.Exit();
                                    }
                                });
                            });
                        }
                        else
                        {
                            rankedPlayScreen?.Exit();
                        }
                    }
                });
            }

            protected override void LoadComplete()
            {
                base.LoadComplete();
                ScheduleAfterChildren(() => GetContainingFocusManager()!.ChangeFocus(this));
            }

            private void addButton(LocalisableString text, IconUsage? icon, Action? action, Color4? colour = null)
            {
                var button = new OptionButton(colourProvider)
                {
                    Text = text,
                    Icon = icon ?? new IconUsage(),
                    BackgroundColour = colourProvider.Background3,
                    TextColour = colour,
                    DefaultTextColour = OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : Color4.White,
                    Action = () =>
                    {
                        Scheduler.AddDelayed(Hide, 50);
                        action?.Invoke();
                    },
                };

                buttonFlow.Add(button);
            }

            private partial class OptionButton : OsuButton
            {
                private readonly OverlayColourProvider colourProvider;
                private IBindable<Colour4> themeColour = null!;
                private SpriteIcon icon = null!;

                public IconUsage Icon { get; init; }
                public Color4? TextColour { get; init; }
                public Color4 DefaultTextColour { get; init; } = Color4.White;

                public OptionButton(OverlayColourProvider colourProvider)
                {
                    this.colourProvider = colourProvider;
                    Size = new Vector2(265, 50);
                }

                [BackgroundDependencyLoader]
                private void load()
                {
                    themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                    themeColour.BindValueChanged(_ => updateColours(), true);
                    Content.CornerRadius = 10;

                    Add(icon = new SpriteIcon
                    {
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                        Size = new Vector2(17),
                        X = 15,
                        Icon = Icon,
                    });
                }

                private void updateColours()
                {
                    var textColour = TextColour ?? (OverlayColourProvider.IsLightTheme ? colourProvider.Content1 : DefaultTextColour);
                    SpriteText.Colour = textColour;
                    if (icon != null)
                        icon.Colour = textColour;
                }

                protected override SpriteText CreateText() => new OsuSpriteText
                {
                    Depth = -1,
                    Origin = Anchor.CentreLeft,
                    Anchor = Anchor.CentreLeft,
                    X = 40
                };
            }
        }
    }
}
