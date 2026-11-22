// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Input.Events;
using osu.Game.Graphics;
using osu.Game.Configuration;
using osu.Game.Graphics.Containers;
using osu.Game.Graphics.Sprites;
using osu.Game.Overlays.CustomTheme;
using osuTK.Graphics;
using osu.Game.Resources.Localisation.Web;
using osuTK;

namespace osu.Game.Overlays.Chat
{
    public partial class ChatOverlayTopBar : Container
    {
        public Drawable DragBar { get; private set; } = null!;
        private Box background = null!;
        private OsuSpriteText title = null!;
        private SpriteIcon icon = null!;
        private IBindable<Colour4> themeColour = null!;
        private readonly Bindable<bool> translatorEnabled = new Bindable<bool>();
        private TranslatorToggleButton translatorButton = null!;
        private Container translatorSurface = null!;
        private Box translatorBackground = null!;
        private Circle translatorStatus = null!;
        private SpriteIcon translatorIcon = null!;
        private OsuSpriteText translatorLabel = null!;
        private IBindable<float> buttonRadius = null!;
        private readonly OsuColour osuColour = new OsuColour();
        private OverlayColourProvider colourProvider = null!;

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colourProvider, TextureStore textures, OsuConfigManager config)
        {
            this.colourProvider = colourProvider;
            Children = new[]
            {
                background = new Box
                {
                    RelativeSizeAxes = Axes.Both,
                    Colour = colourProvider.Background3,
                },
                new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(GridSizeMode.Absolute, 50),
                        new Dimension(),
                        new Dimension(GridSizeMode.Absolute, 138),
                    },
                    Content = new[]
                    {
                        new Drawable[]
                        {
                            icon = new SpriteIcon
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Icon = OsuIcon.Chat,
                                Size = new Vector2(24),
                            },
                            // Placeholder text
                            title = new OsuSpriteText
                            {
                                Anchor = Anchor.CentreLeft,
                                Origin = Anchor.CentreLeft,
                                Text = ChatStrings.TitleCompact,
                                Font = OsuFont.Torus.With(size: 16, weight: FontWeight.SemiBold),
                                Margin = new MarginPadding { Bottom = 2f },
                            },
                            translatorButton = new TranslatorToggleButton
                            {
                                Anchor = Anchor.Centre,
                                Origin = Anchor.Centre,
                                Size = new Vector2(126, 28),
                                Action = () => translatorEnabled.Value = !translatorEnabled.Value,
                                Child = translatorSurface = new Container
                                {
                                    RelativeSizeAxes = Axes.Both,
                                    Masking = true,
                                    Children = new Drawable[]
                                    {
                                        translatorBackground = new Box { RelativeSizeAxes = Axes.Both },
                                        new GridContainer
                                        {
                                            RelativeSizeAxes = Axes.Both,
                                            Padding = new MarginPadding { Horizontal = 9 },
                                            ColumnDimensions = new[]
                                            {
                                                new Dimension(GridSizeMode.Absolute, 17),
                                                new Dimension(),
                                                new Dimension(GridSizeMode.Absolute, 9),
                                            },
                                            Content = new[]
                                            {
                                                new Drawable[]
                                                {
                                                    translatorIcon = new SpriteIcon
                                                    {
                                                        Icon = FontAwesome.Solid.Globe,
                                                        Size = new Vector2(13),
                                                        Anchor = Anchor.CentreLeft,
                                                        Origin = Anchor.CentreLeft,
                                                    },
                                                    translatorLabel = new OsuSpriteText
                                                    {
                                                        Text = osu.Game.Localisation.ForkSettingsStrings.ChatTranslator,
                                                        Font = OsuFont.Torus.With(size: 12, weight: FontWeight.SemiBold),
                                                        Anchor = Anchor.CentreLeft,
                                                        Origin = Anchor.CentreLeft,
                                                    },
                                                    translatorStatus = new Circle
                                                    {
                                                        Size = new Vector2(7),
                                                        Anchor = Anchor.Centre,
                                                        Origin = Anchor.Centre,
                                                    },
                                                },
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
                DragBar = new DragArea
                {
                    Alpha = RuntimeInfo.IsMobile ? 1 : 0,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Colour = colourProvider.Background4,
                }
            };

            translatorButton.HoverChanged = updateTranslatorButton;
            config.BindWith(OsuSetting.ForkChatTranslatorEnabled, translatorEnabled);
            translatorEnabled.BindValueChanged(_ => updateTranslatorButton(), true);
            buttonRadius = colourProvider.GetRadiusBindable(GeometryRole.Button);
            buttonRadius.BindValueChanged(radius => translatorSurface.CornerRadius = radius.NewValue, true);

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ =>
            {
                background.Colour = colourProvider.Background3;
                title.Colour = icon.Colour = colourProvider.Content1;
                DragBar.Colour = colourProvider.Background4;
                updateTranslatorButton();
            }, true);
        }

        private void updateTranslatorButton()
        {
            translatorBackground.Colour = translatorEnabled.Value
                ? (translatorButton.IsHovered ? colourProvider.Colour1 : colourProvider.Colour0)
                : (translatorButton.IsHovered ? colourProvider.Background2 : colourProvider.Background4);
            translatorLabel.Colour = translatorIcon.Colour = translatorEnabled.Value ? colourProvider.Background6 : colourProvider.Content1;
            translatorStatus.Colour = translatorEnabled.Value ? osuColour.Green2 : colourProvider.Content2;
            translatorButton.TooltipText = translatorEnabled.Value ? osu.Game.Localisation.ForkSettingsStrings.ChatTranslatorDisable : osu.Game.Localisation.ForkSettingsStrings.ChatTranslatorEnable;
        }

        protected override bool OnHover(HoverEvent e)
        {
            if (!RuntimeInfo.IsMobile)
                DragBar.FadeIn(100);
            return base.OnHover(e);
        }

        protected override void OnHoverLost(HoverLostEvent e)
        {
            if (!RuntimeInfo.IsMobile)
                DragBar.FadeOut(100);
            base.OnHoverLost(e);
        }

        private partial class TranslatorToggleButton : OsuClickableContainer
        {
            public System.Action? HoverChanged;

            protected override bool OnHover(HoverEvent e)
            {
                HoverChanged?.Invoke();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                base.OnHoverLost(e);
                HoverChanged?.Invoke();
            }
        }

        private partial class DragArea : CompositeDrawable
        {
            private readonly Circle circle;

            public DragArea()
            {
                AutoSizeAxes = Axes.Both;

                InternalChildren = new Drawable[]
                {
                    circle = new Circle
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Size = new Vector2(150, 7),
                        Margin = new MarginPadding(12),
                    }
                };
            }

            protected override bool OnHover(HoverEvent e)
            {
                updateScale();
                return base.OnHover(e);
            }

            protected override void OnHoverLost(HoverLostEvent e)
            {
                updateScale();
                base.OnHoverLost(e);
            }

            private bool dragging;

            protected override bool OnMouseDown(MouseDownEvent e)
            {
                dragging = true;
                updateScale();
                return base.OnMouseDown(e);
            }

            protected override void OnMouseUp(MouseUpEvent e)
            {
                dragging = false;
                updateScale();
                base.OnMouseUp(e);
            }

            private void updateScale()
            {
                if (dragging || IsHovered)
                    circle.FadeIn(100);
                else
                    circle.FadeTo(0.6f, 100);

                if (dragging)
                    circle.ScaleTo(1f, 400, Easing.OutQuint);
                else if (IsHovered)
                    circle.ScaleTo(1.05f, 400, Easing.OutElasticHalf);
                else
                    circle.ScaleTo(1f, 500, Easing.OutQuint);
            }
        }
    }
}
