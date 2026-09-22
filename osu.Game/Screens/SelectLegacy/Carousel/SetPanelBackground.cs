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
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osuTK;
using osuTK.Graphics;

using osu.Game.Screens.Select;

namespace osu.Game.Screens.SelectLegacy.Carousel
{
    public partial class SetPanelBackground : BufferedContainer
    {
        private const float legacy_preview_aspect_ratio = 16f / 9f;

        private readonly BindableBool useLegacyPreviewLayout = new BindableBool();

        private Box fallbackBackground = null!;
        private Container previewFrame = null!;
        private PanelBeatmapBackground backgroundSprite = null!;
        private Drawable gradientOverlay = null!;

        private readonly IWorkingBeatmap working;

        public SetPanelBackground(IWorkingBeatmap working)
            : base(cachedFrameBuffer: true)
        {
            this.working = working;

            RedrawOnScale = false;

            Children = new Drawable[]
            {
                // Shown behind the 16:9 preview block in the legacy layout, matching the current carousel's look.
                fallbackBackground = new Box
                {
                    Depth = 3,
                    RelativeSizeAxes = Axes.Both,
                    Alpha = 0,
                },
                previewFrame = new Container
                {
                    Depth = 2,
                    RelativeSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Child = backgroundSprite = new PanelBeatmapBackground(working)
                    {
                        RelativeSizeAxes = Axes.Both,
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        FillMode = FillMode.Fill,
                    },
                },
                gradientOverlay = new FillFlowContainer
                {
                    Depth = -1,
                    RelativeSizeAxes = Axes.Both,
                    Direction = FillDirection.Horizontal,
                    // This makes the gradient not be perfectly horizontal, but diagonal at a ~40-degree angle
                    Shear = new Vector2(0.8f, 0),
                    Alpha = 0.5f,
                    Children = new[]
                    {
                        // The left half with no gradient applied
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = Color4.Black,
                            Width = 0.4f,
                        },
                        // Piecewise-linear gradient with 3 segments to make it appear smoother
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientHorizontal(Color4.Black, new Color4(0f, 0f, 0f, 0.9f)),
                            Width = 0.05f,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientHorizontal(new Color4(0f, 0f, 0f, 0.9f), new Color4(0f, 0f, 0f, 0.1f)),
                            Width = 0.2f,
                        },
                        new Box
                        {
                            RelativeSizeAxes = Axes.Both,
                            Colour = ColourInfo.GradientHorizontal(new Color4(0f, 0f, 0f, 0.1f), new Color4(0, 0, 0, 0)),
                            Width = 0.05f,
                        },
                    }
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, OverlayColourProvider? colourProvider)
        {
            fallbackBackground.Colour = colourProvider != null
                ? ColourInfo.GradientHorizontal(colourProvider.Background3, colourProvider.Background4)
                : ColourInfo.GradientHorizontal(Color4.Black, OsuColour.Gray(0.2f));

            config.BindWith(OsuSetting.ForkSongSelectOldCarouselPreviews, useLegacyPreviewLayout);
            useLegacyPreviewLayout.BindValueChanged(_ => updatePreviewLayout(), true);
        }

        private void updatePreviewLayout()
        {
            bool legacyLayout = useLegacyPreviewLayout.Value;

            fallbackBackground.Alpha = legacyLayout ? 0.62f : 0;
            gradientOverlay.Alpha = legacyLayout ? 0 : 0.5f;

            previewFrame.Masking = legacyLayout;
            previewFrame.CornerRadius = legacyLayout ? Panel.CORNER_RADIUS : 0;

            backgroundSprite.UpdateTexture(legacyLayout);
        }

        protected override void Update()
        {
            base.Update();

            if (useLegacyPreviewLayout.Value)
            {
                // Preview is fixed to a 16:9 block on the left edge of the card.
                previewFrame.RelativeSizeAxes = Axes.Y;
                previewFrame.Anchor = Anchor.CentreLeft;
                previewFrame.Origin = Anchor.CentreLeft;
                previewFrame.Width = Math.Max(DrawHeight * legacy_preview_aspect_ratio, 1);
            }
            else
            {
                previewFrame.RelativeSizeAxes = Axes.Both;
                previewFrame.Anchor = Anchor.Centre;
                previewFrame.Origin = Anchor.Centre;
                previewFrame.Width = 1;
            }
        }

        public partial class PanelBeatmapBackground : Sprite
        {
            private readonly IWorkingBeatmap working;

            public PanelBeatmapBackground(IWorkingBeatmap working)
            {
                ArgumentNullException.ThrowIfNull(working);

                this.working = working;
            }

            /// <summary>
            /// The legacy layout requires the dedicated 16:9 preview texture; the panel texture is pre-cropped for wide panels
            /// and would display heavily zoomed-in inside a 16:9 frame.
            /// </summary>
            public void UpdateTexture(bool legacyLayout)
            {
                Texture = legacyLayout
                    ? working.GetLegacyPreviewBackground() ?? working.GetPanelBackground()
                    : working.GetPanelBackground();
            }
        }
    }
}
