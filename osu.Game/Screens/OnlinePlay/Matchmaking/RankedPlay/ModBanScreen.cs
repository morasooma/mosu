using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Localisation;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game.Database;
using osu.Game.Graphics.Containers;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Graphics.UserInterface;
using osu.Game.Localisation;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Overlays.Mods;
using osu.Game.Rulesets;
using osu.Game.Users.Drawables;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay
{
    public partial class ModBanScreen : RankedPlaySubScreen
    {
        public override LocalisableString StageHeading => RankedPlayModBanStrings.Heading;
        public override bool ShowStageOverlay => true;

        [Resolved]
        private RankedPlayMatchInfo matchInfo { get; set; } = null!;

        private readonly bool readOnly;
        private readonly List<ModPanel> panels = [];
        private readonly Dictionary<string, UpdateableAvatar> opponentMarkers = [];
        private readonly Dictionary<string, UpdateableAvatar> playerMarkers = [];
        private ShearedButton confirm = null!;
        private OsuTextFlowContainer status = null!;
        private string? selectedAcronym;
        private bool sending;
        private bool updatingSelection;
        private ScheduledDelegate? pendingPreview;
        private Task previewTask = Task.CompletedTask;

        public ModBanScreen(bool readOnly = false)
        {
            this.readOnly = readOnly;
            StageCaption = readOnly ? RankedPlayModBanStrings.Spectating : RankedPlayModBanStrings.Caption;

            // Reuse the ranked countdown, but place it directly above the mod list.
            StageDisplay.Anchor = Anchor.TopCentre;
            StageDisplay.Origin = Anchor.TopCentre;
        }

        [BackgroundDependencyLoader]
        private void load(RulesetStore rulesets)
        {
            var state = (RankedPlayRoomState)Client.Room!.MatchState!;
            var ruleset = rulesets.GetRuleset(state.RandomModRulesetId)!.CreateInstance();
            var flow = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Spacing = new Vector2(0, 8),
                Padding = new MarginPadding { Horizontal = 20 },
            };

            foreach (string acronym in state.BannableRandomMods.Union(state.RandomModBans.Values.OfType<string>()))
            {
                var mod = ruleset.CreateModFromAcronym(acronym);
                if (mod == null)
                    continue;

                var panel = new ModPanel(mod) { Width = 0.82f };
                panel.Active.BindValueChanged(e =>
                {
                    if (updatingSelection)
                        return;

                    updatingSelection = true;
                    selectedAcronym = e.NewValue ? acronym : null;
                    foreach (var other in panels.Where(p => p != panel))
                        other.Active.Value = false;
                    updatingSelection = false;
                    updateState();
                    queuePreview();
                });
                panels.Add(panel);

                var marker = createAvatar($"Opponent Ban {acronym}");
                var ownMarker = createAvatar($"Player Ban {acronym}");
                ownMarker.X = -42;
                opponentMarkers[acronym] = marker;
                playerMarkers[acronym] = ownMarker;
                flow.Add(new Container
                {
                    RelativeSizeAxes = Axes.X,
                    Height = ModSelectPanel.HEIGHT,
                    Children = [panel, ownMarker, marker],
                });
            }

            CenterColumn.Children =
            [
                new Container
                {
                    Name = "Ban Mod List",
                    RelativeSizeAxes = Axes.Both,
                    Padding = new MarginPadding { Top = 200, Bottom = 220 },
                    Child = new OsuScrollContainer
                    {
                        RelativeSizeAxes = Axes.Both,
                        Child = flow,
                    },
                },
                status = new OsuTextFlowContainer
                {
                    RelativeSizeAxes = Axes.X,
                    AutoSizeAxes = Axes.Y,
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -140,
                    TextAnchor = Anchor.TopCentre,
                },
                confirm = new ShearedButton
                {
                    Name = "Confirm Mod Ban",
                    Anchor = Anchor.BottomCentre,
                    Origin = Anchor.BottomCentre,
                    Y = -80,
                    Width = 280,
                    Action = () => submit().FireAndForget(),
                    Alpha = readOnly ? 0 : 1,
                },
            ];
        }

        private static UpdateableAvatar createAvatar(string name) => new UpdateableAvatar(isInteractive: false)
        {
            Name = name,
            Size = new Vector2(36),
            Anchor = Anchor.CentreRight,
            Origin = Anchor.CentreRight,
            Masking = true,
            CornerRadius = 6,
            Alpha = 0,
        };

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Client.MatchRoomStateChanged += onStateChanged;
            updateState();
            loadAvatars().FireAndForget();
        }

        [Resolved]
        private UserLookupCache users { get; set; } = null!;

        private async Task loadAvatars()
        {
            int playerId = matchInfo.PlayerId;
            int opponentId = matchInfo.OpponentId;
            var participants = await users.GetUsersAsync([playerId, opponentId]).ConfigureAwait(false);
            Schedule(() =>
            {
                foreach (var avatar in playerMarkers.Values)
                    avatar.User = participants.FirstOrDefault(user => user?.Id == playerId) ?? APIUser.UnknownUser(playerId);
                foreach (var avatar in opponentMarkers.Values)
                    avatar.User = participants.FirstOrDefault(user => user?.Id == opponentId) ?? APIUser.UnknownUser(opponentId);
            });
        }

        private void onStateChanged(MatchRoomState state) => updateState();

        private void updateState()
        {
            if (!IsLoaded || Client.Room?.MatchState is not RankedPlayRoomState state)
                return;

            bool confirmed = state.RandomModBans.TryGetValue(matchInfo.PlayerId, out string? ownBan);
            if (confirmed || (selectedAcronym != null && !state.BannableRandomMods.Contains(selectedAcronym)))
            {
                updatingSelection = true;
                selectedAcronym = confirmed ? ownBan : null;
                foreach (var panel in panels)
                    panel.Active.Value = panel.Mod.Acronym == selectedAcronym;
                updatingSelection = false;
            }

            bool opponentConfirmed = state.RandomModBans.TryGetValue(matchInfo.OpponentId, out string? opponentMod);
            if (!opponentConfirmed)
                state.RandomModBanSelections.TryGetValue(matchInfo.OpponentId, out opponentMod);

            foreach (var panel in panels)
            {
                panel.Enabled.Value = !readOnly && !sending && !confirmed && state.BannableRandomMods.Contains(panel.Mod.Acronym);
                opponentMarkers[panel.Mod.Acronym].FadeTo(panel.Mod.Acronym == opponentMod ? opponentConfirmed ? 1 : 0.45f : 0, 120);
                playerMarkers[panel.Mod.Acronym].FadeTo(panel.Mod.Acronym == selectedAcronym ? confirmed ? 1 : 0.45f : 0, 120);
            }

            confirm.Enabled.Value = !readOnly && !sending && !confirmed;
            confirm.Text = selectedAcronym == null ? RankedPlayModBanStrings.Skip : RankedPlayModBanStrings.Ban(selectedAcronym);
            status.Clear();
            status.AddParagraph(RankedPlayModBanStrings.YourBan(describeBan(state, matchInfo.PlayerId)));
            status.AddParagraph(RankedPlayModBanStrings.OpponentBan(describeBan(state, matchInfo.OpponentId)));
            if (confirmed && !readOnly)
                StageCaption = RankedPlayModBanStrings.Waiting;
        }

        private static LocalisableString describeBan(RankedPlayRoomState state, int userId)
            => state.RandomModBans.TryGetValue(userId, out string? ban)
                ? ban == null ? RankedPlayModBanStrings.NoBan : (LocalisableString)ban
                : RankedPlayModBanStrings.Choosing;

        private void queuePreview()
        {
            pendingPreview?.Cancel();
            if (readOnly || sending)
                return;

            pendingPreview = Scheduler.AddDelayed(() =>
            {
                string? selection = selectedAcronym;
                previewTask = sendPreview(previewTask, selection);
            }, 100);
        }

        private async Task sendPreview(Task previous, string? selection)
        {
            await previous.ConfigureAwait(false);
            try
            {
                await Client.SendMatchRequest(new RankedPlayModBanRequest { Acronym = selection, Confirm = false }).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Unable to share tentative random mod ban.");
            }
        }

        private async Task submit()
        {
            if (sending || readOnly)
                return;

            pendingPreview?.Cancel();
            sending = true;
            string? selection = selectedAcronym;
            updateState();
            try
            {
                await previewTask.ConfigureAwait(false);
                await Client.SendMatchRequest(new RankedPlayModBanRequest { Acronym = selection }).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                Logger.Error(e, "Unable to confirm random mod ban.");
                Schedule(() => StageCaption = RankedPlayModBanStrings.Retry);
            }
            finally
            {
                Schedule(() =>
                {
                    sending = false;
                    updateState();
                });
            }
        }

        public override void OnExiting(RankedPlaySubScreen? next)
        {
            pendingPreview?.Cancel();
            base.OnExiting(next);
        }

        protected override void Dispose(bool isDisposing)
        {
            pendingPreview?.Cancel();
            Client.MatchRoomStateChanged -= onStateChanged;
            base.Dispose(isDisposing);
        }
    }
}
