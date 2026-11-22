using System.Collections.Generic;
using osu.Framework.Input.Bindings;

namespace osu.Game.Rulesets.UI
{
    public interface IHasGameplayKeyBindings
    {
        IEnumerable<IKeyBinding> GameplayKeyBindings { get; }
    }
}
