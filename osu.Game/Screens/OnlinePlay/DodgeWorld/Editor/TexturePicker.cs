// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.IO;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Editor
{
    /// <summary>
    /// The file browser the editor imports images through.
    /// </summary>
    /// <remarks>
    /// The desktop host has no system file dialog — <c>GameHost.CreateSystemFileSelector</c> returns
    /// null everywhere except Android and iOS — so asking for one and giving up when it is missing
    /// left the import button doing nothing at all on Windows. The framework's own
    /// <see cref="osu.Framework.Graphics.UserInterface.FileSelector"/> presents the system dialog
    /// where one exists and otherwise browses in game, which is what osu! itself imports through.
    /// </remarks>
    internal partial class TexturePicker : CompositeDrawable
    {
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Pink);

        private readonly string[] extensions;
        private readonly Container panel;
        private readonly Container browserArea;

        private Action<FileInfo>? chosen;

        /// <summary>
        /// The directory the last import came from, so a second image is a click away rather than a
        /// walk back down the same tree.
        /// </summary>
        private string? lastDirectory;

        public TexturePicker(OsuColour colours, string[] extensions)
        {
            this.extensions = extensions;

            RelativeSizeAxes = Axes.Both;
            Alpha = 0;

            InternalChildren = new Drawable[]
            {
                new ClickableContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    Action = Close,
                    Child = new Box { RelativeSizeAxes = Axes.Both, Colour = Color4.Black.Opacity(0.7f) },
                },
                panel = new Container
                {
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Size = new Vector2(880, 620),
                    Masking = true,
                    CornerRadius = 14,
                    BorderThickness = 2,
                    BorderColour = colours.Pink1,
                    Children = new Drawable[]
                    {
                        new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Gray1 },
                        new OsuSpriteText
                        {
                            Margin = new MarginPadding { Left = 20, Top = 16 },
                            Text = "ВЫБЕРИ КАРТИНКУ ДЛЯ БИБЛИОТЕКИ ТЕКСТУР",
                            Font = OsuFont.Default.With(size: 18, weight: FontWeight.Bold),
                            Colour = colours.Pink1,
                        },
                        browserArea = new Container
                        {
                            RelativeSizeAxes = Axes.Both,
                            Padding = new MarginPadding { Top = 52, Bottom = 58, Horizontal = 12 },
                        },
                        new RoundedButton
                        {
                            Anchor = Anchor.BottomCentre,
                            Origin = Anchor.BottomCentre,
                            Margin = new MarginPadding { Bottom = 14 },
                            Width = 220,
                            Height = 32,
                            Text = "Отмена",
                            Action = Close,
                        },
                    },
                },
            };
        }

        public bool IsOpen { get; private set; }

        public void Open(Action<FileInfo> onChosen)
        {
            chosen = onChosen;
            IsOpen = true;

            var file = new Bindable<FileInfo?>();
            file.BindValueChanged(value =>
            {
                if (value.NewValue == null)
                    return;

                lastDirectory = value.NewValue.DirectoryName;
                Action<FileInfo>? callback = chosen;
                Close();
                callback?.Invoke(value.NewValue);
            });

            // Rebuilt per open, because the framework's selector presents the system dialog once, from
            // its own load, and a reused instance would never present it again.
            browserArea.Child = new OsuFileSelector(lastDirectory, extensions)
            {
                RelativeSizeAxes = Axes.Both,
                CurrentFile = { BindTarget = file },
            };

            this.FadeIn(150, Easing.OutQuint);
            panel.ScaleTo(0.96f).ScaleTo(1, 200, Easing.OutQuint);
        }

        public void Close()
        {
            if (!IsOpen)
                return;

            IsOpen = false;
            chosen = null;
            this.FadeOut(120, Easing.OutQuint);
        }

        /// <summary>
        /// Swallows clicks that miss the panel, so that a stray press does not reach the world behind.
        /// </summary>
        protected override bool OnMouseDown(MouseDownEvent e) => true;
    }
}
