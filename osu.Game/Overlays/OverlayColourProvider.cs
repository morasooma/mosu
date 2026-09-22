// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Bindables;
using osuTK.Graphics;

namespace osu.Game.Overlays
{
    public class OverlayColourProvider
    {
        /// <summary>
        /// The global theme mode bindable. When set to <see cref="ThemeMode.Dark"/>,
        /// all colours produced by any <see cref="OverlayColourProvider"/> become monochrome (grayscale)
        /// with darker backgrounds.
        /// </summary>
        public static readonly Bindable<ThemeMode> CurrentTheme = new Bindable<ThemeMode>(ThemeMode.Default);

        private readonly IBindable<ThemeMode> currentTheme;

        /// <summary>
        /// Indicates that palette values are currently being invalidated due to
        /// a theme change. UI controls can use this to skip interaction fades
        /// and apply the new palette in the same frame.
        /// </summary>
        public static bool IsThemeUpdate { get; private set; }

        public static double ThemeTransitionDuration(double normalDuration) => IsThemeUpdate ? 0 : normalDuration;

        private readonly BindableColour4 colour0 = new BindableColour4();
        private readonly BindableColour4 colour1 = new BindableColour4();
        private readonly BindableColour4 colour2 = new BindableColour4();
        private readonly BindableColour4 colour3 = new BindableColour4();
        private readonly BindableColour4 colour4 = new BindableColour4();
        private readonly BindableColour4 highlight1 = new BindableColour4();
        private readonly BindableColour4 content1 = new BindableColour4();
        private readonly BindableColour4 content2 = new BindableColour4();
        private readonly BindableColour4 light1 = new BindableColour4();
        private readonly BindableColour4 light2 = new BindableColour4();
        private readonly BindableColour4 light3 = new BindableColour4();
        private readonly BindableColour4 light4 = new BindableColour4();
        private readonly BindableColour4 dark1 = new BindableColour4();
        private readonly BindableColour4 dark2 = new BindableColour4();
        private readonly BindableColour4 dark3 = new BindableColour4();
        private readonly BindableColour4 dark4 = new BindableColour4();
        private readonly BindableColour4 dark5 = new BindableColour4();
        private readonly BindableColour4 dark6 = new BindableColour4();
        private readonly BindableColour4 foreground1 = new BindableColour4();
        private readonly BindableColour4 background1 = new BindableColour4();
        private readonly BindableColour4 background2 = new BindableColour4();
        private readonly BindableColour4 background3 = new BindableColour4();
        private readonly BindableColour4 background4 = new BindableColour4();
        private readonly BindableColour4 background5 = new BindableColour4();
        private readonly BindableColour4 background6 = new BindableColour4();

        /// <summary>
        /// The hue degree associated with the colour shades provided by this <see cref="OverlayColourProvider"/>.
        /// </summary>
        public int Hue { get; private set; }

        public OverlayColourProvider(OverlayColourScheme colourScheme)
            : this(colourScheme.GetHue())
        {
        }

        public OverlayColourProvider(int hue)
        {
            Hue = hue;

            // A bound copy is weakly held by the static source. Providers can
            // therefore be created by short-lived overlays without leaking
            // them through a global theme event.
            currentTheme = CurrentTheme.GetBoundCopy();
            currentTheme.BindValueChanged(_ => updateColours(), true);
        }

        // Note that the following five colours are also defined in `OsuColour` as `{colourScheme}{0,1,2,3,4}`.
        // The difference as to which should be used where comes down to context.
        // If the colour in question is supposed to always match the view in which it is displayed theme-wise, use `OverlayColourProvider`.
        // If the colour usage is special and in general differs from the surrounding view in choice of hue, use the `OsuColour` constants.
        public Color4 Colour0 => getColour(1, 0.8f);
        public Color4 Colour1 => getColour(1, 0.7f);
        public Color4 Colour2 => getColour(0.8f, 0.6f);
        public Color4 Colour3 => getColour(0.6f, 0.5f);
        public Color4 Colour4 => getColour(0.4f, 0.3f);

        public Color4 Highlight1 => getColour(1, 0.7f);
        public Color4 Content1 => getColour(0.4f, 1);
        public Color4 Content2 => getColour(0.4f, 0.9f);
        public Color4 Light1 => getColour(0.4f, 0.8f);
        public Color4 Light2 => getColour(0.4f, 0.75f);
        public Color4 Light3 => getColour(0.4f, 0.7f);
        public Color4 Light4 => getColour(0.4f, 0.5f);
        public Color4 Dark1 => getColour(0.2f, 0.35f);
        public Color4 Dark2 => getColour(0.2f, 0.3f);
        public Color4 Dark3 => getColour(0.2f, 0.25f);
        public Color4 Dark4 => getColour(0.2f, 0.2f);
        public Color4 Dark5 => getColour(0.2f, 0.15f);
        public Color4 Dark6 => getColour(0.2f, 0.1f);
        public Color4 Foreground1 => getColour(0.1f, 0.6f);
        public Color4 Background1 => getColour(0.1f, 0.4f);
        public Color4 Background2 => getColour(0.1f, 0.3f);
        public Color4 Background3 => getColour(0.1f, 0.25f);
        public Color4 Background4 => getColour(0.1f, 0.2f);
        public Color4 Background5 => getColour(0.1f, 0.15f);
        public Color4 Background6 => getColour(0.1f, 0.1f);

        /// <summary>
        /// Retrieves a weakly-bound copy of a palette colour. The copy changes
        /// whenever the global theme or this provider's hue changes.
        /// Hold the returned bindable for as long as the receiving drawable is
        /// alive, then bind its value to every derived colour/state that needs
        /// to update together.
        /// </summary>
        public IBindable<Framework.Graphics.Colour4> GetColourBindable(OverlayColour colour) => getBindable(colour).GetBoundCopy();

        /// <summary>
        /// Changes the <see cref="Hue"/> to a different degree.
        /// Note that this does not trigger any kind of signal to any drawable that received colours from here, all drawables need to be updated manually.
        /// </summary>
        /// <param name="colourScheme">The proposed colour scheme.</param>
        public void ChangeColourScheme(OverlayColourScheme colourScheme) => ChangeColourScheme(colourScheme.GetHue());

        /// <summary>
        /// Changes the <see cref="Hue"/> to a different degree.
        /// Note that this does not trigger any kind of signal to any drawable that received colours from here, all drawables need to be updated manually.
        /// </summary>
        /// <param name="hue">The proposed hue degree.</param>
        public void ChangeColourScheme(int hue)
        {
            if (Hue == hue)
                return;

            Hue = hue;
            updateColours();
        }

        /// <summary>
        /// Returns a colour as if <see cref="ThemeMode.Default"/> was active.
        /// Useful for UI surfaces that should remain dark even in <see cref="ThemeMode.Light"/>.
        /// </summary>
        public Color4 GetDefaultColour(float saturation, float lightness) =>
            Framework.Graphics.Colour4.FromHSL(Hue / 360f, saturation, lightness);

        /// <summary>
        /// Returns true when the global theme is set to <see cref="ThemeMode.Light"/>.
        /// </summary>
        public static bool IsLightTheme => CurrentTheme.Value == ThemeMode.Light;

        /// <summary>
        /// Returns true when the global theme is set to <see cref="ThemeMode.Dark"/>.
        /// </summary>
        public static bool IsDarkTheme => CurrentTheme.Value == ThemeMode.Dark;

        /// <summary>
        /// When <c>true</c>, colours ignore <see cref="ThemeMode.Light"/> adjustments and stay as <see cref="ThemeMode.Default"/>.
        /// Use for UI drawn on dark surfaces (song select panels over beatmap backgrounds).
        /// </summary>
        public bool IgnoreLightTheme
        {
            get => ignoreLightTheme;
            set
            {
                if (ignoreLightTheme == value)
                    return;

                ignoreLightTheme = value;
                updateColours();
            }
        }

        private bool ignoreLightTheme;

        private BindableColour4 getBindable(OverlayColour colour) => colour switch
        {
            OverlayColour.Colour0 => colour0,
            OverlayColour.Colour1 => colour1,
            OverlayColour.Colour2 => colour2,
            OverlayColour.Colour3 => colour3,
            OverlayColour.Colour4 => colour4,
            OverlayColour.Highlight1 => highlight1,
            OverlayColour.Content1 => content1,
            OverlayColour.Content2 => content2,
            OverlayColour.Light1 => light1,
            OverlayColour.Light2 => light2,
            OverlayColour.Light3 => light3,
            OverlayColour.Light4 => light4,
            OverlayColour.Dark1 => dark1,
            OverlayColour.Dark2 => dark2,
            OverlayColour.Dark3 => dark3,
            OverlayColour.Dark4 => dark4,
            OverlayColour.Dark5 => dark5,
            OverlayColour.Dark6 => dark6,
            OverlayColour.Foreground1 => foreground1,
            OverlayColour.Background1 => background1,
            OverlayColour.Background2 => background2,
            OverlayColour.Background3 => background3,
            OverlayColour.Background4 => background4,
            OverlayColour.Background5 => background5,
            OverlayColour.Background6 => background6,
            _ => throw new System.ArgumentOutOfRangeException(nameof(colour), colour, null),
        };

        private void updateColours()
        {
            bool wasThemeUpdate = IsThemeUpdate;
            IsThemeUpdate = true;

            try
            {
                colour0.Value = getColour(1, 0.8f);
                colour1.Value = getColour(1, 0.7f);
                colour2.Value = getColour(0.8f, 0.6f);
                colour3.Value = getColour(0.6f, 0.5f);
                colour4.Value = getColour(0.4f, 0.3f);
                highlight1.Value = getColour(1, 0.7f);
                content1.Value = getColour(0.4f, 1);
                content2.Value = getColour(0.4f, 0.9f);
                light1.Value = getColour(0.4f, 0.8f);
                light2.Value = getColour(0.4f, 0.75f);
                light3.Value = getColour(0.4f, 0.7f);
                light4.Value = getColour(0.4f, 0.5f);
                dark1.Value = getColour(0.2f, 0.35f);
                dark2.Value = getColour(0.2f, 0.3f);
                dark3.Value = getColour(0.2f, 0.25f);
                dark4.Value = getColour(0.2f, 0.2f);
                dark5.Value = getColour(0.2f, 0.15f);
                dark6.Value = getColour(0.2f, 0.1f);
                foreground1.Value = getColour(0.1f, 0.6f);
                background1.Value = getColour(0.1f, 0.4f);
                background2.Value = getColour(0.1f, 0.3f);
                background3.Value = getColour(0.1f, 0.25f);
                background4.Value = getColour(0.1f, 0.2f);
                background5.Value = getColour(0.1f, 0.15f);
                background6.Value = getColour(0.1f, 0.1f);

                colour0.TriggerChange();
                colour1.TriggerChange();
                colour2.TriggerChange();
                colour3.TriggerChange();
                colour4.TriggerChange();
                highlight1.TriggerChange();
                content1.TriggerChange();
                content2.TriggerChange();
                light1.TriggerChange();
                light2.TriggerChange();
                light3.TriggerChange();
                light4.TriggerChange();
                dark1.TriggerChange();
                dark2.TriggerChange();
                dark3.TriggerChange();
                dark4.TriggerChange();
                dark5.TriggerChange();
                dark6.TriggerChange();
                foreground1.TriggerChange();
                background1.TriggerChange();
                background2.TriggerChange();
                background3.TriggerChange();
                background4.TriggerChange();
                background5.TriggerChange();
                background6.TriggerChange();
            }
            finally
            {
                IsThemeUpdate = wasThemeUpdate;
            }
        }

        private Color4 getColour(float saturation, float lightness)
        {
            if (IgnoreLightTheme && CurrentTheme.Value == ThemeMode.Light)
                return Framework.Graphics.Colour4.FromHSL(Hue / 360f, saturation, lightness);

            if (CurrentTheme.Value == ThemeMode.Dark)
            {
                saturation = 0;
                if (lightness < 0.5f)
                    lightness *= 0.3f;
            }
            else if (CurrentTheme.Value == ThemeMode.Light)
            {
                // Backgrounds (low lightness in default) → light/white surfaces
                // Content/text (high lightness in default) → dark readable text
                if (lightness < 0.5f)
                    lightness = 0.91f - lightness * 0.35f;  // 0.1→0.875, 0.2→0.84, 0.3→0.805, 0.4→0.77
                else
                    lightness = (1f - lightness) * 0.55f;   // 0.5→0.275, 0.7→0.165, 0.8→0.11, 1.0→0.0
            }

            return Framework.Graphics.Colour4.FromHSL(Hue / 360f, saturation, lightness);
        }
    }
}
