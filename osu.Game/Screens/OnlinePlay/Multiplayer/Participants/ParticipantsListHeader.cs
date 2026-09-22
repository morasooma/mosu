// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using osu.Framework.Allocation;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.Legacy;
using osu.Game.Online.Multiplayer;
using osu.Game.Resources.Localisation.Web;

namespace osu.Game.Screens.OnlinePlay.Multiplayer.Participants
{
    public partial class ParticipantsListHeader : SectionHeader
    {
        [Resolved]
        private MultiplayerClient client { get; set; } = null!;

        [Resolved(CanBeNull = true)]
        private StableBanchoSession? stableBanchoSession { get; set; }

        public ParticipantsListHeader()
            : base(RankingsStrings.SpotlightParticipants)
        {
        }

        protected override void Update()
        {
            base.Update();

            var room = client.Room;
            if (room == null)
                return;

            if (stableBanchoSession?.CurrentMatch.Value is StableBanchoMatch match)
            {
                int openSlots = match.SlotStatuses.Count(status => status != StableMatchSlotStatus.Locked);
                DetailsText.Value = $@"{room.Users.Count} players • {openSlots}/{match.SlotStatuses.Length} slots open";
            }
            else
                DetailsText.Value = room.Settings.MaxParticipants == null ? $@"{room.Users.Count}" : $@"{room.Users.Count} / {room.Settings.MaxParticipants}";
        }
    }
}
