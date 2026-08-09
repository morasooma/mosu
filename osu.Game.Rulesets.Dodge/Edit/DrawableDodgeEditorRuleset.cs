// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.UI;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.UI;

namespace osu.Game.Rulesets.Dodge.Edit
{
    public partial class DrawableDodgeEditorRuleset : DrawableDodgeRuleset
    {
        private readonly DodgeEditorGrid positionSnapGrid;

        public DrawableDodgeEditorRuleset(Ruleset ruleset, IBeatmap beatmap, IReadOnlyList<Mod> mods, DodgeEditorGrid positionSnapGrid)
            : base((DodgeRuleset)ruleset, beatmap, mods)
        {
            this.positionSnapGrid = positionSnapGrid;
        }

        public override bool RequiresEditorReplay => false;

        protected override Playfield CreatePlayfield() => new DodgePlayfield(
            Beatmap.HitObjects,
            false,
            new Container
            {
                Size = DodgePlayfield.BASE_SIZE,
                Children = new Drawable[]
                {
                    new DodgeCoverageOverlay(),
                    new DodgeAutoplayRouteOverlay(),
                    positionSnapGrid,
                },
            },
            playerSize: DodgeBeatmapSettings.GetPlayerSize(Beatmap.Difficulty),
            optimiseArenaFill: false);

        public override PlayfieldAdjustmentContainer CreatePlayfieldAdjustmentContainer()
            => new DodgeEditorPlayfieldAdjustmentContainer();
    }
}
