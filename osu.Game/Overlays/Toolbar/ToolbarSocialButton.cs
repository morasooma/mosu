// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Input.Bindings;
using osu.Game.Online.Metadata;
using osu.Game.Users;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarSocialButton : ToolbarOverlayToggleButton
    {
        protected override Anchor TooltipAnchor => Anchor.TopRight;

        private readonly OnlineCountCircle countDisplay;

        [Resolved]
        private MetadataClient metadataClient { get; set; } = null!;

        private IDisposable? userPresenceWatchToken;
        private readonly IBindable<bool> isConnected = new Bindable<bool>();
        private readonly IBindableDictionary<int, UserPresence> userPresences = new BindableDictionary<int, UserPresence>();

        public ToolbarSocialButton()
        {
            Hotkey = GlobalAction.ToggleSocial;

            Add(countDisplay = new OnlineCountCircle
            {
                Alpha = 0,
                Height = 16,
                RelativePositionAxes = Axes.Both,
                Origin = Anchor.Centre,
                Position = new Vector2(0.7f, 0.25f),
            });
        }

        [BackgroundDependencyLoader(true)]
        private void load(DashboardOverlay dashboard)
        {
            StateContainer = dashboard;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            isConnected.BindTo(metadataClient.IsConnected);
            isConnected.BindValueChanged(connected =>
            {
                if (connected.NewValue)
                    userPresenceWatchToken ??= metadataClient.BeginWatchingUserPresence();
                else
                {
                    userPresenceWatchToken?.Dispose();
                    userPresenceWatchToken = null;
                }
            }, true);

            userPresences.BindTo(metadataClient.UserPresences);
            userPresences.BindCollectionChanged((_, _) => updateCount(), true);
        }

        private void updateCount()
        {
            int count = userPresences.Count;

            if (count == 0)
                countDisplay.FadeOut(200, Easing.OutQuint);
            else
            {
                countDisplay.Count = count;
                countDisplay.FadeIn(200, Easing.OutQuint);
            }
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            userPresenceWatchToken?.Dispose();
        }

        private partial class OnlineCountCircle : CompositeDrawable
        {
            private readonly OsuSpriteText countText;
            private readonly Circle circle;

            private int count;

            public int Count
            {
                get => count;
                set
                {
                    if (count == value)
                        return;

                    count = value;
                    countText.Text = value.ToString("#,0");
                }
            }

            public OnlineCountCircle()
            {
                AutoSizeAxes = Axes.X;

                InternalChildren = new Drawable[]
                {
                    circle = new Circle
                    {
                        RelativeSizeAxes = Axes.Both,
                        Colour = new Color4(67, 181, 129, 255),
                    },
                    countText = new OsuSpriteText
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Y = -1,
                        Font = OsuFont.GetFont(size: 14, weight: FontWeight.Bold),
                        Padding = new MarginPadding(5),
                        Colour = Color4.White,
                        UseFullGlyphHeight = true,
                    }
                };
            }
        }
    }
}
