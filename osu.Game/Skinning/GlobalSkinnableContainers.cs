// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Represents a particular area or part of a game screen whose layout can be customised using the skin editor.
    /// </summary>
    public enum GlobalSkinnableContainers
    {
        [Description("HUD"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5341))]
        MainHUDComponents,

        [Description("Song select"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5342))]
        SongSelect,

        [Description("Playfield"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J5343))]
        Playfield
    }
}
