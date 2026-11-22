using System;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.Sprites;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.Footer;
using osu.Game.Screens.OnlinePlay.Playlists;
using System.Linq;

namespace osu.Game.Screens.OnlinePlay.DailyChallenge
{
    public partial class DailyChallengeProposalSongSelect : PlaylistsSongSelect
    {
        private readonly bool directlyApprove;
        private readonly DailyChallengeProposal? editing;
        private string requestKey = Guid.NewGuid().ToString("N");
        private string? previousPayload;
        private bool saving;
        private OsuSpriteText status = null!;
        private MutateDailyChallengeProposalRequest? request;

        [Resolved] private IAPIProvider api { get; set; } = null!;
        [Resolved] private BeatmapManager beatmaps { get; set; } = null!;
        [Resolved] private RulesetStore rulesets { get; set; } = null!;

        public override bool DisallowExternalBeatmapRulesetChanges => true;

        public override void OnEntering(ScreenTransitionEvent e)
        {
            // The daily lobby owns a lease on Mods. Use that leased branch and enable edits before
            // song select binds its overlays, whose ordinary bindables cannot write while disabled.
            Mods.Disabled = false;
            base.OnEntering(e);
        }

        protected override bool ShowPlaylistTray => false;
        public override string Title => "Daily challenge proposal";

        public DailyChallengeProposalSongSelect(bool directlyApprove = false, DailyChallengeProposal? editing = null)
            : base(new Room { Type = MatchType.Playlists }, editing != null ? DailyChallengeProposalStrings.Save :
                directlyApprove ? DailyChallengeProposalStrings.AddToQueue : DailyChallengeProposalStrings.Submit)
        {
            this.directlyApprove = directlyApprove;
            this.editing = editing;
            // A daily challenge stays on the selected difficulty/ruleset. Free mods remain configurable.
            Freestyle.Value = false;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            AddInternal(status = new OsuSpriteText
            {
                Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft,
                Margin = new MarginPadding { Left = 35, Bottom = ScreenFooterButton.HEIGHT + 20 },
                Text = DailyChallengeProposalStrings.SelectionHint,
            });
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            // Do not inherit the current daily challenge's mandatory mods into a new proposal.
            if (editing == null)
            {
                Mods.Value = Array.Empty<Mod>();
                return;
            }
            var local = beatmaps.QueryOnlineBeatmapId(editing.BeatmapID);
            if (local != null) Beatmap.Value = beatmaps.GetWorkingBeatmap(local);
            var ruleset = rulesets.GetRuleset(editing.RulesetID);
            if (ruleset == null) return;
            Ruleset.Value = ruleset;
            Freestyle.Value = editing.Freestyle;
            var instance = ruleset.CreateInstance();
            Mods.Value = editing.RequiredMods.Select(m => m.ToMod(instance)).ToArray();
            freeMods.Value = editing.AllowedMods.Select(m => m.ToMod(instance)).ToArray();
        }

        public override void AddNewItem()
        {
            if (saving) return;
            var item = CreatePlaylistItem();
            if (item.Beatmap.OnlineID <= 0)
            {
                status.Text = DailyChallengeProposalStrings.Unavailable;
                return;
            }
            var payload = JsonConvert.SerializeObject(item);
            if (previousPayload != null && previousPayload != payload) requestKey = Guid.NewGuid().ToString("N");
            previousPayload = payload;
            saving = true;
            addToPlaylistFooterButton.Enabled.Value = false;
            status.Text = DailyChallengeProposalStrings.Saving;
            request = editing == null ? new MutateDailyChallengeProposalRequest(item, requestKey, directlyApprove)
                : new MutateDailyChallengeProposalRequest(editing, "edit", item);
            request.Success += _ => Schedule(() =>
            {
                saving = false;
                if (this.IsCurrentScreen()) this.Exit();
            });
            request.Failure += exception => Schedule(() =>
            {
                saving = false;
                addToPlaylistFooterButton.Enabled.Value = true;
                status.Text = ProposalError(exception.Message);
            });
            api.Queue(request);
        }

        internal static osu.Framework.Localisation.LocalisableString ProposalError(string message)
        {
            if (message.Contains("daily_submission_limit", StringComparison.Ordinal)) return DailyChallengeProposalStrings.Limit;
            if (message.Contains("daily_beatmap_unavailable", StringComparison.Ordinal)) return DailyChallengeProposalStrings.Unavailable;
            if (message.Contains("daily_proposal_conflict", StringComparison.Ordinal)) return DailyChallengeProposalStrings.Conflict;
            return DailyChallengeProposalStrings.Failed;
        }

        public override bool OnExiting(ScreenExitEvent e) => saving || base.OnExiting(e);

        protected override void Dispose(bool isDisposing)
        {
            request?.Cancel();
            base.Dispose(isDisposing);
        }
    }
}
