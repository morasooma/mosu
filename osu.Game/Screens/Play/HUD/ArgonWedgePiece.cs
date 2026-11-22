// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Colour;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Game.Configuration;
using osu.Game.Localisation.SkinComponents;
using osu.Game.Performance;
using osu.Game.Skinning;
using osuTK;

namespace osu.Game.Screens.Play.HUD
{
    public partial class ArgonWedgePiece : CompositeDrawable, ISerialisableDrawable
    {
        public bool UsesFixedAnchor { get; set; }

        [SettingSource(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5084))]
        public BindableBool InvertShear { get; } = new BindableBool();

        [SettingSource(typeof(SkinnableComponentStrings), nameof(SkinnableComponentStrings.Colour))]
        public BindableColour4 AccentColour { get; } = new BindableColour4(Color4Extensions.FromHex("#66CCFF"));

        private readonly SkinComponentColourProvider themeColours = new SkinComponentColourProvider();

        public ArgonWedgePiece()
        {
            CornerRadius = 10f;

            Size = new Vector2(400, 100);
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            Masking = true;
            Shear = new Vector2(0.8f, 0f);

            InternalChild = new Box
            {
                RelativeSizeAxes = Axes.Both,
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            InvertShear.BindValueChanged(v => Shear = new Vector2(0.8f, 0f) * (v.NewValue ? -1 : 1), true);
            void updateColour()
            {
                var colour = themeColours.GetColour(Overlays.OverlayColour.Highlight1, AccentColour);
                InternalChild.Colour = ColourInfo.GradientVertical(colour.Opacity(0), colour.Opacity(0.25f));
            }
            AccentColour.BindValueChanged(_ => updateColour());
            themeColours.Bind(action => Scheduler.Add(action), updateColour);

            // Purely decorative translucent gradient (max 25% alpha) measured at ~99k px/frame for the two
            // default-layout instances — the largest HUD fill item. Skin reloads on perf-setting changes,
            // so a load-time read is sufficient (same contract as ArgonCounterTextComponent's wireframes).
            if (SkinPerformanceMode.ShouldSimplifyHud && MosuOptimisationToggles.PerfHideHudWedges)
                Alpha = 0;
        }
    }
}
