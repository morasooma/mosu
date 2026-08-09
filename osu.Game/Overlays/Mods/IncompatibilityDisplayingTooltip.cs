// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Play.HUD;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Overlays.Mods
{
    internal partial class IncompatibilityDisplayingTooltip : ModButtonTooltip
    {
        private readonly OsuSpriteText incompatibleText;
        private readonly OverlayColourProvider colourProvider;
        private readonly IBindable<Colour4> themeColour;

        private readonly Bindable<IReadOnlyList<Mod>> incompatibleMods = new Bindable<IReadOnlyList<Mod>>();

        [Resolved]
        private Bindable<RulesetInfo> ruleset { get; set; } = null!;

        public IncompatibilityDisplayingTooltip(OverlayColourProvider colourProvider)
            : base(colourProvider)
        {
            this.colourProvider = colourProvider;
            AddRange(new Drawable[]
            {
                incompatibleText = new OsuSpriteText
                {
                    Margin = new MarginPadding { Top = 5 },
                    Colour = colourProvider.Content2,
                    Font = OsuFont.GetFont(weight: FontWeight.Regular),
                    Text = "Incompatible with:"
                },
                new ModDisplay
                {
                    Current = incompatibleMods,
                    ExpansionMode = ExpansionMode.AlwaysExpanded,
                    Scale = new Vector2(0.7f)
                }
            });

            themeColour = colourProvider.GetColourBindable(OverlayColour.Content2);
            themeColour.BindValueChanged(_ => incompatibleText.Colour = this.colourProvider.Content2, true);
        }

        protected override void UpdateDisplay(Mod mod)
        {
            base.UpdateDisplay(mod);

            var incompatibleTypes = mod.IncompatibleMods;

            var allMods = ruleset.Value.CreateInstance().AllMods;

            incompatibleMods.Value = allMods.Where(m => m.GetType() != mod.GetType() && (incompatibleTypes.Any(t => t.IsInstanceOfType(m)) || (m as Mod)?.IncompatibleMods.Any(t => t.IsInstanceOfType(mod)) == true)).Select(m => m.CreateInstance()).ToList();
            incompatibleText.Text = incompatibleMods.Value.Any() ? "Incompatible with:" : "Compatible with all mods";
        }
    }
}
