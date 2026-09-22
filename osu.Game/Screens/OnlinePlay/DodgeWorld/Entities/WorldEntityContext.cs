// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Graphics.Textures;
using osu.Game.Graphics;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities
{
    /// <summary>
    /// Everything an <see cref="IWorldEntityKind"/> needs to build a drawable, supplied by the screen.
    /// </summary>
    /// <param name="Colours">Palette for entity visuals.</param>
    /// <param name="Textures">Store holding textures shipped with the client.</param>
    /// <param name="IsEditing">Whether the editor is currently open.</param>
    /// <param name="Select">Called by an entity when the user picks it in the editor.</param>
    /// <param name="DialogueLanguage">The language dialogue should be shown in.</param>
    /// <param name="RequestTexture">
    /// Asks the screen to resolve and apply an entity's configured texture. Loading needs the
    /// screen's texture stores, so a kind can only request it.
    /// </param>
    internal sealed record WorldEntityContext(
        OsuColour Colours,
        LargeTextureStore Textures,
        Func<bool> IsEditing,
        Action<EditableWorldEntity> Select,
        Func<string> DialogueLanguage,
        Action<ITexturedEntity> RequestTexture);
}
