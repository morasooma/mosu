// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Text;
using DiscordRPC;
using DiscordRPC.Message;
using Newtonsoft.Json;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Threading;
using osu.Game;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Rooms;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Users;
using LogLevel = osu.Framework.Logging.LogLevel;

namespace osu.Desktop
{
    internal partial class DiscordRichPresence : Component
    {
        private const string client_id = "1539651886993383564";
        private const string large_image_key = "morasooma_logo";

        private DiscordRpcClient client = null!;

        [Resolved]
        private IBindable<RulesetInfo> ruleset { get; set; } = null!;

        [Resolved]
        private IAPIProvider api { get; set; } = null!;

        [Resolved]
        private OsuGame game { get; set; } = null!;

        [Resolved]
        private LoginOverlay? login { get; set; }

        [Resolved]
        private MultiplayerClient multiplayerClient { get; set; } = null!;

        [Resolved]
        private LocalUserStatisticsProvider statisticsProvider { get; set; } = null!;

        private IBindable<DiscordRichPresenceMode> privacyMode = null!;
        private IBindable<UserStatus> userStatus = null!;
        private IBindable<UserActivity?> userActivity = null!;

        private readonly RichPresence presence = new RichPresence
        {
            Assets = new Assets(),
            Timestamps = Timestamps.Now,
            Secrets = new Secrets
            {
                JoinSecret = null,
                SpectateSecret = null,
            },
        };

        private IBindable<APIUser>? user;

        [BackgroundDependencyLoader]
        private void load(OsuConfigManager config, SessionStatics session)
        {
            privacyMode = config.GetBindable<DiscordRichPresenceMode>(OsuSetting.DiscordRichPresence);
            userStatus = config.GetBindable<UserStatus>(OsuSetting.UserOnlineStatus);
            userActivity = session.GetBindable<UserActivity?>(Static.UserOnlineActivity);

            client = new DiscordRpcClient(client_id)
            {
                // SkipIdenticalPresence allows us to fire SetPresence at any point and leave it to the underlying implementation
                // to check whether a difference has actually occurred before sending a command to Discord (with a minor caveat that's handled in onReady).
                SkipIdenticalPresence = true
            };

            client.OnReady += onReady;
            client.OnError += (_, e) => Logger.Log($"An error occurred with Discord RPC Client: {e.Message} ({e.Code})", LoggingTarget.Network);

            try
            {
                client.RegisterUriScheme();
                client.Subscribe(EventType.Join);
                client.OnJoin += onJoin;
            }
            catch (Exception ex)
            {
                // This is known to fail in at least the following sandboxed environments:
                // - macOS (when packaged as an app bundle)
                // - flatpak (see: https://github.com/flathub/sh.ppy.osu/issues/170)
                // There is currently no better way to do this offered by Discord, so the best we can do is simply ignore it for now.
                Logger.Log($"Failed to register Discord URI scheme: {ex}");
            }

            client.Initialize();
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            user = api.LocalUser.GetBoundCopy();

            ruleset.BindValueChanged(_ => schedulePresenceUpdate());
            user.ValueChanged += onUserChanged;
            userStatus.BindValueChanged(_ => schedulePresenceUpdate());
            userActivity.BindValueChanged(_ => schedulePresenceUpdate());
            privacyMode.BindValueChanged(_ => schedulePresenceUpdate());

            multiplayerClient.RoomUpdated += onRoomUpdated;
            statisticsProvider.StatisticsUpdated += onStatisticsUpdated;
        }

        private void onReady(object _, ReadyMessage __)
        {
            Logger.Log("Discord RPC Client ready.", LoggingTarget.Network, LogLevel.Debug);

            // when RPC is lost and reconnected, we have to clear presence state for updatePresence to work (see DiscordRpcClient.SkipIdenticalPresence).
            if (client.CurrentPresence != null)
                client.SetPresence(null);

            schedulePresenceUpdate();
        }

        private void onRoomUpdated() => schedulePresenceUpdate();

        private void onStatisticsUpdated(UserStatisticsUpdate _) => schedulePresenceUpdate();

        private void onUserChanged(ValueChangedEvent<APIUser> _) => schedulePresenceUpdate();

        private ScheduledDelegate? presenceUpdateDelegate;

        private void schedulePresenceUpdate()
        {
            presenceUpdateDelegate?.Cancel();
            presenceUpdateDelegate = Scheduler.AddDelayed(() =>
            {
                if (!client.IsInitialized)
                    return;

                // Rich Presence describes the locally running game and must not depend on
                // osu!web authentication. Stable Bancho and some third-party profiles can be
                // fully connected while IAPIProvider.IsLoggedIn remains false.
                if (userStatus.Value == UserStatus.Offline || privacyMode.Value == DiscordRichPresenceMode.Off)
                {
                    client.ClearPresence();
                    return;
                }

                bool hasIdentifiableUser = user?.Value.Id > APIUser.SYSTEM_USER_ID && !string.IsNullOrWhiteSpace(user.Value.Username);
                bool hideIdentifiableInformation = !hasIdentifiableUser
                                                     || privacyMode.Value == DiscordRichPresenceMode.Limited
                                                     || userStatus.Value == UserStatus.DoNotDisturb;

                updatePresence(hideIdentifiableInformation);
                client.SetPresence(presence);
            }, 200);
        }

        private void updatePresence(bool hideIdentifiableInformation)
        {
            if (user == null)
                return;

            // user activity
            if (userActivity.Value != null)
            {
                presence.State = clampLength($"{userActivity.Value.GetStatus(hideIdentifiableInformation)} • {getRulesetDisplayName(ruleset.Value.ShortName)}");
                presence.Details = clampLength(userActivity.Value.GetDetails(hideIdentifiableInformation) ?? string.Empty);

                if (userActivity.Value.GetBeatmapID(hideIdentifiableInformation) is int beatmapId && beatmapId > 0)
                {
                    presence.Buttons = new[]
                    {
                        new Button
                        {
                            Label = "View beatmap",
                            Url = $@"{api.Endpoints.WebsiteUrl}/beatmaps/{beatmapId}?mode={ruleset.Value.ShortName}"
                        }
                    };
                }
                else
                {
                    presence.Buttons = null;
                }
            }
            else
            {
                presence.State = $"Idle • {getRulesetDisplayName(ruleset.Value.ShortName)}";
                presence.Details = string.Empty;
            }

            // user party
            if (!hideIdentifiableInformation && multiplayerClient.Room != null && !multiplayerClient.Room.Settings.MatchType.IsMatchmakingType())
            {
                MultiplayerRoom room = multiplayerClient.Room;

                presence.Party = new Party
                {
                    Privacy = string.IsNullOrEmpty(room.Settings.Password) ? Party.PrivacySetting.Public : Party.PrivacySetting.Private,
                    ID = room.RoomID.ToString(),
                    // technically lobbies can have infinite users, but Discord needs this to be set to something.
                    // to make party display sensible, assign a powers of two above participants count (8 at minimum).
                    Max = (int)Math.Max(8, Math.Pow(2, Math.Ceiling(Math.Log2(room.Users.Count)))),
                    Size = room.Users.Count,
                };

                RoomSecret roomSecret = new RoomSecret
                {
                    RoomID = room.RoomID,
                    Password = room.Settings.Password,
                };

                if (client.HasRegisteredUriScheme)
                    presence.Secrets.JoinSecret = JsonConvert.SerializeObject(roomSecret);

                // discord cannot handle both secrets and buttons at the same time, so we need to choose something.
                // the multiplayer room seems more important.
                presence.Buttons = null;
            }
            else
            {
                presence.Party = null;
                presence.Secrets.JoinSecret = null;
            }

            presence.Assets.LargeImageKey = large_image_key;

            // Show profile information in the image tooltip only when identifiable
            // information is allowed. The selected mode itself is not identifying.
            string rulesetDisplayName = getRulesetDisplayName(ruleset.Value.ShortName);

            if (hideIdentifiableInformation)
                presence.Assets.LargeImageText = $"Morasooma • {rulesetDisplayName}";
            else
            {
                var statistics = statisticsProvider.GetStatisticsFor(ruleset.Value);
                string rank = statistics?.GlobalRank > 0 ? $" (rank #{statistics.GlobalRank:N0})" : string.Empty;
                presence.Assets.LargeImageText = clampLength($"{user.Value.Username}{rank} • {rulesetDisplayName}");
            }

            presence.Assets.SmallImageKey = null;
            presence.Assets.SmallImageText = null;
        }

        private void onJoin(object sender, JoinMessage args) => Scheduler.AddOnce(() =>
        {
            game.Window?.Raise();

            if (!api.IsLoggedIn)
            {
                login?.Show();
                return;
            }

            Logger.Log($"Received room secret from Discord RPC Client: \"{args.Secret}\"", LoggingTarget.Network, LogLevel.Debug);

            if (string.IsNullOrEmpty(args.Secret) || args.Secret[0] != '{' || !tryParseRoomSecret(args.Secret, out long roomId, out string? password))
            {
                Logger.Log("Could not join multiplayer room, invitation is invalid or incompatible.", LoggingTarget.Network, LogLevel.Important);
                return;
            }

            var request = new GetRoomRequest(roomId);
            request.Success += room => Schedule(() =>
            {
                game.PresentMultiplayerMatch(room, password);
            });
            request.Failure += _ => Logger.Log($"Could not join multiplayer room, room could not be found (room ID: {roomId}).", LoggingTarget.Network, LogLevel.Important);
            api.Queue(request);
        });

        private static readonly int ellipsis_length = Encoding.UTF8.GetByteCount(new[] { '…' });

        private static string getRulesetDisplayName(string shortName) => shortName switch
        {
            RulesetInfo.OSU_MODE_SHORTNAME => "Standard",
            RulesetInfo.OSU_RELAX_MODE_SHORTNAME => "Standard (Relax)",
            RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME => "Standard (Autopilot)",
            RulesetInfo.TAIKO_MODE_SHORTNAME => "Drums",
            RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME => "Drums (Relax)",
            RulesetInfo.CATCH_MODE_SHORTNAME => "Catch",
            RulesetInfo.CATCH_RELAX_MODE_SHORTNAME => "Catch (Relax)",
            RulesetInfo.DODGE_MODE_SHORTNAME => "Dodge",
            "mania" => "Keys",
            _ => "Custom mode",
        };

        private static string clampLength(string str)
        {
            // Empty strings are fine to discord even though single-character strings are not. Make it make sense.
            if (string.IsNullOrEmpty(str))
                return str;

            // As above, discord decides that *non-empty* strings shorter than 2 characters cannot possibly be valid input, because... reasons?
            // And yes, that is two *characters*, or *codepoints*, not *bytes* as further down below (as determined by empirical testing).
            // Also, spaces don't count. Because reasons, clearly.
            // That all seems very questionable, and isn't even documented anywhere. So to *make it* accept such valid input,
            // just tack on enough of U+200B ZERO WIDTH SPACEs at the end. After making sure to trim whitespace.
            string trimmed = str.Trim();
            if (trimmed.Length < 2)
                return trimmed.PadRight(2, '\u200B');

            if (Encoding.UTF8.GetByteCount(str) <= 128)
                return str;

            ReadOnlyMemory<char> strMem = str.AsMemory();

            do
            {
                strMem = strMem[..^1];
            } while (Encoding.UTF8.GetByteCount(strMem.Span) + ellipsis_length > 128);

            return string.Create(strMem.Length + 1, strMem, (span, mem) =>
            {
                mem.Span.CopyTo(span);
                span[^1] = '…';
            });
        }

        private static bool tryParseRoomSecret(string secretJson, out long roomId, out string? password)
        {
            roomId = 0;
            password = null;

            RoomSecret? roomSecret;

            try
            {
                roomSecret = JsonConvert.DeserializeObject<RoomSecret>(secretJson);
            }
            catch
            {
                return false;
            }

            if (roomSecret == null) return false;

            roomId = roomSecret.RoomID;
            password = roomSecret.Password;

            return true;
        }

        protected override void Dispose(bool isDisposing)
        {
            if (multiplayerClient.IsNotNull())
                multiplayerClient.RoomUpdated -= onRoomUpdated;

            if (statisticsProvider.IsNotNull())
                statisticsProvider.StatisticsUpdated -= onStatisticsUpdated;

            if (user != null)
                user.ValueChanged -= onUserChanged;

            client.Dispose();
            base.Dispose(isDisposing);
        }

        private class RoomSecret
        {
            [JsonProperty(@"roomId", Required = Required.Always)]
            public long RoomID { get; set; }

            [JsonProperty(@"password", Required = Required.AllowNull)]
            public string? Password { get; set; }
        }
    }
}
