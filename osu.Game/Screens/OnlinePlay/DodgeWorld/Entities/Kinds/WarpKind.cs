// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Editor;
using osu.Game.Screens.OnlinePlay.DodgeWorld.Model;
using osu.Game.Screens.OnlinePlay.DodgeWorld.View;

namespace osu.Game.Screens.OnlinePlay.DodgeWorld.Entities.Kinds
{
    /// <summary>
    /// A travel point a player opens once and can then return to for a fee.
    /// </summary>
    /// <remarks>
    /// Both prices belong to the world document, so the author sets them and the server charges them.
    /// A starting location is not a special kind of warp — it is one whose prices are zero.
    /// </remarks>
    internal sealed class WarpKind : WorldEntityKind<WorldWarp>
    {
        public override string Kind => EntityKinds.WARP;

        protected override WorldWarp CreateEntity(EntityRecord record, WorldEntityContext context) =>
            new WorldWarp(context.Colours, context.IsEditing, context.Select);

        protected override void Read(WorldWarp entity, EntityRecord record, WorldEntityContext context)
        {
            entity.UnlockCost = Math.Clamp(record.WarpUnlockCost ?? 0, 0, WarpCatalogue.MAXIMUM_COST);
            entity.TravelCost = Math.Clamp(record.WarpTravelCost ?? 0, 0, WarpCatalogue.MAXIMUM_COST);
            entity.PricesChanged();
        }

        protected override void Write(WorldWarp entity, EntityRecord record)
        {
            record.WarpUnlockCost = entity.UnlockCost;
            record.WarpTravelCost = entity.TravelCost;
        }

        protected override void Initialise(EntityRecord record)
        {
            record.WarpUnlockCost = 0;
            record.WarpTravelCost = 0;
        }

        protected override IEnumerable<EntityField> DescribeFields()
        {
            yield return Field("Цена открытия", "монет один раз, чтобы игрок открыл этот варп; 0 — бесплатно",
                warp => EditorValue.Format(warp.UnlockCost),
                (warp, text) =>
                {
                    warp.UnlockCost = EditorValue.Int(text, warp.UnlockCost, 0, WarpCatalogue.MAXIMUM_COST);
                    warp.PricesChanged();
                });

            yield return Field("Цена перехода", "монет за каждый переход сюда; 0 — бесплатно",
                warp => EditorValue.Format(warp.TravelCost),
                (warp, text) =>
                {
                    warp.TravelCost = EditorValue.Int(text, warp.TravelCost, 0, WarpCatalogue.MAXIMUM_COST);
                    warp.PricesChanged();
                });
        }
    }
}
