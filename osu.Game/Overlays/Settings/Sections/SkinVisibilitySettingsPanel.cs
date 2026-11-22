using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Localisation;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Overlays.Settings.Sections
{
    public partial class SkinVisibilitySettingsPanel : SettingsSubPanel
    {
        protected override Drawable CreateHeader() => new SettingsHeader(SkinVisibilityStrings.Title, string.Empty);

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            AddSection(new ElementSection(SkinVisibilityStrings.Notes,
                row(config, SkinVisibilityStrings.Numbers, OsuSetting.ForkShowHitCircleNumbers, "default-1", "1"),
                row(config, SkinVisibilityStrings.FollowPoints, OsuSetting.ForkShowFollowPoints, "followpoint", "· · ·")));
            AddSection(new ElementSection(SkinVisibilityStrings.Sliders,
                row(config, SkinVisibilityStrings.Ball, OsuSetting.ForkShowSliderBall, "sliderb0", "●", "sliderb"),
                row(config, SkinVisibilityStrings.FollowCircle, OsuSetting.ForkShowSliderFollowCircle, "sliderfollowcircle", "◎"),
                row(config, SkinVisibilityStrings.Arrows, OsuSetting.ForkShowReverseArrows, "reversearrow", "↶"),
                row(config, SkinVisibilityStrings.Ticks, OsuSetting.ForkShowSliderTicks, "sliderscorepoint", "•")));
            AddSection(new ElementSection(SkinVisibilityStrings.Other,
                row(config, SkinVisibilityStrings.Spinner, OsuSetting.ForkShowSpinners, "spinner-circle", "◉", "spinner-top")));
        }

        private static Drawable row(OsuConfigManager config, LocalisableString caption, OsuSetting setting, string texture, string fallback, string? alternate = null) =>
            new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Left = SettingsPanel.CONTENT_PADDING.Left, Right = SettingsPanel.CONTENT_PADDING.Right, Vertical = 7 },
                Children = new Drawable[]
                {
                    new ElementPreview(texture, alternate, fallback)
                    {
                        Width = 60,
                        Height = 60,
                        Anchor = Anchor.CentreLeft,
                        Origin = Anchor.CentreLeft,
                    },
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Padding = new MarginPadding { Left = 70 },
                        Child = new FormCheckBox
                        {
                            Caption = caption,
                            HintText = SkinVisibilityStrings.PreviewHint,
                            ExtendedHeight = true,
                            Current = config.GetBindable<bool>(setting),
                        },
                    },
                },
            };

        private partial class ElementSection : SettingsSection
        {
            public override LocalisableString Header { get; }
            public override Drawable CreateIcon() => new SpriteIcon { Icon = OsuIcon.SkinB };

            public ElementSection(LocalisableString header, params Drawable[] rows)
            {
                Header = header;
                Children = rows;
            }
        }

        // A texture-only preview deliberately avoids gameplay drawables, which require live hitobject dependencies.
        private partial class ElementPreview : SkinReloadableDrawable
        {
            private readonly string texture;
            private readonly string? alternate;
            private readonly string fallback;

            [Resolved]
            private OverlayColourProvider colours { get; set; } = null!;

            public ElementPreview(string texture, string? alternate, string fallback)
            {
                this.texture = texture;
                this.alternate = alternate;
                this.fallback = fallback;
                Masking = true;
                CornerRadius = 6;
            }

            protected override void SkinChanged(ISkinSource skin)
            {
                var image = skin.GetTexture(texture) ?? skin.GetTexture(texture + "-0");
                if (image == null && alternate != null)
                    image = skin.GetTexture(alternate);

                InternalChildren = new Drawable[]
                {
                    new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background5 },
                    image != null
                        ? new Sprite
                        {
                            Texture = image,
                            RelativeSizeAxes = Axes.Both,
                            Size = new Vector2(0.8f),
                            FillMode = FillMode.Fit,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        }
                        : new OsuSpriteText
                        {
                            Text = fallback,
                            Font = OsuFont.Default.With(size: 28),
                            Colour = colours.Content1,
                            Anchor = Anchor.Centre,
                            Origin = Anchor.Centre,
                        },
                };
            }
        }
    }
}
