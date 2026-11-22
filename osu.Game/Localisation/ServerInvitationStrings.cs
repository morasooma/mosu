using osu.Framework.Localisation;

namespace osu.Game.Localisation
{
    public static class ServerInvitationStrings
    {
        private const string prefix = "osu.Game.Resources.Localisation.ForkSettings";

        public static LocalisableString Join => new TranslatableString($"{prefix}:server_invitation_join", "Join");
        public static LocalisableString Multiplayer => new TranslatableString($"{prefix}:server_invitation_multiplayer", "Multiplayer invitation");
        public static LocalisableString Ranked(string mode) => new TranslatableString($"{prefix}:server_invitation_ranked", "Looking for an opponent · {0}", mode);
        public static LocalisableString Searching(string username, int elo) => new TranslatableString($"{prefix}:server_invitation_searching", "{0} · {1:N0} Elo", username, elo);
        public static LocalisableString InviteServer => new TranslatableString($"{prefix}:server_invitation_broadcast", "Invite the whole server");
        public static LocalisableString CooldownHint => new TranslatableString($"{prefix}:server_invitation_cooldown_hint", "Supporters only: once every 10 minutes per room and 20 minutes per user.");
        public static LocalisableString JoinQueue => new TranslatableString($"{prefix}:server_invitation_join_queue", "Join the queue");
        public static LocalisableString RandomModsQueue => new TranslatableString($"{prefix}:server_invitation_random_mods", "Join the random mods queue");
        public static LocalisableString Enabled => new TranslatableString($"{prefix}:server_invitation_enabled", "Toolbar invitations");
        public static LocalisableString EnabledHint => new TranslatableString($"{prefix}:server_invitation_enabled_hint", "Show multiplayer invitations and ranked play opponent searches.");
        public static LocalisableString AllModes => new TranslatableString($"{prefix}:server_invitation_all_modes", "Ranked invitations for all modes");
        public static LocalisableString AllModesHint => new TranslatableString($"{prefix}:server_invitation_all_modes_hint", "Show opponent searches for all modes, not just the selected mode.");
    }
}
