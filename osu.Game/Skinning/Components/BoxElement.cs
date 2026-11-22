// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Overlays.Settings;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Skinning.Components
{
    public partial class BoxElement : CompositeDrawable, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.CornerRadius), nameof(SkinnableComponentStrings.CornerRadiusDescription),
            SettingControlType = typeof(SettingsPercentageSlider<float>))]
        public new BindableFloat CornerRadius { get; } = new BindableFloat(0.25f)
        {
            MinValue = 0,
            MaxValue = 0.5f,
            Precision = 0.01f
        };

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Colour4.White);

        private readonly SkinComponentColourProvider themeColours = new SkinComponentColourProvider();

        public BoxElement()
        {
            Size = new Vector2(400, 80);

            InternalChildren = new Drawable[]
            {
                new Box
                {
                    Colour = Color4.White,
                    RelativeSizeAxes = Axes.Both,
                },
            };

            Masking = true;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            void updateColour() => Colour = themeColours.GetColour(Overlays.OverlayColour.Background3, AccentColour);
            AccentColour.BindValueChanged(_ => updateColour());
            themeColours.Bind(action => Scheduler.Add(action), updateColour);
        }

        protected override void Update()
        {
            base.Update();

            base.CornerRadius = CornerRadius.Value * Math.Min(DrawWidth, DrawHeight);
        }
    }
}
