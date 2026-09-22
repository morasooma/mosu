// Copyright (c) Morasooma contributors. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class SupporterQuestsStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.SupporterQuests";

        /// <summary>
        /// "Задания"
        /// </summary>
        public static LocalisableString Tasks => new TranslatableString(getKey(@"tasks"), @"Задания");

        /// <summary>
        /// "Текущее задание"
        /// </summary>
        public static LocalisableString Active => new TranslatableString(getKey(@"active"), @"Текущее задание");

        /// <summary>
        /// "Выполнено"
        /// </summary>
        public static LocalisableString Completed => new TranslatableString(getKey(@"completed"), @"Выполнено");

        /// <summary>
        /// "Откроется позже"
        /// </summary>
        public static LocalisableString Locked => new TranslatableString(getKey(@"locked"), @"Откроется позже");

        /// <summary>
        /// "Сейчас нет доступных заданий. Попробуйте обновить список позже."
        /// </summary>
        public static LocalisableString Empty => new TranslatableString(getKey(@"empty"), @"Сейчас нет доступных заданий. Попробуйте обновить список позже.");

        /// <summary>
        /// "Не удалось загрузить задания. Проверьте подключение и попробуйте ещё раз."
        /// </summary>
        public static LocalisableString LoadFailed => new TranslatableString(getKey(@"load_failed"), @"Не удалось загрузить задания. Проверьте подключение и попробуйте ещё раз.");

        /// <summary>
        /// "Обновить задания"
        /// </summary>
        public static LocalisableString Retry => new TranslatableString(getKey(@"retry"), @"Обновить задания");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
