// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Rulesets.Mods
{
    /// <summary>
    /// Marks a mod implemented for compatibility with the Torii server.
    /// The implementation remains registered for API/replay deserialisation, but is only
    /// exposed for user selection while connected to Torii.
    /// </summary>
    public interface IToriiServerMod
    {
    }
}
