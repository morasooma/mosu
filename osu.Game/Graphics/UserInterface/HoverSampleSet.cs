// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.ComponentModel;

namespace osu.Game.Graphics.UserInterface
{
    public enum HoverSampleSet
    {
        [Description("default"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3592))]
        Default,

        [Description("button"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3593))]
        Button,

        [Description("button-sidebar"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3594))]
        ButtonSidebar,

        [Description("tabselect"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3595))]
        TabSelect,

        [Description("dialog-cancel"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3596))]
        DialogCancel,

        [Description("dialog-ok"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3597))]
        DialogOk,

        [Description("menu-open"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3598))]
        MenuOpen,
    }

    public static class HoverSampleSetExtensions
    {
        public static string GetResourceName(this HoverSampleSet value)
        {
            switch (value)
            {
                case HoverSampleSet.Default:
                    return "default";

                case HoverSampleSet.Button:
                    return "button";

                case HoverSampleSet.ButtonSidebar:
                    return "button-sidebar";

                case HoverSampleSet.TabSelect:
                    return "tabselect";

                case HoverSampleSet.DialogCancel:
                    return "dialog-cancel";

                case HoverSampleSet.DialogOk:
                    return "dialog-ok";

                case HoverSampleSet.MenuOpen:
                    return "menu-open";

                default:
                    throw new ArgumentOutOfRangeException(nameof(value));
            }
        }
    }
}
