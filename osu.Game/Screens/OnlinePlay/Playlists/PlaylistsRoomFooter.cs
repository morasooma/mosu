// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Game.Online.Rooms;
using osu.Game.Online.API;
using osu.Game.Screens.OnlinePlay.Match.Components;
using osuTK;

namespace osu.Game.Screens.OnlinePlay.Playlists
{
    public partial class PlaylistsRoomFooter : CompositeDrawable
    {
        public Action? OnStart;
        public Action? OnAddBeatmaps;

        private PurpleRoundedButton addBeatmapsButton = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        private readonly Room room;

        public PlaylistsRoomFooter(Room room)
        {
            this.room = room;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            RelativeSizeAxes = Axes.Both;

            InternalChild = new FillFlowContainer
            {
                AutoSizeAxes = Axes.X,
                RelativeSizeAxes = Axes.Y,
                Anchor = Anchor.Centre,
                Origin = Anchor.Centre,
                Direction = FillDirection.Horizontal,
                Spacing = new Vector2(10),
                Children = new Drawable[]
                {
                    new PlaylistsReadyButton(room)
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        RelativeSizeAxes = Axes.Y,
                        Size = new Vector2(600, 1),
                        Action = () => OnStart?.Invoke()
                    },
                    addBeatmapsButton = new PurpleRoundedButton
                    {
                        Anchor = Anchor.Centre,
                        Origin = Anchor.Centre,
                        Width = 230,
                        RelativeSizeAxes = Axes.Y,
                        Height = 1,
                        Text = global::osu.Game.Localisation.ClientTranslationStrings.J5018,
                        Alpha = 0,
                        Enabled = { Value = false },
                        Action = () =>
                        {
                            if (canAddBeatmaps)
                                OnAddBeatmaps?.Invoke();
                        }
                    }
                }
            };
        }

        private bool canAddBeatmaps => room.RoomID != null && room.Host?.Id == api.LocalUser.Value.Id && !room.HasEnded;

        protected override void Update()
        {
            base.Update();
            bool canAdd = canAddBeatmaps;
            addBeatmapsButton.Alpha = canAdd ? 1 : 0;
            addBeatmapsButton.Enabled.Value = canAdd;
        }
    }
}
