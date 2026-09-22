// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Linq;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Framework.Graphics.Sprites;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Online.Chat;
using osu.Game.Overlays.Chat;
using osuTK;
using osuTK.Input;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.View
{
    internal partial class LobbyChatPanel : CompositeDrawable
    {
        private const string world_channel_name = "dodge-world";

        private readonly ChannelManager? channelManager;
        private readonly Func<string> languageProvider;
        private readonly DodgeWorldChatDisplay display;
        private readonly OsuSpriteText title;
        private Channel? channel;

        public bool InputFocused => display.InputFocused;

        /// <summary>The default corner the panel sits in, as an offset from the bottom right.</summary>
        private static readonly Vector2 default_offset = new Vector2(-16, -68);

        private static readonly Vector2 default_size = new Vector2(370, 220);

        private static readonly Vector2 minimum_size = new Vector2(240, 130);

        /// <summary>
        /// Where the panel was left and how big it was made.
        /// </summary>
        /// <remarks>
        /// Static so that walking out of the world and back in does not undo the arrangement. Not written
        /// to disk: this is a window position, and remembering it for as long as the game is running is
        /// what makes it stop being annoying.
        /// </remarks>
        private static Vector2 rememberedOffset = default_offset;

        private static Vector2 rememberedSize = default_size;

        public LobbyChatPanel(OsuColour colours, ChannelManager? channelManager, Func<string> languageProvider)
        {
            this.channelManager = channelManager;
            this.languageProvider = languageProvider;
            Size = rememberedSize;
            Anchor = Anchor.BottomRight;
            Origin = Anchor.BottomRight;
            Position = rememberedOffset;
            Depth = -100;
            Alpha = 0.62f;
            Masking = true;
            CornerRadius = 11;
            BorderThickness = 2;
            BorderColour = colours.Gray4;

            InternalChildren = new Drawable[]
            {
                new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray0.Opacity(0.94f) },
                new DragHandle(offset => moveBy(offset))
                {
                    RelativeSizeAxes = Axes.X,
                    Height = header_height,
                    Child = title = new OsuSpriteText
                    {
                        Position = new Vector2(15, 12),
                        Text = languageProvider() == "ru" ? "ЧАТ DODGE WORLD • ПОДКЛЮЧЕНИЕ" : "DODGE WORLD CHAT • CONNECTING",
                        Font = OsuFont.Default.With(size: 13, weight: FontWeight.Bold),
                    },
                },
                new Container
                {
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Left = 10, Right = 10, Top = header_height, Bottom = 10 },
                    Child = display = new DodgeWorldChatDisplay(languageProvider) { RelativeSizeAxes = Axes.Both },
                },
                // Top left, because the panel grows away from the corner it is anchored in: dragging the
                // handle out from the bottom right corner is what makes it bigger.
                new DragHandle(offset => resizeBy(offset))
                {
                    Anchor = Anchor.TopLeft,
                    Origin = Anchor.TopLeft,
                    Size = new Vector2(18),
                    Child = new SpriteIcon
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(9),
                        Rotation = 90,
                        Icon = FontAwesome.Solid.ExpandArrowsAlt,
                        Colour = colours.Gray6,
                    },
                },
            };

            display.FocusGained = showActive;
            display.FocusLost = showInactive;
        }

        private const float header_height = 32;

        /// <summary>
        /// Moves the panel, keeping it inside the screen: a panel dragged off the edge could not be
        /// dragged back.
        /// </summary>
        private void moveBy(Vector2 offset)
        {
            if (Parent == null)
                return;

            Vector2 room = Parent.DrawSize - DrawSize;
            Position = rememberedOffset = new Vector2(
                Math.Clamp(Position.X + offset.X, -Math.Max(0, room.X), 0),
                Math.Clamp(Position.Y + offset.Y, -Math.Max(0, room.Y), 0));
        }

        private void resizeBy(Vector2 offset)
        {
            Size = rememberedSize = new Vector2(
                Math.Clamp(Size.X - offset.X, minimum_size.X, 900),
                Math.Clamp(Size.Y - offset.Y, minimum_size.Y, 700));
        }

        protected override void Update()
        {
            base.Update();
            if (channel != null)
                return;

            if (channelManager == null)
            {
                title.Text = languageProvider() == "ru" ? "ЧАТ DODGE WORLD • НЕДОСТУПЕН В PREVIEW" : "DODGE WORLD CHAT • PREVIEW";
                return;
            }

            Channel? available = channelManager.JoinedChannels.Concat(channelManager.AvailableChannels)
                                               .FirstOrDefault(candidate => candidate.Name.TrimStart('#') == world_channel_name);
            if (available == null)
                return;

            channel = channelManager.JoinChannel(available);
            display.Channel.Value = channel;
            title.Text = languageProvider() == "ru" ? "ЧАТ DODGE WORLD" : "DODGE WORLD CHAT";
        }

        public void ReleaseFocus() => display.ReleaseFocus();

        public void FocusInput() => display.FocusInput();

        protected override bool OnHover(HoverEvent e)
        {
            showActive();
            return true;
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            if (!display.InputFocused)
                showInactive();
            base.OnHoverLost(e);
        }

        private void showActive() => this.FadeTo(1, 180, Easing.OutQuint);
        private void showInactive() => this.FadeTo(0.62f, 350, Easing.OutQuint);

        internal Vector2 SizeForTesting => Size;

        internal Vector2 OffsetForTesting => Position;

        /// <summary>
        /// A patch of the panel that reports how far the mouse has been dragged across it.
        /// </summary>
        /// <remarks>
        /// Reports the movement rather than an absolute position so that the same handle can move the panel
        /// or resize it, and so neither has to know where the drag began.
        /// </remarks>
        private partial class DragHandle : Container
        {
            private readonly Action<Vector2> dragged;

            public DragHandle(Action<Vector2> dragged)
            {
                this.dragged = dragged;
            }

            protected override bool OnDragStart(DragStartEvent e) => e.Button == MouseButton.Left;

            protected override void OnDrag(DragEvent e)
            {
                base.OnDrag(e);
                dragged(e.Delta);
            }

            // So the whole patch is grabbable rather than only wherever a child happens to be drawn.
            protected override bool OnMouseDown(MouseDownEvent e) => e.Button == MouseButton.Left;
        }

        private partial class DodgeWorldChatDisplay : StandAloneChatDisplay
        {
            public Action? FocusGained;
            public Action? FocusLost;

            public bool InputFocused => TextBox?.HasFocus == true;

            public DodgeWorldChatDisplay(Func<string> languageProvider)
                : base(true)
            {
                Masking = true;
                CornerRadius = 8;
                if (TextBox == null)
                    return;

                TextBox.PlaceholderText = languageProvider() == "ru" ? "Сообщение в Dodge World" : "Message Dodge World";
                TextBox.ReleaseFocusOnCommit = true;
                TextBox.HoldFocus = false;
                TextBox.Focus = () => FocusGained?.Invoke();
                TextBox.FocusLost = () => FocusLost?.Invoke();
            }

            public void ReleaseFocus() => TextBox?.KillFocus();
            public void FocusInput() => GetContainingFocusManager()?.ChangeFocus(TextBox);

            protected override ChatLine? CreateMessage(Message message) => new StandAloneMessage(message);
        }
    }
}
