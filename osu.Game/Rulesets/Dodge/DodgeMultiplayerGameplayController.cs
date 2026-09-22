// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Rulesets.UI;
using osuTK;
using osuTK.Graphics;

namespace osu.Game.Rulesets.Dodge
{
    /// <summary>
    /// Configuration shared between multiplayer and the Dodge ruleset implementation.
    /// </summary>
    public class DodgeMultiplayerGameplayConfiguration
    {
        public required int LocalUserId { get; init; }

        public required IReadOnlyDictionary<int, string> Usernames { get; init; }

        public required IReadOnlyDictionary<int, Color4> PlayerColours { get; init; }

        public required IBindable<bool> IsBreakTime { get; init; }
    }

    /// <summary>
    /// Implemented by a ruleset which can display live multiplayer player positions.
    /// </summary>
    public interface IDodgeMultiplayerRuleset
    {
        DodgeMultiplayerGameplayController CreateDodgeMultiplayerGameplayController(
            DrawableRuleset drawableRuleset,
            DodgeMultiplayerGameplayConfiguration configuration);
    }

    /// <summary>
    /// Bridges ruleset-specific player visuals with the generic multiplayer screen.
    /// </summary>
    public abstract partial class DodgeMultiplayerGameplayController : CompositeDrawable
    {
        /// <summary>
        /// The current local player position, normalised to the 512x384 Dodge playfield.
        /// </summary>
        public abstract Vector2 LocalPlayerPosition { get; }

        public abstract void SetLocalPing(ushort pingMilliseconds);

        public abstract void PushRemotePlayerState(int userId, uint sequence, Vector2 normalisedPosition, ushort pingMilliseconds);
    }
}
