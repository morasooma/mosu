// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using osu.Framework.Extensions;
using osu.Framework.Logging;
using osu.Game.Beatmaps;
using osu.Game.Scoring;
using osu.Game.Scoring.Legacy;

namespace osu.Game.Online.Legacy
{
    public sealed class StableScoreSubmissionClient : IDisposable
    {
        private static readonly Regex version_regex = new Regex(@"(?:^b)?(?<date>\d{8})", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private readonly ServerProfile profile;
        private readonly HttpClient httpClient;
        private readonly SemaphoreSlim loginLock = new SemaphoreSlim(1, 1);
        private readonly SemaphoreSlim banchoRequestLock = new SemaphoreSlim(1, 1);

        private string? choToken;
        private int loggedInUserId;

        public event Action<IReadOnlyList<StableBanchoPacket>>? BanchoPacketsReceived;

        public StableScoreSubmissionClient(ServerProfile profile)
            : this(profile, new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
        {
        }

        internal StableScoreSubmissionClient(ServerProfile profile, HttpMessageHandler handler)
            : this(profile, new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) })
        {
        }

        private StableScoreSubmissionClient(ServerProfile profile, HttpClient httpClient)
        {
            this.profile = profile;
            this.httpClient = httpClient;
            ServerProfileManager.EnsureStableIdentity(profile);
        }

        public bool IsConfigured(out string error)
        {
            if (!profile.UseStableProtocol)
            {
                error = "The active server profile does not use the stable protocol.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(profile.ApiUrl) || string.IsNullOrWhiteSpace(profile.BanchoUrl))
            {
                error = "Both the stable web URL and Bancho URL must be configured.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(profile.Username) || string.IsNullOrWhiteSpace(profile.StablePasswordHash))
            {
                error = "A username and password must be configured for the stable server profile.";
                return false;
            }

            if (!tryGetOsuVersion(out _))
            {
                error = $"Stable client version must contain an 8-digit build date, for example {ServerProfileManager.DEFAULT_STABLE_CLIENT_VERSION}.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        public string GetAvatarUrl(int userId)
            => $"{profile.BanchoUrl.TrimEnd('/').Replace("//c.", "//a.")}/{userId}";

        public Task<StableScoreSubmissionResult> SubmitAsync(Score score, IBeatmap beatmap, int legacyTotalScore, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException("Score submission is disabled in the public build.");
        }

        public async Task<StableBanchoLoginResult> LoginAsync(CancellationToken cancellationToken = default)
        {
            if (!IsConfigured(out string configurationError))
                throw new InvalidOperationException(configurationError);

            await loginLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                if (!string.IsNullOrEmpty(choToken) && loggedInUserId > 0)
                    return new StableBanchoLoginResult(loggedInUserId, profile.Username);

                string loginBody = string.Join("\n", new[]
                {
                    profile.Username,
                    profile.StablePasswordHash,
                    $"b{getOsuVersion()}|{getUtcOffset()}|0|{buildClientHash()}|1",
                    string.Empty,
                });

                using var request = new HttpRequestMessage(HttpMethod.Post, normaliseUrl(profile.BanchoUrl))
                {
                    Content = new StringContent(loginBody, Encoding.UTF8, "text/plain"),
                };
                request.Headers.TryAddWithoutValidation("User-Agent", "osu!");

                using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                byte[] responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                ensureSuccess(response, Encoding.UTF8.GetString(responseBytes), "Stable Bancho endpoint");

                IReadOnlyList<StableBanchoPacket> responsePackets = StableBanchoPacketCodec.Decode(responseBytes);
                int userId = readLoginUserId(responsePackets);
                string? responseToken = response.Headers.TryGetValues("cho-token", out var tokenValues) ? tokenValues.FirstOrDefault() : null;

                if (userId <= 0)
                {
                    string? serverMessage = readLoginNotification(responsePackets);
                    throw new StableBanchoLoginException(userId, responseToken, serverMessage);
                }

                if (responseToken == null)
                    throw new InvalidOperationException("Stable Bancho login response did not contain a cho-token header.");

                choToken = responseToken;
                if (string.IsNullOrWhiteSpace(choToken) || choToken.Contains("failed", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Stable Bancho login returned an invalid token.");

                loggedInUserId = userId;
                dispatchBanchoPackets(responsePackets);
                return new StableBanchoLoginResult(userId, profile.Username);
            }
            finally
            {
                loginLock.Release();
            }
        }

        public async Task PollAsync(CancellationToken cancellationToken = default)
        {
            await SendBanchoPacketsAsync([new StableBanchoPacket(StableBanchoClientPacket.Ping)], cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Verifies that the configured credentials can establish a Stable Bancho session, then logs the temporary session out.
        /// </summary>
        public async Task<StableBanchoLoginResult> TestConnectionAsync(CancellationToken cancellationToken = default)
        {
            StableBanchoLoginResult login = await LoginAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                // bancho.py deliberately ignores logout packets sent during the first second
                // of a session. Give the temporary connection time to become log-outable so
                // that a subsequent real connection is not rejected as already logged in.
                await Task.Delay(TimeSpan.FromMilliseconds(1100), cancellationToken).ConfigureAwait(false);
                await LogoutAsync(cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                InvalidateBanchoSession();
            }

            return login;
        }

        public async Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(choToken) || loggedInUserId <= 0)
                return;

            try
            {
                await SendBanchoPacketsAsync([new StableBanchoPacket(StableBanchoClientPacket.Logout)], cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                InvalidateBanchoSession();
            }
        }

        public async Task<IReadOnlyList<StableBanchoPacket>> SendBanchoPacketsAsync(IEnumerable<StableBanchoPacket> packets, CancellationToken cancellationToken = default)
        {
            await LoginAsync(cancellationToken).ConfigureAwait(false);

            string token = choToken ?? throw new InvalidOperationException("Stable Bancho session has no token.");
            byte[] requestBody = StableBanchoPacketCodec.Encode(packets.ToArray());

            await banchoRequestLock.WaitAsync(cancellationToken).ConfigureAwait(false);

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, normaliseUrl(profile.BanchoUrl))
                {
                    Content = new ByteArrayContent(requestBody),
                };
                request.Headers.TryAddWithoutValidation("User-Agent", "osu!");
                request.Headers.TryAddWithoutValidation("osu-token", token);

                using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                byte[] responseBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
                ensureSuccess(response, Encoding.UTF8.GetString(responseBytes), "Stable Bancho endpoint");

                IReadOnlyList<StableBanchoPacket> responsePackets = dispatchBanchoPackets(responseBytes);

                if (responsePackets.Any(packet => packet.Id == (ushort)StableBanchoServerPacket.Restart))
                {
                    InvalidateBanchoSession();
                    throw new InvalidOperationException("Stable Bancho requested a session restart.");
                }

                return responsePackets;
            }
            finally
            {
                banchoRequestLock.Release();
            }
        }

        public async Task<StableLeaderboardResult> FetchLeaderboardAsync(IBeatmapInfo beatmap, int rulesetId, int legacyMods, int leaderboardType,
                                                                         CancellationToken cancellationToken = default)
        {
            if (!IsConfigured(out string configurationError))
                throw new InvalidOperationException(configurationError);

            if (string.IsNullOrWhiteSpace(beatmap.MD5Hash))
                throw new InvalidOperationException("The beatmap has no stable MD5 checksum.");

            string filename = $"{beatmap.Metadata.Artist} - {beatmap.Metadata.Title} ({beatmap.Metadata.Author.Username}) [{beatmap.DifficultyName}].osu";
            int beatmapSetId = beatmap.BeatmapSet?.OnlineID ?? -1;

            var parameters = new Dictionary<string, string>
            {
                ["us"] = profile.Username,
                ["ha"] = profile.StablePasswordHash,
                ["s"] = "0",
                ["vv"] = "4",
                ["v"] = leaderboardType.ToString(CultureInfo.InvariantCulture),
                ["c"] = beatmap.MD5Hash,
                ["f"] = filename,
                ["m"] = rulesetId.ToString(CultureInfo.InvariantCulture),
                ["i"] = beatmapSetId.ToString(CultureInfo.InvariantCulture),
                ["mods"] = legacyMods.ToString(CultureInfo.InvariantCulture),
                ["h"] = string.Empty,
                ["a"] = "0",
            };

            string query = string.Join("&", parameters.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{combineUrl(profile.ApiUrl, "/web/osu-osz2-getscores.php")}?{query}");
            request.Headers.TryAddWithoutValidation("User-Agent", "osu!");

            using HttpResponseMessage response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            string responseText = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            ensureSuccess(response, responseText, "Stable leaderboard endpoint");

            StableLeaderboardResult result = parseLeaderboard(responseText);
            string responseKind = classifyLeaderboardResponse(responseText);
            Logger.Log(
                $"Stable leaderboard response: kind={responseKind}, beatmap={beatmap.OnlineID}, set={beatmapSetId}, md5={beatmap.MD5Hash}, scores={result.Scores.Count}, total={result.ScoreCount}.",
                LoggingTarget.Network);
            return result;
        }

        private static string classifyLeaderboardResponse(string responseText)
        {
            string header = responseText.Replace("\r", string.Empty).Split('\n').FirstOrDefault()?.Trim() ?? string.Empty;

            return header switch
            {
                "-1|false" => "not-submitted",
                "1|false" => "needs-update",
                _ when header.Split('|').Length >= 5 => "found",
                _ when header.EndsWith("|false", StringComparison.OrdinalIgnoreCase) => $"no-leaderboard(status={header.Split('|')[0]})",
                _ => "invalid-response",
            };
        }

        private static StableLeaderboardResult parseLeaderboard(string responseText)
        {
            string[] lines = responseText.Replace("\r", string.Empty).Split('\n');
            string[] header = lines.FirstOrDefault()?.Split('|') ?? Array.Empty<string>();

            if (header.Length < 5)
            {
                // Stable servers use 0|false for maps which are known but do not currently
                // have an enabled leaderboard (usually pending/unranked maps). Treating this
                // as an unavailable local beatmap makes song select offer a metadata refresh,
                // which cannot change the server-side state. Present it as an empty leaderboard
                // instead. Other short responses (-1|false, 1|false, malformed data) still
                // represent an unavailable or mismatched beatmap.
                bool knownWithoutLeaderboard = header.Length >= 2
                                               && header[0] == "0"
                                               && string.Equals(header[1], "false", StringComparison.OrdinalIgnoreCase);

                return new StableLeaderboardResult(knownWithoutLeaderboard, 0, null, Array.Empty<StableLeaderboardScore>());
            }

            int.TryParse(header[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int scoreCount);
            StableLeaderboardScore? personalBest = lines.Length > 4 ? parseLeaderboardScore(lines[4]) : null;
            var scores = lines.Skip(5).Select(parseLeaderboardScore).Where(score => score != null).Select(score => score!).ToArray();
            return new StableLeaderboardResult(true, scoreCount, personalBest, scores);
        }

        private static StableLeaderboardScore? parseLeaderboardScore(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return null;

            string[] values = line.Split('|');
            if (values.Length < 16)
                return null;

            if (!long.TryParse(values[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out long id)
                || !long.TryParse(values[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out long score)
                || !int.TryParse(values[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int maxCombo)
                || !int.TryParse(values[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count50)
                || !int.TryParse(values[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count100)
                || !int.TryParse(values[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out int count300)
                || !int.TryParse(values[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out int countMiss)
                || !int.TryParse(values[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out int countKatu)
                || !int.TryParse(values[9], NumberStyles.Integer, CultureInfo.InvariantCulture, out int countGeki)
                || !int.TryParse(values[11], NumberStyles.Integer, CultureInfo.InvariantCulture, out int mods)
                || !int.TryParse(values[12], NumberStyles.Integer, CultureInfo.InvariantCulture, out int userId)
                || !int.TryParse(values[13], NumberStyles.Integer, CultureInfo.InvariantCulture, out int position))
                return null;

            if (!tryParseLeaderboardDate(values[14], out DateTimeOffset date))
                return null;

            return new StableLeaderboardScore(id, values[1], score, maxCombo, count50, count100, count300, countMiss, countKatu, countGeki,
                values[10] == "1", mods, userId, position, date, values[15] == "1");
        }

        private static bool tryParseLeaderboardDate(string value, out DateTimeOffset date)
        {
            if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long timestamp))
            {
                try
                {
                    // Stable-compatible servers exist with both seconds and milliseconds in
                    // leaderboard responses. Millisecond timestamps currently have 13 digits.
                    date = timestamp <= -100_000_000_000 || timestamp >= 100_000_000_000
                        ? DateTimeOffset.FromUnixTimeMilliseconds(timestamp)
                        : DateTimeOffset.FromUnixTimeSeconds(timestamp);
                    return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    date = default;
                    return false;
                }
            }

            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out date);
        }

        public void InvalidateBanchoSession()
        {
            choToken = null;
            loggedInUserId = 0;
        }

        private string buildClientHash()
        {
            const string adapters = "mosu.";
            string uninstallHash = profile.StableUninstallId.ComputeMD5Hash();
            string diskHash = profile.StableDiskId.ComputeMD5Hash();
            return $"{profile.StableOsuPathHash}:{adapters}:{profile.StableAdapterHash}:{uninstallHash}:{diskHash}:";
        }

        private bool tryGetOsuVersion(out string version)
        {
            Match match = version_regex.Match(profile.ClientVersion ?? string.Empty);
            version = match.Success ? match.Groups["date"].Value : string.Empty;
            return match.Success;
        }

        private string getOsuVersion()
        {
            if (!tryGetOsuVersion(out string version))
                throw new InvalidOperationException("Invalid stable client version.");
            return version;
        }

        private static short getUtcOffset()
            // Stable Bancho expects the signed UTC offset (for example, Moscow is 3).
            // Adding 24 produces 27 and is rejected by stricter servers as invalid-request.
            => (short)TimeZoneInfo.Local.GetUtcOffset(DateTimeOffset.Now).TotalHours;

        private static int readLoginUserId(IReadOnlyList<StableBanchoPacket> packets)
        {
            foreach (StableBanchoPacket packet in packets)
            {
                if (packet.Id == (ushort)StableBanchoServerPacket.UserId && packet.Payload.Length == 4)
                    return BinaryPrimitives.ReadInt32LittleEndian(packet.Payload);
            }

            throw new InvalidOperationException("Stable Bancho login response did not contain a login reply packet.");
        }

        private static string? readLoginNotification(IReadOnlyList<StableBanchoPacket> packets)
        {
            var messages = packets
                           .Where(packet => packet.Id == (ushort)StableBanchoServerPacket.Notification)
                           .Select(packet => StableBanchoPacketCodec.DecodeString(packet.Payload))
                           .Where(message => !string.IsNullOrWhiteSpace(message))
                           .Distinct(StringComparer.Ordinal)
                           .ToArray();

            return messages.Length == 0 ? null : string.Join(Environment.NewLine, messages);
        }

        private IReadOnlyList<StableBanchoPacket> dispatchBanchoPackets(byte[] response)
        {
            IReadOnlyList<StableBanchoPacket> packets = StableBanchoPacketCodec.Decode(response);
            dispatchBanchoPackets(packets);
            return packets;
        }

        private void dispatchBanchoPackets(IReadOnlyList<StableBanchoPacket> packets)
        {
            if (packets.Count > 0)
                BanchoPacketsReceived?.Invoke(packets);
        }

        private static void ensureSuccess(HttpResponseMessage response, string responseText, string endpointName)
        {
            if (response.IsSuccessStatusCode)
                return;

            string responsePreview = responseText.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (responsePreview.Length > 300)
                responsePreview = responsePreview.Substring(0, 300);

            string details = string.IsNullOrEmpty(responsePreview) ? string.Empty : $" Response: {responsePreview}";
            throw new HttpRequestException($"{endpointName} {response.RequestMessage?.RequestUri} returned {(int)response.StatusCode} ({response.ReasonPhrase}).{details}");
        }

        private static string combineUrl(string baseUrl, string path) => normaliseUrl(baseUrl).TrimEnd('/') + path;

        private static string normaliseUrl(string url)
        {
            string result = url.Trim();
            if (!result.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !result.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                result = "https://" + result;
            return result;
        }

        public void Dispose()
        {
            httpClient.Dispose();
            loginLock.Dispose();
            banchoRequestLock.Dispose();
        }
    }

    public sealed class StableBanchoLoginException : InvalidOperationException
    {
        public int FailureCode { get; }
        public string? ServerReason { get; }
        public string? ServerMessage { get; }

        public StableBanchoLoginException(int failureCode, string? serverReason, string? serverMessage)
            : base(buildMessage(failureCode, serverReason, serverMessage))
        {
            FailureCode = failureCode;
            ServerReason = serverReason;
            ServerMessage = serverMessage;
        }

        private static string buildMessage(int failureCode, string? serverReason, string? serverMessage)
        {
            string description = failureCode switch
            {
                -1 => "authentication failed",
                -2 => "client version is too old",
                -3 or -4 => "account is banned",
                -5 => "server error",
                -6 => "supporter status is required",
                -7 => "password reset is required",
                -8 => "account verification is required",
                _ => "unknown login error",
            };

            string message = $"Stable Bancho login failed with code {failureCode} ({description}).";

            if (!string.IsNullOrWhiteSpace(serverReason))
                message += $" Server reason: {serverReason}.";

            if (!string.IsNullOrWhiteSpace(serverMessage))
                message += $" Server message: {serverMessage}";

            return message;
        }
    }

    public record StableLeaderboardResult(bool BeatmapAvailable, int ScoreCount, StableLeaderboardScore? PersonalBest, IReadOnlyList<StableLeaderboardScore> Scores);

    public record StableLeaderboardScore(long Id, string Username, long TotalScore, int MaxCombo, int Count50, int Count100, int Count300,
                                         int CountMiss, int CountKatu, int CountGeki, bool Perfect, int Mods, int UserId, int Position,
                                         DateTimeOffset Date, bool HasReplay);

    public sealed class StableBanchoLoginResult
    {
        public int UserId { get; }
        public string Username { get; }

        public StableBanchoLoginResult(int userId, string username)
        {
            UserId = userId;
            Username = username;
        }
    }

    public sealed class StableScoreSubmissionResult
    {
        public long OnlineId { get; }
        public double? PerformancePoints { get; }
        public string Response { get; }

        public StableScoreSubmissionResult(long onlineId, double? performancePoints, string response)
        {
            OnlineId = onlineId;
            PerformancePoints = performancePoints;
            Response = response;
        }
    }
}
