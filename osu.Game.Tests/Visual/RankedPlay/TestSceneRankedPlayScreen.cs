// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Extensions;
using osu.Framework.Screens;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Screens.OnlinePlay.Multiplayer.Spectate;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Components;
using osu.Framework.Testing;
using osu.Game.Graphics.UserInterface;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Online.Multiplayer;
using osu.Game.Online.Multiplayer.MatchTypes.RankedPlay;
using osu.Game.Online.Rooms;
using osu.Game.Rulesets.Osu;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Card;
using osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Hand;
using osuTK.Input;

namespace osu.Game.Tests.Visual.RankedPlay
{
    public partial class TestSceneRankedPlayScreen : RankedPlayTestScene
    {
        private RankedPlayScreen screen = null!;

        [Cached(typeof(osu.Game.Audio.PreviewTrackManager))]
        private readonly osu.Game.Tests.Visual.Components.TestScenePreviewTrackManager.TestPreviewTrackManager previewTracks =
            new osu.Game.Tests.Visual.Components.TestScenePreviewTrackManager.TestPreviewTrackManager(new osu.Framework.Audio.AudioAdjustments());

        protected override void LoadComplete()
        {
            base.LoadComplete();
            Add(previewTracks);
        }

        [Resolved]
        private OsuGameBase game { get; set; } = null!;

        [Resolved]
        private BeatmapManager beatmapManager { get; set; } = null!;

        protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
        {
            var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
            dependencies.CacheAs<IBeatmapApiProvider>(new BeatmapApiProvider(API, null));
            return dependencies;
        }

        public override void SetUpSteps()
        {
            base.SetUpSteps();

            AddStep("join room", () => JoinRoom(CreateDefaultRoom(MatchType.RankedPlay)));
            WaitForJoined();
        }

        [TestCase(RankedPlayStage.RoundWarmup)]
        [TestCase(RankedPlayStage.FinishCardDiscard)]
        [TestCase(RankedPlayStage.CardDiscard)]
        [TestCase(RankedPlayStage.CardPlay)]
        [TestCase(RankedPlayStage.FinishCardPlay)]
        [TestCase(RankedPlayStage.GameplayWarmup)]
        [TestCase(RankedPlayStage.Gameplay)]
        [TestCase(RankedPlayStage.Results)]
        [TestCase(RankedPlayStage.Ended)]
        public void TestSpectatorLateJoin(RankedPlayStage stage)
        {
            AddStep("join both participants", () =>
            {
                MultiplayerClient.AddUser(new APIUser { Id = 2 });
                MultiplayerClient.AddUser(new APIUser { Id = 3 });
            });
            AddStep("mock results", () =>
            {
                var api = (DummyAPIAccess)API;
                var handler = api.HandleRequest;
                var sample = GetSampleBeatmaps(Ruleset.Value).First();
                api.HandleRequest = request =>
                {
                    if (request is osu.Game.Online.API.Requests.GetBeatmapsRequest maps)
                    {
                        maps.TriggerSuccess(new osu.Game.Online.API.Requests.GetBeatmapsResponse
                        {
                            Beatmaps = maps.BeatmapIds.Select(id => new APIBeatmap
                            {
                                OnlineID = id, BeatmapSet = sample.BeatmapSet, RulesetID = 0
                            }).ToList()
                        });
                        return true;
                    }
                    if (request is IndexPlaylistScoresRequest index)
                    {
                        index.TriggerSuccess(new IndexedMultiplayerScores
                        {
                            Scores = new System.Collections.Generic.List<MultiplayerScore>
                            {
                                new MultiplayerScore { ID = 2, User = new APIUser { Id = 2 }, TotalScore = 700_000 },
                                new MultiplayerScore { ID = 3, User = new APIUser { Id = 3 }, TotalScore = 800_000 }
                            }
                        });
                        return true;
                    }
                    return handler?.Invoke(request) ?? false;
                };
            });
            AddStep("set observer snapshot", () => MultiplayerClient.RankedPlayChangeStage(stage, state =>
            {
                state.Users.Remove(API.LocalUser.Value.Id);
                state.ActiveUserId = 2;
                if (stage == RankedPlayStage.GameplayWarmup)
                    state.LastPlayedCard = new RankedPlayCardItem();
                foreach (var user in state.Users.Values)
                    user.DamageInfo = new RankedPlayDamageInfo { OldLife = 1_000_000, NewLife = 1_000_000 };
                MultiplayerClient.ClientRoom!.Users.Single(u => u.UserID == API.LocalUser.Value.Id).Role = MultiplayerRoomUserRole.Spectator;
            }).WaitSafely());
            if (stage == RankedPlayStage.Gameplay)
                AddStep("prepare already running gameplay", importSpectatorMap);
            AddStep("load spectator screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!, spectatedPlayerId: 2)));
            AddUntilStep("initial stage displayed", () => screen.ActiveSubScreen != null);
            AddAssert("spectator perspective is participant", () => screen.ChildrenOfType<RankedPlayMatchInfo>().Single().PlayerId == 2);
            AddAssert("spectator opponent is other participant", () => screen.ChildrenOfType<RankedPlayMatchInfo>().Single().OpponentId == 3);
            AddAssert("observer did not leave on missing beatmap", () => MultiplayerClient.RoomJoined);
            AddAssert("spectator chat is absent", () => !screen.ChildrenOfType<RankedPlayChatDisplay>().Any());
            if (stage == RankedPlayStage.GameplayWarmup)
                AddUntilStep("played card created for spectator warmup", () => screen.ChildrenOfType<RankedPlayCard>().Any());
            if (stage == RankedPlayStage.Gameplay)
                AddUntilStep("late join starts gameplay", () => screen.GetChildScreen() is MultiSpectatorScreen { IsLoaded: true });
            AddAssert("not an interactive pick screen", () => !screen.ChildrenOfType<PickScreen>().Any() && !screen.ChildrenOfType<DiscardScreen>().Any());
            AddStep("advance to card play", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardPlay).WaitSafely());
            AddUntilStep("read-only cards displayed", () => screen.ActiveSubScreen is RankedPlaySpectatorCardsScreen);
        }

        [Test]
        public void TestSpectatorGameplayTransitions()
        {
            TestSpectatorLateJoin(RankedPlayStage.CardPlay);

            MultiSpectatorScreen? gameplay = null;
            AddStep("import spectator map", importSpectatorMap);
            AddStep("enter gameplay", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.Gameplay).WaitSafely());
            AddUntilStep("ranked pushes multi spectator", () => (gameplay = screen.GetChildScreen() as MultiSpectatorScreen)?.IsLoaded == true);
            AddStep("receive both replay streams", () =>
            {
                SpectatorClient.SendFramesFromUser(2, 1000);
                SpectatorClient.SendFramesFromUser(3, 1000);
            });
            AddUntilStep("both ranked fields loaded", () => gameplay!.AllPlayersLoaded);
            AddAssert("only two ranked fields", () => gameplay!.ChildrenOfType<PlayerArea>().Count() == 2);
            AddStep("enter results", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.Results).WaitSafely());
            AddUntilStep("returned from gameplay to results", () => screen.IsCurrentScreen() && screen.ActiveSubScreen is ResultsScreen);
            AddUntilStep("both result scores rendered", () => screen.ChildrenOfType<RankedPlayScoreCounter>().Count() >= 2);
            AddStep("next round cards", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardPlay).WaitSafely());
            AddUntilStep("cards continue after results", () => screen.IsCurrentScreen() && screen.ActiveSubScreen is RankedPlaySpectatorCardsScreen);
            AddAssert("observer remains connected", () => MultiplayerClient.RoomJoined);
        }

        [Test]
        public void TestSpectatorUsesReplacementBeatmapTrackInSamePlaylistSlot()
        {
            TestSpectatorLateJoin(RankedPlayStage.CardPlay);

            long slot = 0;
            string previousChecksum = string.Empty;
            AddStep("prepare first card's beatmap", () =>
            {
                var imported = beatmapManager.Import(new ImportTask(osu.Game.Tests.Resources.TestResources.GetQuickTestBeatmapForImport())).GetResultSafely()!;
                var map = beatmapManager.GetAllUsableBeatmapSets().Single(set => set.ID == imported.ID).Beatmaps.First(info => info.Ruleset.OnlineID == 0);
                foreach (var room in new[] { MultiplayerClient.ServerRoom!, MultiplayerClient.ClientRoom! })
                {
                    room.CurrentPlaylistItem.BeatmapID = map.OnlineID;
                    room.CurrentPlaylistItem.BeatmapChecksum = map.MD5Hash;
                    room.CurrentPlaylistItem.RulesetID = 0;
                }
                slot = MultiplayerClient.ClientRoom!.CurrentPlaylistItem.ID;
                previousChecksum = map.MD5Hash;
            });
            AddStep("enter warmup with first revision", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.GameplayWarmup).WaitSafely());
            AddUntilStep("spectator prepared first revision", () => screen.Beatmap.Value.BeatmapInfo.MD5Hash == previousChecksum);
            AddStep("replace beatmap in same slot", importSpectatorMap);
            AddAssert("slot unchanged but revision changed", () => MultiplayerClient.ClientRoom!.CurrentPlaylistItem.ID == slot
                                                                 && MultiplayerClient.ClientRoom.CurrentPlaylistItem.BeatmapChecksum != previousChecksum);
            AddStep("start replacement gameplay", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.Gameplay).WaitSafely());
            AddUntilStep("spectator gameplay loaded", () => screen.GetChildScreen() is MultiSpectatorScreen { IsLoaded: true });
            AddAssert("spectator uses selected revision", () => ((MultiSpectatorScreen)screen.GetChildScreen()!).Beatmap.Value.BeatmapInfo.MD5Hash
                                                               == MultiplayerClient.ClientRoom!.CurrentPlaylistItem.BeatmapChecksum);
            AddAssert("master audio clock uses selected track", () =>
            {
                var gameplay = (MultiSpectatorScreen)screen.GetChildScreen()!;
                var master = gameplay.ChildrenOfType<osu.Game.Screens.Play.MasterGameplayClockContainer>().Single();
                return ReferenceEquals(master.ChildrenOfType<FramedBeatmapClock>().First().Source, gameplay.Beatmap.Value.Track);
            });
        }

        [Test]
        public void TestSpectatorExitThenRejoinWithoutReconnect()
        {
            TestSpectatorLateJoin(RankedPlayStage.CardPlay);
            System.Threading.Tasks.Task leave = null!;
            Room room = null!;
            AddStep("remember room", () => room = MultiplayerClient.ServerAPIRoom!);
            AddStep("exit spectator screen", () => screen.Exit());
            AddUntilStep("server room left", () => !MultiplayerClient.RoomJoined);
            // Repeat the room reset while the exited screen still awaits disposal.
            AddStep("repeat room update", () => leave = MultiplayerClient.LeaveRoom());
            AddUntilStep("repeat leave finished", () => leave.IsCompleted);
            AddStep("no double exit failure", () => Assert.That(leave.Exception, Is.Null));
            AddStep("rejoin without reconnect", () => JoinRoom(room));
            WaitForJoined();
        }

        [Test]
        public void TestSpectatorRoomLossDuringGameplay()
        {
            TestSpectatorLateJoin(RankedPlayStage.CardPlay);
            System.Threading.Tasks.Task leave = null!;
            AddStep("import spectator map", importSpectatorMap);
            AddStep("enter gameplay", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.Gameplay).WaitSafely());
            AddUntilStep("gameplay above ranked", () => screen.GetChildScreen() is MultiSpectatorScreen { IsLoaded: true });
            AddStep("lose room with gameplay still above ranked", () => leave = MultiplayerClient.LeaveRoom());
            AddUntilStep("server leave completed", () => leave.IsCompleted && !MultiplayerClient.RoomJoined);
            AddStep("room loss did not throw", () => Assert.That(leave.Exception, Is.Null));
            AddUntilStep("ranked and gameplay unwound", () => !screen.IsCurrentScreen() && screen.GetChildScreen() == null);
        }

        private void importSpectatorMap()
        {
            var imported = beatmapManager.Import(new ImportTask(osu.Game.Tests.Resources.TestResources.GetTestBeatmapForImport(true))).GetResultSafely()!;
            game.Dependencies.Get<RealmAccess>().Write(r =>
            {
                foreach (var info in r.All<BeatmapInfo>().AsEnumerable().Where(info => info.BeatmapSet!.ID == imported.ID))
                    info.OnlineMD5Hash = info.MD5Hash;
            });
            var map = beatmapManager.GetAllUsableBeatmapSets().Single(set => set.ID == imported.ID).Beatmaps.First(info => info.Ruleset.OnlineID == 0);
            foreach (var room in new[] { MultiplayerClient.ServerRoom!, MultiplayerClient.ClientRoom! })
            {
                room.CurrentPlaylistItem.BeatmapID = map.OnlineID;
                room.CurrentPlaylistItem.BeatmapChecksum = map.MD5Hash;
                room.CurrentPlaylistItem.RulesetID = 0;
            }
            if (((MultiplayerClient)MultiplayerClient).LocalUser!.Role == MultiplayerRoomUserRole.Spectator)
            {
                foreach (int id in ((RankedPlayRoomState)MultiplayerClient.ClientRoom!.MatchState!).Users.Keys)
                {
                    SpectatorClient.SendStartPlay(id, map.OnlineID);
                    MultiplayerClient.ChangeUserState(id, MultiplayerUserState.Playing);
                }
            }
        }

        [TestCase("", true)]
        [TestCase("stale-online-checksum", true)]
        [TestCase("", false)]
        public void TestFreshImportWarmupBeforeMetadataProcessing(string onlineChecksum, bool correctMatchChecksum)
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));
            AddStep("import without background metadata processing", () =>
            {
                var realm = game.Dependencies.Get<RealmAccess>();
                var importer = new BeatmapImporter(LocalStorage, realm);
                var imported = importer.Import(new ImportTask(osu.Game.Tests.Resources.TestResources.GetTestBeatmapForImport(true))).GetResultSafely()!;
                BeatmapInfo map = null!;
                imported.PerformWrite(set =>
                {
                    foreach (var info in set.Beatmaps)
                    {
                        info.OnlineMD5Hash = onlineChecksum;
                        info.LastOnlineUpdate = null;
                    }
                    map = set.Beatmaps.First(info => info.Ruleset.OnlineID == 0).Detach();
                });

                foreach (var room in new[] { MultiplayerClient.ServerRoom!, MultiplayerClient.ClientRoom! })
                {
                    room.CurrentPlaylistItem.BeatmapID = map.OnlineID;
                    room.CurrentPlaylistItem.BeatmapChecksum = correctMatchChecksum ? map.MD5Hash : new string('0', 32);
                    room.CurrentPlaylistItem.RulesetID = 0;
                }

                var api = (DummyAPIAccess)API;
                var handler = api.HandleRequest;
                var sample = GetSampleBeatmaps(Ruleset.Value).First();
                api.HandleRequest = request =>
                {
                    if (request is osu.Game.Online.API.Requests.GetBeatmapsRequest maps)
                    {
                        maps.TriggerSuccess(new osu.Game.Online.API.Requests.GetBeatmapsResponse
                        {
                            Beatmaps = maps.BeatmapIds.Select(id => new APIBeatmap
                            {
                                OnlineID = id, Checksum = map.MD5Hash, BeatmapSet = sample.BeatmapSet, RulesetID = 0
                            }).ToList()
                        });
                        return true;
                    }
                    return handler?.Invoke(request) ?? false;
                };
            });
            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));
            AddUntilStep("match info initialised", () => screen.IsCurrentScreen()
                                                        && screen.ChildrenOfType<RankedPlayMatchInfo>().SingleOrDefault()?.IsLoaded == true);
            AddStep("enter warmup before metadata is committed", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.GameplayWarmup).WaitSafely());
            if (correctMatchChecksum)
            {
                AddUntilStep("freshly imported player becomes ready", () => MultiplayerClient.LocalUser?.State == MultiplayerUserState.Ready);
                AddAssert("player remains in room", () => MultiplayerClient.RoomJoined);
                AddAssert("readiness did not require metadata processing", () => game.Dependencies.Get<RealmAccess>().Run(r =>
                    r.All<BeatmapInfo>().Single(info => info.OnlineID == MultiplayerClient.ClientRoom!.CurrentPlaylistItem.BeatmapID).OnlineMD5Hash == onlineChecksum));
            }
            else
                AddUntilStep("different revision still leaves unplayable room", () => !MultiplayerClient.RoomJoined);
        }

        [Test]
        public void TestOpponentPlayedCardTransfersBeforeReveal()
        {
            OpponentPickScreen pick = null!;
            RankedPlayCard? played = null;
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));
            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));
            AddUntilStep("match loaded", () => screen.ChildrenOfType<RankedPlayMatchInfo>().SingleOrDefault()?.IsLoaded == true);
            AddStep("opponent turn", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardPlay,
                state => state.ActiveUserId = 2).WaitSafely());
            AddUntilStep("opponent pick loaded", () => (pick = (screen.ActiveSubScreen as OpponentPickScreen)!)?.IsLoaded == true);
            AddStep("play unrevealed card", () => MultiplayerClient.PlayUserCard(2, hand => hand[0]).WaitSafely());
            AddUntilStep("played card in centre without metadata", () => (played = pick.CenterRow.Children.SingleOrDefault()) != null);
            AddAssert("reveal still pending", () => !played!.CardRevealed.IsCompleted);
            AddStep("repeat played event", () => MultiplayerClient.PlayUserCard(2, hand => hand[0]).WaitSafely());
            AddAssert("one facade owns the played card", () => pick.CenterRow.Children.Count == 1);
        }

        [Test]
        public void TestMissingWarmupBeatmapLeavesUnplayableRoom()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));
            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));
            AddStep("enter warmup without imported beatmap", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.GameplayWarmup).WaitSafely());
            AddUntilStep("unplayable room left", () => !MultiplayerClient.RoomJoined);
        }

        [TestCase(RankedPlayStage.RoundWarmup, typeof(osu.Game.Screens.OnlinePlay.Matchmaking.RankedPlay.Intro.IntroScreen))]
        [TestCase(RankedPlayStage.ModBan, typeof(ModBanScreen))]
        [TestCase(RankedPlayStage.CardDiscard, typeof(DiscardScreen))]
        [TestCase(RankedPlayStage.CardPlay, typeof(PickScreen))]
        [TestCase(RankedPlayStage.Ended, typeof(EndedScreen))]
        public void TestParticipantInitialStageSnapshot(RankedPlayStage stage, System.Type expectedScreen)
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));
            AddStep("set stage before screen loads", () => MultiplayerClient.RankedPlayChangeStage(stage, state =>
            {
                state.ActiveUserId = API.LocalUser.Value.Id;
                if (stage == RankedPlayStage.ModBan)
                {
                    state.RandomMods = true;
                    state.BannableRandomMods = ["HD", "HR", "EZ"];
                }
            }).WaitSafely());
            if (stage == RankedPlayStage.Ended)
            {
                AddStep("opponent already left", () =>
                {
                    MultiplayerClient.ServerRoom!.Users.Remove(MultiplayerClient.ServerRoom.Users.Single(user => user.UserID == 2));
                    MultiplayerClient.ClientRoom!.Users.Remove(MultiplayerClient.ClientRoom.Users.Single(user => user.UserID == 2));
                });
            }
            AddStep("load screen after stage update", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));
            AddUntilStep("initial stage displayed without another update", () => screen.ActiveSubScreen is { IsLoaded: true } subScreen && subScreen.GetType() == expectedScreen);
            AddAssert("participant remains connected", () => MultiplayerClient.RoomJoined);
            AddAssert("participant chat remains available", () => screen.ChildrenOfType<RankedPlayChatDisplay>().Any());
            if (stage == RankedPlayStage.Ended)
            {
                AddStep("quit finished match", () => screen.ActiveSubScreen!.ChildrenOfType<ShearedButton>().Single(button => button.Text.ToString() == "Quit").TriggerClick());
                AddUntilStep("finished room left", () => !MultiplayerClient.RoomJoined);
            }
        }

        [Test]
        public void TestIntroStage()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));

            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));

            AddStep("set round warmup phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.RoundWarmup, s => s.StarRating = 6.3f).WaitSafely());
        }

        [Test]
        public void TestUnresolvedUser()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = TestUserLookupCache.UNRESOLVED_USER_ID }));

            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));

            AddStep("set round warmup phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.RoundWarmup, s => s.StarRating = 6.3f).WaitSafely());
        }

        [Test]
        public void TestDiscardCardsStage()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));

            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));

            AddStep("set discard phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardDiscard).WaitSafely());

            AddUntilStep("wait until cards are present", () => this.ChildrenOfType<PlayerHandOfCards.PlayerHandCard>().Count() == 5);

            for (int i = 0; i < 3; i++)
            {
                int i2 = i;
                AddStep($"click card {i2}", () =>
                {
                    InputManager.MoveMouseTo(this.ChildrenOfType<PlayerHandOfCards.PlayerHandCard>().ElementAt(i2));
                    InputManager.Click(MouseButton.Left);
                });
            }

            AddWaitStep("wait", 3);

            AddStep("click discard button", () =>
            {
                var button = screen.ChildrenOfType<ShearedButton>().Single(it => it.Name == "Discard Button");

                InputManager.MoveMouseTo(button);
                InputManager.Click(MouseButton.Left);
            });

            AddWaitStep("wait", 13);
            AddStep("set finish discard phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.FinishCardDiscard).WaitSafely());
        }

        [Test]
        public void TestAddRemoveCards()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));

            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));

            AddStep("set discard phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardDiscard).WaitSafely());

            for (int i = 0; i < 3; i++)
                AddStep("add card", () => MultiplayerClient.RankedPlayAddCards([new RankedPlayCardItem()]).WaitSafely());

            for (int i = 0; i < 3; i++)
                AddStep("remove card", () => MultiplayerClient.RankedPlayRemoveCards(hand => [hand[0]]).WaitSafely());
        }

        [Test]
        public void TestRevealCards()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));

            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));

            var requestHandler = new BeatmapRequestHandler(Ruleset.Value);

            AddStep("setup request handler", () => ((DummyAPIAccess)API).HandleRequest = requestHandler.HandleRequest);

            AddStep("set discard phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardDiscard).WaitSafely());

            for (int i = 0; i < 3; i++)
            {
                int i2 = i;
                AddStep("reveal card", () => MultiplayerClient.RankedPlayRevealCard(hand => hand[i2], new MultiplayerPlaylistItem
                {
                    ID = i2,
                    BeatmapID = requestHandler.Beatmaps[i2].OnlineID
                }).WaitSafely());
            }
        }

        [Test]
        public void TestPlayCardDirect()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));

            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));

            AddStep("set play phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardPlay, state => state.ActiveUserId = API.LocalUser.Value.OnlineID).WaitSafely());
            AddWaitStep("wait", 3);
            AddStep("play card", () => MultiplayerClient.PlayCard(hand => hand[0]).WaitSafely());
        }

        [Test]
        public void TestDiscardCardsDirect()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));

            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));

            AddStep("set discard phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardDiscard).WaitSafely());
            AddWaitStep("wait", 3);
            AddStep("discard cards", () => MultiplayerClient.DiscardCards(hand => hand.Take(3)).WaitSafely());
            AddWaitStep("wait", 13);
            AddStep("set finish discard phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.FinishCardDiscard).WaitSafely());
        }

        [Test]
        public void TestPlayStage()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));

            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));

            AddStep("set play phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardPlay, state => state.ActiveUserId = API.LocalUser.Value.OnlineID).WaitSafely());
            AddUntilStep("wait until cards are present", () => this.ChildrenOfType<PlayerHandOfCards.PlayerHandCard>().Count() == 5);

            for (int i = 0; i < 3; i++)
            {
                int i2 = i;
                AddStep($"click card {i2}", () =>
                {
                    InputManager.MoveMouseTo(this.ChildrenOfType<PlayerHandOfCards.PlayerHandCard>().ElementAt(i2));
                    InputManager.Click(MouseButton.Left);
                });
            }

            AddWaitStep("wait", 3);

            AddStep("click play button", () =>
            {
                var button = screen
                             .ChildrenOfType<PlayerHandOfCards.PlayerHandCard>()
                             .First(it => it.Selected)
                             .ChildrenOfType<ShearedButton>()
                             .First();

                InputManager.MoveMouseTo(button);
                InputManager.Click(MouseButton.Left);
            });
        }

        [Test]
        public void TestOtherPlaysCard()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));

            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));

            AddStep("set play phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardPlay, state => state.ActiveUserId = 2).WaitSafely());
            AddWaitStep("wait", 5);
            AddStep("play beatmap", () => MultiplayerClient.PlayUserCard(2, hand => hand[0]).WaitSafely());
            AddStep("reveal card", () => MultiplayerClient.RankedPlayRevealUserCard(2, hand => hand[0], new MultiplayerPlaylistItem
            {
                ID = 0,
                BeatmapID = 0
            }).WaitSafely());
        }

        [Test]
        public void TestHealthChange()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));

            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));

            AddStep("set play phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardPlay, state => state.ActiveUserId = 2).WaitSafely());
            AddWaitStep("wait", 5);
            AddStep("change player 1 health", () => MultiplayerClient.RankedPlayChangeUserState(MultiplayerClient.LocalUser!.UserID, state => state.Life = 250_000).WaitSafely());
            AddWaitStep("wait", 5);
            AddStep("change player 2 health", () => MultiplayerClient.RankedPlayChangeUserState(2, state => state.Life = 250_000).WaitSafely());
        }

        [Test]
        public void TestPreviewStopsOnEnteringGameplay()
        {
            AddStep("join other user", () => MultiplayerClient.AddUser(new APIUser { Id = 2 }));

            AddStep("load screen", () => LoadScreen(screen = new RankedPlayScreen(MultiplayerClient.ClientRoom!)));

            BeatmapRequestHandler requestHandler = null!;
            AddStep("setup ruleset", () => requestHandler = new BeatmapRequestHandler(new OsuRuleset().RulesetInfo));

            AddStep("setup request handler", () => ((DummyAPIAccess)API).HandleRequest = requestHandler.HandleRequest);

            AddStep("set play phase", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.CardPlay, state => state.ActiveUserId = 1001).WaitSafely());

            for (int i = 0; i < 3; i++)
            {
                int i2 = i;
                AddStep("reveal card", () => MultiplayerClient.RankedPlayRevealCard(hand => hand[i2], new MultiplayerPlaylistItem
                {
                    ID = i2,
                    BeatmapID = requestHandler.Beatmaps[i2].OnlineID
                }).WaitSafely());
            }

            AddStep("hover first card", () => InputManager.MoveMouseTo(this.ChildrenOfType<PlayerHandOfCards>().Single().Cards.First()));
            AddUntilStep("preview playing", () => this.ChildrenOfType<RankedPlayCard.SongPreviewContainer>().Any(p => p.IsRunning), () => Is.True);

            AddWaitStep("wait", 1);
            AddStep("play beatmap", () => MultiplayerClient.PlayUserCard(1001, hand => hand[0]).WaitSafely());

            AddStep("import warmup beatmap", () =>
            {
                importSpectatorMap();
                requestHandler.Beatmaps[0].OnlineID = MultiplayerClient.ClientRoom!.CurrentPlaylistItem.BeatmapID;
            });
            AddStep("set warmup", () => MultiplayerClient.RankedPlayChangeStage(RankedPlayStage.GameplayWarmup).WaitSafely());
            AddUntilStep("preview running", () => this.ChildrenOfType<RankedPlayCard.SongPreviewContainer>().Any(p => p.IsRunning), () => Is.True);

            AddStep("load requested", () => ((IMultiplayerClient)MultiplayerClient).LoadRequested());
            AddUntilStep("preview stopped", () => this.ChildrenOfType<RankedPlayCard.SongPreviewContainer>().Any(p => p.IsRunning), () => Is.False);
        }
    }
}
