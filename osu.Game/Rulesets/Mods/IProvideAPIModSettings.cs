// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Allows a mod to extend the default <see cref="Online.API.APIMod"/> serialisation process
    /// with derived or implementation-specific settings that are not exposed via public setting bindables.
    /// </summary>
    public interface IProvideAPIModSettings
    {
        /// <summary>
        /// Adds extra serialisable settings to the outgoing API settings dictionary.
        /// </summary>
        void AddAPIModSettings(Dictionary<string, object> settings);

        /// <summary>
        /// Attempts to import an extra setting that was not matched to a public setting bindable.
        /// </summary>
        /// <returns>Whether the setting was recognised and applied.</returns>
        bool TryApplyAPIModSetting(string settingKey, object settingValue);
    }
}
