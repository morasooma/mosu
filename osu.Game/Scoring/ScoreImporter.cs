// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.IO.Archives;
using osu.Game.Rulesets;
using osu.Game.Scoring.Legacy;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Utils;
using Realms;

namespace osu.Game.Scoring
{
    public class ScoreImporter : RealmArchiveModelImporter<ScoreInfo>
    {
        public override IEnumerable<string> HandledExtensions => new[] { ".osr" };

        protected override string[] HashableFileTypes => new[] { ".osr" };

        private readonly RulesetStore rulesets;
        private readonly Func<BeatmapManager> beatmaps;

        private readonly IAPIProvider api;

        public ScoreImporter(RulesetStore rulesets, Func<BeatmapManager> beatmaps, Storage storage, RealmAccess realm, IAPIProvider api)
            : base(storage, realm)
        {
            this.rulesets = rulesets;
            this.beatmaps = beatmaps;
            this.api = api;
        }

        protected override ScoreInfo? CreateModel(ArchiveReader archive, ImportParameters parameters)
        {
            string name = archive.Filenames.First(f => f.EndsWith(".osr", StringComparison.OrdinalIgnoreCase));

            using (var stream = archive.GetStream(name))
            {
                try
                {
                    return new DatabasedLegacyScoreDecoder(rulesets, beatmaps()).Parse(stream).ScoreInfo;
                }
                catch (LegacyScoreDecoder.BeatmapNotFoundException notFound)
                {
                    Logger.Log($@"Score '{archive.Name}' failed to import: no corresponding beatmap with the hash '{notFound.Hash}' could be found.", LoggingTarget.Database);

                    if (!parameters.Batch)
                    {
                        // In the case of a missing beatmap, let's attempt to resolve it and show a prompt to the user to download the required beatmap.
                        var req = new GetBeatmapRequest(new BeatmapInfo { MD5Hash = notFound.Hash });
                        req.Success += res => PostNotification?.Invoke(new MissingBeatmapNotification(res, notFound.Hash, archive));
                        api.Queue(req);
                    }

                    return null;
                }
                catch (Exception e)
                {
                    Logger.Log($@"Failed to parse headers of score '{archive.Name}': {e}.", LoggingTarget.Database);
                    return null;
                }
            }
        }

        public Score GetScore(ScoreInfo score) => new LegacyDatabasedScore(score, rulesets, beatmaps(), Files.Store);

        protected override void Populate(ScoreInfo model, ArchiveReader? archive, Realm realm, CancellationToken cancellationToken = default)
        {
            Debug.Assert(model.BeatmapInfo != null);

            // Ensure the beatmap is not detached.
            if (!model.BeatmapInfo.IsManaged)
            {
                var found = realm.Find<BeatmapInfo>(model.BeatmapInfo.ID);
                if (found != null)
                    model.BeatmapInfo = found;
                else if (StablePathManager.IsStableBeatmap(model.BeatmapInfo.ID))
                {
                    var original = model.BeatmapInfo;
                    var matchingDownloadedBeatmap = string.IsNullOrEmpty(original.MD5Hash)
                        ? null
                        : realm.All<BeatmapInfo>()
                               .Filter($@"{nameof(BeatmapInfo.BeatmapSet)}.{nameof(BeatmapSetInfo.DeletePending)} == false"
                                       + $@" AND {nameof(BeatmapInfo.MD5Hash)} == $0", original.MD5Hash)
                               .AsEnumerable()
                               .OrderByDescending(beatmap => beatmap.OnlineID == original.OnlineID)
                               .FirstOrDefault();

                    if (matchingDownloadedBeatmap != null)
                    {
                        model.BeatmapInfo = matchingDownloadedBeatmap;
                        model.BeatmapHash = matchingDownloadedBeatmap.Hash;
                    }
                    else
                    {
                        // Clone it and null out the BeatmapSet so we don't insert a ghost BeatmapSet.
                        // This prevents the original BeatmapInfo from becoming managed, which caused RealmClosedException.
                        model.BeatmapInfo = original.Clone();
                        model.BeatmapInfo.Difficulty = original.Difficulty?.Clone();
                        model.BeatmapInfo.Metadata = original.Metadata?.DeepClone();
                        if (original.UserSettings != null)
                            model.BeatmapInfo.UserSettings = new BeatmapUserSettings { Offset = original.UserSettings.Offset };
                        model.BeatmapInfo.BeatmapSet = null;
                    }
                }
                else
                {
                    model.BeatmapInfo = null!;
                }
            }

            if (!model.Ruleset.IsManaged)
            {
                RulesetInfo? ruleset = realm.Find<RulesetInfo>(model.Ruleset.ShortName);

                if (ruleset == null && model.Ruleset.IsSpecialRuleset())
                    ruleset = realm.Find<RulesetInfo>(model.Ruleset.CreateNormalRuleset().ShortName);

                model.Ruleset = ruleset!;
            }

            if (model.BeatmapInfo != null && !model.BeatmapInfo.Ruleset.IsManaged)
            {
                model.BeatmapInfo.Ruleset = realm.Find<RulesetInfo>(model.BeatmapInfo.Ruleset.ShortName)!;

                if (model.BeatmapInfo.BeatmapSet != null)
                {
                    foreach (var b in model.BeatmapInfo.BeatmapSet.Beatmaps)
                    {
                        if (!b.Ruleset.IsManaged)
                            b.Ruleset = realm.Find<RulesetInfo>(b.Ruleset.ShortName)!;
                    }
                }
            }

            // These properties are known to be non-null, but these final checks ensure a null hasn't come from somewhere (or the refetch has failed).
            // Under no circumstance do we want these to be written to realm as null.
            ArgumentNullException.ThrowIfNull(model.BeatmapInfo);
            ArgumentNullException.ThrowIfNull(model.Ruleset);

            if (!ModUtils.CheckCompatibleSet(model.Mods))
                throw new InvalidOperationException(@"The score specifies an incompatible set of mods!");

            if (string.IsNullOrEmpty(model.StatisticsJson))
                model.StatisticsJson = JsonConvert.SerializeObject(model.Statistics);

            if (string.IsNullOrEmpty(model.MaximumStatisticsJson))
                model.MaximumStatisticsJson = JsonConvert.SerializeObject(model.MaximumStatistics);
        }

        // Very naive local caching to improve performance of large score imports (where the username is usually the same for most or all scores).

        // TODO: `UserLookupCache` cannot currently be used here because of async foibles.
        // It only supports lookups by user ID (username would require web changes), and even then the ID lookups cannot be used.
        // That is because that component provides an async interface, and async functions cannot be consumed safely here due to the rigid structure of `RealmArchiveModelImporter`.
        // The importer has two paths, one async and one sync; the async path runs the sync path in a task.
        // This means that sometimes `PostImport()` is called from a sync context, and sometimes from an async one, whilst itself being a sync method.
        // That in turn makes `.GetResultSafely()` not callable inside `PostImport()`, as it will throw when called from an async context,
        private readonly Dictionary<int, APIUser> idLookupCache = new Dictionary<int, APIUser>();
        private readonly Dictionary<string, APIUser> usernameLookupCache = new Dictionary<string, APIUser>();

        protected override void PostImport(ScoreInfo model, Realm realm, ImportParameters parameters)
        {
            base.PostImport(model, realm, parameters);

            populateUserDetails(model);

            Debug.Assert(model.BeatmapInfo != null);

            // This needs to be run after user detail population to ensure we have a valid user id.
            if (api.IsLoggedIn && api.LocalUser.Value.OnlineID == model.UserID && (model.BeatmapInfo.LastPlayed == null || model.Date > model.BeatmapInfo.LastPlayed))
                model.BeatmapInfo.LastPlayed = model.Date;
        }

        /// <summary>
        /// Legacy replays only store a username.
        /// This will populate a user ID during import.
        /// </summary>
        private void populateUserDetails(ScoreInfo model)
        {
            if (model.RealmUser.OnlineID == APIUser.SYSTEM_USER_ID)
                return;

            if (model.RealmUser.OnlineID > 1)
            {
                model.User = lookupUserById(model.RealmUser.OnlineID) ?? model.User;
                return;
            }

            if (model.OnlineID < 0 && model.LegacyOnlineID <= 0)
                return;

            model.User = lookupUserByName(model.RealmUser.Username) ?? model.User;
        }

        private APIUser? lookupUserById(int id)
        {
            if (idLookupCache.TryGetValue(id, out var existing))
            {
                return existing;
            }

            var userRequest = new GetUserRequest(id);

            api.Perform(userRequest);

            if (userRequest.Response is APIUser user)
            {
                APIUser cachedUser;

                idLookupCache.TryAdd(id, cachedUser = new APIUser
                {
                    // Because this is a permanent cache, let's only store the pieces we're interested in,
                    // rather than the full API response. If we start to store more than these three fields
                    // in realm, this should be undone.
                    Id = user.Id,
                    Username = user.Username,
                    CountryCode = user.CountryCode,
                });

                return cachedUser;
            }

            return null;
        }

        private APIUser? lookupUserByName(string username)
        {
            if (usernameLookupCache.TryGetValue(username, out var existing))
            {
                return existing;
            }

            var userRequest = new GetUserRequest(username);

            api.Perform(userRequest);

            if (userRequest.Response is APIUser user)
            {
                APIUser cachedUser;

                usernameLookupCache.TryAdd(username, cachedUser = new APIUser
                {
                    // Because this is a permanent cache, let's only store the pieces we're interested in,
                    // rather than the full API response. If we start to store more than these three fields
                    // in realm, this should be undone.
                    Id = user.Id,
                    Username = user.Username,
                    CountryCode = user.CountryCode,
                });

                return cachedUser;
            }

            return null;
        }
    }
}
