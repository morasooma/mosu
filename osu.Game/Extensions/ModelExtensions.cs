// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// This file is partly modified by GooGuTeam.
// See the LICENCE file in the repository root for full licence text.

using System.IO;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.IO;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Users;

namespace osu.Game.Extensions
{
    public static class ModelExtensions
    {
        /// <summary>
        /// Get the relative path in osu! storage for this file.
        /// </summary>
        /// <param name="fileInfo">The file info.</param>
        /// <returns>A relative file path.</returns>
        public static string GetStoragePath(this IFileInfo fileInfo) => Path.Combine(fileInfo.Hash.Remove(1), fileInfo.Hash.Remove(2), fileInfo.Hash);

        /// <summary>
        /// Returns a user-facing string representing the <paramref name="model"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Non-interface types without special handling will fall back to <see cref="object.ToString()"/>.
        /// </para>
        /// <para>
        /// Warning: This method is _purposefully_ not called <c>GetDisplayTitle()</c> like the others, because otherwise
        /// extension method type inference rules cause this method to call itself and cause a stack overflow.
        /// </para>
        /// </remarks>
        public static string GetDisplayString(this object? model)
        {
            string? result = null;

            switch (model)
            {
                case IBeatmapSetInfo beatmapSetInfo:
                    result = beatmapSetInfo.Metadata.GetDisplayTitle();
                    break;

                case IBeatmapInfo beatmapInfo:
                    result = beatmapInfo.GetDisplayTitle();
                    break;

                case IBeatmapMetadataInfo metadataInfo:
                    result = metadataInfo.GetDisplayTitle();
                    break;

                case IScoreInfo scoreInfo:
                    result = scoreInfo.GetDisplayTitle();
                    break;

                case IRulesetInfo rulesetInfo:
                    result = rulesetInfo.Name;
                    break;

                case IUser user:
                    result = user.Username;
                    break;
            }

            // fallback in case none of the above happens to match.
            result ??= model?.ToString() ?? @"null";
            return result;
        }

        public static string GetDisplayUsername(this IUser? user, string? customUsername = null)
        {
            string username = customUsername?.Trim() ?? string.Empty;
            return username.Length > 0 ? username : user?.Username ?? string.Empty;
        }

        public static APIUser WithDisplayUsername(this APIUser? user, string? customUsername)
        {
            string displayUsername = user.GetDisplayUsername(customUsername);

            if (user == null)
                return new APIUser { Username = displayUsername };

            if (displayUsername == user.Username)
                return user;

            return new APIUser
            {
                Id = user.Id,
                Username = displayUsername,
                CountryCode = user.CountryCode,
                AvatarUrl = user.AvatarUrl,
                IsSupporter = user.IsSupporter,
                Colour = user.Colour,
                Team = user.Team,
            };
        }

        /// <summary>
        /// Check whether this <see cref="IRulesetInfo"/>'s online ID is within the range that defines it as a legacy ruleset (ie. either osu!, osu!taiko, osu!catch or osu!mania).
        /// </summary>
        public static bool IsLegacyRuleset(this IRulesetInfo ruleset) => ruleset.OnlineID >= 0 && ruleset.OnlineID <= ILegacyRuleset.MAX_LEGACY_RULESET_ID;

        /// <summary>
        /// Check whether this <see cref="IRulesetInfo"/> represents a special ruleset (ie. any of the relax or autopilot modes).
        /// </summary>
        public static bool IsSpecialRuleset(this IRulesetInfo ruleset) => ruleset.ShortName is RulesetInfo.OSU_RELAX_MODE_SHORTNAME or RulesetInfo.OSU_AUTOPILOT_MODE_SHORTNAME
            or RulesetInfo.TAIKO_RELAX_MODE_SHORTNAME or RulesetInfo.CATCH_RELAX_MODE_SHORTNAME;

        /// <summary>
        /// Check whether this <see cref="IRulesetInfo"/> has special rulesets associated with it (ie. is either osu!, osu!taiko, or osu!catch).
        /// </summary>
        public static bool HasSpecialRuleset(this IRulesetInfo ruleset) => ruleset.ShortName is RulesetInfo.OSU_MODE_SHORTNAME or RulesetInfo.TAIKO_MODE_SHORTNAME or RulesetInfo.CATCH_MODE_SHORTNAME;

        /// <summary>
        /// Check whether the online ID of two <see cref="IBeatmapSetInfo"/>s match.
        /// </summary>
        /// <param name="instance">The instance to compare.</param>
        /// <param name="other">The other instance to compare against.</param>
        /// <returns>Whether online IDs match. If either instance is missing an online ID, this will return false.</returns>
        public static bool MatchesOnlineID(this IBeatmapSetInfo? instance, IBeatmapSetInfo? other) => matchesOnlineID(instance, other);

        /// <summary>
        /// Check whether the online ID of two <see cref="IBeatmapInfo"/>s match.
        /// </summary>
        /// <param name="instance">The instance to compare.</param>
        /// <param name="other">The other instance to compare against.</param>
        /// <returns>Whether online IDs match. If either instance is missing an online ID, this will return false.</returns>
        public static bool MatchesOnlineID(this IBeatmapInfo? instance, IBeatmapInfo? other) => matchesOnlineID(instance, other);

        /// <summary>
        /// Check whether the online ID of two <see cref="IRulesetInfo"/>s match.
        /// </summary>
        /// <param name="instance">The instance to compare.</param>
        /// <param name="other">The other instance to compare against.</param>
        /// <returns>Whether online IDs match. If either instance is missing an online ID, this will return false.</returns>
        public static bool MatchesOnlineID(this IRulesetInfo? instance, IRulesetInfo? other) => matchesOnlineID(instance, other);

        /// <summary>
        /// Check whether the online ID of two <see cref="APIUser"/>s match.
        /// </summary>
        /// <param name="instance">The instance to compare.</param>
        /// <param name="other">The other instance to compare against.</param>
        /// <returns>Whether online IDs match. If either instance is missing an online ID, this will return false.</returns>
        public static bool MatchesOnlineID(this APIUser? instance, APIUser? other) => matchesOnlineID(instance, other);

        /// <summary>
        /// Check whether the online ID of two <see cref="IScoreInfo"/>s match.
        /// </summary>
        /// <param name="instance">The instance to compare.</param>
        /// <param name="other">The other instance to compare against.</param>
        /// <returns>
        /// Whether online IDs match.
        /// Both <see cref="IHasOnlineID{T}.OnlineID"/> and <see cref="IScoreInfo.LegacyOnlineID"/> are checked, in that order.
        /// If either instance is missing an online ID, this will return false.
        /// </returns>
        public static bool MatchesOnlineID(this IScoreInfo? instance, IScoreInfo? other)
        {
            if (matchesOnlineID(instance, other))
                return true;

            if (instance == null || other == null)
                return false;

            if (instance.LegacyOnlineID < 0 || other.LegacyOnlineID < 0)
                return false;

            return instance.LegacyOnlineID.Equals(other.LegacyOnlineID);
        }

        /// <summary>
        /// Checks whether two scores describe the same online score, including enough surrounding
        /// context to avoid treating score IDs from different servers or special rulesets as equal.
        /// </summary>
        public static bool MatchesOnlineIDAndContext(this IScoreInfo? instance, IScoreInfo? other)
            => instance.MatchesOnlineID(other) && instance.MatchesScoreContext(other);

        /// <summary>
        /// Checks the contextual identity of two scores without comparing their score IDs.
        /// </summary>
        public static bool MatchesScoreContext(this IScoreInfo? instance, IScoreInfo? other)
        {
            if (instance == null || other == null)
                return false;

            string instanceBeatmapHash = getScoreBeatmapHash(instance);
            string otherBeatmapHash = getScoreBeatmapHash(other);
            string instanceBeatmapMd5Hash = instance.Beatmap?.MD5Hash ?? string.Empty;
            string otherBeatmapMd5Hash = other.Beatmap?.MD5Hash ?? string.Empty;

            // Server-exclusive beatmaps can temporarily have ambiguous online metadata while a
            // set is being imported or refreshed. The file hash identifies the exact difficulty
            // and prevents a replay from another difficulty in the same set being reused.
            if (!string.IsNullOrEmpty(instanceBeatmapHash) && !string.IsNullOrEmpty(otherBeatmapHash)
                && !string.Equals(instanceBeatmapHash, otherBeatmapHash, System.StringComparison.OrdinalIgnoreCase))
                return false;

            // The legacy replay format stores this checksum, so it is also the most reliable
            // discriminator when deciding whether a downloaded replay belongs to this difficulty.
            if (!string.IsNullOrEmpty(instanceBeatmapMd5Hash) && !string.IsNullOrEmpty(otherBeatmapMd5Hash)
                && !string.Equals(instanceBeatmapMd5Hash, otherBeatmapMd5Hash, System.StringComparison.OrdinalIgnoreCase))
                return false;

            if (instance.Beatmap?.OnlineID > 0 && other.Beatmap?.OnlineID > 0
                && instance.Beatmap.OnlineID != other.Beatmap.OnlineID)
                return false;

            string instanceRuleset = getScoreRulesetKey(instance);
            string otherRuleset = getScoreRulesetKey(other);

            if (!string.IsNullOrEmpty(instanceRuleset) && !string.IsNullOrEmpty(otherRuleset)
                && !string.Equals(instanceRuleset, otherRuleset, System.StringComparison.Ordinal))
                return false;

            if (instance.User.OnlineID > 0 && other.User.OnlineID > 0
                && instance.User.OnlineID != other.User.OnlineID)
                return false;

            return true;
        }

        private static string getScoreBeatmapHash(IScoreInfo score)
        {
            if (!string.IsNullOrEmpty(score.Beatmap?.Hash))
                return score.Beatmap.Hash;

            // A score can keep the hash it was played on after its linked BeatmapInfo disappears
            // (for example after a local beatmap update).
            return score is ScoreInfo scoreInfo ? scoreInfo.BeatmapHash : string.Empty;
        }

        private static string getScoreRulesetKey(IScoreInfo score)
        {
            if (score.Ruleset.IsSpecialRuleset())
                return score.Ruleset.ShortName;

            if (score is ScoreInfo scoreInfo)
                return scoreInfo.Ruleset.CreateSpecialRulesetByScore(scoreInfo)?.ShortName ?? scoreInfo.Ruleset.ShortName;

            return score.Ruleset.ShortName;
        }

        private static bool matchesOnlineID(this IHasOnlineID<long>? instance, IHasOnlineID<long>? other)
        {
            if (instance == null || other == null)
                return false;

            if (instance.OnlineID < 0 || other.OnlineID < 0)
                return false;

            return instance.OnlineID.Equals(other.OnlineID);
        }

        private static bool matchesOnlineID(this IHasOnlineID<int>? instance, IHasOnlineID<int>? other)
        {
            if (instance == null || other == null)
                return false;

            if (instance.OnlineID < 0 || other.OnlineID < 0)
                return false;

            return instance.OnlineID.Equals(other.OnlineID);
        }

        // intentionally chosen to match stable.
        // see https://referencesource.microsoft.com/#mscorlib/system/io/path.cs,88
        private static readonly char[] invalid_filename_chars =
        {
            '\"', '<', '>', '|', '\0', (char)1, (char)2, (char)3, (char)4, (char)5, (char)6, (char)7, (char)8, (char)9, (char)10, (char)11, (char)12, (char)13, (char)14, (char)15, (char)16, (char)17,
            (char)18, (char)19, (char)20, (char)21, (char)22, (char)23, (char)24, (char)25, (char)26, (char)27, (char)28, (char)29, (char)30, (char)31, ':', '*', '?', '\\', '/'
        };

        /// <summary>
        /// Create a valid filename which should work across all platforms.
        /// </summary>
        /// <remarks>
        /// <para>
        /// We are using this in place of <see cref="Path.GetInvalidFileNameChars"/>
        /// as that function works per-platform, and therefore returns a different set of characters on different OSes.
        /// </para>
        /// <para>
        /// Note that the behaviour of this method is LOAD-BEARING for things such as interoperability of beatmap exports with stable,
        /// especially with respect to beatmap submission.
        /// DO NOT CHANGE THE SEMANTICS OF THIS METHOD unless you know well what you are doing.
        /// </para>
        /// </remarks>
        /// <seealso href="https://github.com/peppy/osu-stable-reference/blob/67795dba3c308e7d0493b296149dcb073ca47ecb/osu!common/Helpers/GeneralHelper.cs#L41-L46"/>
        public static string GetValidFilename(this string filename)
        {
            foreach (char c in invalid_filename_chars)
                filename = filename.Replace(c.ToString(), string.Empty);
            return filename;
        }

        public static bool RequiresSupporter(this BeatmapLeaderboardScope scope, bool filterMods)
        {
            switch (scope)
            {
                case BeatmapLeaderboardScope.Local:
                    return false;

                case BeatmapLeaderboardScope.Country:
                case BeatmapLeaderboardScope.Friend:
                    return true;
            }

            return filterMods;
        }
    }
}
