// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds
{
    /// <summary>
    /// A service kiosk: a quest board, a shop, a notice board or a workshop.
    /// </summary>
    /// <remarks>
    /// Which one it is comes from the record's <c>style</c>, the same field every other appearance choice
    /// uses. It used to be keyed off the entity's id, which meant only two ids could exist, renaming one
    /// silently changed how it looked, and "add a kiosk" could only ever produce a quest board — the id it
    /// was given was not one the code recognised.
    /// <para>
    /// A record from before that field carried a style falls back to the old rule, so the accessory shop in
    /// every published world keeps looking like a shop.
    /// </para>
    /// </remarks>
    internal sealed class TerminalKind : WorldEntityKind<WorldTerminal>
    {
        /// <summary>The id the accessory shop was recognised by before kiosks had a style.</summary>
        private const string legacy_shop_id = "accessory-shop";

        public override string Kind => EntityKinds.TERMINAL;

        /// <summary>A kiosk stood along a wall has to face along it, and now its collision faces the same way.</summary>
        protected override bool SupportsRotation => true;

        protected override WorldTerminal CreateEntity(EntityRecord record, WorldEntityContext context) =>
            new WorldTerminal(context.Colours, context.IsEditing, context.Select, StyleOf(record));

        internal static int StyleOf(EntityRecord record) =>
            record.Style ?? (record.Id == legacy_shop_id ? TerminalVariants.SHOP : TerminalVariants.QUEST_BOARD);

        protected override void Initialise(EntityRecord record) => record.Style = TerminalVariants.QUEST_BOARD;
    }
}
