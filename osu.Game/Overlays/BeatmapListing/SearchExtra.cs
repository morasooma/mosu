// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.ComponentModel;
using osu.Framework.Localisation;
using osu.Game.Resources.Localisation.Web;

namespace osu.Game.Overlays.BeatmapListing
{
    public enum SearchExtra
    {
        [Description("Server Exclusive"), global::osu.Framework.Localisation.LocalisableDescription(typeof(global::osu.Game.Localisation.ClientTranslationStrings), nameof(global::osu.Game.Localisation.ClientTranslationStrings.J3892))]
        ServerExclusive,

        [LocalisableDescription(typeof(BeatmapsStrings), nameof(BeatmapsStrings.ExtraVideo))]
        [Description("Has Video")]
        Video,

        [LocalisableDescription(typeof(BeatmapsStrings), nameof(BeatmapsStrings.ExtraStoryboard))]
        [Description("Has Storyboard")]
        Storyboard
    }
}
