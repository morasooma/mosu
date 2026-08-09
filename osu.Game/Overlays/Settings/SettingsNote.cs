// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Containers;
using osuTK.Graphics;

namespace osu.Game.Overlays.Settings
{
    public sealed partial class SettingsNote : CompositeDrawable
    {
        public readonly Bindable<Data?> Current = new Bindable<Data?>();

        private Box background = null!;
        private Box accentBar = null!;
        private OsuTextFlowContainer text = null!;
        private IBindable<Colour4> themeColour = null!;

        [Resolved]
        private OsuColour colours { get; set; } = null!;

        [Resolved]
        private OverlayColourProvider colourProvider { get; set; } = null!;

        [BackgroundDependencyLoader]
        private void load()
        {
            AutoSizeDuration = 300;
            AutoSizeEasing = Easing.OutQuint;

            InternalChild = new Container
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Padding = new MarginPadding { Top = SettingsSection.ITEM_SPACING_V2 },
                Child = new Container
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    CornerRadius = 5,
                    CornerExponent = 2.5f,
                    Masking = true,
                    Children = new Drawable[]
                    {
                        background = new Box
                        {
                            Colour = Color4.Black,
                            RelativeSizeAxes = Axes.Both,
                        },
                        accentBar = new Box
                        {
                            Width = 4,
                            RelativeSizeAxes = Axes.Y,
                            Alpha = 0,
                        },
                        text = new OsuTextFlowContainer(s => s.Font = OsuFont.Style.Caption1.With(weight: FontWeight.SemiBold))
                        {
                            Padding = new MarginPadding(8),
                            RelativeSizeAxes = Axes.X,
                            AutoSizeAxes = Axes.Y,
                        },
                    }
                },
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            Current.BindValueChanged(_ => updateDisplay(), true);
            themeColour = colourProvider.GetColourBindable(OverlayColour.Content1);
            themeColour.BindValueChanged(_ => updateDisplay());
            FinishTransforms(true);
        }

        private void updateDisplay()
        {
            // Explicitly use ClearTransforms to clear any existing auto-size transform before modifying size / flag.
            // TODO: This is dodgy as hell and needs to go.
            ClearTransforms(false, @"baseSize");
            ClearTransforms(false, nameof(Height));

            if (Current.Value == null)
            {
                AutoSizeAxes = Axes.None;
                this.ResizeHeightTo(0, 300, Easing.OutQuint);
                this.FadeOut(250, Easing.OutQuint);
                return;
            }

            AutoSizeAxes = Axes.Y;
            this.FadeIn(250, Easing.OutQuint);

            switch (Current.Value.Type)
            {
                case Type.Informational:
                    background.Colour = colourProvider.Dark2;
                    text.Colour = colourProvider.Content2;
                    break;

                case Type.Warning:
                    background.Colour = colours.Orange1;
                    text.Colour = colourProvider.Background5;
                    break;

                case Type.Critical:
                    background.Colour = colours.Red1;
                    text.Colour = colourProvider.Background5;
                    break;
            }

            applyAccent(Current.Value.ColourAccent);
            text.Text = Current.Value.Text;
        }

        private void applyAccent(Accent accent)
        {
            accentBar.Alpha = accent == Accent.None ? 0 : 1;

            switch (accent)
            {
                case Accent.None:
                    return;

                case Accent.HighGain:
                    background.Colour = colours.Green4;
                    accentBar.Colour = colours.Green1;
                    text.Colour = colours.Green0;
                    break;

                case Accent.MediumGain:
                    background.Colour = colours.Lime4;
                    accentBar.Colour = colours.Lime1;
                    text.Colour = colours.Lime0;
                    break;

                case Accent.SmallGain:
                    background.Colour = colours.Blue4;
                    accentBar.Colour = colours.Blue1;
                    text.Colour = colours.Blue0;
                    break;

                case Accent.Latency:
                    background.Colour = colours.Orange4;
                    accentBar.Colour = colours.Orange1;
                    text.Colour = colours.Orange0;
                    break;

                case Accent.TradeOff:
                    background.Colour = colours.DarkOrange4;
                    accentBar.Colour = colours.DarkOrange1;
                    text.Colour = colours.DarkOrange0;
                    break;

                case Accent.Situational:
                    background.Colour = colours.Purple4;
                    accentBar.Colour = colours.Purple1;
                    text.Colour = colours.Purple0;
                    break;

                case Accent.Negative:
                    background.Colour = colours.Red4;
                    accentBar.Colour = colours.Red1;
                    text.Colour = colours.Red0;
                    break;
            }
        }

        public record Data(LocalisableString Text, Type Type, Accent ColourAccent = Accent.None);

        public enum Type
        {
            Informational,
            Warning,
            Critical,
        }

        public enum Accent
        {
            None,
            HighGain,
            MediumGain,
            SmallGain,
            Latency,
            TradeOff,
            Situational,
            Negative,
        }
    }
}
