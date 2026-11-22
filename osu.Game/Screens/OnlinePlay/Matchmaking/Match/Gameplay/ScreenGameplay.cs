// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using osu.Framework.Screens;
using osu.Game.Online.API;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Scoring;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay;
using osu.Game.Screens.OnlinePlay.Multiplayer;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.Match.Gameplay
{
    public partial class ScreenGameplay : MultiplayerPlayer
    {
        /// <summary>
        /// This is a Ranked Play session — all mod multipliers are forced to 1.00x.
        /// </summary>
        protected override bool IsRankedPlaySession => true;

        private readonly LocalRankedPlayVisualMods localVisualMods;

        protected override Mod[] CreateLocalVisualMods(Ruleset currentRuleset, IReadOnlyList<Mod> gameplayMods)
            => localVisualMods.Resolve(currentRuleset, gameplayMods);

        public ScreenGameplay(Room room, PlaylistItem playlistItem, MultiplayerRoomUser[] users,
                              LocalRankedPlayVisualMods? localVisualMods = null)
            : base(room, playlistItem, users, showFailingOverlay: false)
        {
            this.localVisualMods = localVisualMods ?? new LocalRankedPlayVisualMods();
        }

        protected override async Task PrepareScoreForResultsAsync(Score score)
        {
            try
            {
                await base.PrepareScoreForResultsAsync(score).ConfigureAwait(false);
            }
            finally
            {
                // Submission/state-change failures must not strand the ranked gameplay screen.
                Scheduler.Add(() =>
                {
                    if (this.IsCurrentScreen())
                        this.Exit();
                });
            }
        }
    }
}
