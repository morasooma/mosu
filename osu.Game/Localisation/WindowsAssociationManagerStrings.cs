// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class WindowsAssociationManagerStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.WindowsAssociationManager";

        /// <summary>
        /// "Morasooma Beatmap"
        /// </summary>
        public static LocalisableString OsuBeatmap => new TranslatableString(getKey(@"osu_beatmap"), @"Morasooma Beatmap");

        /// <summary>
        /// "Morasooma Replay"
        /// </summary>
        public static LocalisableString OsuReplay => new TranslatableString(getKey(@"osu_replay"), @"Morasooma Replay");

        /// <summary>
        /// "Morasooma Skin"
        /// </summary>
        public static LocalisableString OsuSkin => new TranslatableString(getKey(@"osu_skin"), @"Morasooma Skin");

        /// <summary>
        /// "Morasooma"
        /// </summary>
        public static LocalisableString OsuProtocol => new TranslatableString(getKey(@"osu_protocol"), @"Morasooma");

        /// <summary>
        /// "Morasooma Multiplayer"
        /// </summary>
        public static LocalisableString OsuMultiplayer => new TranslatableString(getKey(@"osu_multiplayer"), @"Morasooma Multiplayer");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
