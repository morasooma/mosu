using System;
using System.Collections.Generic;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Logging;
using osu.Game.Configuration;
using osu.Game.Localisation;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Overlays.CustomTheme;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Screens.OnlinePlay.Matchmaking.Queue;
using osu.Game.Users.Drawables;
using osuTK;

namespace osu.Game.Overlays.Toolbar
{
    public partial class ToolbarServerInvitation : CompositeDrawable
    {
        [Resolved] private MultiplayerClient client { get; set; } = null!;
        [Resolved] private IAPIProvider api { get; set; } = null!;
        [Resolved] private Bindable<RulesetInfo> ruleset { get; set; } = null!;
        [Resolved] private Bindable<IReadOnlyList<Mod>> mods { get; set; } = null!;
        [Resolved(CanBeNull = true)] private OsuGame? game { get; set; }
        [Resolved(CanBeNull = true)] private QueueController? queue { get; set; }
        [Resolved] private OverlayColourProvider colours { get; set; } = null!;

        private IBindable<bool> enabled = null!;
        private IBindable<bool> allModes = null!;
        private readonly List<ServerPlayInvitation> pending = new List<ServerPlayInvitation>();
        private readonly HashSet<string> revoked = new HashSet<string>();
        private ServerPlayInvitation? current;
        private double visibleTime;
        private readonly Box progress;
        private readonly Box background;
        private readonly Container avatars;
        private readonly OsuSpriteText title;
        private readonly OsuSpriteText detail;
        private readonly RoundedButton join;

        public ToolbarServerInvitation()
        {
            RelativeSizeAxes = Axes.Y;
            Width = 340;
            Masking = true;
            Alpha = 0;
            AlwaysPresent = true;
            InternalChildren = new Drawable[]
            {
                background = new Box { RelativeSizeAxes = Axes.Both },
                title = new OsuSpriteText { X = 8, Y = 3, Font = osu.Game.Graphics.OsuFont.Default.With(size: 12) },
                avatars = new Container { Position = new Vector2(8, 18), Size = new Vector2(120, 20) },
                detail = new OsuSpriteText { X = 34, Y = 20, Font = osu.Game.Graphics.OsuFont.Default.With(size: 12) },
                join = new RoundedButton
                {
                    Text = ServerInvitationStrings.Join, Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight,
                    X = -28, Size = new Vector2(68, 28), Action = accept,
                },
                new RoundedButton
                {
                    Text = "×", Anchor = Anchor.CentreRight, Origin = Anchor.CentreRight,
                    Size = new Vector2(24, 28), Action = dismiss,
                },
                progress = new Box
                {
                    RelativeSizeAxes = Axes.X, Height = 2, Anchor = Anchor.BottomLeft, Origin = Anchor.BottomLeft,
                },
            };
        }

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config)
        {
            enabled = config.GetBindable<bool>(OsuSetting.ForkToolbarInvitations);
            allModes = config.GetBindable<bool>(OsuSetting.ForkRankedInvitationsAllModes);
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            client.ServerPlayInvited += receive;
            client.ServerPlayInvitationRevoked += revoke;
        }

        private void receive(ServerPlayInvitation invitation)
        {
            Logger.Log($"Toolbar invitation received: {invitation.Id}, user={invitation.UserId}, mode={invitation.Pool?.RulesetId}, currentMode={currentMode()}, enabled={enabled.Value}, allModes={allModes.Value}", LoggingTarget.Network);
            if (!enabled.Value || invitation.UserId == api.LocalUser.Value.Id || revoked.Contains(invitation.Id)) return;
            if (pending.Any(i => i.Id == invitation.Id) || current?.Id == invitation.Id) return;
            // Bound the deferred queue during long gameplay sessions.
            if (pending.Count >= 20) pending.RemoveAt(0);
            pending.Add(invitation);
        }

        private void revoke(string id)
        {
            if (revoked.Count >= 256) revoked.Clear();
            revoked.Add(id);
            pending.RemoveAll(i => i.Id == id);
            if (current?.Id == id) dismiss();
        }

        private bool eligible(ServerPlayInvitation invitation)
            => invitation.ValidUntil > DateTimeOffset.UtcNow
               && (invitation.Pool == null || allModes.Value || invitation.Pool.RulesetId == currentMode());

        private int currentMode()
        {
            int id = ruleset.Value.OnlineID;
            if (id == 0 && mods.Value.Any(m => m.Acronym == "AP")) return 5;
            if (mods.Value.Any(m => m.Acronym == "RX"))
                return id switch { 0 => 4, 1 => 6, 2 => 7, _ => id };
            return id;
        }

        protected override void Update()
        {
            base.Update();
            background.Colour = colours.Background4;
            title.Colour = detail.Colour = colours.Content1;
            progress.Colour = colours.Colour3;
            CornerRadius = OverlayColourProvider.CalculateEffectiveRadius(GeometryRole.Card, 6);
            if (!enabled.Value || !client.IsConnected.Value)
            {
                pending.Clear();
                dismiss();
                return;
            }
            // A mode mismatch is temporary (e.g. mods change while entering/exiting
            // gameplay). Keep unseen invitations until expiry or server revocation.
            pending.RemoveAll(i => i.ValidUntil <= DateTimeOffset.UtcNow);
            if (current != null && (!eligible(current) || visibleTime >= 20000)) dismiss();
            bool canShow = game != null && game.Toolbar.State.Value == Visibility.Visible && game.OverlayActivationMode.Value != OverlayActivation.Disabled;
            if (!canShow) return;
            if (current != null) visibleTime += Time.Elapsed;
            var next = pending.FirstOrDefault(eligible);
            if (current == null && next != null)
            {
                current = next;
                pending.Remove(next);
                visibleTime = 0;
                display(current);
                Alpha = 1;
                Logger.Log($"Toolbar invitation shown: {current.Id}", LoggingTarget.Network);
            }
            if (current != null) progress.Width = (float)Math.Clamp(1 - visibleTime / 20000, 0, 1);
        }

        private void display(ServerPlayInvitation invitation)
        {
            avatars.Clear();
            if (invitation.Pool != null)
            {
                title.Text = ServerInvitationStrings.Ranked(modeName(invitation.Pool.RulesetId));
                detail.Text = ServerInvitationStrings.Searching(invitation.Username, invitation.Elo);
                avatar(invitation.UserId, 0);
            }
            else
            {
                title.Text = ServerInvitationStrings.Multiplayer;
                detail.Text = string.Empty;
                int count = Math.Min(invitation.Participants.Length, 4);
                for (int i = 0; i < count; i++) avatar(invitation.Participants[i], i);
                if (invitation.Participants.Length > count)
                    avatars.Add(new CircularContainer
                    {
                        Position = new Vector2(count * 16, 0), Size = new Vector2(20), Masking = true,
                        Children = new Drawable[]
                        {
                            new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background2 },
                            new OsuSpriteText
                            {
                                Text = $"+{invitation.Participants.Length - count}", Font = osu.Game.Graphics.OsuFont.Default.With(size: 10),
                                Anchor = Anchor.Centre, Origin = Anchor.Centre,
                            },
                        },
                    });
            }
            join.TooltipText = invitation.RoomId != null ? invitation.RoomName : invitation.RandomMods ? ServerInvitationStrings.RandomModsQueue : ServerInvitationStrings.JoinQueue;
        }

        private static string modeName(int id) => id switch
        {
            0 => "Standard", 1 => "Taiko", 2 => "Catch", 3 => "Mania",
            4 => "Standard Relax", 5 => "Standard Autopilot", 6 => "Taiko Relax", 7 => "Catch Relax",
            _ => "Dodge",
        };

        private void avatar(int userId, int index)
        {
            var invitation = current;
            var slot = new CircularContainer
            {
                Position = new Vector2(index * 16, 0), Size = new Vector2(20), Masking = true,
                Child = new Box { RelativeSizeAxes = Axes.Both, Colour = colours.Background2 },
            };
            avatars.Add(slot);
            // DrawableAvatar is LongRunningLoad: load it directly asynchronously,
            // not as a synchronously added descendant of an already loaded container.
            LoadComponentAsync(new DrawableAvatar(new APIUser { Id = userId }), loaded =>
            {
                if (current != invitation || slot.Parent != avatars)
                {
                    loaded.Dispose();
                    return;
                }
                slot.Add(loaded);
            });
        }

        private void accept()
        {
            if (current == null || queue == null || client.Room != null || queue.CurrentState.Value != ScreenQueue.MatchmakingScreenState.Idle) return;
            if (current.RoomId is long roomId) client.PresentServerInvitation(roomId);
            else if (current.Pool != null)
            {
                queue.SelectedPool.Value = current.Pool;
                queue.JoinQueue(current.Pool, current.RandomMods);
            }
            dismiss();
        }

        private void dismiss()
        {
            current = null;
            Alpha = 0;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (client != null)
            {
                client.ServerPlayInvited -= receive;
                client.ServerPlayInvitationRevoked -= revoke;
            }
            base.Dispose(isDisposing);
        }
    }
}
