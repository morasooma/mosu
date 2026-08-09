// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Utils;
using osu.Game.Graphics;
using osu.Game.Overlays;
using osu.Game.Rulesets.Mods;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.UI
{
    public partial class ModSwitchSmall : CompositeDrawable
    {
        public BindableBool Active { get; } = new BindableBool();

        public const float DEFAULT_SIZE = 60;

        private readonly IMod mod;

        private Drawable background = null!;
        private SpriteIcon? modIcon;

        private Color4 activeForegroundColour;
        private Color4 inactiveForegroundColour;

        private Color4 activeBackgroundColour;
        private Color4 inactiveBackgroundColour;
        private IBindable<Colour4>? themeColour;
        private OverlayColourProvider? overlayColours;
        private OsuColour? osuColours;

        public ModSwitchSmall(IMod mod)
        {
            this.mod = mod;

            Size = new Vector2(DEFAULT_SIZE);
        }

        [BackgroundDependencyLoader]
        private void load(TextureStore textures, OsuColour colours, OverlayColourProvider? colourProvider)
        {
            FillFlowContainer contentFlow;
            ModSwitchTiny tinySwitch;

            InternalChildren = new[]
            {
                background = new Sprite
                {
                    RelativeSizeAxes = Axes.Both,
                    FillMode = FillMode.Fit,
                    Texture = textures.Get("Icons/BeatmapDetails/mod-icon"),
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                },
                contentFlow = new FillFlowContainer
                {
                    AutoSizeAxes = Axes.Both,
                    Anchor = Anchor.Centre,
                    Origin = Anchor.Centre,
                    Direction = FillDirection.Vertical,
                    Child = tinySwitch = new ModSwitchTiny(mod)
                    {
                        Anchor = Anchor.TopCentre,
                        Origin = Anchor.TopCentre,
                        Scale = new Vector2(0.6f),
                        Active = { BindTarget = Active }
                    }
                }
            };

            if (mod.Icon != null)
            {
                contentFlow.Insert(-1, modIcon = new SpriteIcon
                {
                    Anchor = Anchor.TopCentre,
                    Origin = Anchor.TopCentre,
                    Size = new Vector2(37, 26) * mod.IconScale,
                    // arbitrary adjustment for better vertical alignment
                    Margin = new MarginPadding { Top = -1 },
                    Icon = mod.Icon.Value
                });
                tinySwitch.Scale = new Vector2(0.3f);
            }

            overlayColours = colourProvider;
            osuColours = colours;
            var modTypeColour = colours.ForModType(mod.Type);
            activeForegroundColour = modTypeColour;
            activeBackgroundColour = Interpolation.ValueAt<Colour4>(0.1f, Colour4.Black, modTypeColour, 0, 1);

            if (colourProvider != null)
            {
                themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
                themeColour.BindValueChanged(_ => updateThemeColours(), true);
            }
            else
                updateThemeColours();
        }

        private void updateThemeColours()
        {
            if (overlayColours != null && OverlayColourProvider.IsDarkTheme)
            {
                inactiveForegroundColour = overlayColours.Light4;
                inactiveBackgroundColour = overlayColours.Background2;
            }
            else if (overlayColours != null && OverlayColourProvider.IsLightTheme)
            {
                inactiveForegroundColour = overlayColours.GetDefaultColour(0.4f, 0.5f);
                inactiveBackgroundColour = overlayColours.GetDefaultColour(0.1f, 0.3f);
            }
            else
            {
                inactiveForegroundColour = overlayColours?.Background5 ?? osuColours!.Gray3;
                inactiveBackgroundColour = overlayColours?.Background2 ?? osuColours!.Gray5;
            }

            if (IsLoaded)
                updateState();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Active.BindValueChanged(_ => updateState(), true);
            FinishTransforms(true);
        }

        private void updateState()
        {
            modIcon?.FadeColour(Active.Value ? activeForegroundColour : inactiveForegroundColour, 200, Easing.OutQuint);
            background.FadeColour(Active.Value ? activeBackgroundColour : inactiveBackgroundColour, 200, Easing.OutQuint);
        }
    }
}
