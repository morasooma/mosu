// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using osu.Game.Beatmaps;
using osu.Game.Rulesets.Dodge.Objects;
using osu.Game.Rulesets.Dodge.Replays;
using osu.Game.Rulesets.Mods;

namespace osu.Game.Rulesets.Dodge.Mods
{
    public class DodgeModCinema : ModCinema<DodgeHitObject>
    {
        public override ModReplayData CreateReplayData(IBeatmap beatmap, IReadOnlyList<Mod> mods)
            => new ModReplayData(new DodgeAutoGenerator(beatmap).Generate(), new ModCreatedUser { Username = "DodgeBot" });
    }
}
