// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Allocation;
using osu.Framework.Input.Bindings;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Dodge
{
    [Cached]
    public partial class DodgeInputManager : RulesetInputManager<DodgeAction>
    {
        public DodgeInputManager(RulesetInfo ruleset)
            : base(ruleset, 0, SimultaneousBindingMode.Unique)
        {
        }
    }

    public enum DodgeAction
    {
        [Description("Move left")]
        MoveLeft,

        [Description("Move right")]
        MoveRight,

        [Description("Move up")]
        MoveUp,

        [Description("Move down")]
        MoveDown,

        [Description("Slow movement")]
        Slow,
    }
}
