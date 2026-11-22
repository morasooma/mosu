using System.Collections.Generic;
using osu.Framework.Localisation;
using osu.Game.Graphics.UserInterface;

namespace osu.Game.Screens.Select.Hinamizawa
{
    public partial class HinamizawaButton : OsuButton
    {
    }

    public partial class HinamizawaDropdown<T> : OsuDropdown<T> where T : notnull
    {
        public Dictionary<T, LocalisableString> Labels { get; } = new Dictionary<T, LocalisableString>();
        protected override LocalisableString GenerateItemText(T item) => Labels.TryGetValue(item, out LocalisableString label) ? label : base.GenerateItemText(item);
    }
}
