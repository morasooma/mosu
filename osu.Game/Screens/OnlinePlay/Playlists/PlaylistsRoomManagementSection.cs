// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Extensions.Color4Extensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Shapes;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Localisation;
using osu.Game.Graphics;
using osu.Game.Graphics.Sprites;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Overlays.Dialog;
using osu.Game.Users.Drawables;
using osuTK;
using osuTK.Graphics;
using Container = osu.Framework.Graphics.Containers.Container;

namespace osu.Game.Screens.OnlinePlay.Playlists
{
    public partial class PlaylistsRoomManagementSection : CompositeDrawable
    {
        private readonly Room room;

        private OsuSpriteText emptyStateText = null!;
        private FillFlowContainer participantRows = null!;

        private bool lastManageState;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private IDialogOverlay? dialogOverlay { get; set; }

        public PlaylistsRoomManagementSection(Room room)
        {
            this.room = room;

            RelativeSizeAxes = Axes.X;
            AutoSizeAxes = Axes.Y;
        }

        [BackgroundDependencyLoader]
        private void load(OverlayColourProvider colours)
        {
            InternalChild = new FillFlowContainer
            {
                RelativeSizeAxes = Axes.X,
                AutoSizeAxes = Axes.Y,
                Direction = FillDirection.Vertical,
                Children = new Drawable[]
                {
                    new SectionHeader("Management"),
                    new Container
                    {
                        RelativeSizeAxes = Axes.X,
                        AutoSizeAxes = Axes.Y,
                        Masking = true,
                        CornerRadius = 10,
                        Margin = new MarginPadding { Horizontal = 5 },
                        Children = new Drawable[]
                        {
                            new Box
                            {
                                RelativeSizeAxes = Axes.Both,
                                Colour = colours.Background4,
                            },
                            new FillFlowContainer
                            {
                                RelativeSizeAxes = Axes.X,
                                AutoSizeAxes = Axes.Y,
                                Direction = FillDirection.Vertical,
                                Padding = new MarginPadding(10),
                                Spacing = new Vector2(0, 8),
                                Children = new Drawable[]
                                {
                                    emptyStateText = new OsuSpriteText
                                    {
                                        Font = OsuFont.Style.Caption1,
                                        Alpha = 0,
                                        Text = "No visible users returned by the room API.",
                                    },
                                    participantRows = new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.X,
                                        AutoSizeAxes = Axes.Y,
                                        Direction = FillDirection.Vertical,
                                        Spacing = new Vector2(0, 6),
                                    }
                                }
                            }
                        }
                    }
                }
            };
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            room.PropertyChanged += onRoomPropertyChanged;
            refreshDisplay();
        }

        protected override void Update()
        {
            base.Update();

            bool canManage = canManageRoom();

            if (canManage != lastManageState)
                refreshDisplay();
        }

        private void onRoomPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case nameof(Room.Host):
                case nameof(Room.ParticipantCount):
                case nameof(Room.RecentParticipants):
                case nameof(Room.RoomID):
                case nameof(Room.EndDate):
                case nameof(Room.Status):
                    refreshDisplay();
                    break;
            }
        }

        private void refreshDisplay()
        {
            lastManageState = canManageRoom();

            List<APIUser> visibleUsers = getVisibleUsers();

            participantRows.Clear();

            foreach (APIUser user in visibleUsers)
            {
                bool isLocalUser = user.Id == api.LocalUser.Value.Id;

                participantRows.Add(new ParticipantActionRow(
                    user,
                    room.Host?.Id == user.Id,
                    isLocalUser,
                    lastManageState && !isLocalUser,
                    () => requestKickUser(user)
                ));
            }

            emptyStateText.Alpha = visibleUsers.Count == 0 ? 1 : 0;
        }

        private List<APIUser> getVisibleUsers()
        {
            var visibleUsers = new List<APIUser>();

            if (room.Host != null)
                visibleUsers.Add(room.Host);

            foreach (APIUser participant in room.RecentParticipants)
            {
                if (visibleUsers.All(existing => existing.Id != participant.Id))
                    visibleUsers.Add(participant);
            }

            return visibleUsers;
        }

        private bool canManageRoom() => room.RoomID != null && !room.HasEnded;

        private void requestCloseRoom()
        {
            if (room.RoomID == null || room.HasEnded)
                return;

            if (dialogOverlay == null)
            {
                performCloseRoom();
                return;
            }

            dialogOverlay.Push(new ClosePlaylistDialog(room, performCloseRoom));
        }

        private void performCloseRoom()
        {
            if (room.RoomID == null)
                return;

            var request = new ClosePlaylistRequest(room.RoomID.Value);
            request.Success += () =>
            {
                room.EndDate = DateTimeOffset.UtcNow;
                refreshRoom();
            };
            request.Failure += ex => showDialog("Room close failed", buildErrorText(ex));
            api.Queue(request);
        }

        private void requestKickUser(APIUser user)
        {
            if (room.RoomID == null || room.HasEnded || user.Id == api.LocalUser.Value.Id)
                return;

            if (dialogOverlay == null)
            {
                performKickUser(user);
                return;
            }

            dialogOverlay.Push(new KickRoomUserDialog(user, () => performKickUser(user)));
        }

        private void performKickUser(APIUser user)
        {
            if (room.RoomID == null)
                return;

            var request = new PartUserFromRoomRequest(room.RoomID.Value, user.Id);
            request.Success += refreshRoom;
            request.Failure += ex => showDialog($"Kick failed for {user.Username}", buildErrorText(ex));
            api.Queue(request);
        }

        private void refreshRoom()
        {
            if (room.RoomID == null)
                return;

            var request = new GetRoomRequest(room.RoomID.Value);
            request.Success += room.CopyFrom;
            api.Queue(request);
        }

        private void showDialog(LocalisableString title, LocalisableString body)
            => Schedule(() => dialogOverlay?.Push(new RoomManagementResultDialog(title, body)));

        private static string buildErrorText(Exception ex)
            => $"The room API request failed: {ex.Message}";

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            room.PropertyChanged -= onRoomPropertyChanged;
        }

        private sealed partial class ParticipantActionRow : CompositeDrawable
        {
            public ParticipantActionRow(APIUser user, bool isHost, bool isLocalUser, bool canKick, Action kickAction)
            {
                RelativeSizeAxes = Axes.X;
                Height = 40;

                string subtitle = isHost && isLocalUser ? "Host / You"
                    : isHost ? "Host"
                    : isLocalUser ? "You"
                    : "Recent participant";

                InternalChild = new GridContainer
                {
                    RelativeSizeAxes = Axes.Both,
                    ColumnDimensions = new[]
                    {
                        new Dimension(),
                        new Dimension(GridSizeMode.AutoSize),
                    },
                    Content = new[]
                    {
                        new Drawable?[]
                        {
                            new Container
                            {
                                RelativeSizeAxes = Axes.Both,
                                Masking = true,
                                CornerRadius = 8,
                                Children = new Drawable[]
                                {
                                    new Box
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Colour = Color4.Black.Opacity(0.08f),
                                    },
                                    new FillFlowContainer
                                    {
                                        RelativeSizeAxes = Axes.Both,
                                        Direction = FillDirection.Horizontal,
                                        Spacing = new Vector2(8, 0),
                                        Padding = new MarginPadding { Horizontal = 10, Vertical = 6 },
                                        Children = new Drawable[]
                                        {
                                            new UpdateableAvatar(showUserPanelOnHover: true)
                                            {
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Size = new Vector2(26),
                                                User = user,
                                            },
                                            new FillFlowContainer
                                            {
                                                RelativeSizeAxes = Axes.Y,
                                                AutoSizeAxes = Axes.X,
                                                Direction = FillDirection.Vertical,
                                                Anchor = Anchor.CentreLeft,
                                                Origin = Anchor.CentreLeft,
                                                Children = new Drawable[]
                                                {
                                                    new OsuSpriteText
                                                    {
                                                        Font = OsuFont.GetFont(weight: FontWeight.Bold, size: 14),
                                                        Text = user.Username,
                                                    },
                                                    new OsuSpriteText
                                                    {
                                                        Font = OsuFont.Style.Caption2,
                                                        Alpha = 0.8f,
                                                        Text = subtitle,
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            null
                        }
                    }
                };
            }
        }

        private sealed partial class KickRoomUserDialog : DeletionDialog
        {
            public KickRoomUserDialog(APIUser user, Action kickAction)
            {
                HeaderText = "Kick participant";
                BodyText = user.Username;
                DangerousAction = kickAction;
            }
        }

        private sealed partial class RoomManagementResultDialog : PopupDialog
        {
            public RoomManagementResultDialog(LocalisableString title, LocalisableString body)
            {
                HeaderText = title;
                BodyText = body;
                Icon = FontAwesome.Solid.ExclamationTriangle;

                Buttons = new PopupDialogButton[]
                {
                    new PopupDialogOkButton
                    {
                        Text = "OK",
                    },
                };
            }
        }
    }
}
