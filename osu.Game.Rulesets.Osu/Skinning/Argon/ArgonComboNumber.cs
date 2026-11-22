using System.Collections.Generic;
using System.Linq;
using osu.Game.Rulesets.Mods;
using osu.Game.Skinning;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Configuration;
using osu.Game.Graphics.Sprites;

namespace osu.Game.Rulesets.Osu.Skinning.Argon
{
    public partial class ArgonComboNumber : OsuSpriteText
    {
        private Bindable<bool>? visible;

        private bool hiddenByMod;

        public override bool IsPresent => !hiddenByMod && (visible?.Value ?? true) && base.IsPresent;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager? config, IReadOnlyList<Mod>? mods)
        {
            hiddenByMod = mods?.OfType<IHidesSkinElements>().Any(mod => mod.Hides(OsuSetting.ForkShowHitCircleNumbers)) == true;
            visible = config?.GetBindable<bool>(OsuSetting.ForkShowHitCircleNumbers);
            visible?.BindValueChanged(_ => Invalidate(Invalidation.Presence));
        }
    }
}
