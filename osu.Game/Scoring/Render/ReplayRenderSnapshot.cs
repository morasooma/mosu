using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using osu.Game.Configuration;
using osu.Game.Extensions;
using osu.Game.Online;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Leaderboards;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Users;
using osu.Game.Users.Drawables;
using SixLabors.ImageSharp;

namespace osu.Game.Scoring.Render
{
    // A presentation-only handoff. Never serialise ScoreInfo/APIProvider wholesale: they may
    // contain Realm links, integrity challenges or account state unrelated to video export.
    public sealed class ReplayRenderSnapshot
    {
        public RenderUser Player { get; set; } = new RenderUser();
        public int? PlayerPosition { get; set; }
        public bool ShowLeaderboard { get; set; }
        public ScoringMode ScoreDisplayMode { get; set; }
        public RenderLayout? Layout { get; set; }
        public bool UsesStableProtocol { get; set; }
        public bool IsThirdPartyServer { get; set; }
        public BeatmapLeaderboardScope Scope { get; set; }
        public LeaderboardSortMode Sorting { get; set; }
        public APIMod[]? ExactMods { get; set; }
        public RenderScore[] TopScores { get; set; } = [];
        public RenderScore? UserScore { get; set; }
        public int ScoresRequested { get; set; }
        public int TotalScores { get; set; }
        public Dictionary<string, byte[]> Avatars { get; set; } = new Dictionary<string, byte[]>();

        public static ReplayRenderSnapshot Capture(ScoreInfo score, LeaderboardManager? manager, IAPIProvider api, OsuConfigManager config)
        {
            var criteria = manager?.CurrentCriteria;
            bool sameBeatmap = criteria?.Beatmap != null && score.BeatmapInfo != null
                && (criteria.Beatmap.ID == score.BeatmapInfo.ID
                    || (!string.IsNullOrEmpty(score.BeatmapInfo.MD5Hash) && criteria.Beatmap.MD5Hash == score.BeatmapInfo.MD5Hash));
            var scores = sameBeatmap && criteria?.Ruleset?.OnlineID == GetLeaderboardRulesetId(score)
                && manager?.Scores.Value?.FailState == null ? manager?.Scores.Value : null;
            APIUser player = scores?.AllScores.FirstOrDefault(s => s.UserID == score.UserID)?.User
                             ?? (api.LocalUser.Value.OnlineID == score.UserID ? api.LocalUser.Value : score.User);

            return new ReplayRenderSnapshot
            {
                Player = RenderUser.From(player, api),
                PlayerPosition = scores?.AllScores.FirstOrDefault(s => s.Equals(score) || s.MatchesOnlineID(score))?.Position ?? score.Position,
                ShowLeaderboard = config.Get<bool>(OsuSetting.GameplayLeaderboard),
                ScoreDisplayMode = config.Get<ScoringMode>(OsuSetting.ScoreDisplayMode),
                Layout = RenderLayout.Capture(config),
                UsesStableProtocol = MosuServerEnvironment.UsesStableProtocol,
                IsThirdPartyServer = MosuServerEnvironment.IsThirdPartyServer,
                Scope = criteria?.Scope ?? BeatmapLeaderboardScope.Global,
                Sorting = criteria?.Sorting ?? LeaderboardSortMode.Score,
                ExactMods = criteria?.ExactMods == null ? null
                    : JsonConvert.DeserializeObject<APIMod[]>(JsonConvert.SerializeObject(criteria.ExactMods.Select(m => new APIMod(m)))),
                TopScores = scores?.TopScores.Select(s => RenderScore.From(s, api)).ToArray() ?? [],
                UserScore = scores?.UserScore is ScoreInfo userScore ? RenderScore.From(userScore, api) : null,
                ScoresRequested = scores?.ScoresRequested ?? 0,
                TotalScores = scores?.TotalScores ?? 0,
            };
        }

        private static int GetLeaderboardRulesetId(ScoreInfo score)
        {
            bool relax = score.Mods.Any(m => m.Acronym is "RX" or "MRX");
            bool autopilot = score.Mods.Any(m => m.Acronym == "AP");
            return score.Ruleset.ShortName switch
            {
                RulesetInfo.OSU_MODE_SHORTNAME when relax => RulesetInfo.OSU_RELAX_ONLINE_ID,
                RulesetInfo.TAIKO_MODE_SHORTNAME when relax && !MosuServerEnvironment.OnlyOsuRelax => RulesetInfo.TAIKO_RELAX_ONLINE_ID,
                RulesetInfo.CATCH_MODE_SHORTNAME when relax && !MosuServerEnvironment.OnlyOsuRelax => RulesetInfo.CATCH_RELAX_ONLINE_ID,
                RulesetInfo.OSU_MODE_SHORTNAME when autopilot && !MosuServerEnvironment.OnlyOsuRelax => RulesetInfo.OSU_AUTOPILOT_ONLINE_ID,
                _ => score.Ruleset.OnlineID,
            };
        }

        public string GetAvatarUrl(int userId)
            => Player.Id == userId ? Player.AvatarUrl
                : TopScores.Select(s => s.User).FirstOrDefault(u => u.Id == userId)?.AvatarUrl
                  ?? (UserScore?.User.Id == userId ? UserScore.User.AvatarUrl : string.Empty);

        public LeaderboardScores CreateLeaderboardScores()
            => LeaderboardScores.Success(TopScores.Select(s => s.ToScoreInfo()).ToArray(), ScoresRequested, TotalScores, UserScore?.ToScoreInfo());

        public async Task PrepareAvatarsAsync(OsuConfigManager config)
        {
            using var store = new TrustedDomainOnlineStore(config);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            using var concurrency = new SemaphoreSlim(4);
            var urls = TopScores.Select(s => s.User).Append(Player)
                .Concat(UserScore == null ? [] : new[] { UserScore.User })
                .Where(u => u.Id > 1 && !string.IsNullOrWhiteSpace(u.AvatarUrl))
                .Select(u => u.AvatarUrl).Distinct().ToArray();

            await Task.WhenAll(urls.Select(async url =>
            {
                try
                {
                    await concurrency.WaitAsync(deadline.Token).ConfigureAwait(false);
                    try
                    {
                        byte[]? data = await store.GetAsync(url, deadline.Token).ConfigureAwait(false);
                        if (data is { Length: > 0 and <= 4 * 1024 * 1024 } && IsValidAvatar(data))
                        {
                            lock (Avatars)
                                Avatars[url] = data;
                        }
                    }
                    finally
                    {
                        concurrency.Release();
                    }
                }
                catch (OperationCanceledException)
                {
                    // Missing avatars use the same guest fallback as the interactive client.
                }
            })).ConfigureAwait(false);
        }

        internal static bool IsValidAvatar(byte[] data)
        {
            try
            {
                var info = Image.Identify(data);
                return info.Width is > 0 and <= 4096 && info.Height is > 0 and <= 4096;
            }
            catch (UnknownImageFormatException) { return false; }
            catch (InvalidImageContentException) { return false; }
        }

        public void Write(string path) => File.WriteAllText(path, JsonConvert.SerializeObject(this));

        public static ReplayRenderSnapshot Read(string path)
            => JsonConvert.DeserializeObject<ReplayRenderSnapshot>(File.ReadAllText(path))
               ?? throw new InvalidDataException("Replay render snapshot is empty.");

        // Explicit presentation-only allowlist; never transfer the complete config/account state.
        public sealed class RenderLayout
        {
            public ScalingMode Scaling { get; set; }
            public float SizeX { get; set; }
            public float SizeY { get; set; }
            public float PositionX { get; set; }
            public float PositionY { get; set; }
            public float BackgroundDim { get; set; }
            public float UIScale { get; set; }
            public bool SafeAreaConsiderations { get; set; }
            public bool DisableInterfaceShear { get; set; }

            public static RenderLayout Capture(OsuConfigManager config) => new RenderLayout
            {
                Scaling = config.Get<ScalingMode>(OsuSetting.Scaling),
                SizeX = config.Get<float>(OsuSetting.ScalingSizeX),
                SizeY = config.Get<float>(OsuSetting.ScalingSizeY),
                PositionX = config.Get<float>(OsuSetting.ScalingPositionX),
                PositionY = config.Get<float>(OsuSetting.ScalingPositionY),
                BackgroundDim = config.Get<float>(OsuSetting.ScalingBackgroundDim),
                UIScale = config.Get<float>(OsuSetting.UIScale),
                SafeAreaConsiderations = config.Get<bool>(OsuSetting.SafeAreaConsiderations),
                DisableInterfaceShear = config.Get<bool>(OsuSetting.ForkDisableInterfaceShear),
            };

            public void Apply(OsuConfigManager config)
            {
                config.SetValue(OsuSetting.Scaling, Scaling);
                config.SetValue(OsuSetting.ScalingSizeX, SizeX);
                config.SetValue(OsuSetting.ScalingSizeY, SizeY);
                config.SetValue(OsuSetting.ScalingPositionX, PositionX);
                config.SetValue(OsuSetting.ScalingPositionY, PositionY);
                config.SetValue(OsuSetting.ScalingBackgroundDim, BackgroundDim);
                config.SetValue(OsuSetting.UIScale, UIScale);
                config.SetValue(OsuSetting.SafeAreaConsiderations, SafeAreaConsiderations);
                config.SetValue(OsuSetting.ForkDisableInterfaceShear, DisableInterfaceShear);
            }
        }

        public sealed class RenderUser
        {
            public int Id { get; set; }
            public string Username { get; set; } = string.Empty;
            public CountryCode CountryCode { get; set; }
            public string AvatarUrl { get; set; } = string.Empty;
            public string? Colour { get; set; }
            public bool IsBot { get; set; }
            public bool IsSupporter { get; set; }

            internal static RenderUser From(APIUser user, IAPIProvider api) => new RenderUser
            {
                Id = user.OnlineID,
                Username = user.Username,
                CountryCode = user.CountryCode,
                AvatarUrl = DrawableAvatar.GetAvatarLookup(user, api.Endpoints.APIUrl, MosuServerEnvironment.IsThirdPartyServer),
                Colour = user.Colour,
                IsBot = user.IsBot,
                IsSupporter = user.IsSupporter,
            };

            public APIUser ToUser() => new APIUser
            {
                Id = Id, Username = Username, CountryCode = CountryCode, AvatarUrl = AvatarUrl,
                Colour = Colour, IsBot = IsBot, IsSupporter = IsSupporter,
            };
        }

        public sealed class RenderScore
        {
            public Guid Id { get; set; }
            public long OnlineId { get; set; }
            public RenderUser User { get; set; } = new RenderUser();
            public int? Position { get; set; }
            public long TotalScore { get; set; }
            public long? MosuTotalScore { get; set; }
            public long? LegacyTotalScore { get; set; }
            public double Accuracy { get; set; }
            public int MaxCombo { get; set; }
            public int Combo { get; set; }
            public DateTimeOffset Date { get; set; }
            public double? PP { get; set; }
            public ScoreRank Rank { get; set; }
            public bool IsLegacyScore { get; set; }
            public int RulesetId { get; set; }
            public string RulesetShortName { get; set; } = string.Empty;
            public string RulesetInstantiationInfo { get; set; } = string.Empty;
            public APIMod[] Mods { get; set; } = [];
            public Dictionary<HitResult, int> Statistics { get; set; } = new Dictionary<HitResult, int>();
            public Dictionary<HitResult, int> MaximumStatistics { get; set; } = new Dictionary<HitResult, int>();

            internal static RenderScore From(ScoreInfo score, IAPIProvider api) => new RenderScore
            {
                Id = score.ID, OnlineId = score.OnlineID, User = RenderUser.From(score.User, api), Position = score.Position,
                TotalScore = score.TotalScore, MosuTotalScore = score.GetLeaderboardScore(), LegacyTotalScore = score.LegacyTotalScore,
                Accuracy = score.Accuracy, MaxCombo = score.MaxCombo, Combo = score.Combo, Date = score.Date, PP = score.PP,
                Rank = score.Rank, IsLegacyScore = score.IsLegacyScore,
                RulesetId = score.Ruleset.OnlineID, RulesetShortName = score.Ruleset.ShortName,
                RulesetInstantiationInfo = score.Ruleset.InstantiationInfo,
                Mods = JsonConvert.DeserializeObject<APIMod[]>(JsonConvert.SerializeObject(score.APIMods)) ?? [], Statistics = new Dictionary<HitResult, int>(score.Statistics),
                MaximumStatistics = new Dictionary<HitResult, int>(score.MaximumStatistics),
            };

            public ScoreInfo ToScoreInfo() => new ScoreInfo
            {
                ID = Id, OnlineID = OnlineId, User = User.ToUser(), Position = Position,
                TotalScore = TotalScore, MosuTotalScore = MosuTotalScore, LegacyTotalScore = LegacyTotalScore,
                Accuracy = Accuracy, MaxCombo = MaxCombo, Combo = Combo, Date = Date, PP = PP, Rank = Rank, IsLegacyScore = IsLegacyScore,
                Ruleset = new RulesetInfo { OnlineID = RulesetId, ShortName = RulesetShortName, InstantiationInfo = RulesetInstantiationInfo, Available = true },
                APIMods = Mods, Statistics = new Dictionary<HitResult, int>(Statistics),
                MaximumStatistics = new Dictionary<HitResult, int>(MaximumStatistics),
            };
        }
    }
}
