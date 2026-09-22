// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Textures
{
    /// <summary>
    /// One entry in the editor's texture browser.
    /// </summary>
    /// <param name="Id">Library key. For an imported image this is also its <paramref name="Path"/>.</param>
    /// <param name="Name">Name shown on the card.</param>
    /// <param name="Path">
    /// Storage path or URL the world should reference, or <c>null</c> for a texture that ships with
    /// the client and therefore cannot be stored in a world document.
    /// </param>
    /// <param name="BuiltIn">Whether this ships with the client.</param>
    internal sealed record TextureLibraryEntry(string Id, string Name, string? Path, bool BuiltIn);
}
