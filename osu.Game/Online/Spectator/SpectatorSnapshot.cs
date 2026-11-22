using System;
using System.Collections.Generic;
using System.Linq;
using osu.Game.Configuration;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer.MatchTypes.TagCoop;
using osu.Game.Rulesets.Scoring;

namespace osu.Game.Online.Spectator
{
    /// <summary>
    /// Detaches mutable Update-owned state before outgoing worker serialization. Matches the
    /// mod formatter's underlying-value conversion without serializing on the caller.
    /// </summary>
    internal static class SpectatorSnapshot
    {
        internal static SpectatorState State(SpectatorState state) => new SpectatorState
        {
            BeatmapID = state.BeatmapID,
            BeatmapChecksum = state.BeatmapChecksum,
            RulesetID = state.RulesetID,
            State = state.State,
            Mods = Mods(state.Mods),
            MaximumStatistics = state.MaximumStatistics == null ? new Dictionary<HitResult, int>() : new Dictionary<HitResult, int>(state.MaximumStatistics),
            TagCoopPlayers = state.TagCoopPlayers?.ToArray() ?? Array.Empty<TagCoopReplayPlayer>(),
        };

        internal static APIMod[] Mods(IEnumerable<APIMod> mods) => mods.Select(mod => new APIMod
        {
            Acronym = mod.Acronym,
            Settings = mod.Settings.ToDictionary(pair => pair.Key, pair => pair.Value.GetUnderlyingSettingValue()),
        }).ToArray();
    }
}
