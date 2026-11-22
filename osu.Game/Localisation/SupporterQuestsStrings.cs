// Copyright (c) Morasooma contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SupporterQuestsStrings
    {
        private const string prefix = @"osu.Game.Localisation.SupporterQuests";

        public static LocalisableString Tasks => new TranslatableString($@"{prefix}:tasks", @"Quests");

        public static LocalisableString Active => new TranslatableString($@"{prefix}:active", @"Current quest");

        public static LocalisableString Completed => new TranslatableString($@"{prefix}:completed", @"Complete");

        public static LocalisableString Locked => new TranslatableString($@"{prefix}:locked", @"Unlocks later");

        public static LocalisableString Empty => new TranslatableString($@"{prefix}:empty", @"No quests are available. Try refreshing later.");

        public static LocalisableString LoadFailed => new TranslatableString($@"{prefix}:load_failed", @"Could not load quests. Check your connection and try again.");

        public static LocalisableString Retry => new TranslatableString($@"{prefix}:retry", @"Refresh quests");

        public static LocalisableString ShowAll(int count) => new TranslatableString($@"{prefix}:show_all", @"Show all quests ({0})", count);

        public static LocalisableString OtherQuests => new TranslatableString($@"{prefix}:other_quests", @"Other quests");

        public static LocalisableString CollapseAll => new TranslatableString($@"{prefix}:collapse_all", @"Collapse quest list");

        public static LocalisableString AllCompleted => new TranslatableString($@"{prefix}:all_completed", @"All quests complete. Expand to view the full list.");
    }
}
