// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio;
using osu.Framework.Bindables;
using osu.Framework.Extensions;
using osu.Framework.Extensions.ObjectExtensions;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Graphics.Cursor;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Placeholders;
using osu.Game.Online.Leaderboards;
using osu.Game.Overlays;
using osu.Game.Rulesets;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Osu.Mods;
using osu.Game.Scoring;
using osu.Game.Screens.Play.Leaderboards;
using osu.Game.Screens.Select;
using osu.Game.Tests.Resources;
using osu.Game.Users;
using osuTK;
using osuTK.Input;

namespace osu.Game.Tests.Visual.SongSelect
{
    public partial class TestSceneBeatmapLeaderboardWedge : SongSelectComponentsTestScene
    {
        protected override bool UseFreshStoragePerRun => true;
        [Cached]
        private readonly OverlayColourProvider colourProvider = new OverlayColourProvider(OverlayColourScheme.Aquamarine);

        private TestBeatmapLeaderboardWedge leaderboard = null!;
        private ScoreManager scoreManager = null!;
        private RulesetStore rulesetStore = null!;
        private BeatmapManager beatmapManager = null!;
        private OsuContextMenuContainer contentContainer = null!;
        private DialogOverlay dialogOverlay = null!;

        private LeaderboardManager leaderboardManager = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));

            dependencies.Cache(rulesetStore = new RealmRulesetStore(Realm));
            dependencies.Cache(beatmapManager = new BeatmapManager(LocalStorage, Realm, null, dependencies.Get<AudioManager>(), Resources, dependencies.Get<GameHost>(), Beatmap.Default));
            dependencies.Cache(scoreManager = new ScoreManager(rulesetStore, () => beatmapManager, LocalStorage, Realm, API));
            dependencies.Cache(leaderboardManager = new LeaderboardManager());
            // Upload/export and metadata reloads must use this fixture's storage and no real API.
            dependencies.CacheAs<Storage>(LocalStorage);
            dependencies.CacheAs<IBeatmapUpdater>(Moq.Mock.Of<IBeatmapUpdater>());

            Dependencies.Cache(Realm);

            return dependencies;
        }

        [BackgroundDependencyLoader]
        private void load()
        {
            LoadComponent(dialogOverlay = new DialogOverlay
            {
                Depth = -1
            });

            LoadComponent(leaderboardManager);

            Child = contentContainer = new OsuContextMenuContainer
            {
                RelativeSizeAxes = Axes.X,
                Height = 500,
                Children = new Drawable[]
                {
                    dialogOverlay,
                }
            };

            AddSliderStep("change relative height", 0f, 1f, 0.65f, v => Schedule(() =>
            {
                contentContainer.Height = v * DrawHeight;
            }));
        }

        [SetUp]
        public void SetUp() => Schedule(() =>
        {
            if (leaderboard.IsNotNull())
                contentContainer.Remove(leaderboard, false);

            contentContainer.Add(leaderboard = new TestBeatmapLeaderboardWedge
            {
                RelativeSizeAxes = Axes.Both,
                State = { Value = Visibility.Visible },
            });
        });

        [SetUpSteps]
        public override void SetUpSteps()
        {
            base.SetUpSteps();
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestUploadButtonStillAppearsAfterReloadAndUsesLegacyPostContract(bool successful)
        {
            UploadServerBeatmapSetRequest? upload = null;
            APIBeatmapSet published = new APIBeatmapSet { OnlineID = 2_000_000_000, Status = BeatmapOnlineStatus.Loved,
                Author = new APIUser { Id = 1, Username = "TestBot" }, Tags = "mosu-exclusive" };
            bool downloaded = false;
            string canonicalFilename = Path.Combine(Path.GetTempPath(), $"mosu-publication-ui-{Guid.NewGuid():N}.osz");
            setScope(BeatmapLeaderboardScope.Global);
            AddStep("prepare unpublished map and local API", () =>
            {
                // Other cases in this scene intentionally log out of the shared dummy API.
                ((DummyAPIAccess)API).SetState(APIState.Online);
                ((DummyAPIAccess)API).HandleRequest = request =>
                {
                    if (request is GetBeatmapRequest)
                    {
                        request.Fail(new APIException("Map is not published", new InvalidOperationException(), System.Net.HttpStatusCode.NotFound));
                        return true;
                    }
                    if (request is DownloadBeatmapSetRequest download)
                    {
                        downloaded = download.Model.OnlineID == published.OnlineID && download.Mirror == DownloadMirror.Server;
                        typeof(APIDownloadRequest).GetMethod("TriggerSuccess", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance,
                            null, new[] { typeof(string) }, null)!.Invoke(download, new object[] { canonicalFilename });
                        return true;
                    }
                    if (request is not UploadServerBeatmapSetRequest publication)
                        return false;
                    upload = publication;
                    if (successful)
                        publication.TriggerSuccess(published);
                    else
                        publication.Fail(new InvalidOperationException("Controlled upload rejection"));
                    return true;
                };
                beatmapManager.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                var set = beatmapManager.GetAllUsableBeatmapSets().First();
                Realm.Write(r =>
                {
                    var managed = r.Find<BeatmapSetInfo>(set.ID)!;
                    managed.OnlineID = -1;
                    foreach (var map in managed.Beatmaps)
                        map.OnlineID = -1;
                });
                var mapId = set.Beatmaps.First().ID;
                Beatmap.Value = beatmapManager.GetWorkingBeatmap(beatmapManager.QueryBeatmap(b => b.ID == mapId)!);
                if (successful)
                    createCanonicalArchive(set, published, canonicalFilename);
                leaderboard.SetState(LeaderboardState.BeatmapUnavailable);
            });
            AddUntilStep("only reload is initially offered", () => unavailableButtons().Length, () => Is.EqualTo(1));
            AddStep("reload map metadata", () => unavailableButtons().Single().TriggerClick());
            AddUntilStep("upload appears alongside reload", () => unavailableButtons().Length, () => Is.EqualTo(2));
            AddStep("click upload to server", () => unavailableButtons().Last().TriggerClick());
            AddUntilStep("legacy publication POST requested", () => upload != null);
            AddAssert("no set or revision required for leaderboard upload", () => upload!.BeatmapSetID == null
                && upload.BaseRevision == null && upload.SubmissionTarget == null);
            if (successful)
            {
                AddUntilStep("canonical server archive requested", () => downloaded);
                AddUntilStep("canonical files imported and selected", () => Beatmap.Value.BeatmapInfo.OnlineID >= 2_000_000_000
                    && Beatmap.Value.BeatmapSetInfo.OnlineID == published.OnlineID
                    && Beatmap.Value.BeatmapInfo.MD5Hash == published.Beatmaps.Single(b => b.OnlineID == Beatmap.Value.BeatmapInfo.OnlineID).MD5Hash);
            }
            AddStep("remove own temporary archive", () => File.Delete(canonicalFilename));
        }

        private void createCanonicalArchive(BeatmapSetInfo set, APIBeatmapSet published, string filename)
        {
            using var exported = new MemoryStream();
            Realm.Run(r => new LegacyBeatmapExporter(LocalStorage).ExportToStream(r.Find<BeatmapSetInfo>(set.ID)!, exported, null));
            exported.Position = 0;
            using var input = new ZipArchive(exported, ZipArchiveMode.Read);
            using var output = new ZipArchive(File.Create(filename), ZipArchiveMode.Create);
            int id = 2_000_000_000;
            foreach (var entry in input.Entries)
            {
                using var source = entry.Open();
                using var target = output.CreateEntry(entry.FullName).Open();
                if (!entry.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                {
                    source.CopyTo(target);
                    continue;
                }
                using var reader = new StreamReader(source);
                string text = reader.ReadToEnd();
                int mapId = id++;
                text = Regex.Replace(text, @"(?m)^BeatmapID:[^\r\n]*", $"BeatmapID:{mapId}");
                text = Regex.Replace(text, @"(?m)^BeatmapSetID:[^\r\n]*", $"BeatmapSetID:{published.OnlineID}");
                byte[] bytes = new UTF8Encoding(false).GetBytes(text);
                target.Write(bytes);
                string difficulty = Regex.Match(text, @"(?m)^Version:([^\r\n]*)").Groups[1].Value;
                int mode = int.Parse(Regex.Match(text, @"(?m)^Mode:[ \t]*([0-3])").Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                published.Beatmaps = published.Beatmaps.Append(new APIBeatmap
                {
                    OnlineID = mapId, DifficultyName = difficulty, RulesetID = mode,
                    Checksum = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(bytes)).ToLowerInvariant(),
                    Status = BeatmapOnlineStatus.Loved, BeatmapSet = published,
                }).ToArray();
            }
        }

        private OsuAnimatedButton[] unavailableButtons() => leaderboard.ChildrenOfType<Placeholder>()
            .SelectMany(p => p.ChildrenOfType<OsuAnimatedButton>()).ToArray();

        [Test]
        public void TestPersonalBest()
        {
            AddStep(@"Show personal best", showPersonalBest);
            AddUntilStep("personal best loaded asynchronously", () => leaderboard.ChildrenOfType<BeatmapLeaderboardScore>().Count(), () => Is.GreaterThan(0));
        }

        [Test]
        public void TestRapidPersonalBestReplacement()
        {
            AddStep("queue personal best then replace scores", () =>
            {
                showPersonalBest();
                leaderboard.SetScores(Array.Empty<ScoreInfo>());
            });
            AddWaitStep("allow cancelled load to settle", 5);
            AddAssert("no stale personal best card", () => leaderboard.ChildrenOfType<BeatmapLeaderboardScore>().Count(), () => Is.Zero);
        }

        [Test]
        public void TestDoesNotReceiveInputOutsideBounds()
        {
            AddAssert("outside input is rejected", () => !leaderboard.ReceivePositionalInputAt(new Vector2(-100, -100)));
        }

        [Test]
        public void TestGlobalScoresDisplay()
        {
            setScope(BeatmapLeaderboardScope.Global);

            AddStep(@"New Scores", () => leaderboard.SetScores(GenerateSampleScores(new BeatmapInfo())));
            AddStep(@"New Scores with teams", () => leaderboard.SetScores(GenerateSampleScores(new BeatmapInfo()).Select(s =>
            {
                s.User.Team = new APITeam();
                return s;
            })));
        }

        [Test]
        public void TestPersonalBestWithNullPosition()
        {
            AddStep("null personal best position", showPersonalBestWithNullPosition);
        }

        [Test]
        public void TestPlaceholderStates()
        {
            AddStep("ensure no scores displayed", () => leaderboard.SetScores(Array.Empty<ScoreInfo>()));

            AddStep(@"Retrieving", () => leaderboard.SetState(LeaderboardState.Retrieving));
            AddStep(@"Network failure", () => leaderboard.SetState(LeaderboardState.NetworkFailure));
            AddStep(@"No team", () => leaderboard.SetState(LeaderboardState.NoTeam));
            AddStep(@"No supporter", () => leaderboard.SetState(LeaderboardState.NotSupporter));
            AddStep(@"Not logged in", () => leaderboard.SetState(LeaderboardState.NotLoggedIn));
            AddStep(@"Ruleset unavailable", () => leaderboard.SetState(LeaderboardState.RulesetUnavailable));
            AddStep(@"Beatmap unavailable", () => leaderboard.SetState(LeaderboardState.BeatmapUnavailable));
            AddStep(@"None selected", () => leaderboard.SetState(LeaderboardState.NoneSelected));
        }

        [Test]
        public void TestUseTheseModsDoesNotCopySystemMods()
        {
            AddStep(@"set scores", () => leaderboard.SetScores(GenerateSampleScores(new BeatmapInfo()), new ScoreInfo
            {
                OnlineID = 1337,
                Position = 999,
                Rank = ScoreRank.XH,
                Accuracy = 1,
                MaxCombo = 244,
                TotalScore = 1707827,
                Ruleset = new OsuRuleset().RulesetInfo,
                Mods = new Mod[] { new OsuModHidden(), new ModScoreV2(), },
                User = new APIUser
                {
                    Id = 6602580,
                    Username = @"waaiiru",
                    CountryCode = CountryCode.ES,
                }
            }));
            AddUntilStep("wait for scores", () => this.ChildrenOfType<BeatmapLeaderboardScore>().Count(), () => Is.GreaterThan(0));
            AddStep("right click panel", () =>
            {
                InputManager.MoveMouseTo(this.ChildrenOfType<BeatmapLeaderboardScore>().Last());
                InputManager.Click(MouseButton.Right);
            });
            AddStep("click use these mods", () =>
            {
                InputManager.MoveMouseTo(this.ChildrenOfType<DrawableOsuMenuItem>().First());
                InputManager.Click(MouseButton.Left);
            });
            AddAssert("received HD", () => this.ChildrenOfType<BeatmapLeaderboardScore>().Last().SelectedMods.Value.Any(m => m is OsuModHidden));
            AddAssert("did not receive SV2", () => !this.ChildrenOfType<BeatmapLeaderboardScore>().Last().SelectedMods.Value.Any(m => m is ModScoreV2));
        }

        [Test]
        public void TestLocalScoresDisplay()
        {
            BeatmapInfo beatmapInfo = null!;

            setScope(BeatmapLeaderboardScope.Local);

            AddStep(@"Set beatmap", () =>
            {
                beatmapManager.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                beatmapInfo = beatmapManager.GetAllUsableBeatmapSets().First().Beatmaps.First();

                Beatmap.Value = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            });

            clearScores();
            checkDisplayedCount(0);

            importMoreScores(() => beatmapInfo);
            checkDisplayedCount(10);

            importMoreScores(() => beatmapInfo);
            checkDisplayedCount(20);

            clearScores();
            checkDisplayedCount(0);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestManagedLocalCriteria(bool recycleBeforeQuery)
        {
            BeatmapInfo info = null!;
            setScope(BeatmapLeaderboardScope.Local);
            AddStep("import map", () =>
            {
                beatmapManager.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                info = beatmapManager.GetAllUsableBeatmapSets().SelectMany(s => s.Beatmaps).First(b => b.Ruleset.OnlineID == 0);
                Beatmap.Value = beatmapManager.GetWorkingBeatmap(info);
            });
            clearScores();
            importMoreScores(() => info);
            checkDisplayedCount(10);
            AddStep("query using Update-owned models", () => Realm.Run(r =>
            {
                var managed = r.Find<BeatmapInfo>(info.ID)!;
                Assert.That(managed.IsManaged, Is.True);
                var criteria = new LeaderboardCriteria(managed, managed.Ruleset, BeatmapLeaderboardScope.Local, null);
                var query = LeaderboardManager.LocalScoreQuery.FromCriteria(criteria);
                Assert.That(LeaderboardManager.readLocalScores(r, query), Has.Length.EqualTo(10), $"{query}");
                leaderboardManager.FetchWithCriteria(criteria, true);
            }));
            if (recycleBeforeQuery)
                AddStep("recycle with snapshot queued", () =>
                {
                    using (Realm.BlockAllOperations("leaderboard managed criteria recycle")) { }
                });
            AddAssert("local criteria retained", () => leaderboardManager.CurrentCriteria?.Scope == BeatmapLeaderboardScope.Local);
            AddUntilStep("detached snapshot returned", () => leaderboardManager.Scores.Value?.TopScores.Count, () => Is.EqualTo(10));
            AddAssert("successful local query", () => leaderboardManager.Scores.Value?.FailState == null);
            clearScores();
            AddUntilStep("subscribed deletion updates snapshot", () => leaderboardManager.Scores.Value?.TopScores.Count, () => Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TestLocalRelaxScoresDisplay(bool useMosuRelax)
        {
            BeatmapInfo beatmapInfo = null!;
            Mod relaxMod = useMosuRelax ? new OsuModMosuRelax() : new OsuModRelax();

            setScope(BeatmapLeaderboardScope.Local);

            AddStep(@"Set beatmap", () =>
            {
                beatmapManager.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                beatmapInfo = beatmapManager.GetAllUsableBeatmapSets().First().Beatmaps.First();

                Beatmap.Value = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            });

            clearScores();
            checkDisplayedCount(0);

            AddStep(@"Import relax score", () =>
            {
                var score = GenerateSampleScores(beatmapInfo).First();
                score.Mods = new[] { relaxMod };
                scoreManager.Import(score);
            });

            AddStep(@"Select relax and filter by selected mods", () =>
            {
                SelectedMods.Value = new[] { relaxMod };
                ((Bindable<bool>)leaderboard.FilterBySelectedMods).Value = true;
            });

            checkDisplayedCount(1);
        }

        [Test]
        public void TestRelaxSelectionRefetchesWithoutSelectedModFilter()
        {
            BeatmapInfo beatmapInfo = null!;

            setScope(BeatmapLeaderboardScope.Local);

            AddStep(@"Set beatmap", () =>
            {
                beatmapManager.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                beatmapInfo = beatmapManager.GetAllUsableBeatmapSets().First().Beatmaps.First();
                Beatmap.Value = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            });

            AddUntilStep("normal leaderboard requested", () => leaderboardManager.CurrentCriteria?.Ruleset?.ShortName, () => Is.EqualTo(RulesetInfo.OSU_MODE_SHORTNAME));
            AddStep("select relax without mod filter", () => SelectedMods.Value = new Mod[] { new OsuModRelax() });
            AddUntilStep("relax leaderboard requested", () => leaderboardManager.CurrentCriteria?.Ruleset?.ShortName, () => Is.EqualTo(RulesetInfo.OSU_RELAX_MODE_SHORTNAME));
        }

        [Test]
        public void TestLocalScoresDisplayWorksWhenStartingOffline()
        {
            BeatmapInfo beatmapInfo = null!;

            AddStep("Log out", () => API.Logout());
            setScope(BeatmapLeaderboardScope.Local);

            AddStep(@"Import beatmap", () =>
            {
                beatmapManager.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                beatmapInfo = beatmapManager.GetAllUsableBeatmapSets().First().Beatmaps.First();

                Beatmap.Value = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            });

            clearScores();
            importMoreScores(() => beatmapInfo);
            checkDisplayedCount(10);
        }

        [Test]
        public void TestLocalScoresDisplayOnBeatmapEdit()
        {
            BeatmapInfo beatmapInfo = null!;
            string originalHash = string.Empty;

            setScope(BeatmapLeaderboardScope.Local);

            AddStep(@"Import beatmap", () =>
            {
                beatmapManager.Import(TestResources.GetQuickTestBeatmapForImport()).WaitSafely();
                beatmapInfo = beatmapManager.GetAllUsableBeatmapSets().First().Beatmaps.First();

                Beatmap.Value = beatmapManager.GetWorkingBeatmap(beatmapInfo);
            });

            clearScores();
            checkDisplayedCount(0);

            AddStep(@"Perform initial save to guarantee stable hash", () =>
            {
                IBeatmap beatmap = beatmapManager.GetWorkingBeatmap(beatmapInfo).Beatmap;
                beatmapManager.Save(beatmapInfo, beatmap);

                originalHash = beatmapInfo.Hash;
            });

            importMoreScores(() => beatmapInfo);

            checkDisplayedCount(10);
            checkStoredCount(10);

            AddStep(@"Save with changes", () =>
            {
                IBeatmap beatmap = beatmapManager.GetWorkingBeatmap(beatmapInfo).Beatmap;
                beatmap.Difficulty.ApproachRate = 12;
                beatmapManager.Save(beatmapInfo, beatmap);
            });

            AddAssert("Hash changed", () => beatmapInfo.Hash, () => Is.Not.EqualTo(originalHash));
            checkDisplayedCount(0);
            checkStoredCount(10);

            importMoreScores(() => beatmapInfo);
            importMoreScores(() => beatmapInfo);
            checkDisplayedCount(20);
            checkStoredCount(30);

            AddStep(@"Revert changes", () =>
            {
                IBeatmap beatmap = beatmapManager.GetWorkingBeatmap(beatmapInfo).Beatmap;
                beatmap.Difficulty.ApproachRate = 8;
                beatmapManager.Save(beatmapInfo, beatmap);
            });

            AddAssert("Hash restored", () => beatmapInfo.Hash, () => Is.EqualTo(originalHash));
            checkDisplayedCount(10);
            checkStoredCount(30);

            clearScores();
            checkDisplayedCount(0);
            checkStoredCount(0);
        }

        private void showPersonalBestWithNullPosition()
        {
            leaderboard.SetScores(GenerateSampleScores(new BeatmapInfo()), new ScoreInfo
            {
                OnlineID = 1337,
                Rank = ScoreRank.XH,
                Accuracy = 1,
                MaxCombo = 244,
                TotalScore = 1707827,
                Mods = new Mod[] { new OsuModHidden(), new OsuModHardRock() },
                Ruleset = new OsuRuleset().RulesetInfo,
                User = new APIUser
                {
                    Id = 6602580,
                    Username = @"waaiiru",
                    CountryCode = CountryCode.ES,
                },
                Date = DateTimeOffset.Now,
            }, 1234567);
        }

        private void showPersonalBest()
        {
            leaderboard.SetScores(GenerateSampleScores(new BeatmapInfo()), new ScoreInfo
            {
                OnlineID = 1337,
                Position = 999,
                Rank = ScoreRank.XH,
                Accuracy = 1,
                MaxCombo = 244,
                TotalScore = 1707827,
                Ruleset = new OsuRuleset().RulesetInfo,
                Mods = new Mod[] { new OsuModHidden(), new OsuModHardRock() },
                User = new APIUser
                {
                    Id = 6602580,
                    Username = @"waaiiru",
                    CountryCode = CountryCode.ES,
                },
                Date = DateTimeOffset.Now,
            }, 1234567);
        }

        private void setScope(BeatmapLeaderboardScope scope)
        {
            AddStep(@"Set scope", () => ((Bindable<BeatmapLeaderboardScope>)leaderboard.Scope).Value = scope);
        }

        private void importMoreScores(Func<BeatmapInfo> beatmapInfo)
        {
            AddStep(@"Import new scores", () =>
            {
                foreach (var score in GenerateSampleScores(beatmapInfo()))
                    scoreManager.Import(score);
            });
        }

        private void clearScores()
        {
            AddStep("Clear all scores", () => scoreManager.Delete());
        }

        private void checkDisplayedCount(int expected) =>
            AddUntilStep($"{expected} scores displayed", () => leaderboard.ChildrenOfType<BeatmapLeaderboardScore>().Count(), () => Is.EqualTo(expected));

        private void checkStoredCount(int expected) =>
            AddUntilStep($"Total scores stored is {expected}", () => Realm.Run(r => r.All<ScoreInfo>().Count(s => !s.DeletePending)), () => Is.EqualTo(expected));

        private partial class TestBeatmapLeaderboardWedge : BeatmapLeaderboardWedge
        {
            public new void SetState(LeaderboardState state) => base.SetState(state);
            public new void SetScores(IEnumerable<ScoreInfo> scores, ScoreInfo? userScore = null, int? totalCount = null) => base.SetScores(scores, userScore, totalCount);
        }

        public static ScoreInfo[] GenerateSampleScores(BeatmapInfo beatmapInfo)
        {
            return new[]
            {
                new ScoreInfo
                {
                    Rank = ScoreRank.XH,
                    Accuracy = 1,
                    MaxCombo = 244,
                    TotalScore = 1707827,
                    Date = DateTime.Now,
                    Mods = new Mod[]
                    {
                        new OsuModHidden(),
                        new OsuModFlashlight
                        {
                            FollowDelay = { Value = 200 },
                            SizeMultiplier = { Value = 5 },
                        },
                        new OsuModDifficultyAdjust
                        {
                            CircleSize = { Value = 11 },
                            ApproachRate = { Value = 10 },
                            OverallDifficulty = { Value = 10 },
                            DrainRate = { Value = 10 },
                            ExtendedLimits = { Value = true }
                        }
                    },
                    Ruleset = new OsuRuleset().RulesetInfo,
                    BeatmapInfo = beatmapInfo,
                    BeatmapHash = beatmapInfo.Hash,
                    User = new APIUser
                    {
                        Id = 6602580,
                        Username = @"waaiiru",
                        CountryCode = CountryCode.ES,
                    },
                },
                new ScoreInfo
                {
                    Rank = ScoreRank.X,
                    Accuracy = 1,
                    MaxCombo = 244,
                    TotalScore = 1707827,
                    Date = DateTime.Now.AddSeconds(-30),
                    Mods = new Mod[] { new OsuModHidden(), new OsuModHardRock(), },
                    BeatmapInfo = beatmapInfo,
                    BeatmapHash = beatmapInfo.Hash,
                    Ruleset = new OsuRuleset().RulesetInfo,
                    User = new APIUser
                    {
                        Id = 4608074,
                        Username = @"Skycries",
                        CountryCode = CountryCode.BR,
                    },
                },
                new ScoreInfo
                {
                    Rank = ScoreRank.SH,
                    Accuracy = 1,
                    MaxCombo = 244,
                    TotalScore = 1707827,
                    Date = DateTime.Now.AddSeconds(-70),
                    Mods = new Mod[] { new OsuModHidden(), new OsuModHardRock(), },
                    BeatmapInfo = beatmapInfo,
                    BeatmapHash = beatmapInfo.Hash,
                    Ruleset = new OsuRuleset().RulesetInfo,

                    User = new APIUser
                    {
                        Id = 1014222,
                        Username = @"eLy",
                        CountryCode = CountryCode.JP,
                    },
                },
                new ScoreInfo
                {
                    Rank = ScoreRank.S,
                    Accuracy = 1,
                    MaxCombo = 244,
                    TotalScore = 1707827,
                    Date = DateTime.Now.AddMinutes(-40),
                    Mods = new Mod[] { new OsuModHidden(), new OsuModHardRock(), },
                    BeatmapInfo = beatmapInfo,
                    BeatmapHash = beatmapInfo.Hash,
                    Ruleset = new OsuRuleset().RulesetInfo,

                    User = new APIUser
                    {
                        Id = 1541390,
                        Username = @"Toukai",
                        CountryCode = CountryCode.CA,
                    },
                },
                new ScoreInfo
                {
                    Rank = ScoreRank.A,
                    Accuracy = 1,
                    MaxCombo = 244,
                    TotalScore = 1707827,
                    Date = DateTime.Now.AddHours(-2),
                    Mods = new Mod[] { new OsuModHidden(), new OsuModHardRock(), },
                    BeatmapInfo = beatmapInfo,
                    BeatmapHash = beatmapInfo.Hash,
                    Ruleset = new OsuRuleset().RulesetInfo,

                    User = new APIUser
                    {
                        Id = 2243452,
                        Username = @"Satoruu",
                        CountryCode = CountryCode.VE,
                    },
                },
                new ScoreInfo
                {
                    Rank = ScoreRank.B,
                    Accuracy = 0.9826,
                    MaxCombo = 244,
                    TotalScore = 1707827,
                    Date = DateTime.Now.AddHours(-25),
                    Mods = new Mod[] { new OsuModHidden(), new OsuModHardRock(), },
                    BeatmapInfo = beatmapInfo,
                    BeatmapHash = beatmapInfo.Hash,
                    Ruleset = new OsuRuleset().RulesetInfo,

                    User = new APIUser
                    {
                        Id = 2705430,
                        Username = @"Mooha",
                        CountryCode = CountryCode.FR,
                    },
                },
                new ScoreInfo
                {
                    Rank = ScoreRank.C,
                    Accuracy = 0.9654,
                    MaxCombo = 244,
                    TotalScore = 1707827,
                    Date = DateTime.Now.AddHours(-50),
                    Mods = new Mod[] { new OsuModHidden(), new OsuModHardRock(), },
                    BeatmapInfo = beatmapInfo,
                    BeatmapHash = beatmapInfo.Hash,
                    Ruleset = new OsuRuleset().RulesetInfo,

                    User = new APIUser
                    {
                        Id = 7151382,
                        Username = @"Mayuri Hana",
                        CountryCode = CountryCode.TH,
                    },
                },
                new ScoreInfo
                {
                    Rank = ScoreRank.D,
                    Accuracy = 0.6025,
                    MaxCombo = 244,
                    TotalScore = 1707827,
                    Date = DateTime.Now.AddHours(-72),
                    Mods = new Mod[] { new OsuModHidden(), new OsuModHardRock(), },
                    BeatmapInfo = beatmapInfo,
                    BeatmapHash = beatmapInfo.Hash,
                    Ruleset = new OsuRuleset().RulesetInfo,

                    User = new APIUser
                    {
                        Id = 2051389,
                        Username = @"FunOrange",
                        CountryCode = CountryCode.CA,
                    },
                },
                new ScoreInfo
                {
                    Rank = ScoreRank.D,
                    Accuracy = 0.5140,
                    MaxCombo = 244,
                    TotalScore = 1707827,
                    Date = DateTime.Now.AddMonths(-10),
                    Mods = new Mod[] { new OsuModHidden(), new OsuModHardRock(), },
                    BeatmapInfo = beatmapInfo,
                    BeatmapHash = beatmapInfo.Hash,
                    Ruleset = new OsuRuleset().RulesetInfo,

                    User = new APIUser
                    {
                        Id = 6169483,
                        Username = @"-Hebel-",
                        CountryCode = CountryCode.MX,
                    },
                },
                new ScoreInfo
                {
                    Rank = ScoreRank.D,
                    Accuracy = 0.4222,
                    MaxCombo = 244,
                    TotalScore = 1707827,
                    Date = DateTime.Now.AddYears(-2),
                    Mods = new Mod[] { new OsuModHidden(), new OsuModHardRock(), },
                    BeatmapInfo = beatmapInfo,
                    BeatmapHash = beatmapInfo.Hash,
                    Ruleset = new OsuRuleset().RulesetInfo,

                    User = new APIUser
                    {
                        Id = 6702666,
                        Username = @"prhtnsm",
                        CountryCode = CountryCode.DE,
                    },
                },
            };
        }

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);

            if (rulesetStore.IsNotNull())
                rulesetStore.Dispose();
        }
    }
}
